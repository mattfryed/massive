using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    public sealed partial class PlayerMeleePlasma
    {
        [Header("Repulsor — launch tears and broken crests")]
        [Tooltip("Uneven clusters of needle-ended pressure streaks, released with the main pulse.")]
        public bool repulsorBurstRays = true;
        [Tooltip("Short, curling pieces of the pressure crest leave a slower, fragmented wake.")]
        public bool repulsorBurstCrescents = true;
        [Range(0, 2)] public float repulsorBurstIntensity = .85f;
        [Tooltip("Length of the trailing detail. The leading edge still follows the pulse radius.")]
        [Range(.25f, 1.5f)] public float repulsorBurstSpread = 1f;

        [Header("Repulsor — seismic ejecta")]
        [Tooltip("Short, independently moving streaks accelerate away from the discharge and gather inward during its windup.")]
        public bool repulsorSparkEjecta = true;
        [Tooltip("Brief, hairline discharges fork through the expanding pulse. They do not form an orbiting ring.")]
        public bool repulsorArcDischarge = true;
        [Range(0, 1)] public float repulsorEjectaDensity = .65f;

        const int RepulsorRayCount = 15;
        const int RepulsorCrescentCount = 5;
        const int RepulsorDetailSegments = 24;
        const int RepulsorLegacyDetailCount = RepulsorRayCount + RepulsorCrescentCount;
        const int RepulsorEjectaCount = 96;
        const int RepulsorDischargeCount = 8;
        const int RepulsorDetailCount = RepulsorLegacyDetailCount + RepulsorEjectaCount + RepulsorDischargeCount * 2;
        const int RepulsorDetailVertexCount = RepulsorDetailCount * (RepulsorDetailSegments + 1) * 2;

        // The parent has three pulse slots. Each gets one reusable mesh, never a
        // GameObject/material per streak or an allocation per simulation frame.
        sealed class RepulsorBurstGeometry
        {
            public GameObject root;
            public Mesh mesh;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public readonly Vector3[] vertices = new Vector3[RepulsorDetailVertexCount];
            public readonly Color[] colors = new Color[RepulsorDetailVertexCount];
        }

        readonly Dictionary<RepulsorPulse, RepulsorBurstGeometry> repulsorBursts =
            new Dictionary<RepulsorPulse, RepulsorBurstGeometry>(3);
        Material ownedRepulsorBurstMaterial;
        bool repulsorBurstMaterialResolved;
        static readonly int RepulsorDetailPhaseId = Shader.PropertyToID("_DetailPhase");

        Material ResolveRepulsorBurstMaterial()
        {
            if (!repulsorBurstMaterialResolved)
            {
                repulsorBurstMaterialResolved = true;
                var shader = Resources.Load<Shader>("MeleeRepulsorDetail");
                if (!shader) shader = Shader.Find("MASSIVE/MeleeRepulsorDetail");
                if (shader) ownedRepulsorBurstMaterial = new Material(shader)
                { name = "Repulsor broken crests (temporary)", hideFlags = HideFlags.HideAndDontSave };
            }
            return ownedRepulsorBurstMaterial;
        }

        void InitializeRepulsorBurst(RepulsorPulse pulse)
        {
            RepulsorBurstGeometry burst;
            if (repulsorBursts.TryGetValue(pulse, out burst) && burst.root)
            { burst.renderer.enabled = false; return; }

            burst = new RepulsorBurstGeometry();
            burst.root = new GameObject("Repulsor launch tears (temporary)")
            { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            burst.mesh = new Mesh { name = "Repulsor launch tears", hideFlags = HideFlags.HideAndDontSave };
            burst.mesh.MarkDynamic();
            var uv = new Vector2[RepulsorDetailVertexCount];
            var descriptors = new Vector2[RepulsorDetailVertexCount];
            var indices = new int[RepulsorDetailCount * RepulsorDetailSegments * 6];
            for (int stroke = 0; stroke < RepulsorDetailCount; stroke++)
            {
                int first = stroke * (RepulsorDetailSegments + 1) * 2;
                for (int segment = 0; segment <= RepulsorDetailSegments; segment++)
                {
                    int index = first + segment * 2;
                    float along = segment / (float)RepulsorDetailSegments;
                    uv[index] = new Vector2(along, -1);
                    uv[index + 1] = new Vector2(along, 1);
                    float kind = stroke >= RepulsorLegacyDetailCount + RepulsorEjectaCount ? 4 :
                        stroke >= RepulsorLegacyDetailCount ? 3 :
                        stroke >= RepulsorRayCount ? 1 : (stroke % 6 == 0 ? 2 : 0);
                    descriptors[index] = descriptors[index + 1] =
                        new Vector2(kind, RepulsorDetailHash(stroke * 4.37f + 1.27f));
                    if (segment == RepulsorDetailSegments) continue;
                    int triangle = (stroke * RepulsorDetailSegments + segment) * 6;
                    indices[triangle] = index;
                    indices[triangle + 1] = index + 2;
                    indices[triangle + 2] = index + 1;
                    indices[triangle + 3] = index + 1;
                    indices[triangle + 4] = index + 2;
                    indices[triangle + 5] = index + 3;
                }
            }
            burst.mesh.vertices = burst.vertices;
            burst.mesh.uv = uv;
            burst.mesh.uv2 = descriptors;
            burst.mesh.colors = burst.colors;
            burst.mesh.triangles = indices;
            burst.root.AddComponent<MeshFilter>().sharedMesh = burst.mesh;
            burst.renderer = burst.root.AddComponent<MeshRenderer>();
            burst.renderer.enabled = false;
            burst.renderer.shadowCastingMode = ShadowCastingMode.Off;
            burst.renderer.receiveShadows = false;
            burst.renderer.lightProbeUsage = LightProbeUsage.Off;
            burst.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            burst.properties = new MaterialPropertyBlock();
            repulsorBursts[pulse] = burst;
        }

        static float RepulsorDetailHash(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value * 12.9898f + 4.1414f) * 43758.5453f, 1);
        }

        // Deliberately unequal spacing and groups of unequal width prevent the
        // regular starburst silhouette that a uniform radial emitter produces.
        static float RepulsorDetailClusterAngle(int cluster)
        {
            switch (cluster % 5)
            {
                case 0: return .12f;
                case 1: return 1.04f;
                case 2: return 2.51f;
                case 3: return 3.86f;
                default: return 5.09f;
            }
        }

        void UpdateRepulsorBurst(RepulsorPulse pulse, float elapsed, float radius, float fade, float opacity)
        {
            if (Effective_repulsorTreatment == RepulsorVisualTreatment.SeismicDetonation)
            { UpdateRepulsorSeismicDetail(pulse, elapsed, opacity); return; }
            if ((!Effective_repulsorBurstRays && !Effective_repulsorBurstCrescents) || !pulse.emitted || opacity <= .001f ||
                Effective_repulsorBurstIntensity <= .001f)
            { HideRepulsorBurst(pulse); return; }
            RepulsorBurstGeometry burst;
            if (!repulsorBursts.TryGetValue(pulse, out burst) || !burst.root)
            {
                InitializeRepulsorBurst(pulse);
                burst = repulsorBursts[pulse];
            }
            var material = ResolveRepulsorBurstMaterial();
            if (!material) { burst.renderer.enabled = false; return; }

            float released = Mathf.Max(0, elapsed - pulse.activationStart);
            float activeSeconds = Mathf.Max(.001f, pulse.activationEnd - pulse.activationStart);
            float size = Mathf.Max(.001f, pulse.size);
            float spread = Mathf.Clamp(Effective_repulsorBurstSpread, .25f, 1.5f);
            System.Array.Clear(burst.colors, RepulsorLegacyDetailCount * (RepulsorDetailSegments + 1) * 2,
                (RepulsorDetailCount - RepulsorLegacyDetailCount) * (RepulsorDetailSegments + 1) * 2);
            bool visible = false;
            for (int stroke = 0; stroke < RepulsorLegacyDetailCount; stroke++)
            {
                bool crescent = stroke >= RepulsorRayCount;
                bool hero = !crescent && stroke % 6 == 0;
                int indexInType = crescent ? stroke - RepulsorRayCount : stroke;
                float seed = RepulsorDetailHash(stroke * 7.47f + pulse.seed * 63.14f);
                float variation = RepulsorDetailHash(stroke * 3.19f + pulse.seed * 93.7f);
                float delay = crescent ? .035f + variation * .08f : variation * (hero ? .018f : .055f);
                float age = released - delay;
                float lifetime = activeSeconds + (crescent ? .13f + seed * .22f :
                    hero ? .19f + seed * .13f : -.025f + seed * .12f);
                float onset = Mathf.SmoothStep(0, 1, age / (crescent ? .045f : .018f));
                float dissolve = Mathf.SmoothStep(0, 1, (age / Mathf.Max(.05f, lifetime) - .46f) / .54f);
                float strength = onset * (1 - dissolve) * opacity;
                strength *= crescent ? (Effective_repulsorBurstCrescents ? .75f : 0) : (Effective_repulsorBurstRays ? 1 : 0);
                if (age < 0) strength = 0;
                visible |= strength > .001f;
                int cluster = crescent ? indexInType : indexInType / 3;
                float angle = RepulsorDetailClusterAngle(cluster) + pulse.seed * 6.2831853f +
                    (crescent ? .19f + (variation - .5f) * .24f : (indexInType % 3 - 1) * (.07f + seed * .13f));
                float headRadius = Mathf.Lerp(pulse.startRadius, radius, hero ? .99f : .84f + seed * .16f);
                // A crest fragment may drift slightly within the original pulse's
                // footprint, but these decorative strokes do not imply extra reach.
                float advance = Mathf.Clamp01(age / activeSeconds);
                float radialLength = Mathf.Min(headRadius - pulse.startRadius * .96f,
                    (hero ? .90f + seed * .60f : .30f + seed * .90f) * size * spread * Mathf.Lerp(.65f, 1, advance));
                radialLength = Mathf.Max(0, radialLength);
                float arcSpan = (.22f + variation * .49f) * spread;
                float halfWidth = (crescent ? .032f + seed * .051f : .018f + seed * .038f) * size;
                // Three substantial tears carry the impact at normal game zoom;
                // the other strokes remain fine, so the whole burst is not noisy.
                if (!crescent && indexInType % 3 == 0) halfWidth *= 1.5f;
                if (hero) halfWidth = Mathf.Max(.12f * size, halfWidth * 2.5f) *
                    (1 + .28f * (1 - Mathf.SmoothStep(0, 1, age / .055f)));
                int first = stroke * (RepulsorDetailSegments + 1) * 2;
                for (int segment = 0; segment <= RepulsorDetailSegments; segment++)
                {
                    float along = segment / (float)RepulsorDetailSegments;
                    Vector3 point = RepulsorDetailPoint(along, crescent, hero, angle, headRadius, radialLength,
                        arcSpan, size, seed, age, fade);
                    Vector3 before = RepulsorDetailPoint(Mathf.Max(0, along - .012f), crescent, hero, angle,
                        headRadius, radialLength, arcSpan, size, seed, age, fade);
                    Vector3 after = RepulsorDetailPoint(Mathf.Min(1, along + .012f), crescent, hero, angle,
                        headRadius, radialLength, arcSpan, size, seed, age, fade);
                    Vector3 tangent = after - before;
                    Vector3 across = new Vector3(-tangent.z, 0, tangent.x).normalized;
                    float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(along * Mathf.PI)), crescent ? .75f : 1.15f);
                    taper *= crescent ? 1 : Mathf.Lerp(1.2f, .18f, along * along);
                    float width = halfWidth * taper * Mathf.Lerp(1, .35f, dissolve);
                    int index = first + segment * 2;
                    burst.vertices[index] = point - across * width;
                    burst.vertices[index + 1] = point + across * width;
                    burst.colors[index] = burst.colors[index + 1] = new Color(1, 1, 1, strength);
                }
            }

            burst.root.transform.SetPositionAndRotation(pulse.origin, Quaternion.identity);
            burst.root.transform.localScale = Vector3.one;
            burst.mesh.vertices = burst.vertices;
            burst.mesh.colors = burst.colors;
            float bound = radius + .3f * size;
            burst.mesh.bounds = new Bounds(Vector3.zero, new Vector3(bound * 2, size, bound * 2));
            burst.properties.SetVector(RepulsorDetailPhaseId, new Vector4(released, pulse.seed,
                Mathf.Clamp(Effective_repulsorBurstIntensity, 0, 2), fade));
            burst.renderer.sharedMaterial = material;
            burst.renderer.SetPropertyBlock(burst.properties);
            burst.renderer.enabled = visible;
        }

        void UpdateRepulsorSeismicDetail(RepulsorPulse pulse, float elapsed, float opacity)
        {
            bool windup = !pulse.emitted || elapsed < pulse.activationStart;
            if (windup && !Effective_repulsorImplosion) { HideRepulsorBurst(pulse); return; }
            if ((!Effective_repulsorSparkEjecta && !Effective_repulsorArcDischarge) || Effective_repulsorBurstIntensity <= .001f ||
                (!windup && opacity <= .001f))
            { HideRepulsorBurst(pulse); return; }
            RepulsorBurstGeometry burst;
            if (!repulsorBursts.TryGetValue(pulse, out burst) || !burst.root)
            { InitializeRepulsorBurst(pulse); burst = repulsorBursts[pulse]; }
            var material = ResolveRepulsorBurstMaterial();
            if (!material) { burst.renderer.enabled = false; return; }

            // One finite batch per release. Each stroke has its own birth and
            // velocity rather than remaining attached to the pressure front.
            System.Array.Clear(burst.colors, 0, burst.colors.Length);
            float size = Mathf.Max(.001f, pulse.size);
            float source = Mathf.Max(.001f, pulse.startRadius);
            float reach = Mathf.Max(source + .02f * size, pulse.maximumRadius);
            float travel = reach - source;
            float released = elapsed - pulse.activationStart;
            float spread = Mathf.Clamp(Effective_repulsorBurstSpread, .25f, 1.5f);
            int sparkCount = Mathf.RoundToInt(RepulsorEjectaCount * Mathf.Clamp01(Effective_repulsorEjectaDensity));
            bool visible = false;
            for (int detail = 0; detail < RepulsorEjectaCount + RepulsorDischargeCount * 2; detail++)
            {
                bool arc = detail >= RepulsorEjectaCount;
                int arcIndex = (detail - RepulsorEjectaCount) / 2;
                bool branch = arc && (detail - RepulsorEjectaCount) % 2 != 0;
                int family = arc ? arcIndex + 157 : detail;
                if ((arc && !Effective_repulsorArcDischarge) || (!arc && (!Effective_repulsorSparkEjecta || detail >= sparkCount))) continue;
                // Windup remains spare; its streaks all terminate at the source
                // within 0.1 seconds, so no orbital halo is left behind.
                if (windup && ((!arc && detail % 3 != 0) || (arc && arcIndex % 2 != 0))) continue;
                float seed = RepulsorDetailHash(family * 7.47f + pulse.seed * 63.14f);
                float variation = RepulsorDetailHash(family * 3.19f + pulse.seed * 93.7f);
                float scatter = RepulsorDetailHash(family * 9.73f + pulse.seed * 43.7f);
                float lifetime = windup ? .052f + seed * .042f :
                    arc ? .065f + seed * .135f : .12f + seed * .22f;
                float delay = windup ? pulse.activationStart - lifetime - variation * .012f :
                    arc ? .012f + variation * .10f : variation * variation * .095f;
                float age = (windup ? elapsed : released) - delay;
                if (age <= 0 || age >= lifetime) continue;
                float progress = age / lifetime;
                float lifeFade = Mathf.InverseLerp(arc ? .48f : .30f, 1, progress);
                float strength = Mathf.SmoothStep(0, 1, age / (arc ? .006f : .009f)) *
                    (1 - Mathf.SmoothStep(0, 1, lifeFade));
                strength *= windup ? .45f + seed * .25f : opacity * (.40f + seed * .60f);
                if (arc) strength *= .75f + .25f * Mathf.Abs(Mathf.Sin(age * 73 + seed * 31));
                if (branch) strength *= .55f;
                float head, tail;
                if (windup)
                {
                    head = source * 1.015f + travel * (.12f + seed * .20f) * (1 - progress * progress);
                    tail = head + Mathf.Min(travel * .13f, (.06f + variation * .20f) * size) *
                        Mathf.Sin(progress * Mathf.PI);
                }
                else
                {
                    float velocity = travel * (2.3f + scatter * 5.5f);
                    float displacement = velocity * age / (1 + age * (1.2f + variation * 2));
                    head = source + displacement;
                    float limit = reach * (1.015f + seed * .06f);
                    // Mathf.SmoothStep interpolates values, unlike HLSL's edge
                    // thresholds. Normalize world distance before applying it.
                    float rangeFade = Mathf.InverseLerp(limit * .90f, limit, head);
                    strength *= 1 - Mathf.SmoothStep(0, 1, rangeFade);
                    head = Mathf.Min(head, limit);
                    float length = arc ? travel * (.27f + variation * .33f) :
                        Mathf.Min(travel * .27f, (.075f + variation * variation * .40f) * size);
                    tail = Mathf.Max(source * 1.01f, head - length * spread * Mathf.SmoothStep(0, 1, age / .025f));
                }
                if (strength <= .001f || Mathf.Abs(head - tail) <= .002f * size) continue;
                visible = true;
                // Golden-angle ordering keeps density changes spread over the
                // whole burst; jitter avoids evenly spaced spokes.
                float angle = family * 2.39996323f + pulse.seed * 6.2831853f + (variation - .5f) * .70f;
                float halfWidth = (arc ? .009f + seed * .007f : .010f + variation * .014f) * size;
                if (branch) halfWidth *= .72f;
                if (windup) halfWidth *= .78f;
                int first = (RepulsorLegacyDetailCount + detail) * (RepulsorDetailSegments + 1) * 2;
                for (int segment = 0; segment <= RepulsorDetailSegments; segment++)
                {
                    float along = segment / (float)RepulsorDetailSegments;
                    Vector3 point = RepulsorSeismicPoint(along, arc, branch, angle, tail, head, size, seed, age);
                    Vector3 before = RepulsorSeismicPoint(Mathf.Max(0, along - .012f), arc, branch,
                        angle, tail, head, size, seed, age);
                    Vector3 after = RepulsorSeismicPoint(Mathf.Min(1, along + .012f), arc, branch,
                        angle, tail, head, size, seed, age);
                    Vector3 tangent = after - before;
                    Vector3 across = new Vector3(-tangent.z, 0, tangent.x).normalized;
                    float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(along * Mathf.PI)), arc ? .30f : .65f);
                    float width = halfWidth * taper * (arc ? 1 : Mathf.Lerp(.24f, 1, along));
                    int vertex = first + segment * 2;
                    burst.vertices[vertex] = point - across * width;
                    burst.vertices[vertex + 1] = point + across * width;
                    burst.colors[vertex] = burst.colors[vertex + 1] = new Color(1, 1, 1, strength);
                }
            }
            burst.root.transform.SetPositionAndRotation(pulse.origin, Quaternion.identity);
            burst.root.transform.localScale = Vector3.one;
            burst.mesh.vertices = burst.vertices;
            burst.mesh.colors = burst.colors;
            float bound = reach * 1.10f + .04f * size;
            burst.mesh.bounds = new Bounds(Vector3.zero, new Vector3(bound * 2, size, bound * 2));
            burst.properties.SetVector(RepulsorDetailPhaseId, new Vector4(released, pulse.seed,
                Mathf.Clamp(Effective_repulsorBurstIntensity, 0, 2), 0));
            burst.renderer.sharedMaterial = material;
            burst.renderer.SetPropertyBlock(burst.properties);
            burst.renderer.enabled = visible;
        }

        static Vector3 RepulsorSeismicPoint(float along, bool arc, bool branch, float angle,
            float tail, float head, float size, float seed, float age)
        {
            // A fork begins exactly on its parent, then diverges outward. Jagged
            // waves are spatially persistent and only tremble slightly with time.
            float path = branch ? Mathf.Lerp(.42f, .94f, along) : along;
            float radius = Mathf.Lerp(tail, head, path);
            float envelope = Mathf.Sin(path * Mathf.PI);
            float wander = Mathf.Sin(path * 6.3f + seed * 17) * envelope;
            float rough = Mathf.Sin(path * 39 + seed * 9 + Mathf.Sin(age * 63) * .18f) *
                Mathf.Sin(path * 21 - seed * 11) * envelope;
            float side = wander * (arc ? .08f : .012f) * size + rough * (arc ? .023f : .002f) * size;
            if (branch) side += (seed < .5f ? -1 : 1) * Mathf.Pow(along, .8f) * (.08f + seed * .13f) * size;
            float elevation = (.025f + seed * .035f + envelope * (arc ? .035f : .08f)) * size;
            return new Vector3(Mathf.Cos(angle) * radius - Mathf.Sin(angle) * side, elevation,
                Mathf.Sin(angle) * radius + Mathf.Cos(angle) * side);
        }

        static Vector3 RepulsorDetailPoint(float along, bool crescent, bool hero, float angle, float headRadius,
            float radialLength, float arcSpan, float size, float seed, float age, float fade)
        {
            float envelope = Mathf.Sin(along * Mathf.PI);
            float curve = Mathf.Sin(along * 4.1f + seed * 5.3f) * envelope;
            float crackle = Mathf.Sin(along * 37 + seed * 19 + age * 13) *
                Mathf.Sin(along * 19 - seed * 7) * envelope;
            float radius;
            if (crescent)
            {
                angle += (along - .5f) * arcSpan + age * (.11f + seed * .16f);
                radius = headRadius * (.91f + seed * .045f) -
                    size * ((.05f + seed * .08f) * envelope + .012f * crackle + fade * .04f);
            }
            else
            {
                radius = headRadius - radialLength * (1 - along);
                angle += curve * (hero ? .12f + seed * .085f : .028f + seed * .055f) +
                    crackle * (hero ? .011f : .0035f);
            }
            float elevation = (.016f + seed * .05f + envelope * (.035f + seed * .045f)) * size;
            return new Vector3(Mathf.Cos(angle) * radius, elevation, Mathf.Sin(angle) * radius);
        }

        void HideRepulsorBurst(RepulsorPulse pulse)
        {
            RepulsorBurstGeometry burst;
            if (pulse != null && repulsorBursts.TryGetValue(pulse, out burst) && burst.renderer)
                burst.renderer.enabled = false;
        }

        void ReleaseRepulsorBurst(RepulsorPulse pulse)
        {
            RepulsorBurstGeometry burst;
            if (pulse == null || !repulsorBursts.TryGetValue(pulse, out burst)) return;
            if (burst.root) { if (Application.isPlaying) Destroy(burst.root); else DestroyImmediate(burst.root); }
            if (burst.mesh) { if (Application.isPlaying) Destroy(burst.mesh); else DestroyImmediate(burst.mesh); }
            repulsorBursts.Remove(pulse);
            if (repulsorBursts.Count != 0) return;
            if (ownedRepulsorBurstMaterial)
            { if (Application.isPlaying) Destroy(ownedRepulsorBurstMaterial); else DestroyImmediate(ownedRepulsorBurstMaterial); }
            ownedRepulsorBurstMaterial = null;
            repulsorBurstMaterialResolved = false;
        }
    }
}
