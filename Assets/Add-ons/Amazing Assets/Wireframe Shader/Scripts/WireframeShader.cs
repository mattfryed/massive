// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>

using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace AmazingAssets.WireframeShader
{
    static public class WireframeShaderCore
    {
        static public WireframeShader WireframeShader(this Mesh mesh)
        {
            return new WireframeShader(mesh);
        }
    }

    public class WireframeShader
    {
        Mesh mesh;


        public WireframeShader(Mesh mesh)
        {
            this.mesh = mesh;
        }
        public Mesh GenerateWireframeMesh(bool normalizeEdges, bool tryQuad, WireframeShaderEnum.VertexAttribute storeInside = WireframeShaderEnum.VertexAttribute.UV3)
        {
            if (WireframeShaderUtilities.IsMeshValid(this.mesh, true) == false)
            {
                WireframeShaderDebug.Log(LogType.Error, "Generating wireframe mesh has failed!\nMesh may be null, or vertex/triangle indexies are incorrect, or mesh is not readable.", null, this.mesh);
                return null;
            }

            if (this.mesh.GetTrianglesCount() * 3 >= WireframeShaderConstants.vertexLimitIn16BitsIndexBuffer && SystemInfo.supports32bitsIndexBuffer == false)
            {
                WireframeShaderDebug.Log(LogType.Error, "Generating wireframe mesh has failed!\nSystem does not support 32 bits index buffer.", null, this.mesh);
                return null;
            }


            Mesh wireframeMesh = ExplodeMesh(mesh);

            GenerateWireframeData(ref wireframeMesh, normalizeEdges, tryQuad, storeInside);

            return wireframeMesh;
        }
        public Texture2D GenerateWireframeTexture(int textureResolution, WireframeShaderEnum.Solver solver, WireframeShaderEnum.VertexAttribute readBakedWireframeFromAttribute, int submeshIndex, bool normalizeEdges, bool tryQuad, float thickness, float smoothness)
        {
            #region Check
            if (WireframeShaderUtilities.IsMeshValid(mesh, true) == false)
            {
                WireframeShaderDebug.Log(LogType.Error, "Generating wireframe texture has failed!\nMesh may be null, or vertex/triangle indexies are incorrect, or mesh is not readable.", null, mesh);
                return null;
            }

            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0) == false)
            {
                WireframeShaderDebug.Log(LogType.Error, $"Generating wireframe texture has failed!\nMesh '{mesh.name}' has no UV0.", null, mesh);
                return null;
            }

            if (submeshIndex >= mesh.subMeshCount)
            {
                WireframeShaderDebug.Log(LogType.Error, $"Generating wireframe texture has failed!\nMesh '{mesh.name}' submesh count is less than requested index ({submeshIndex}).", null, mesh);
                return null;
            }

            if (solver == WireframeShaderEnum.Solver.Dynamic)
            {
                if (SystemInfo.supportsGeometryShaders == false)
                {
                    WireframeShaderDebug.Log(LogType.Error, "Generating wireframe texture has failed!\nSystem doesn't support GeometryShaders.", null, mesh);
                    return null;
                }
            }
            else
            {
                if (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV0)
                {
                    WireframeShaderDebug.Log(LogType.Error, $"Generating wireframe texture has failed!\n'UV0' should contain default mesh UVs and not the wireframe data.", null, mesh);
                    return null;
                }


                if ((readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV0 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV1 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV2 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV3 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord3) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV4 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord4) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV5 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord5) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV6 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord6) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.UV7 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord7) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.Normal && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal) == false) ||
                    (readBakedWireframeFromAttribute == WireframeShaderEnum.VertexAttribute.Tangent && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent) == false))

                {
                    WireframeShaderDebug.Log(LogType.Error, $"Generating wireframe texture has failed!\nMesh '{mesh.name}' doesn't contain prebaked wireframe data in the '{readBakedWireframeFromAttribute}' buffer.", null, mesh);
                    return null;
                }
            }

            Shader shader = Shader.Find(WireframeShaderConstants.shaderWireframeTextureGenerator);
            if (shader == null)
            {
                WireframeShaderDebug.Log(LogType.Error, $"Generating wireframe texture has failed!\nShader '{WireframeShaderConstants.shaderWireframeTextureGenerator}' is missing.", null);
                return null;
            }
            #endregion


            if (submeshIndex < -1)
                submeshIndex = -1;


            textureResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(textureResolution), 16, 8192);



            //Save
            RenderTexture saveRT = RenderTexture.active;


            RenderTexture renderTexture = RenderTexture.GetTemporary(textureResolution, textureResolution, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 4);
            renderTexture.wrapMode = TextureWrapMode.Clamp;

            RenderTexture.active = renderTexture;


            Material material = new Material(shader);
            if (solver == WireframeShaderEnum.Solver.Dynamic)
            {
                material.EnableKeyword("WIREFRAME_CALCULATE_USING_GEOMETRY_SHADER");

                if (normalizeEdges)
                    material.EnableKeyword("WIREFRAME_NORMALIZE_EDGES_ON");
                if (tryQuad)
                    material.EnableKeyword("WIREFRAME_TRY_QUAD_ON");
            }

            material.SetFloat("_WireframeShader_Thickness", Mathf.Clamp01(thickness));
            material.SetFloat("_WireframeShader_Smoothness", Mathf.Clamp01(smoothness));

            GL.Clear(false, true, Color.clear, 1.0f);


            //Render mesh RGB
            material.SetPass(0);
            Graphics.DrawMeshNow(mesh, Vector3.zero, Quaternion.identity, submeshIndex);



            //Create texture
            Texture2D texture = new Texture2D(textureResolution, textureResolution);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = mesh.name;



            //Read final
            texture.ReadPixels(new Rect(0, 0, textureResolution, textureResolution), 0, 0, true);
            texture.Apply(true);


            //Cleanup
            RenderTexture.ReleaseTemporary(renderTexture);

            GameObject.DestroyImmediate(material);


            if (Application.isEditor)
                RenderTexture.active = null;
            else
                RenderTexture.active = saveRT;

            return texture;
        }

        static Mesh ExplodeMesh(Mesh sourceMesh)
        {
            Vector3[] mVertices = sourceMesh.vertices;
            List<Vector4> mUV0 = null;
            List<Vector4> mUV1 = null;
            List<Vector4> mUV2 = null;
            List<Vector4> mUV3 = null;
            List<Vector4> mUV4 = null;
            List<Vector4> mUV5 = null;
            List<Vector4> mUV6 = null;
            List<Vector4> mUV7 = null;
            Vector3[] mNormal = null;
            Vector4[] mTangent = null;
            Color[] mColor = null;
            BoneWeight[] mBW = null;


            List<Vector3> newVertices = new List<Vector3>();
            List<List<int>> subMeshIndeces = new List<List<int>>();
            List<Vector4> newUV0 = null;
            List<Vector4> newUV1 = null;
            List<Vector4> newUV2 = null;
            List<Vector4> newUV3 = null;
            List<Vector4> newUV4 = null;
            List<Vector4> newUV5 = null;
            List<Vector4> newUV6 = null;
            List<Vector4> newUV7 = null;
            List<Vector3> newNormal = null;
            List<Vector4> newTangent = null;
            List<Color> newColor = null;
            List<BoneWeight> newBW = null;


            bool hasUV0 = false;
            bool hasUV1 = false;
            bool hasUV2 = false;
            bool hasUV3 = false;
            bool hasUV4 = false;
            bool hasUV5 = false;
            bool hasUV6 = false;
            bool hasUV7 = false;
            bool hasNormal = false;
            bool hasTangent = false;
            bool hasVertexColor = false;
            bool hasSkin = false;


            UnityEngine.Rendering.VertexAttributeDescriptor[] vertexAttributeDescriptor = sourceMesh.GetVertexAttributes();
            for (int i = 0; i < vertexAttributeDescriptor.Length; i++)
            {
                switch (vertexAttributeDescriptor[i].attribute)
                {
                    case UnityEngine.Rendering.VertexAttribute.TexCoord0:
                        mUV0 = new List<Vector4>(); sourceMesh.GetUVs(0, mUV0);
                        newUV0 = new List<Vector4>();
                        hasUV0 = true; break;

                    case UnityEngine.Rendering.VertexAttribute.TexCoord1:
                        mUV1 = new List<Vector4>(); sourceMesh.GetUVs(1, mUV1);
                        newUV1 = new List<Vector4>();
                        hasUV1 = true; break;

                    case UnityEngine.Rendering.VertexAttribute.TexCoord2:
                        mUV2 = new List<Vector4>(); sourceMesh.GetUVs(2, mUV2);
                        newUV2 = new List<Vector4>();
                        hasUV2 = true; break;

                    case UnityEngine.Rendering.VertexAttribute.TexCoord3:
                        mUV3 = new List<Vector4>(); sourceMesh.GetUVs(3, mUV3);
                        newUV3 = new List<Vector4>();
                        hasUV3 = true; break;

                    case UnityEngine.Rendering.VertexAttribute.TexCoord4:
                        mUV4 = new List<Vector4>(); sourceMesh.GetUVs(4, mUV4);
                        newUV4 = new List<Vector4>();
                        hasUV4 = true; break;

                    case UnityEngine.Rendering.VertexAttribute.TexCoord5:
                        mUV5 = new List<Vector4>(); sourceMesh.GetUVs(5, mUV5);
                        newUV5 = new List<Vector4>();
                        hasUV5 = true; break;

                    case UnityEngine.Rendering.VertexAttribute.TexCoord6:
                        mUV6 = new List<Vector4>(); sourceMesh.GetUVs(6, mUV6);
                        newUV6 = new List<Vector4>();
                        hasUV6 = true; break;

                    case UnityEngine.Rendering.VertexAttribute.TexCoord7:
                        mUV7 = new List<Vector4>(); sourceMesh.GetUVs(7, mUV7);
                        newUV7 = new List<Vector4>();
                        hasUV7 = true; break;

                    case UnityEngine.Rendering.VertexAttribute.Normal:
                        mNormal = sourceMesh.normals;
                        newNormal = new List<Vector3>();
                        hasNormal = true; break;

                    case UnityEngine.Rendering.VertexAttribute.Tangent:
                        mTangent = sourceMesh.tangents;
                        newTangent = new List<Vector4>();
                        hasTangent = true; break;

                    case UnityEngine.Rendering.VertexAttribute.Color:
                        mColor = sourceMesh.colors;
                        newColor = new List<Color>();
                        hasVertexColor = true; break;

                    case UnityEngine.Rendering.VertexAttribute.BlendIndices:
                    case UnityEngine.Rendering.VertexAttribute.BlendWeight:
                        if (hasSkin == false)
                        {
                            mBW = sourceMesh.boneWeights;
                            newBW = new List<BoneWeight>();
                            hasSkin = true;
                        }
                        break;

                    default:
                        break;
                }
            }


            int tIndec = 0;
            for (int i = 0; i < sourceMesh.subMeshCount; i++)
            {
                int[] mT = sourceMesh.GetTriangles(i);

                subMeshIndeces.Add(new List<int>());

                for (int j = 0; j < mT.Length; j += 3)
                {
                    int index1 = mT[j];
                    int index2 = mT[j + 1];
                    int index3 = mT[j + 2];

                    //Indexes
                    subMeshIndeces[subMeshIndeces.Count - 1].Add(tIndec++);
                    subMeshIndeces[subMeshIndeces.Count - 1].Add(tIndec++);
                    subMeshIndeces[subMeshIndeces.Count - 1].Add(tIndec++);

                    //Add vertices
                    newVertices.Add(mVertices[index1]);
                    newVertices.Add(mVertices[index2]);
                    newVertices.Add(mVertices[index3]);

                    //UV0
                    if (hasUV0)
                    {
                        newUV0.Add(mUV0[index1]);
                        newUV0.Add(mUV0[index2]);
                        newUV0.Add(mUV0[index3]);
                    }
                    //UV1
                    if (hasUV1)
                    {
                        newUV1.Add(mUV1[index1]);
                        newUV1.Add(mUV1[index2]);
                        newUV1.Add(mUV1[index3]);
                    }

                    //UV2
                    if (hasUV2)
                    {
                        newUV2.Add(mUV2[index1]);
                        newUV2.Add(mUV2[index2]);
                        newUV2.Add(mUV2[index3]);
                    }
                    //UV3
                    if (hasUV3)
                    {
                        newUV3.Add(mUV3[index1]);
                        newUV3.Add(mUV3[index2]);
                        newUV3.Add(mUV3[index3]);
                    }
                    //UV4
                    if (hasUV4)
                    {
                        newUV4.Add(mUV4[index1]);
                        newUV4.Add(mUV4[index2]);
                        newUV4.Add(mUV4[index3]);
                    }
                    //UV5
                    if (hasUV5)
                    {
                        newUV5.Add(mUV5[index1]);
                        newUV5.Add(mUV5[index2]);
                        newUV5.Add(mUV5[index3]);
                    }
                    //UV6
                    if (hasUV6)
                    {
                        newUV6.Add(mUV6[index1]);
                        newUV6.Add(mUV6[index2]);
                        newUV6.Add(mUV6[index3]);
                    }
                    //UV7
                    if (hasUV7)
                    {
                        newUV7.Add(mUV7[index1]);
                        newUV7.Add(mUV7[index2]);
                        newUV7.Add(mUV7[index3]);
                    }

                    //Normal
                    if (hasNormal)
                    {
                        newNormal.Add(mNormal[index1]);
                        newNormal.Add(mNormal[index2]);
                        newNormal.Add(mNormal[index3]);
                    }
                    //Tangent
                    if (hasTangent)
                    {
                        newTangent.Add(mTangent[index1]);
                        newTangent.Add(mTangent[index2]);
                        newTangent.Add(mTangent[index3]);
                    }
                    //Vertex Color
                    if (hasVertexColor)
                    {
                        newColor.Add(mColor[index1]);
                        newColor.Add(mColor[index2]);
                        newColor.Add(mColor[index3]);
                    }
                    //Skinn
                    if (hasSkin)
                    {
                        newBW.Add(mBW[index1]);
                        newBW.Add(mBW[index2]);
                        newBW.Add(mBW[index3]);
                    }
                }
            }



            Mesh explodedMesh = new Mesh();
            explodedMesh.name = string.IsNullOrEmpty(sourceMesh.name) ? sourceMesh.GetInstanceID().ToString() : sourceMesh.name;

            //Set indexFormat before subMeshCount!
            explodedMesh.indexFormat = newVertices.Count >= WireframeShaderConstants.vertexLimitIn16BitsIndexBuffer ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

            explodedMesh.subMeshCount = sourceMesh.subMeshCount;


            explodedMesh.vertices = newVertices.ToArray();
            for (int i = 0; i < subMeshIndeces.Count; i++)
                explodedMesh.SetTriangles(subMeshIndeces[i].ToArray(), i);

            if (hasUV0)
                explodedMesh.SetUVs(0, new List<Vector4>(newUV0));
            if (hasUV1)
                explodedMesh.SetUVs(1, new List<Vector4>(newUV1));
            if (hasUV2)
                explodedMesh.SetUVs(2, new List<Vector4>(newUV2));
            if (hasUV3)
                explodedMesh.SetUVs(3, new List<Vector4>(newUV3));
            if (hasUV4)
                explodedMesh.SetUVs(4, new List<Vector4>(newUV4));
            if (hasUV5)
                explodedMesh.SetUVs(5, new List<Vector4>(newUV5));
            if (hasUV6)
                explodedMesh.SetUVs(6, new List<Vector4>(newUV6));
            if (hasUV7)
                explodedMesh.SetUVs(7, new List<Vector4>(newUV7));

            if (hasNormal)
                explodedMesh.normals = newNormal.ToArray();
            if (hasTangent)
                explodedMesh.tangents = newTangent.ToArray();
            if (hasVertexColor)
                explodedMesh.colors = newColor.ToArray();

            if (hasSkin)
            {
                explodedMesh.boneWeights = newBW.ToArray();
                explodedMesh.bindposes = sourceMesh.bindposes;
            }



            #region BlendShape
            if (sourceMesh.blendShapeCount > 0)
            {
                Dictionary<int, int> blensShapesIndexMap = new Dictionary<int, int>();


                int dataIndex = -1;
                for (int i = 0; i < sourceMesh.subMeshCount; i++)
                {
                    int vCount = sourceMesh.GetTriangles(i).Length;

                    int[] mT = sourceMesh.GetTriangles(i);

                    for (int j = 0; j < vCount; j++)
                    {
                        int index = mT[j];

                        blensShapesIndexMap.Add(++dataIndex, index);
                    }
                }


                int dataCount = sourceMesh.GetTrianglesCount();


                var deltaVertices = new Vector3[sourceMesh.vertexCount];
                var deltaNormals = new Vector3[sourceMesh.vertexCount];
                var deltaTangents = new Vector3[sourceMesh.vertexCount];
                var newDeltaVertices = new Vector3[dataCount];
                var newDeltaNormals = new Vector3[dataCount];
                var newDeltaTangents = new Vector3[dataCount];
                for (int shapeIndex = 0; shapeIndex < sourceMesh.blendShapeCount; shapeIndex++)
                {
                    var shapeName = sourceMesh.GetBlendShapeName(shapeIndex);
                    var frameCount = sourceMesh.GetBlendShapeFrameCount(shapeIndex);

                    for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                    {
                        var frameWeight = sourceMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                        sourceMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);

                        for (int newIdx = 0; newIdx < dataCount; newIdx++)
                        {
                            int idx = blensShapesIndexMap[newIdx];
                            newDeltaVertices[newIdx] = deltaVertices[idx];
                            newDeltaNormals[newIdx] = deltaNormals[idx];
                            newDeltaTangents[newIdx] = deltaNormals[idx];
                        }

                        explodedMesh.AddBlendShapeFrame(shapeName, frameWeight, newDeltaVertices, newDeltaNormals, newDeltaTangents);
                    }
                }
            }
            #endregion


            return explodedMesh;
        }
        static void GenerateWireframeData(ref Mesh mesh, bool normalizeEdges, bool tryQuad, WireframeShaderEnum.VertexAttribute saveIn)
        {
            Vector3[] vertices = mesh.vertices;
            Vector4[] wireframeData = new Vector4[mesh.vertexCount];

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                int[] mT = mesh.GetTriangles(i);
                for (int j = 0; j < mT.Length; j += 3)
                {
                    int index1 = mT[j];
                    int index2 = mT[j + 1];
                    int index3 = mT[j + 2];

                    GenerateWireframeData(vertices[index1], vertices[index2], vertices[index3], normalizeEdges, tryQuad, out wireframeData[index1], out wireframeData[index2], out wireframeData[index3]);
                }
            }


            switch (saveIn)
            {
                case WireframeShaderEnum.VertexAttribute.UV0: mesh.SetUVs(0, wireframeData); break;
                case WireframeShaderEnum.VertexAttribute.UV1: mesh.SetUVs(1, wireframeData); break;
                case WireframeShaderEnum.VertexAttribute.UV2: mesh.SetUVs(2, wireframeData); break;
                case WireframeShaderEnum.VertexAttribute.UV3: mesh.SetUVs(3, wireframeData); break;
                case WireframeShaderEnum.VertexAttribute.UV4: mesh.SetUVs(4, wireframeData); break;
                case WireframeShaderEnum.VertexAttribute.UV5: mesh.SetUVs(5, wireframeData); break;
                case WireframeShaderEnum.VertexAttribute.UV6: mesh.SetUVs(6, wireframeData); break;
                case WireframeShaderEnum.VertexAttribute.UV7: mesh.SetUVs(7, wireframeData); break;
                case WireframeShaderEnum.VertexAttribute.Normal: mesh.normals = wireframeData.Select(c => new Vector3(c.x, c.y, c.z)).ToArray(); break;
                case WireframeShaderEnum.VertexAttribute.Tangent: mesh.tangents = wireframeData; break;

                default:
                    break;
            }
        }
        static void GenerateWireframeData(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, bool normalizeEdges, bool tryQuad, out Vector4 uv1, out Vector4 uv2, out Vector4 uv3)
        {
            if (normalizeEdges)
            {
                float d1 = Vector3.Distance(vertex1, vertex2);
                float d2 = Vector3.Distance(vertex2, vertex3);
                float d3 = Vector3.Distance(vertex3, vertex1);

                Vector4 b = new Vector4(0, DistanceToEdge(vertex3, vertex1, vertex2) / d1, DistanceToEdge(vertex1, vertex2, vertex3) / d2, DistanceToEdge(vertex2, vertex1, vertex3) / d3);
                b /= Mathf.Min(b.y, b.z, b.w);


                uv1 = new Vector4(b.x, b.z, b.x, 0);
                uv2 = new Vector4(b.x, b.x, b.w, 0);
                uv3 = new Vector4(b.y, b.x, b.x, 0);

                if (tryQuad)
                {
                    uv1.x = ((d1 > d2) && (d1 > d3)) ? 10000 : 0;
                    uv1.z = ((d3 >= d1) && (d3 > d2)) ? 10000 : 0;
                    uv2.y = ((d2 >= d1) && (d2 >= d3)) ? 10000 : 0;
                }
            }
            else
            {
                if (tryQuad)
                {
                    float d1 = Vector3.Distance(vertex1, vertex2);
                    float d2 = Vector3.Distance(vertex2, vertex3);
                    float d3 = Vector3.Distance(vertex3, vertex1);

                    Vector4 offset = Vector4.zero;
                    if (d1 > d2 && d1 > d3)
                        offset.y = 1;
                    else if (d2 > d3 && d2 > d1)
                        offset.x = 1;
                    else
                        offset.z = 1;

                    uv1 = new Vector4(1, 0, 0, 0) + offset;
                    uv2 = new Vector4(0, 0, 1, 0) + offset;
                    uv3 = new Vector4(0, 1, 0, 0) + offset;
                }
                else
                {
                    uv1 = new Vector4(1, 0, 0, 0);
                    uv2 = new Vector4(0, 1, 0, 0);
                    uv3 = new Vector4(0, 0, 1, 0);
                }
            }
        }
        static float DistanceToEdge(Vector3 a, Vector3 b, Vector3 c)
        {
            return Vector3.Magnitude(Vector3.Cross(a - b, a - c));
        }
    }
}