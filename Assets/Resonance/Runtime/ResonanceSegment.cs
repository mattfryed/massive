using System.Collections.Generic;
using UnityEngine;

namespace Massive.Resonance
{
    /// <summary>Owns one exposed arc and deduplicates compound-trigger contacts per body per physics step.</summary>
    public sealed class ResonanceSegment : MonoBehaviour
    {
        private ResonanceArc settings;
        private Vector3[] points;
        // Geometry is rebuilt only when the sampled path or clipped contact extent changes.
        private struct ContactSpan { public Vector3 a, d; public float denominator, ta, tb, low, high; public bool enabled; }
        private ContactSpan[] contactSpans;
        private Bounds[] contactBounds;
        private float cachedFrom = float.NaN, cachedTo = float.NaN;
        private const int QueryBlockSize = 8;
        private LayerMask affectedLayers;
        private bool coresOnly;
        private ResonancePatternController pattern;
        private readonly HashSet<Rigidbody> affectedThisStep = new HashSet<Rigidbody>();
        private float lastStep = float.NegativeInfinity;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        public readonly List<Collider> HardColliders = new List<Collider>();
        public ResonanceArc Settings => settings;
        public bool EnemiesUseSludge => pattern != null && pattern.enemiesUseSludge;
        public float PathLength { get; private set; }
        public ResonanceContactVisuals ContactVisuals { get; internal set; }

        public void Initialize(ResonanceArc arc, Vector3[] path, LayerMask mask, bool onlyCores, ResonancePatternController controller = null)
        {
            settings = arc; points = path; cachedFrom = cachedTo = float.NaN; affectedLayers = mask; coresOnly = onlyCores; pattern = controller;
            PathLength = 0f;
            for (int i = 1; i < path.Length; i++) PathLength += Vector3.Distance(path[i-1], path[i]);
        }
        public void Own(Mesh mesh) { ownedMeshes.Add(mesh); }

        public Vector3 SampleDistance(float distance, out float t)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {
                float length = Vector3.Distance(points[i], points[i+1]);
                if (distance <= length || i == points.Length - 2)
                {
                    float u = Mathf.Clamp01(distance / Mathf.Max(.00001f, length));
                    t = (i + u) / (points.Length - 1f); return Vector3.Lerp(points[i], points[i+1], u);
                }
                distance -= length;
            }
            t = 0f; return points[0];
        }

        private void PrepareContactSpans(float from, float to)
        {
            if (contactSpans != null && cachedFrom == from && cachedTo == to) return;
            cachedFrom = from; cachedTo = to;
            int count = points.Length - 1;
            contactSpans = new ContactSpan[count];
            contactBounds = new Bounds[(count + QueryBlockSize - 1) / QueryBlockSize];
            for (int i = 0; i < count; i++)
            {
                int block = i / QueryBlockSize;
                if (i % QueryBlockSize == 0) contactBounds[block] = new Bounds(points[i], Vector3.zero);
                contactBounds[block].Encapsulate(points[i + 1]);
                float ta = i / (points.Length - 1f), tb = (i + 1f) / (points.Length - 1f);
                if (tb <= from || ta >= to) continue;
                float low = Mathf.Clamp01((from - ta) / (tb - ta)), high = Mathf.Clamp01((to - ta) / (tb - ta));
                Vector3 a = Vector3.Lerp(points[i], points[i + 1], low), b = Vector3.Lerp(points[i], points[i + 1], high);
                Vector3 d = b - a;
                contactSpans[i] = new ContactSpan { a = a, d = d, denominator = Mathf.Max(.000001f, d.sqrMagnitude),
                    ta = ta, tb = tb, low = low, high = high, enabled = true };
            }
        }

        // maxGap is a conservative world-space influence limit. The default retains
        // the exact global-nearest query used by magnetic surface following.
        public bool NearestContact(Vector3 worldPoint, out Vector3 normal, out float gap, out float envelope,
            float maxGap = float.PositiveInfinity)
        {
            normal = Vector3.right; gap = float.PositiveInfinity; envelope = 0f;
            if (settings == null || settings.behavior == ResonanceBehavior.VisualOnly || settings.colliderThickness <= 0f) return false;
            Vector3 p = transform.InverseTransformPoint(worldPoint); p.y = 0f;
            float from = settings.independentCollisionProfile ? settings.collisionStart : 0f;
            float to = settings.independentCollisionProfile ? settings.collisionEnd : 1f;
            PrepareContactSpans(from, to);
            float widthScale = pattern != null ? pattern.SolidVisualWidth : 1f;
            Vector3 lossyScale = transform.lossyScale;
            float scale = Mathf.Min(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
            // Both taper envelopes are bounded by one. A small outward pad protects
            // boundary comparisons from floating-point rounding; it never changes contacts.
            float maxHalfWidth = settings.colliderThickness * .50001f + .0001f;
            for (int block = 0; block < contactBounds.Length; block++)
            {
                float limit = Mathf.Min(gap, maxGap);
                if (scale > .0000001f && !float.IsPositiveInfinity(limit))
                {
                    float reach = Mathf.Max(0f, limit / scale + maxHalfWidth);
                    if (contactBounds[block].SqrDistance(p) > reach * reach) continue;
                }
                int end = Mathf.Min(contactSpans.Length, (block + 1) * QueryBlockSize);
                for (int i = block * QueryBlockSize; i < end; i++)
                {
                    var span = contactSpans[i];
                    if (!span.enabled) continue;
                    float u = Mathf.Clamp01(Vector3.Dot(p - span.a, span.d) / span.denominator);
                    float t = Mathf.Lerp(span.ta, span.tb, Mathf.Lerp(span.low, span.high, u));
                    float e = settings.CollisionEnvelope(t);
                    float width = Mathf.Min(settings.colliderThickness * e, settings.thickness * widthScale * settings.Envelope(t)) * .5f;
                    if (width <= .00001f) continue;
                    Vector3 delta = p - (span.a + span.d * u);
                    float distance = (delta.magnitude - width) * scale;
                    if (distance >= gap || distance > maxGap) continue;
                    gap = distance; envelope = e;
                    normal = transform.TransformDirection(delta.sqrMagnitude > .000001f ? delta.normalized : Vector3.Cross(Vector3.up, span.d).normalized);
                }
            }
            return !float.IsPositiveInfinity(gap);
        }

        public void WallContact(Collision collision)
        {
            if (settings == null || settings.behavior != ResonanceBehavior.HardWall || pattern == null) return;
            Vector3 point=collision.contactCount>0 ? collision.GetContact(0).point : transform.position;
            pattern.NotifyCoreContact(collision.rigidbody,this,point,collision.relativeVelocity,true);
        }

        public void Contact(Collider other)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || settings == null || !settings.IsVisible) return;
            // Pickups are tethered to their spawn anchor: neither physical walls
            // nor membrane/deflector forces should fight that tether.
            if (other.GetComponentInParent<Massive.PowerUps.PowerUpPickup>() != null) return;
            Rigidbody body = other.attachedRigidbody;
            if (body == null || body.isKinematic || (affectedLayers.value & (1 << body.gameObject.layer)) == 0) return;
            var core = body.GetComponent<Massive.Multiplier.AmplifierCoreGameplay>();
            var player = body.GetComponent<PlayerControllerScript>();
            // Enemy sludge is owned by the actor driver, once per pattern/physics step.
            if (EnemiesUseSludge && body.GetComponent<Massive.Enemies.EnemyBase>() != null) return;
            if (pattern != null && ((core != null && pattern.coreResponse != ResonanceCoreResponse.PatternDefault)
                || (player != null && pattern.playerResponse != ResonancePlayerResponse.PatternDefault))) return;
            if (coresOnly && core == null) return;
            if (lastStep != Time.fixedTime) { affectedThisStep.Clear(); lastStep = Time.fixedTime; }
            if (!affectedThisStep.Add(body)) return;
            Vector3 local = transform.InverseTransformPoint(body.worldCenterOfMass);
            float best = float.PositiveInfinity;
            Vector3 tangent = Vector3.right;
            float t = 0.5f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 d = points[i + 1] - points[i];
                float u = Mathf.Clamp01(Vector3.Dot(local - points[i], d) / Mathf.Max(d.sqrMagnitude, 0.000001f));
                float distance = (local - (points[i] + d * u)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; tangent = d.normalized; t = (i + u) / (points.Length - 1f);
            }
            body.linearVelocity = ModifyVelocity(body.linearVelocity, transform.TransformDirection(tangent), settings, settings.CollisionEnvelope(t), Time.fixedDeltaTime);
        }

        public static Vector3 ModifyVelocity(Vector3 velocity, Vector3 tangent, ResonanceArc arc, float weight, float deltaTime)
        {
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            if (arc.behavior == ResonanceBehavior.DampingMembrane)
                planar *= Mathf.Exp(-Mathf.Max(0f, arc.dampingPerSecond) * weight * deltaTime);
            else if (arc.behavior == ResonanceBehavior.LensDeflector && planar.sqrMagnitude > 0.000001f)
            {
                tangent.y = 0f;
                if (arc.deflectionDegreesPerSecond < 0f) tangent = Vector3.Cross(Vector3.up, tangent);
                if (Vector3.Dot(tangent, planar) < 0f) tangent = -tangent;
                planar = Vector3.RotateTowards(planar, tangent.normalized * planar.magnitude,
                    Mathf.Abs(arc.deflectionDegreesPerSecond) * Mathf.Deg2Rad * weight * deltaTime, 0f);
            }
            return new Vector3(planar.x, velocity.y, planar.z);
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            ownedMeshes.Clear();
        }
    }
}
