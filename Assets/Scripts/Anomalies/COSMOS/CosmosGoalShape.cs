using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Cosmos
{
    /// <summary>Maps the normal half-disc score presentation into an exact elliptical end cap.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CosmosGoalShape : MonoBehaviour
    {
        public ArenaBoundsFromVectorGrid arena;
        public Shader boundaryShader;
        public int side = 1;
        const int Segments = 96;
        readonly Vector3[] vertices = new Vector3[Segments * 4];
        Mesh promotionMesh;
        Material promotionMaterial;
        bool promotionVisible;
        static readonly int ColorID = Shader.PropertyToID("_Color");

        public float SeamRatio => arena.Grid.size.x * .5f / arena.OvalHalfWidthLocal;
        public float HalfHeightWorld => arena.OvalGoalHalfHeightLocal * Mathf.Abs(arena.transform.lossyScale.y);
        public float DepthWorld => arena.OvalGoalDepthLocal * Mathf.Abs(arena.transform.lossyScale.x);
        public bool PromotionVisible => promotionVisible;

        public void FitRenderer(Transform target)
        {
            Vector3 scale = target.localScale;
            scale.x *= 2 * DepthWorld / target.TransformVector(Vector3.right).magnitude;
            scale.z *= 2 * HalfHeightWorld / target.TransformVector(Vector3.forward).magnitude;
            target.localScale = scale;
            Vector3 position = arena.transform.TransformPoint(new Vector3(side * arena.OvalHalfWidthLocal, 0, 0));
            position.y = target.position.y;
            target.position = position;
        }

        public Vector3 MapDiscPoint(Vector2 point)
        {
            float k = SeamRatio, t = point.y;
            // Rationalized ellipse-cap width divided by the reference circle width.
            float stretch = (1 + k) * Mathf.Sqrt(Mathf.Max(0, 1 - t * t)) /
                (Mathf.Sqrt(Mathf.Max(0, 1 - (1 - k * k) * t * t)) + k);
            float x = side * (arena.Grid.size.x * .5f + arena.OvalGoalDepthLocal * point.x * stretch);
            Vector3 world = arena.transform.TransformPoint(new Vector3(x, arena.OvalGoalHalfHeightLocal * t, 0));
            world.y = transform.position.y;
            return world;
        }

        public void SetPromotion(float radius01, Color color, float widthWorld)
        {
            if (!arena || !boundaryShader) return;
            EnsureMesh();
            promotionVisible = color.a > .001f;
            promotionMaterial.SetColor(ColorID, color);
            for (int i = 0; i < Segments; i++)
            {
                float a = Mathf.Lerp(Mathf.PI * .5f, -Mathf.PI * .5f, (float)i / Segments);
                float b = Mathf.Lerp(Mathf.PI * .5f, -Mathf.PI * .5f, (float)(i + 1) / Segments);
                Vector3 p = MapDiscPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius01);
                Vector3 q = MapDiscPoint(new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius01);
                Vector3 normal = Vector3.Cross((q - p).normalized, Vector3.up) * widthWorld * .5f;
                int v = i * 4;
                vertices[v] = p - normal; vertices[v + 1] = p + normal;
                vertices[v + 2] = q + normal; vertices[v + 3] = q - normal;
            }
            promotionMesh.vertices = vertices;
            promotionMesh.RecalculateBounds();
        }

        void EnsureMesh()
        {
            if (promotionMesh) return;
            promotionMesh = new Mesh { name = "COSMOS elliptical promotion ring", hideFlags = HideFlags.DontSave };
            promotionMesh.MarkDynamic();
            var indices = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                int v = i * 4, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            promotionMesh.vertices = vertices; promotionMesh.triangles = indices;
            promotionMaterial = new Material(boundaryShader) { hideFlags = HideFlags.DontSave };
        }
        public void HidePromotion() { promotionVisible = false; }
        void LateUpdate()
        {
            if (promotionVisible && promotionMesh && promotionMaterial)
                Graphics.DrawMesh(promotionMesh, Matrix4x4.identity, promotionMaterial, gameObject.layer,
                    null, 0, null, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }
        void OnDisable()
        {
            promotionVisible = false;
            if (Application.isPlaying) { Destroy(promotionMesh); Destroy(promotionMaterial); }
            else { DestroyImmediate(promotionMesh); DestroyImmediate(promotionMaterial); }
            promotionMesh = null; promotionMaterial = null;
        }
    }
}
