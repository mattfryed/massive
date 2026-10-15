using Massive.Player;
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.PowerUps
{
    public sealed class DecoherenceFieldVisual : ImmediateModeShapeDrawer
    {
        private PlayerControllerScript owner;
        private DecoherenceAbility driver;
        internal DecoherenceAbility Driver => driver;
        private DecoherencePowerUpDefinition definition;
        private DecoherenceGridEcho gridEcho;
        private readonly Vector2[] noiseSeeds = new Vector2[5];
        public DecoherencePaint Paint { get; private set; }
        internal void Bind(PlayerControllerScript player, DecoherenceAbility ability, DecoherencePowerUpDefinition settings, DecoherencePaint paint)
        {
            owner = player; driver = ability; definition = settings; Paint = paint; useCullingMasks = true;
            gridEcho?.Dispose(); gridEcho = new DecoherenceGridEcho(player, settings);
            // Local random state: visual variety must not consume gameplay's random sequence.
            var random = new System.Random(player.GetInstanceID());
            for (int i = 0; i < noiseSeeds.Length; i++)
                noiseSeeds[i] = new Vector2((float)random.NextDouble() * 4096f, (float)random.NextDouble() * 4096f);
        }

        // All players have routed their input before painting and dealing damage.
        private void LateUpdate()
        {
            driver?.AdvanceField(Time.deltaTime);
            gridEcho?.Update(Paint);
        }
        public void Clear() => driver?.DisposeField();
        public override void OnDisable()
        {
            base.OnDisable(); gridEcho?.Dispose();
            if (!gameObject.activeInHierarchy) Clear();
        }
        public override void DrawShapes(Camera camera)
        {
            if (!owner || !definition || Paint == null || Paint.Deposits.Count == 0) return;
            float size = PlayerScaleAdjuster.SizeOf(owner);
            float largestRadius = 0f;
            foreach (var deposit in Paint.Deposits) largestRadius = Mathf.Max(largestRadius, deposit.radius);
            // All union junctions share this scale, including separate ghost outlines.
            float collapse = Mathf.Clamp01(largestRadius / Mathf.Max(.01f, size * .25f));
            using (Draw.Command(camera))
            {
                Draw.Matrix = Matrix4x4.identity; Draw.ZTest = CompareFunction.LessEqual;
                Draw.LineGeometry = LineGeometry.Volumetric3D; Draw.ThicknessSpace = ThicknessSpace.Meters;
                // Only exterior arcs survive union clipping: no stack of circular outlines.
                var boundary = Paint.Boundary;
                int ghosts = Mathf.Clamp(definition.ghostOutlineCount, 0, noiseSeeds.Length - 1);
                for (int layer = ghosts; layer >= 0; layer--)
                {
                    float opacity = definition.fieldOpacity * (layer == 0 ? 1f : definition.ghostOutlineOpacity);
                    if (opacity <= 0f) continue;
                    float thickness = Mathf.Max(.001f, definition.fieldLineWidth) * size * (layer == 0 ? 1f : .7f);
                    foreach (var arc in boundary)
                    {
                        var stamp = Paint.Deposits[arc.deposit];
                        // Age changes the footprint as deposits expire, never edge opacity.
                        Draw.Color = new Color(1, 1, 1, opacity);
                        Draw.Thickness = Mathf.Min(thickness, stamp.radius * .25f);
                        int steps = Mathf.Max(2, Mathf.CeilToInt((arc.to - arc.from) * Mathf.Max(4f, stamp.radius / Mathf.Max(.02f, .075f * size))));
                        Vector3 center = stamp.center + Vector3.up * .08f;
                        Vector3 previous = Warp(center + Ring(arc.from) * stamp.radius, layer, size, collapse);
                        for (int i = 1; i <= steps; i++)
                        {
                            float angle = Mathf.Lerp(arc.from, arc.to, (float)i / steps);
                            Vector3 next = Warp(center + Ring(angle) * stamp.radius, layer, size, collapse);
                            Draw.Line(previous, next); previous = next;
                        }
                    }
                }
            }
        }
        private Vector3 Warp(Vector3 point, int layer, float size, float collapse)
        {
            float amount = Mathf.Max(0f, layer == 0 ? definition.outlineNoiseAmount : definition.ghostOutlineNoiseAmount) * size * collapse;
            if (amount <= 0f) return point;
            float frequency = Mathf.Max(.01f, definition.outlineNoiseFrequency) * (1f + layer * .17f) / Mathf.Max(.01f, size);
            float time = Paint.Clock * Mathf.Max(0f, definition.outlineNoiseSpeed) * (1f + layer * .13f);
            Vector2 seed = noiseSeeds[layer];
            float x = point.x * frequency + seed.x, z = point.z * frequency + seed.y;
            // Sample world coordinates, not a deposit's angle: both sides of every
            // union junction receive exactly the same displacement, including ghosts.
            point.x += (Mathf.PerlinNoise(x + time, z - time * .37f) * 2f - 1f) * amount;
            point.z += (Mathf.PerlinNoise(x - time * .53f + 73.1f, z + time + 119.7f) * 2f - 1f) * amount;
            return point;
        }
        private static Vector3 Ring(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }
}
