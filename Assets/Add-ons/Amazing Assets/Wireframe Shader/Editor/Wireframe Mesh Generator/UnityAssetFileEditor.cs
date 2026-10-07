// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System.Linq;

using UnityEditor;
using UnityEngine;


namespace AmazingAssets.WireframeShader.Editor.WireframeMeshGenerator
{
    [CustomEditor(typeof(UnityAssetFile))]
    public class UnityAssetFileEditor : UnityEditor.Editor
    {
        string infoFileSize;
        string infoMeshCount;
        string infoMeshVertexCount;

        void OnEnable()
        {
            string assetPath = AssetDatabase.GetAssetPath((UnityAssetFile)target);

            infoMeshCount = string.Empty;
            infoMeshVertexCount = string.Empty;

            if (string.IsNullOrEmpty(assetPath) == false)
            {
                infoFileSize = UnityEditor.EditorUtility.FormatBytes(new System.IO.FileInfo(assetPath).Length);
                Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(c => c is Mesh).Cast<Mesh>().ToArray();

                if (meshes != null && meshes.Length > 0)
                {
                    infoMeshCount = meshes.Length.ToString("N0");

                    int combinedVertexCount = meshes.Sum(c => c.vertexCount);

                    infoMeshVertexCount = combinedVertexCount.ToString("N0");
                }
            }
        }

        public override void OnInspectorGUI()
        {
            if (string.IsNullOrEmpty(infoMeshCount))
            {
                EditorGUILayout.HelpBox("No meshes available.", MessageType.Warning);
                return;
            }

            GUILayout.Space(10);
            using (new EditorGUIHelper.EditorGUIIndentLevel(1))
            {
                EditorGUILayout.LabelField("File Size: ", infoFileSize);
                EditorGUILayout.LabelField("Mesh Count: ", infoMeshCount);
                EditorGUILayout.LabelField("Vertex Count: ", infoMeshVertexCount);
            }
        }
    }
}