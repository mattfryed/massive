using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Enemies
{
    public sealed partial class EnemySpawnTelegraph
    {
        [Header("Broad glow beneath the outline")]
        [Range(0f, 2f)] public float diffuseGlowStrength = .42f;
        [Range(1f, 4f)] public float diffuseGlowSize = 2.4f;
        public float DiffuseGlowOpacity { get; private set; }

        private static readonly Dictionary<Mesh, Bounds> OutlineBounds = new Dictionary<Mesh, Bounds>();
        private MeshRenderer _diffuseRenderer;
        private Transform _diffuseTransform;
        private MaterialPropertyBlock _diffuseProperties;
        private float _diffuseRadius, _finishGlowOpacity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOutlineBounds() => OutlineBounds.Clear();

        private void InitializeDiffuseGlow()
        {
            // Shared Resources assets also make the effect available to future enemy prefabs/builds.
            var material = Resources.Load<Material>("Enemy Spawn Halo");
            var quad = Resources.Load<Mesh>("Enemy Spawn Halo Quad");
            if (!material || !quad) return;
            var outline = _sourceOutline;
            if (!OutlineBounds.TryGetValue(outline, out var bounds))
            {
                var vertices = outline.vertices;
                bounds = new Bounds(vertices.Length > 0 ? vertices[0] : Vector3.zero, Vector3.zero);
                foreach (var vertex in vertices) bounds.Encapsulate(vertex);
                OutlineBounds[outline] = bounds;
            }
            _diffuseRadius = Mathf.Max(.1f, Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)));
            var go = new GameObject("Diffuse spawn glow"); go.layer = gameObject.layer;
            _diffuseTransform = go.transform; _diffuseTransform.SetParent(transform, false); _diffuseTransform.localPosition = bounds.center;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            _diffuseRenderer = go.AddComponent<MeshRenderer>(); _diffuseRenderer.sharedMaterial = material;
            _diffuseRenderer.shadowCastingMode = ShadowCastingMode.Off; _diffuseRenderer.receiveShadows = false;
            _diffuseRenderer.enabled = false;
            _diffuseProperties = new MaterialPropertyBlock();
        }

        private void SetDiffuseGlow(float opacity)
        {
            DiffuseGlowOpacity = Mathf.Max(0f, opacity);
            if (!_diffuseRenderer) return;
            _diffuseRenderer.enabled = opacity > .0001f;
            _diffuseTransform.localScale = Vector3.one * (_diffuseRadius * diffuseGlowSize * (1f + Defocus * .45f));
            _diffuseProperties.SetColor(ColorId, glowColor);
            _diffuseProperties.SetFloat(OpacityId, opacity);
            _diffuseProperties.SetFloat(DefocusId, Defocus);
            _diffuseRenderer.SetPropertyBlock(_diffuseProperties);
        }
    }
}
