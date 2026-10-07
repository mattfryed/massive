// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor.WireframeMeshGenerator
{
    [CustomEditor(typeof(AmazingAssets.WireframeShader.WireframeShaderConversionDetails))]
    public class WireframeShaderConversionDetailsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            using (new EditorGUIHelper.GUIEnabled(false))
            {
                EditorGUILayout.HelpBox("Script is not used in Runtime, only inside Editor for prefab management.", MessageType.Info);
            }
        }
    }
} 