// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>

using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;


[assembly: InternalsVisibleTo("AmazingAssets.WireframeShader.Editor", AllInternalsVisible = true)]
[assembly: InternalsVisibleTo("AmazingAssets.WireframeShaderEditor", AllInternalsVisible = true)]
namespace AmazingAssets.WireframeShader
{
    static internal class WireframeShaderUtilities
    {
        static internal WireframeShaderEnum.RenderPipeline GetCurrentRenderPipeline()
        {
#if UNITY_6000_0_OR_NEWER
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null && UnityEngine.QualitySettings.renderPipeline == null)
                return WireframeShaderEnum.RenderPipeline.Builtin;
            else
            {
                string currentType = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null ? UnityEngine.QualitySettings.renderPipeline.GetType().ToString() :
                                                                                                      UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.GetType().ToString();

                string parentType = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null ? UnityEngine.QualitySettings.renderPipeline.GetType().GetTypeInfo().BaseType.ToString() :
                                                                                                           UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.GetType().GetTypeInfo().BaseType.ToString();
#else
            if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset == null && UnityEngine.QualitySettings.renderPipeline == null)
                return WireframeShaderEnum.RenderPipeline.Builtin;
            else
            {
                string currentType = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset == null ? UnityEngine.QualitySettings.renderPipeline.GetType().ToString() :
                                                                                                    UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().ToString();

                string parentType = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset == null ? UnityEngine.QualitySettings.renderPipeline.GetType().GetTypeInfo().BaseType.ToString() :
                                                                                                         UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().GetTypeInfo().BaseType.ToString();
#endif

                if (currentType.Contains("UnityEngine.Rendering.Universal.") || parentType.Contains("UnityEngine.Rendering.Universal."))
                    return WireframeShaderEnum.RenderPipeline.Universal;

                else if (currentType.Contains("UnityEngine.Rendering.HighDefinition.") || parentType.Contains("UnityEngine.Rendering.HighDefinition."))
                    return WireframeShaderEnum.RenderPipeline.HighDefinition;


                WireframeShaderDebug.Log(LogType.Error, "Undefined Render Pipeline '" + currentType + "'");
                return WireframeShaderEnum.RenderPipeline.Unknown;
            }
        }
        static internal string GetUnityVersion(bool mainVersionOnly = false)
        {
            string[] info = Application.unityVersion.Split('.');
            if (info == null || info.Length < 2)
            {
                WireframeShaderDebug.Log(LogType.Error, $"Undefined Unity version '{Application.unityVersion}'");
                return string.Empty;
            }

            string unityVersion = info[0];

            if (mainVersionOnly == false)
                unityVersion += "." + info[1];


            return unityVersion;
        }
        static internal void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                StackTraceLogType save = Application.GetStackTraceLogType(LogType.Error);
                Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);


                if (Application.isEditor)
                    GameObject.DestroyImmediate(obj);
                else
                    GameObject.Destroy(obj);


                Application.SetStackTraceLogType(LogType.Error, save);
            }
        }
        static internal Mesh CombineGameObjects(GameObject parentGameObject, bool keepSubMeshes, out List<Material> subMeshesMaterials, string meshName)
        {
            subMeshesMaterials = null;


            if (parentGameObject == null)
            {
                WireframeShaderDebug.Log(LogType.Warning, "Cannot combine null GameObject.", null);
                return null;
            }

            MeshFilter[] meshFilters = parentGameObject.GetComponentsInChildren<MeshFilter>();
            if (meshFilters == null || meshFilters.Length == 0)
            {
                WireframeShaderDebug.Log(LogType.Warning, $"GameObject '{parentGameObject}' has no MeshFilters to combine.", null);
                return null;
            }


            //Destroy LOD Groups
            DestroyLODGroupRenderers(parentGameObject);


            //Save parent transform
            Vector3 savePosition = parentGameObject.transform.position;
            Quaternion saveRotation = parentGameObject.transform.rotation;
            Vector3 saveScale = parentGameObject.transform.localScale;

            //Reset parent transform
            parentGameObject.transform.localPosition = Vector3.zero;
            parentGameObject.transform.rotation = Quaternion.identity;
            parentGameObject.transform.localScale = Vector3.one;


            int verticesCount = 0;
            List<CombineInstance> combineInstances = new List<CombineInstance>();

            for (int i = 0; i < meshFilters.Length; i++)
            {
                if (meshFilters[i] != null && meshFilters[i].sharedMesh != null)
                {
                    for (int s = 0; s < meshFilters[i].sharedMesh.subMeshCount; s++)
                    {
                        MeshRenderer meshRenderer = meshFilters[i].gameObject.GetComponent<MeshRenderer>();
                        if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                        {
                            CombineInstance instance = new CombineInstance
                            {
                                mesh = meshFilters[i].sharedMesh,
                                subMeshIndex = s,
                                transform = meshFilters[i].transform.localToWorldMatrix
                            };
                            combineInstances.Add(instance);

                            verticesCount += meshFilters[i].sharedMesh.vertexCount;
                        }
                    }


                    //Collect submesh materials.
                    //Each submesh needs its own material, even NULL!
                    if (keepSubMeshes)
                    {
                        if (subMeshesMaterials == null)
                            subMeshesMaterials = new List<Material>();

                        Material[] sharedMaterials = new Material[meshFilters[i].sharedMesh.subMeshCount];
                        sharedMaterials.Populate(null);


                        MeshRenderer meshRenderer = meshFilters[i].gameObject.GetComponent<MeshRenderer>();
                        if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                        {
                            for (int m = 0; m < meshRenderer.sharedMaterials.Length; m++)
                            {
                                if (m >= meshFilters[i].sharedMesh.subMeshCount)
                                    break;

                                sharedMaterials[m] = meshRenderer.sharedMaterials[m];
                            }

                            subMeshesMaterials.AddRange(sharedMaterials);
                        }
                    }
                }
            }


            Mesh combinedMesh = new Mesh();
            combinedMesh.subMeshCount = combineInstances.Count;
            if (verticesCount > WireframeShaderConstants.vertexLimitIn16BitsIndexBuffer)
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            combinedMesh.CombineMeshes(combineInstances.ToArray(), keepSubMeshes ? false : true, true);
            combinedMesh.name = meshName;


            //Restore parent transform
            parentGameObject.transform.localPosition = savePosition;
            parentGameObject.transform.rotation = saveRotation;
            parentGameObject.transform.localScale = saveScale;

            return combinedMesh;
        }
        static void DestroyLODGroupRenderers(GameObject parent)
        {
            LODGroup[] lodGroup = parent.GetComponentsInChildren<LODGroup>();
            for (int i = 0; i < lodGroup.Length; i++)
            {
                if (lodGroup[i] != null)
                {
                    LOD[] lods = lodGroup[i].GetLODs();

                    if (lods != null && lods.Length > 1)
                    {
                        for (int j = 1; j < lods.Length; j++)   // Check LODs compared to LOD[0]
                        {
                            if (lods[j].renderers != null && lods[j].renderers.Length > 0)
                            {
                                for (int k = 0; k < lods[j].renderers.Length; k++)
                                {
                                    Renderer currentRenderer = lods[j].renderers[k];

                                    //If current renderer is not part of LOD[0] - destroy it
                                    if (currentRenderer != null && lods[0].renderers != null && lods[0].renderers.Contains(currentRenderer) == false)
                                    {
                                        DestroyUnityObject(currentRenderer.GetComponent<MeshFilter>());

                                        DestroyUnityObject(currentRenderer);
                                    }
                                }
                            }
                        }
                    }
                }


                DestroyUnityObject(lodGroup[i]);
            }
        }

        static internal bool IsMeshValid(Mesh mesh, bool needToBeReadable)
        {
            if (mesh == null || mesh.vertexCount < 3 || mesh.GetTrianglesCount() < 3)
                return false;

            if (needToBeReadable && mesh.isReadable == false)
                return false;

            return true;
        }
    }
}