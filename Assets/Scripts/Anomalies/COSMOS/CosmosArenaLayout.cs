using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Cosmos
{
    /// <summary>Scene-local boundary presentation and physical walls, using the shared bounds profile.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(500)]
    [RequireComponent(typeof(ArenaBoundsFromVectorGrid))]
    public sealed class CosmosArenaLayout : MonoBehaviour
    {
        public Shader boundaryShader;
        [Min(.005f)] public float borderWidth = .045f;
        [Min(.05f)] public float wallThickness = .3f;
        [Min(.1f)] public float wallHeight = 5f;
        public Color borderColor = Color.white;
        public Renderer[] fieldMasks;
        [HideInInspector] public int layoutRevision;
        ArenaBoundsFromVectorGrid bounds;
        MeshRenderer gridRenderer;
        MaterialPropertyBlock block;
        MaterialPropertyBlock maskBlock;
        Mesh borderMesh;
        Material borderMaterial;
        GameObject walls;
        Vector4 builtProfile;
        Vector3 builtScale;
        Vector3 builtSettings;

        public Transform Walls => walls ? walls.transform : null;

        void OnEnable()
        {
            bounds = GetComponent<ArenaBoundsFromVectorGrid>();
            gridRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            maskBlock = new MaterialPropertyBlock();
        }
        void Update()
        {
            if (!bounds || !bounds.Grid || !bounds.ovalOutline) return;
            Vector3 settings = new Vector3(borderWidth, wallThickness, wallHeight);
            if (!walls || builtProfile != bounds.OutlineShaderParameters || builtScale != transform.lossyScale || builtSettings != settings)
                Rebuild();
        }
        void LateUpdate()
        {
            if (!bounds || !gridRenderer) return;
            gridRenderer.GetPropertyBlock(block);
            block.SetVector("_ArenaOval", bounds.OutlineShaderParameters);
            gridRenderer.SetPropertyBlock(block);
            if (fieldMasks != null)
                foreach (var mask in fieldMasks)
                {
                    if (!mask) continue;
                    mask.GetPropertyBlock(maskBlock);
                    maskBlock.SetVector("_ArenaOval", bounds.OutlineShaderParameters);
                    maskBlock.SetMatrix("_CosmosWorldToGrid", transform.worldToLocalMatrix);
                    mask.SetPropertyBlock(maskBlock);
                }
            if (!borderMesh || !borderMaterial) return;
            borderMaterial.SetColor("_Color", borderColor);
            Graphics.DrawMesh(borderMesh, transform.localToWorldMatrix, borderMaterial, gameObject.layer,
                null, 0, null, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }

        public void Rebuild()
        {
            if (!bounds) bounds = GetComponent<ArenaBoundsFromVectorGrid>();
            if (!bounds || !bounds.Grid || !bounds.ovalOutline) return;
            ReleaseGeometry();
            builtProfile = bounds.OutlineShaderParameters;
            builtScale = transform.lossyScale;
            builtSettings = new Vector3(borderWidth, wallThickness, wallHeight);
            walls = new GameObject("COSMOS curved walls") { hideFlags = HideFlags.DontSave };
            walls.transform.SetParent(transform, false);
            int count = ArenaBoundsFromVectorGrid.OvalSegments;
            const int capSegments = 128;
            var vertices = new Vector3[(count * 2 + 2 + capSegments * 2) * 4];
            var triangles = new int[(count * 2 + 2 + capSegments * 2) * 6];
            int segment = 0;
            float halfX = builtProfile.x;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                for (int i = 0; i < count; i++)
                {
                    float x0 = Mathf.Lerp(-halfX, halfX, (float)i / count);
                    float x1 = Mathf.Lerp(-halfX, halfX, (float)(i + 1) / count);
                    Vector2 a = new Vector2(x0, sign * bounds.OutlineHalfHeightLocal(x0));
                    Vector2 b = new Vector2(x1, sign * bounds.OutlineHalfHeightLocal(x1));
                    AddSegment(a, b, sign, segment++, vertices, triangles);
                }
            }
            float end = bounds.OutlineHalfHeightLocal(halfX);
            AddSegment(new Vector2(-halfX, -end), new Vector2(-halfX, end), 1, segment++, vertices, triangles);
            AddSegment(new Vector2(halfX, -end), new Vector2(halfX, end), -1, segment++, vertices, triangles);
            // Goal caps are presentation only. The separators above remain solid walls.
            for (int sign = -1; sign <= 1; sign += 2)
                for (int i = 0; i < capSegments; i++)
                    AddSegment(GoalCapPoint(i, capSegments, sign), GoalCapPoint(i + 1, capSegments, sign),
                        sign, segment++, vertices, triangles, false);
            borderMesh = new Mesh { name = "COSMOS outline (generated)", hideFlags = HideFlags.DontSave };
            borderMesh.vertices = vertices; borderMesh.triangles = triangles; borderMesh.RecalculateBounds();
            if (!boundaryShader) boundaryShader = Shader.Find("MASSIVE/Cosmos/Boundary");
            if (boundaryShader) borderMaterial = new Material(boundaryShader) { hideFlags = HideFlags.DontSave };
        }

        Vector2 GoalCapPoint(int i, int count, int side)
        {
            float angle = Mathf.Acos(builtProfile.x / builtProfile.z);
            float theta = Mathf.Lerp(angle, -angle, (float)i / count);
            return new Vector2(side * builtProfile.z * Mathf.Cos(theta), builtProfile.y * Mathf.Sin(theta));
        }

        void AddSegment(Vector2 a, Vector2 b, int outwardSign, int index, Vector3[] vertices, int[] triangles, bool solid = true)
        {
            Vector3 aw = transform.TransformPoint(a), bw = transform.TransformPoint(b);
            Vector3 tangent = (bw - aw).normalized;
            Vector3 outward = transform.TransformDirection(new Vector3(-(b - a).y, (b - a).x, 0)).normalized * outwardSign;
            if (solid)
            {
                var go = new GameObject("Wall " + index.ToString("000")) { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
                go.transform.SetParent(walls.transform, false);
                go.transform.SetPositionAndRotation((aw + bw) * .5f + outward * wallThickness * .5f,
                    Quaternion.LookRotation(transform.forward, outward));
                // The right axis follows the segment; small end overlap seals the joins.
                Vector3 scale = go.transform.lossyScale;
                var collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3((Vector3.Distance(aw, bw) + .025f) / Mathf.Abs(scale.x),
                    wallThickness / Mathf.Abs(scale.y), wallHeight / Mathf.Abs(scale.z));
            }
            Vector3 offset = outward * borderWidth * .5f;
            Vector3 overlap = tangent * borderWidth * .25f;
            // Grid local -Z faces the top-down camera.
            Vector3 lift = -transform.forward * .03f;
            int v = index * 4, t = index * 6;
            vertices[v] = transform.InverseTransformPoint(aw - overlap - offset + lift);
            vertices[v + 1] = transform.InverseTransformPoint(aw - overlap + offset + lift);
            vertices[v + 2] = transform.InverseTransformPoint(bw + overlap + offset + lift);
            vertices[v + 3] = transform.InverseTransformPoint(bw + overlap - offset + lift);
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        void OnDisable()
        {
            if (gridRenderer && block != null)
            {
                gridRenderer.GetPropertyBlock(block); block.SetVector("_ArenaOval", Vector4.zero); gridRenderer.SetPropertyBlock(block);
            }
            ReleaseGeometry();
        }
        void OnDestroy() { ReleaseGeometry(); }
        void ReleaseGeometry()
        {
            if (walls) { walls.SetActive(false); Release(walls); }
            Release(borderMesh); Release(borderMaterial);
            walls = null; borderMesh = null; borderMaterial = null;
        }
        static void Release(Object value)
        { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
