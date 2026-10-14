using System.Collections.Generic;
using Shapes;
using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class CarrierVisuals
    {
        [Header("Hit reaction — white outlines, cosmetic recoil")]
        public bool hitReactionEnabled = true;
        [Min(.05f)] public float hitReactionSeconds = .28f;
        [Min(.01f)] public float hitOutlineSeconds = .11f;
        [Range(1f, 4f)] public float hitOutlineWidthMultiplier = 2.4f;
        [Tooltip("Visual displacement at Body Scale 1. Collision and launch anchors stay in place.")]
        [Range(0f, .15f)] public float hitRecoilDistance = .045f;
        [Range(0f, 12f)] public float hitRockDegrees = 4f;
        [Header("Hit reaction — core")]
        [Range(0f, .5f)] public float hitCoreContraction = .2f;
        [Range(0f, .1f)] public float hitCoreShift = .035f;
        [Range(0f, .05f)] public float hitCoreWobble = .014f;
        [Header("Hit reaction — white vector sparks")]
        [Range(0, 6)] public int hitSparkCount = 5;
        [Min(.02f)] public float hitSparkLifetime = .22f;
        [Min(0f)] public float hitSparkSpeed = 1.2f;
        [Min(.001f)] public float hitSparkLength = .065f;

        public float HitReaction01 { get; private set; }
        public float CoreHitScale { get; private set; } = 1f;
        public int ActiveHitSparkCount { get; private set; }

        private struct DockedHitVisual
        {
            public Transform root;
            public Vector3 position;
            public Quaternion rotation;
        }

        private struct HitSpark
        {
            public Vector3 origin, velocity;
            public float age, lifetime, length;
        }

        private const int HitSparkCapacity = 18;
        private readonly HitSpark[] hitSparks = new HitSpark[HitSparkCapacity];
        private readonly List<DockedHitVisual> dockedHitVisuals = new(6);
        private Matrix4x4 hitFrameMatrix = Matrix4x4.identity, hitShellMatrix = Matrix4x4.identity;
        private Vector3 hitDirectionWorld, hitDirectionFrame;
        private float hitAge = float.PositiveInfinity, hitOutlinePulse;
        private int hitSparkIndex;
        private bool hitHasDirection;

        private void InitializeHitReaction()
        {
            ResetHitReaction();
            dockedHitVisuals.Clear();
            if (!carrier) return;
            foreach (var dock in carrier.docks)
            {
                if (!dock) continue;
                var display = dock.GetComponentInChildren<DroneVisuals>(true);
                if (!display || display.transform == dock) continue;
                var root = display.transform;
                dockedHitVisuals.Add(new DockedHitVisual
                { root = root, position = root.localPosition, rotation = root.localRotation });
            }
        }

        private void OnDamage(EnemyDamageContext damage)
        {
            // The shared damage event precedes death. Let lethal hits go directly
            // into the existing rigid-face breakup, without a competing flinch.
            if (damage.healthRemaining <= 0f) { ResetHitReaction(); return; }
            if (!hitReactionEnabled || damage.amount <= 0f || deathAge >= 0f) return;
            hitHasDirection = damage.creditedPlayer;
            Vector3 toward = hitHasDirection ? damage.creditedPlayer.transform.position - transform.position : -transform.forward;
            toward.y = 0f;
            hitDirectionWorld = toward.sqrMagnitude > .000001f ? toward.normalized : Vector3.forward;
            hitAge = 0f;
            TickHitReaction(0f);
            EmitHitSparks();
        }

        private void TickHitReaction(float dt)
        {
            if (!hitReactionEnabled || deathAge >= 0f) { ResetHitReaction(); return; }
            hitAge += Mathf.Max(0f, dt);
            float t = Mathf.Clamp01(hitAge / Mathf.Max(.05f, hitReactionSeconds));
            HitReaction01 = (1f - t) * (1f - t);
            hitOutlinePulse = 1f - Mathf.SmoothStep(0f, 1f, hitAge / Mathf.Max(.01f, hitOutlineSeconds));
            hitDirectionFrame = Frame.InverseTransformDirection(hitDirectionWorld).normalized;
            CoreHitScale = 1f - Mathf.Clamp01(hitCoreContraction) * HitReaction01;
            // One bounded kick and a smaller rebound. A new hit refreshes this
            // envelope rather than adding offsets indefinitely during team attacks.
            float kick = Mathf.Clamp(Mathf.Sin(t * Mathf.PI * 2.2f) * Mathf.Exp(-3f * t) * (1f - t) * 1.7f, -1f, 1f);
            Quaternion rockWorld = Quaternion.AngleAxis(hitRockDegrees * kick, Vector3.Cross(Vector3.up, -hitDirectionWorld));
            Quaternion rockFrame = Quaternion.Inverse(Frame.rotation) * rockWorld * Frame.rotation;
            hitFrameMatrix = HitReaction01 > 0f
                ? Matrix4x4.TRS(-hitDirectionFrame * (hitRecoilDistance * BodyScale * kick), rockFrame, Vector3.one)
                : Matrix4x4.identity;
            Matrix4x4 world = Frame.localToWorldMatrix * hitFrameMatrix * Frame.worldToLocalMatrix;
            hitShellMatrix = shell && HitReaction01 > 0f
                ? shell.transform.worldToLocalMatrix * world * shell.transform.localToWorldMatrix
                : Matrix4x4.identity;
            foreach (var display in dockedHitVisuals)
            {
                if (!display.root || !display.root.parent) continue;
                if (HitReaction01 <= 0f)
                {
                    display.root.SetLocalPositionAndRotation(display.position, display.rotation);
                    continue;
                }
                var parent = display.root.parent;
                display.root.SetLocalPositionAndRotation(
                    parent.InverseTransformPoint(world.MultiplyPoint3x4(parent.TransformPoint(display.position))),
                    Quaternion.Inverse(parent.rotation) * rockWorld * parent.rotation * display.rotation);
            }
            ActiveHitSparkCount = 0;
            for (int i = 0; i < hitSparks.Length; i++)
            {
                hitSparks[i].age += Mathf.Max(0f, dt);
                if (hitSparks[i].age < hitSparks[i].lifetime) ActiveHitSparkCount++;
            }
        }

        private Vector3 ReactCoreBall(Vector3 position, int index)
        {
            Vector3 center = coreCenter * BodyScale;
            Vector3 offset = (position - center) * CoreHitScale;
            float wobble = hitCoreWobble * BodyScale * HitReaction01;
            float phase = hitAge * 63f + index * 2.39996f;
            if (HitReaction01 > 0f)
                offset += new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.31f), Mathf.Cos(phase * .83f)) * wobble;
            return hitFrameMatrix.MultiplyPoint3x4(center + offset + hitDirectionFrame * (hitCoreShift * BodyScale * HitReaction01));
        }

        private float HitEdgeWidth(Vector3 a, Vector3 b)
        {
            if (hitOutlinePulse <= 0f) return outlineWidth;
            Vector3 direction = Frame.InverseTransformDirection(hitDirectionWorld);
            Vector3 midpoint = shell.transform.TransformPoint((a + b) * .5f);
            Vector3 local = Frame.InverseTransformPoint(midpoint).normalized;
            float side = hitHasDirection ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.1f, .85f, Vector3.Dot(local, direction))) : 1f;
            return outlineWidth * Mathf.Lerp(1f, hitOutlineWidthMultiplier, hitOutlinePulse * side);
        }

        private void EmitHitSparks()
        {
            if (centers == null || hullVertices == null) return;
            int face = 0; float nearest = float.NegativeInfinity;
            for (int i = 0; i < centers.Length; i++)
            {
                float facing = Vector3.Dot(centers[i].normalized, hitDirectionFrame);
                if (facing > nearest) { nearest = facing; face = i; }
            }
            Vector3 origin = Frame.TransformPoint(hitFrameMatrix.MultiplyPoint3x4(centers[face] * BodyScale));
            Vector3 tangent = Vector3.Cross(Vector3.up, hitDirectionWorld);
            float scale = Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.x)) * BodyScale;
            for (int i = 0; i < Mathf.Clamp(hitSparkCount, 0, 6); i++)
            {
                Vector3 direction = (hitDirectionWorld + tangent * Sample(-.65f, .65f) + Vector3.up * Sample(.05f, .4f)).normalized;
                hitSparks[hitSparkIndex++ % HitSparkCapacity] = new HitSpark
                {
                    origin = origin,
                    velocity = direction * (hitSparkSpeed * scale * Sample(.75f, 1.25f)),
                    age = 0f,
                    lifetime = Mathf.Max(.02f, hitSparkLifetime) * Sample(.8f, 1.2f),
                    length = hitSparkLength * scale * Sample(.65f, 1.2f)
                };
            }
            ActiveHitSparkCount = 0;
            foreach (var spark in hitSparks) if (spark.age < spark.lifetime) ActiveHitSparkCount++;
        }

        private void DrawHitSparks()
        {
            if (ActiveHitSparkCount == 0) return;
            Draw.Matrix = Matrix4x4.identity;
            Draw.Color = Color.white;
            float width = outlineWidth * Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.x)) * .65f;
            foreach (var spark in hitSparks)
            {
                if (spark.age >= spark.lifetime) continue;
                float remaining = 1f - spark.age / spark.lifetime;
                Vector3 head = spark.origin + spark.velocity * spark.age;
                Vector3 tail = head - spark.velocity.normalized * (spark.length * remaining);
                // Shrink pure-white strokes instead of introducing a gray flash.
                Draw.Thickness = width * remaining;
                Draw.Line(tail, head);
            }
        }

        private void ResetHitReaction()
        {
            hitAge = float.PositiveInfinity;
            HitReaction01 = hitOutlinePulse = 0f; CoreHitScale = 1f;
            hitFrameMatrix = hitShellMatrix = Matrix4x4.identity;
            ActiveHitSparkCount = hitSparkIndex = 0;
            for (int i = 0; i < hitSparks.Length; i++) hitSparks[i].age = float.PositiveInfinity;
            foreach (var display in dockedHitVisuals)
                if (display.root) display.root.SetLocalPositionAndRotation(display.position, display.rotation);
        }
    }
}
