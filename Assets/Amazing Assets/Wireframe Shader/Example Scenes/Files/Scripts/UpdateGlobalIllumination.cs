// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>

using UnityEngine;


namespace AmazingAssets.WireframeShader.Examples
{
    [ExecuteAlways]
    public class UpdateGlobalIllumination : MonoBehaviour
    {
        new public Renderer renderer;
        public Material wireframeMaterial;

        public void OnUIColorChange(float value)
        {
            if (wireframeMaterial != null)
                wireframeMaterial.SetColor("_Wireframe_Color", Color.HSVToRGB(value, 1, 1));

            if (renderer != null)
                RendererExtensions.UpdateGIMaterials(renderer);
        }
    }
}