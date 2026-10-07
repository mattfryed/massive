// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System.IO;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor.WireframeMeshGenerator
{
    internal class BatchObject : BatchObjectBase
    {
        public enum OptionsState { Yes, No, Mixed }


        public List<BatchObjectMeshInfo> meshInfo;

        public OptionsState isMeshAssetFormat;

        public string submeshCount;
        public int vertexCountOfOriginalMesh;
        public string vertexAndTriangleCountOfOriginalMesh;
        public string vertexCountOfWireframeMesh;

        public UnityEngine.Rendering.IndexFormat indexFormat;

        public bool hasMeshProblems;

        public BatchObject(UnityEngine.Object unityObject, bool expanded)
            : base(unityObject, expanded)
        {

            meshInfo = new List<BatchObjectMeshInfo>();


            MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters != null && meshFilters.Length > 0)
            {
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    if (meshFilters[i] != null && meshFilters[i].sharedMesh != null)
                    {
                        meshInfo.Add(new BatchObjectMeshInfo(meshFilters[i], null, meshFilters[i].gameObject.GetComponent<Renderer>()));
                    }
                }
            }

            SkinnedMeshRenderer[] skinnedMeshRenderer = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.Length > 0)
            {
                for (int i = 0; i < skinnedMeshRenderer.Length; i++)
                {
                    if (skinnedMeshRenderer[i] != null && skinnedMeshRenderer[i].sharedMesh != null)
                    {
                        meshInfo.Add(new BatchObjectMeshInfo(null, skinnedMeshRenderer[i], skinnedMeshRenderer[i]));
                    }
                }
            }

            hasMeshProblems = meshInfo.Any(c => c.invalidMeshData);


            vertexCountOfOriginalMesh = 0;
            if (meshInfo.Count > 0)
            {
                int submeshCountMin = meshInfo.Min(c => c.mesh.subMeshCount);
                int submeshCountMax = meshInfo.Max(c => c.mesh.subMeshCount);
                submeshCount = submeshCountMin == submeshCountMax ? submeshCountMin.ToString() : string.Format("{0} - {1}", submeshCountMin, submeshCountMax);


                vertexCountOfOriginalMesh = Mathf.Max(0, meshInfo.Sum(c => c.mesh.vertexCount));
                int trianglesSum = meshInfo.Sum(c => c.mesh.GetTrianglesCount());

                vertexAndTriangleCountOfOriginalMesh = $"{vertexCountOfOriginalMesh.ToString("N0")} / {(trianglesSum / 3).ToString("N0")}";
                int vertexCountIncreasePersaentage = vertexCountOfOriginalMesh == 0 ? 0 : ((int)(((float)trianglesSum - vertexCountOfOriginalMesh) / vertexCountOfOriginalMesh * 100));
                vertexCountOfWireframeMesh = trianglesSum.ToString("N0") + $"  ({vertexCountIncreasePersaentage}%)";

                indexFormat = trianglesSum > WireframeShaderConstants.vertexLimitIn16BitsIndexBuffer ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

                isMeshAssetFormat = OptionsState.Mixed;
                if (meshInfo.All(c => c.isMeshAssetFormat)) isMeshAssetFormat = OptionsState.Yes;
                else if (meshInfo.All(c => c.isMeshAssetFormat == false)) isMeshAssetFormat = OptionsState.No;
            }
        }
    }

    internal class BatchObjectMeshInfo : BatchObjectMeshInfoBase
    {
        public bool isMeshAssetFormat;

        public string vertexAndTriangleCountOfOriginalMesh;
        public string vertexCountOfWireframeMesh;
        public UnityEngine.Rendering.IndexFormat indexFormat;


        public BatchObjectMeshInfo(MeshFilter meshFilter, SkinnedMeshRenderer skinnedMeshRenderer, Renderer renderer)
            : base(meshFilter, skinnedMeshRenderer)
        {
            vertexAndTriangleCountOfOriginalMesh = string.Format("{0} / {1}", mesh.vertexCount.ToString("N0"), (mesh.GetTrianglesCount() / 3).ToString("N0"));
            int vertexCountIncreasePersentage = mesh.vertexCount == 0 ? 0 : (int)(((float)mesh.GetTrianglesCount() - mesh.vertexCount) / mesh.vertexCount * 100);
            vertexCountOfWireframeMesh = mesh.GetTrianglesCount().ToString("N0") + $"  ({vertexCountIncreasePersentage}%)";

            indexFormat = mesh.vertexCount >= WireframeShaderConstants.vertexLimitIn16BitsIndexBuffer ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

            isMeshAssetFormat = Path.GetExtension(UnityEditor.AssetDatabase.GetAssetPath(mesh)).Contains(".asset");
        }
    }
}
