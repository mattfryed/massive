// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System.IO;

using UnityEngine;


namespace AmazingAssets.WireframeShader.Editor.WireframeTextureGenerator
{
    internal class BatchObject
    {
        public Mesh mesh;
        public string meshSortName;
        public bool invalidMeshData;

        public bool hasUV0;
        public bool hasUV1;
        public bool hasUV2;
        public bool hasUV3;
        public bool hasUV4;
        public bool hasUV5;
        public bool hasUV6;
        public bool hasUV7;
        public bool hasNormal;
        public bool hasTangent;

        public string savePath;
        public string exception;
        

        public BatchObject(Mesh mesh)
        {
            this.mesh = mesh;

            if (mesh != null)
            {
                meshSortName = EditorUtilities.PadNumbers(mesh.name);
                hasUV0 = mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0);
            }
            
            invalidMeshData = WireframeShaderUtilities.IsMeshValid(mesh, false) == false;

            UnityEngine.Rendering.VertexAttributeDescriptor[] vertexAttributeDescriptor = mesh.GetVertexAttributes();
            for (int i = 0; i < vertexAttributeDescriptor.Length; i++)
            {
                switch (vertexAttributeDescriptor[i].attribute)
                {
                    case UnityEngine.Rendering.VertexAttribute.TexCoord0: hasUV0 = true; break;
                    case UnityEngine.Rendering.VertexAttribute.TexCoord1: hasUV1 = true; break;
                    case UnityEngine.Rendering.VertexAttribute.TexCoord2: hasUV2 = true; break;
                    case UnityEngine.Rendering.VertexAttribute.TexCoord3: hasUV3 = true; break;
                    case UnityEngine.Rendering.VertexAttribute.TexCoord4: hasUV4 = true; break;
                    case UnityEngine.Rendering.VertexAttribute.TexCoord5: hasUV5 = true; break;
                    case UnityEngine.Rendering.VertexAttribute.TexCoord6: hasUV6 = true; break;
                    case UnityEngine.Rendering.VertexAttribute.TexCoord7: hasUV7 = true; break;
                    case UnityEngine.Rendering.VertexAttribute.Normal: hasNormal = true; break;
                    case UnityEngine.Rendering.VertexAttribute.Tangent: hasTangent = true; break;

                    default: break;
                }
            }

            UpdateSavePath();
        }

        public void UpdateSavePath()
        {
            string assetName = EditorWindow.active.editorSettings.GetSaveAssetName(mesh, true);
            string assetSaveDirectory = EditorWindow.active.editorSettings.GetAssetSaveDirectory(mesh, true, true);

            savePath = Path.Combine(assetSaveDirectory, assetName);
        }
    }
}
