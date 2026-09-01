// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>

namespace AmazingAssets.WireframeShader
{
    public static class WireframeShaderEnum
    {
        public enum RenderPipeline { Unknown, Builtin, Universal, HighDefinition }

        public enum VertexAttribute { UV0, UV1, UV2, UV3, UV4, UV5, UV6, UV7, Normal, Tangent }
        public enum Solver { Dynamic, Prebaked }
    }
}