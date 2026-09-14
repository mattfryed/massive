using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Enemies
{
    public sealed partial class EnemySpawnTelegraph
    {
        [Header("Outward ghosts")]
        public bool ghostsEnabled = true;
        [Range(1, 8)] public int ghostCount = 4;
        [Range(0f, .6f)] public float ghostOpacity = .2f;
        [Min(.05f)] public float ghostLifetime = .9f;
        [Min(0f)] public float ghostDistance = .75f;
        [Range(0f, .6f)] public float ghostVariation = .25f;
        [Range(.05f, 1f)] public float ghostEndScale = .3f;

        private sealed class Ghost
        {
            public Transform transform;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties = new MaterialPropertyBlock();
            public Vector3 direction;
            public Quaternion rotation;
            public float age, lifetime, distance, intensity;
        }
        private readonly Ghost[] _ghosts = new Ghost[8];
        private Mesh _ghostMesh;
        private System.Random _ghostRandom;
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        public int VisibleGhostCount { get; private set; }

        private void InitializeGhosts()
        {
            _ghostMesh = GetComponent<MeshFilter>().sharedMesh;
            // Presentation randomness never consumes the gameplay/spawn random stream.
            _ghostRandom = new System.Random(GetInstanceID());
        }

        private void ResetGhosts()
        {
            EnsureGhosts();
            for (int i = 0; i < _ghosts.Length; i++)
            {
                var ghost = _ghosts[i]; if (ghost == null) continue;
                StartGhost(ghost);
                ghost.age = -i * Mathf.Max(.05f, ghostLifetime) / Mathf.Max(1, ghostCount);
                ghost.renderer.enabled = false;
            }
            VisibleGhostCount = 0;
        }

        private void EnsureGhosts()
        {
            if (!ghostsEnabled || !_ghostMesh) return;
            int count = Mathf.Clamp(ghostCount, 1, _ghosts.Length);
            for (int i = 0; i < count; i++)
            {
                if (_ghosts[i] != null) continue;
                var go = new GameObject("Outward ghost " + (i + 1));
                go.layer = gameObject.layer; go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _ghostMesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = _renderer.sharedMaterial;
                renderer.localBounds = _renderer.localBounds;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.enabled = false;
                var ghost = new Ghost { transform = go.transform, renderer = renderer };
                _ghosts[i] = ghost; StartGhost(ghost);
            }
        }

        private float GhostSample(float min, float max) => Mathf.Lerp(min, max, (float)_ghostRandom.NextDouble());

        private void StartGhost(Ghost ghost)
        {
            float angle = GhostSample(0f, Mathf.PI * 2f);
            ghost.direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            ghost.rotation = transform.rotation;
            ghost.age = 0f;
            float variation = Mathf.Clamp01(ghostVariation);
            ghost.lifetime = Mathf.Max(.05f, ghostLifetime * GhostSample(1f - variation, 1f + variation));
            ghost.distance = Mathf.Max(0f, ghostDistance * GhostSample(1f - variation, 1f + variation));
            ghost.intensity = GhostSample(.75f, 1f);
        }

        private void AdvanceGhosts(float delta)
        {
            VisibleGhostCount = 0;
            if (!ghostsEnabled) { HideGhosts(); return; }
            EnsureGhosts();
            int count = Mathf.Clamp(ghostCount, 1, _ghosts.Length);
            for (int i = 0; i < _ghosts.Length; i++)
            {
                var ghost = _ghosts[i]; if (ghost == null) continue;
                if (i >= count) { ghost.renderer.enabled = false; continue; }
                bool waiting = ghost.age < 0f;
                ghost.age += delta;
                if (waiting && ghost.age >= 0f) ghost.rotation = transform.rotation;
                if (ghost.age >= ghost.lifetime && !IsCompleting) StartGhost(ghost);
                float progress = Mathf.Clamp01(ghost.age / ghost.lifetime);
                float opacity = ghost.age < 0f ? 0f : Mathf.SmoothStep(0f, 1f, progress / .12f) *
                    (1f - Mathf.SmoothStep(0f, 1f, progress)) * ghostOpacity * ghost.intensity * Opacity;
                ghost.renderer.enabled = opacity > .0001f;
                if (!ghost.renderer.enabled) continue;
                VisibleGhostCount++;
                float travel = 1f - (1f - progress) * (1f - progress);
                // Fixed world-XZ direction for each life, independent of the main crystal's axial roll.
                ghost.transform.position = transform.position + ghost.direction * (travel * ghost.distance);
                ghost.transform.rotation = ghost.rotation;
                ghost.transform.localScale = Vector3.one * Mathf.Lerp(1f, ghostEndScale, Mathf.SmoothStep(0f, 1f, progress));
                ghost.properties.SetFloat(OpacityId, opacity);
                ghost.properties.SetFloat(ContrastId, 0f);
                ghost.properties.SetColor(ColorId, ghostColor);
                ghost.properties.SetColor(GlowColorId, glowColor);
                ghost.properties.SetFloat(DefocusId, Defocus);
                ghost.properties.SetFloat(DefocusWidthId, defocusWidth);
                ghost.renderer.SetPropertyBlock(ghost.properties);
            }
        }

        private void HideGhosts()
        {
            VisibleGhostCount = 0;
            foreach (var ghost in _ghosts) if (ghost != null && ghost.renderer) ghost.renderer.enabled = false;
        }
    }
}
