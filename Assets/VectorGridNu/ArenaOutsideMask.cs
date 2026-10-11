using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Levels
{
    /// <summary>Continuous exterior cutout driven by the Surface's live arena outline.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(ArenaBoundsFromVectorGrid))]
    [DefaultExecutionOrder(550)]
    public sealed class ArenaOutsideMask : MonoBehaviour
    {
        [SerializeField] private ArenaBoundsFromVectorGrid arena;
        [SerializeField] private Shader maskShader;
        [Tooltip("World Y offset from the grid plane. Keep above background effects and below the world-space HUD.")]
        [SerializeField] private float maskYOffset = 2f;
        [Tooltip("Include both team goal half circles in the mask opening. Off closes the opening at the field's goal mouths, including on oval fields.")]
        [SerializeField] private bool includeTeamGoalHalfCircles = true;
        [Tooltip("Planar goal-cap depth relative to half the field height. 1 gives round caps; 0 gives a rectangle. Oval arenas use their bounds profile instead.")]
        [SerializeField, Min(0)] private float goalCapDepthRatio = 1f;
        [Tooltip("World-space margin outside the outline, preserving the border stroke.")]
        [SerializeField, Min(0)] private float edgePadding = .03f;

        static readonly int WorldToGrid = Shader.PropertyToID("_CosmosWorldToGrid");
        static readonly int Oval = Shader.PropertyToID("_ArenaOval");
        static readonly int Rectangle = Shader.PropertyToID("_ArenaRect");
        static readonly int Padding = Shader.PropertyToID("_ArenaMaskPadding");
        static readonly int ExcludeGoalCaps = Shader.PropertyToID("_ArenaMaskExcludeGoalCaps");
        Material material;
        Mesh mesh;
        MaterialPropertyBlock properties;

        public ArenaBoundsFromVectorGrid Arena => arena;
        public Shader MaskShader => maskShader;

        void Reset() => arena = GetComponent<ArenaBoundsFromVectorGrid>();
        void OnEnable()
        {
            if (!arena) arena = GetComponent<ArenaBoundsFromVectorGrid>();
            properties ??= new MaterialPropertyBlock();
        }
        void OnValidate()
        {
            goalCapDepthRatio = Mathf.Max(0, goalCapDepthRatio);
            edgePadding = Mathf.Max(0, edgePadding);
        }
        void LateUpdate()
        {
            if (!arena || !arena.Grid || !maskShader) return;
            if (!material || material.shader != maskShader)
            {
                Release(material);
                material = new Material(maskShader) { name = "Arena outside mask (generated)", hideFlags = HideFlags.DontSave };
            }
            if (!mesh)
            {
                mesh = new Mesh { name = "Arena outside cutout (generated)", hideFlags = HideFlags.DontSave };
                mesh.vertices = new[] { new Vector3(-1, -1), new Vector3(-1, 1), new Vector3(1, 1), new Vector3(1, -1) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            }
            var grid = arena.Grid;
            var frame = grid.transform;
            Vector2 half = grid.size * .5f;
            if (half.x <= 0 || half.y <= 0) return;
            Vector3 scale = frame.lossyScale;
            float margin = Mathf.Max(edgePadding, grid.boundary.borderWidthWorld * grid.boundary.borderWidthMul * .5f);
            properties.Clear();
            properties.SetMatrix(WorldToGrid, frame.worldToLocalMatrix);
            properties.SetVector(Oval, arena.OutlineShaderParameters);
            float capDepth = includeTeamGoalHalfCircles ? half.y * goalCapDepthRatio : 0;
            properties.SetVector(Rectangle, new Vector4(half.x, half.y, capDepth, 1));
            properties.SetFloat(ExcludeGoalCaps, includeTeamGoalHalfCircles ? 0 : 1);
            properties.SetVector(Padding, new Vector4(margin / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                margin / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 0, 0));
            // As in COSMOS, a single large quad seals corners and follows dimension,
            // translation and scale edits without rebuilding any physical walls.
            float extent = arena.ovalOutline ? arena.OvalHalfWidthLocal : half.x + capDepth;
            var matrix = frame.localToWorldMatrix * Matrix4x4.Scale(new Vector3(Mathf.Max(100, extent * 4), Mathf.Max(100, half.y * 4), 1));
            matrix.m13 += maskYOffset;
            Graphics.DrawMesh(mesh, matrix, material, gameObject.layer, null, 0, properties,
                ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }
        void OnDisable() { Release(mesh); Release(material); mesh = null; material = null; }
        void OnDestroy() => OnDisable();
        static void Release(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
