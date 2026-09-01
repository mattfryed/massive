// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor.WireframeMeshGenerator
{
    internal static class Run
    {
        static List<BatchObject> listBatchObjects;
        static EditorSettings editorSettings;


        static int progressBarTotalMeshCount;
        static int progressBarCurrentMeshIndex;
        static bool progressBarCanceled;


        static GameObject cloneGameObject;   //Hold instantiated clone gameObject, to make it possible to clear scene in the case of error during conversion


        static public void RunConverter()
        {
            listBatchObjects = EditorWindow.active.listBatchObjects;
            editorSettings = EditorWindow.active.editorSettings;


            bool conversionFailed = false;
            BatchObject currentBatchObject = null;
            cloneGameObject = null;


            progressBarTotalMeshCount = listBatchObjects.Sum(c => (c.meshInfo == null ? 0 : c.meshInfo.Count));
            progressBarCurrentMeshIndex = 0;
            progressBarCanceled = false;



            //Delete temp directory
            EditorUtilities.DeleteTempDirectory();

            EditorWindow.active.lastSavedFilePath = string.Empty;

            //Remove focus
            GUI.FocusControl(string.Empty);

            //Make meshes readable
            MakeMeshesReadable(true);


            try
            {
                for (int i = 0; i < listBatchObjects.Count; i++)
                {
                    currentBatchObject = listBatchObjects[i];
                    RunLoopConverter(i);

                    if (progressBarCanceled)
                        break;
                }
            }
            catch (Exception e)
            {
                conversionFailed = true;


                if (currentBatchObject == null || currentBatchObject.gameObject == null)
                    WireframeShaderDebug.Log(LogType.Error, "Encountered an unknown error.", null);
                else
                {
                    WireframeShaderDebug.Log(LogType.Error, string.Format("Encountered an error with object '{0}'.\n{1}", currentBatchObject.gameObject, e.Message), null);

                    EditorWindow.active.problematicBatchObject = currentBatchObject;
                    EditorWindow.active.problematicBatchObject.exception = e.Message;
                }
            }

            if (conversionFailed)
            {
                WireframeShaderUtilities.DestroyUnityObject(cloneGameObject);


                if (Directory.Exists(EditorUtilities.GetAssetTempFolder()))
                    Directory.Delete(EditorUtilities.GetAssetTempFolder(), true);

                string metaFile = EditorUtilities.GetAssetTempFolder() + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            else if (string.IsNullOrWhiteSpace(EditorWindow.active.lastSavedFilePath) == false)
            {
                AssetDatabase.SaveAssets();

                //Copy files to custom location
                if (IsSaveLocationProjectRelative() == false && Directory.Exists(EditorUtilities.GetAssetTempFolder()))
                {
                    EditorWindow.active.lastSavedFilePath = EditorUtilities.CopyTempFolderToDestination(editorSettings.saveFolderCustomPath);

                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
            }



            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();

            UnityEditor.EditorUtility.ClearProgressBar();
        }
        static void RunLoopConverter(int loopIndex)
        {
            if (listBatchObjects[loopIndex] == null || listBatchObjects[loopIndex].gameObject == null)
                return;


            GameObject currentBatchGameObject = listBatchObjects[loopIndex].gameObject;


            string prefabName;
            string prefabSaveDirectory;
            if (GetPrefabNameAndSaveDirectory(currentBatchGameObject, out prefabName, out prefabSaveDirectory) == false)
                return;


            //Create clone gameObject
            if (CreateCloneGameObject(currentBatchGameObject, prefabName) == false)
                return;


            //Generate and save meshes
            if (GenerateWireframe() == false)
            {
                WireframeShaderUtilities.DestroyUnityObject(cloneGameObject);

                return;
            }


            //Key - source material's instance ID
            //Value - new material used instead of the source
            Dictionary<int, Material> prefabMaterials;
            PreparePrefabMaterials(ref cloneGameObject, out prefabMaterials, prefabSaveDirectory, prefabName);


            if ((editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.Prefab || editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.MeshOnly) &&
                (editorSettings.generateMeshCombineType == EditorSettings.Enum.MeshCombineType.OneMeshWithSubmeshes || editorSettings.generateMeshCombineType == EditorSettings.Enum.MeshCombineType.Everything))
            {
                CombineMeshesAndSave(cloneGameObject, prefabMaterials, prefabSaveDirectory, prefabName);
            }
            else
            {
                SaveMeshesSeparetly(cloneGameObject, prefabSaveDirectory, prefabName);
            }


            if (editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.MeshOnly ||
                editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.OverwriteOriginalMesh)
            {
                WireframeShaderUtilities.DestroyUnityObject(cloneGameObject);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return;
            }



            SetupColliders(cloneGameObject);


            SetFlags(cloneGameObject);


            //Save prefab
            GameObject prefab = SaveMainPrefab(cloneGameObject, prefabSaveDirectory, prefabName);


            //If source object is not part of the current scene, destroy it
            if (currentBatchGameObject.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene() ||
                IsSaveLocationProjectRelative() == false ||
                CountPrefabReferencesInThisScene(prefab) > 1)
            {
                WireframeShaderUtilities.DestroyUnityObject(cloneGameObject);

                UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            }


            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        static bool GetPrefabNameAndSaveDirectory(GameObject currentBatchGameObject, out string prefabName, out string prefabSaveDirectory)
        {
            prefabName = string.Empty;
            prefabSaveDirectory = string.Empty;


            if (editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.Prefab || editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.MeshOnly)
            {
                prefabName = editorSettings.GetSaveAssetName(currentBatchGameObject, true);
                prefabSaveDirectory = editorSettings.GetAssetSaveDirectory(currentBatchGameObject, true, true);

                if (EditorUtilities.IsPathWithinStreamingAssetsFolder(prefabSaveDirectory))
                {
                    WireframeShaderDebug.Log(LogType.Warning, $"Cannot convert/save '{currentBatchGameObject.name}' files into streaming assets folder '{prefabSaveDirectory}'");
                    return false;
                }

                try
                {
                    Directory.CreateDirectory(prefabSaveDirectory);
                }
                catch
                {
                    WireframeShaderDebug.Log(LogType.Error, "Can not create directory at \"" + prefabSaveDirectory + "\". Try another save location.", null);
                    return false;
                }
            }

            return true;
        }
        static bool CreateCloneGameObject(GameObject source, string name)
        {
            //Remove all non-asset meshes
            if (editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.OverwriteOriginalMesh)
            {
                MeshFilter[] meshFilters = source.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    if (meshFilters[i] != null && meshFilters[i].sharedMesh != null)
                    {
                        if (Path.GetExtension(AssetDatabase.GetAssetPath(meshFilters[i].sharedMesh)).Contains(".asset") == false)
                            meshFilters[i].sharedMesh = null;
                    }
                }

                SkinnedMeshRenderer[] skinnedMeshRenderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < skinnedMeshRenderers.Length; i++)
                {
                    if (skinnedMeshRenderers[i] != null && skinnedMeshRenderers[i].sharedMesh != null)
                    {
                        if (Path.GetExtension(AssetDatabase.GetAssetPath(skinnedMeshRenderers[i].sharedMesh)).Contains(".asset") == false)
                            skinnedMeshRenderers[i].sharedMesh = null;
                    }
                }

                int finalMeshCount = meshFilters.Count(c => c != null && c.sharedMesh != null) + skinnedMeshRenderers.Count(c => c != null && c.sharedMesh != null);
                if (finalMeshCount == 0)
                {
                    WireframeShaderDebug.Log(LogType.Error, "'" + source.name + "' has no mesh in .asset format to overwrite.", null, source);

                    return false;
                }
            }


            cloneGameObject = GameObject.Instantiate(source);
            cloneGameObject.name = name;

            cloneGameObject.SetActive(true);

            if (source.transform.parent != null)
            {
                cloneGameObject.transform.position = source.transform.position;
                cloneGameObject.transform.rotation = source.transform.rotation;
                cloneGameObject.transform.localScale = source.transform.lossyScale;
            }


            //Replace SkinnedMeshRenderers with MeshFilters
            if ((editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.Prefab || editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.MeshOnly) &&
                editorSettings.generateMeshCombineType == EditorSettings.Enum.MeshCombineType.Everything)
            {
                SkinnedMeshRenderer[] skinnedMeshrenderers = cloneGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                if (skinnedMeshrenderers != null || skinnedMeshrenderers.Length > 0)
                {
                    for (int i = 0; i < skinnedMeshrenderers.Length; i++)
                    {
                        Mesh mesh = new Mesh();
#if UNITY_2020_2_OR_NEWER
                        skinnedMeshrenderers[i].BakeMesh(mesh, true);
#else
                        skinnedMeshrenderers[i].BakeMesh(mesh);
#endif
                        if (mesh == null)
                            mesh = skinnedMeshrenderers[i].sharedMesh;


                        MeshFilter meshFilter = skinnedMeshrenderers[i].gameObject.GetComponent<MeshFilter>();
                        if (meshFilter == null)
                            meshFilter = skinnedMeshrenderers[i].gameObject.AddComponent<MeshFilter>();
                        meshFilter.sharedMesh = mesh;

                        MeshRenderer meshRenderer = skinnedMeshrenderers[i].gameObject.GetComponent<MeshRenderer>();
                        if (meshRenderer == null)
                            meshRenderer = skinnedMeshrenderers[i].gameObject.AddComponent<MeshRenderer>();
                        meshRenderer.sharedMaterials = skinnedMeshrenderers[i].sharedMaterials;


                        WireframeShaderUtilities.DestroyUnityObject(skinnedMeshrenderers[i]);
                    }
                }
            }


            return true;
        }

        static bool GenerateWireframe()
        {
            //Key - source mesh InstanceID
            //Value - generated mesh
            Dictionary<int, Mesh> bakedMeshes = new Dictionary<int, Mesh>();

            bool mergeSubMeshes = editorSettings.generateMeshCombineType == EditorSettings.Enum.MeshCombineType.Submeshes || editorSettings.generateMeshCombineType == EditorSettings.Enum.MeshCombineType.Everything;

            //MeshFilters
            MeshFilter[] meshFilters = cloneGameObject.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                if (meshFilters != null && meshFilters[i].sharedMesh != null)
                {
                    int instanceID = meshFilters[i].sharedMesh.GetInstanceID();
                    if (bakedMeshes.ContainsKey(instanceID) == false)
                    {
                        Mesh generatedMesh = GenerateWireframe(meshFilters[i].sharedMesh, mergeSubMeshes);
                        if (progressBarCanceled)
                            break;


                        if (generatedMesh != null)
                        {
                            if (editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.OverwriteOriginalMesh)
                                generatedMesh = SaveMeshAsset(generatedMesh, AssetDatabase.GetAssetPath(meshFilters[i].sharedMesh));

                            meshFilters[i].sharedMesh = generatedMesh;

                            bakedMeshes.Add(instanceID, generatedMesh);
                        }
                        else
                            meshFilters[i].sharedMesh = null;
                    }
                    else
                    {
                        meshFilters[i].sharedMesh = bakedMeshes[instanceID];
                    }
                }
            }


            //SkinnedMeshes
            if (progressBarCanceled == false)
            {
                SkinnedMeshRenderer[] skinnedMeshRenderers = cloneGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < skinnedMeshRenderers.Length; i++)
                {
                    if (skinnedMeshRenderers != null && skinnedMeshRenderers[i].sharedMesh != null)
                    {
                        int instanceID = skinnedMeshRenderers[i].sharedMesh.GetInstanceID();
                        if (bakedMeshes.ContainsKey(instanceID) == false)
                        {
                            Mesh generatedMesh = GenerateWireframe(skinnedMeshRenderers[i].sharedMesh, mergeSubMeshes);
                            if (progressBarCanceled)
                                break;


                            if (generatedMesh != null)
                            {
                                if (editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.OverwriteOriginalMesh)
                                    generatedMesh = SaveMeshAsset(generatedMesh, AssetDatabase.GetAssetPath(skinnedMeshRenderers[i].sharedMesh));

                                skinnedMeshRenderers[i].sharedMesh = generatedMesh;

                                bakedMeshes.Add(instanceID, generatedMesh);
                            }
                            else
                                skinnedMeshRenderers[i].sharedMesh = null;
                        }
                        else
                        {
                            skinnedMeshRenderers[i].sharedMesh = bakedMeshes[instanceID];
                        }
                    }
                }
            }


            if (progressBarCanceled)
            {
                foreach (var item in bakedMeshes)
                {
                    item.Value.Clear();
                }

                bakedMeshes.Clear();
            }



            return bakedMeshes.Count == 0 ? false : true;
        }
        static Mesh GenerateWireframe(Mesh sourceMesh, bool mergeSubmeshes)
        {
            CallBackConversionyProgress(sourceMesh.name);


            Mesh mesh = sourceMesh.WireframeShader().GenerateWireframeMesh(editorSettings.solverNormalizeEdges, editorSettings.solverTryQuad, editorSettings.solverStoreInsideVertexAttribute);
            if (mesh != null)
            {
                if (mergeSubmeshes)
                {
                    mesh.SetTriangles(mesh.triangles, 0);
                    mesh.subMeshCount = 1;
                }

                RemoveAttributesFromMesh(ref mesh);
            }

            return mesh;
        }

        static public void MakeMeshesReadable(bool readable)
        {
            List<BatchObject> listBatchObjects = EditorWindow.active.listBatchObjects;

            List<ModelImporter> modelImporters = new List<ModelImporter>();


            for (int i = 0; i < listBatchObjects.Count; i++)
            {
                if (listBatchObjects[i] != null && listBatchObjects[i].gameObject != null)
                {
                    UnityEditor.EditorUtility.DisplayProgressBar("Updating Mesh State", listBatchObjects[i].gameObject.name, (float)i / listBatchObjects.Count);

                    EditorUtilities.MakeMeshesReadable(listBatchObjects[i].gameObject, readable, ref modelImporters);
                }
            }

            for (int i = 0; i < modelImporters.Count; i++)
            {
                modelImporters[i].SaveAndReimport();
            }

            UnityEditor.AssetDatabase.Refresh();

            UnityEditor.EditorUtility.ClearProgressBar();
        }
        static void PreparePrefabMaterials(ref GameObject gameObject, out Dictionary<int, Material> prefabMaterials, string prefabSaveDirectory, string prefabName)
        {
            prefabMaterials = new Dictionary<int, Material>();


            if (editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.Prefab)
            {
                switch (editorSettings.generateMaterialType)
                {
                    case EditorSettings.Enum.MaterialType.UseOriginal:
                        {
                            switch (editorSettings.generateMeshCombineType)
                            {
                                case EditorSettings.Enum.MeshCombineType.Submeshes:
                                    {
                                        //In the case of combining submeshes, only the first material is kept.
                                        #region MeshFilter
                                        foreach (var meshFilter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                                        {
                                            if (meshFilter != null && meshFilter.sharedMesh != null)
                                            {
                                                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                                                if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                                                    meshRenderer.sharedMaterials = new Material[] { meshRenderer.sharedMaterials[0] };
                                            }
                                        }
                                        #endregion

                                        #region SkinnedMeshRenderer
                                        foreach (var skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                        {
                                            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                                            {
                                                if (skinnedMeshRenderer.sharedMaterials != null && skinnedMeshRenderer.sharedMaterials.Length > 0)
                                                    skinnedMeshRenderer.sharedMaterials = new Material[] { skinnedMeshRenderer.sharedMaterials[0] };
                                            }
                                        }
                                        #endregion
                                    }
                                    break;

                                case EditorSettings.Enum.MeshCombineType.Everything:
                                    {
                                        List<Material> listMaterials = new List<Material>();
                                        List<MeshRenderer> listMeshRenderers = new List<MeshRenderer>();

                                        //If meshes are using only one material, keep it. Otherwise create new material.
                                        foreach (var meshFilter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                                        {
                                            if (meshFilter != null && meshFilter.sharedMesh != null)
                                            {
                                                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                                                if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                                                {
                                                    listMeshRenderers.Add(meshRenderer);

                                                    foreach (var material in meshRenderer.sharedMaterials)
                                                    {
                                                        if (material != null && listMaterials.Contains(material) == false)
                                                            listMaterials.Add(material);
                                                    }
                                                }
                                            }
                                        }


                                        if (listMaterials.Count == 1)
                                        {
                                            prefabMaterials.Add(0, listMaterials[0]);

                                            foreach (var meshRenderer in listMeshRenderers)
                                            {
                                                meshRenderer.sharedMaterials = new Material[] { listMaterials[0] };
                                            }
                                        }
                                        else
                                        {
                                            Material material = SaveMaterialAsset(CreateDefaultMaterial(), Path.Combine(prefabSaveDirectory, prefabName));

                                            foreach (var meshRenderer in listMeshRenderers)
                                            {
                                                meshRenderer.sharedMaterials = new Material[] { material };
                                            }

                                            prefabMaterials.Add(0, material);
                                        }
                                    }
                                    break;

                                default:
                                    break;
                            }
                        }
                        break;

                    case EditorSettings.Enum.MaterialType.CreateNew:
                        {
                            Material material = SaveMaterialAsset(CreateDefaultMaterial(), Path.Combine(prefabSaveDirectory, prefabName));

                            prefabMaterials.Add(0, material);


                            switch (editorSettings.generateMeshCombineType)
                            {
                                case EditorSettings.Enum.MeshCombineType.Nothing:
                                case EditorSettings.Enum.MeshCombineType.OneMeshWithSubmeshes:
                                    {
                                        #region MeshFilter
                                        foreach (var meshFilter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                                        {
                                            if (meshFilter != null && meshFilter.sharedMesh != null)
                                            {
                                                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                                                if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                                                {
                                                    Material[] materials = new Material[meshFilter.sharedMesh.subMeshCount];
                                                    materials.Populate(material);

                                                    for (int m = 0; m < meshRenderer.sharedMaterials.Length; m++)
                                                    {
                                                        if (m >= meshFilter.sharedMesh.subMeshCount)
                                                            break;

                                                        if (meshRenderer.sharedMaterials[m] == null)
                                                            materials[m] = null;
                                                    }

                                                    meshRenderer.sharedMaterials = materials;
                                                }
                                            }
                                        }
                                        #endregion

                                        #region SkinnedMeshRenderer 
                                        foreach (var skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                        {
                                            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                                            {
                                                if (skinnedMeshRenderer.sharedMaterials != null && skinnedMeshRenderer.sharedMaterials.Length > 0)
                                                {
                                                    Material[] materials = new Material[skinnedMeshRenderer.sharedMesh.subMeshCount];
                                                    materials.Populate(material);

                                                    for (int m = 0; m < skinnedMeshRenderer.sharedMaterials.Length; m++)
                                                    {
                                                        if (m >= skinnedMeshRenderer.sharedMesh.subMeshCount)
                                                            break;

                                                        if (skinnedMeshRenderer.sharedMaterials[m] == null)
                                                            materials[m] = null;
                                                    }

                                                    skinnedMeshRenderer.sharedMaterials = materials;
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                    break;

                                case EditorSettings.Enum.MeshCombineType.Submeshes:
                                case EditorSettings.Enum.MeshCombineType.Everything:
                                    {
                                        //In the case of combining submeshes, only the one material is used
                                        #region MeshFilter     
                                        foreach (var meshFilter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                                        {
                                            if (meshFilter != null && meshFilter.sharedMesh != null)
                                            {
                                                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                                                if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                                                    meshRenderer.sharedMaterials = meshRenderer.sharedMaterials.All(c => c == null) ? new Material[] { null } : new Material[] { material };
                                            }
                                        }
                                        #endregion

                                        #region SkinnedMeshRenderer
                                        foreach (var skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                        {
                                            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                                            {
                                                if (skinnedMeshRenderer.sharedMaterials != null && skinnedMeshRenderer.sharedMaterials.Length > 0)
                                                    skinnedMeshRenderer.sharedMaterials = skinnedMeshRenderer.sharedMaterials.All(c => c == null) ? new Material[] { null } : new Material[] { material };
                                            }
                                        }
                                        #endregion
                                    }
                                    break;

                                default:
                                    break;
                            }
                        }
                        break;

                    case EditorSettings.Enum.MaterialType.CreateDuplicate:
                        {
                            switch (editorSettings.generateMeshCombineType)
                            {
                                case EditorSettings.Enum.MeshCombineType.Nothing:
                                case EditorSettings.Enum.MeshCombineType.OneMeshWithSubmeshes:
                                    {
                                        //Creating duplicates for all materials
                                        #region MeshFilter
                                        foreach (var meshFilter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                                        {
                                            if (meshFilter != null && meshFilter.sharedMesh != null)
                                            {
                                                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                                                if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                                                {
                                                    Material[] sharedMaterials = new Material[meshRenderer.sharedMaterials.Length];
                                                    for (int m = 0; m < sharedMaterials.Length; m++)
                                                    {
                                                        Material originalMaterial = meshRenderer.sharedMaterials[m];


                                                        if (originalMaterial == null)
                                                            sharedMaterials[m] = null;
                                                        else
                                                        {
                                                            int instanceID = originalMaterial.GetInstanceID();
                                                            if (prefabMaterials.ContainsKey(instanceID))
                                                                sharedMaterials[m] = prefabMaterials[instanceID];
                                                            else
                                                            {
                                                                Material duplicate = SaveMaterialAsset(GameObject.Instantiate(originalMaterial), Path.Combine(prefabSaveDirectory, $"{prefabName} {originalMaterial.name} ({instanceID})"));

                                                                sharedMaterials[m] = duplicate;
                                                                prefabMaterials.Add(instanceID, duplicate);
                                                            }
                                                        }
                                                    }

                                                    meshRenderer.sharedMaterials = sharedMaterials;
                                                }
                                            }
                                        }
                                        #endregion

                                        #region SkinnedMeshRenderer
                                        foreach (var skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                        {
                                            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                                            {
                                                if (skinnedMeshRenderer.sharedMaterials != null && skinnedMeshRenderer.sharedMaterials.Length > 0)
                                                {
                                                    Material[] sharedMaterials = new Material[skinnedMeshRenderer.sharedMaterials.Length];
                                                    for (int m = 0; m < sharedMaterials.Length; m++)
                                                    {
                                                        Material originalMaterial = skinnedMeshRenderer.sharedMaterials[m];


                                                        if (originalMaterial == null)
                                                            sharedMaterials[m] = null;
                                                        else
                                                        {
                                                            int instanceID = originalMaterial.GetInstanceID();
                                                            if (prefabMaterials.ContainsKey(instanceID))
                                                                sharedMaterials[m] = prefabMaterials[instanceID];
                                                            else
                                                            {
                                                                Material duplicate = SaveMaterialAsset(GameObject.Instantiate(originalMaterial), Path.Combine(prefabSaveDirectory, $"{prefabName} {originalMaterial.name} ({instanceID})"));

                                                                sharedMaterials[m] = duplicate;
                                                                prefabMaterials.Add(instanceID, duplicate);
                                                            }
                                                        }
                                                    }

                                                    skinnedMeshRenderer.sharedMaterials = sharedMaterials;
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                    break;

                                case EditorSettings.Enum.MeshCombineType.Submeshes:
                                    {
                                        //In the case of combining submeshes, only the one/first material is kept.
                                        #region MeshFilter
                                        foreach (var meshFilter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                                        {
                                            if (meshFilter != null && meshFilter.sharedMesh != null)
                                            {
                                                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                                                if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                                                {
                                                    Material originalMaterial = meshRenderer.sharedMaterials[0];
                                                    if (originalMaterial != null)
                                                    {
                                                        int instanceID = originalMaterial.GetInstanceID();
                                                        if (prefabMaterials.ContainsKey(instanceID))
                                                            meshRenderer.sharedMaterials = new Material[] { prefabMaterials[instanceID] };
                                                        else
                                                        {
                                                            Material duplicate = SaveMaterialAsset(GameObject.Instantiate(originalMaterial), Path.Combine(prefabSaveDirectory, $"{prefabName} {originalMaterial.name} ({instanceID})"));

                                                            meshRenderer.sharedMaterials = new Material[] { duplicate };
                                                            prefabMaterials.Add(instanceID, duplicate);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        #endregion

                                        #region SkinnedMeshRenderer
                                        foreach (var skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                        {
                                            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                                            {
                                                if (skinnedMeshRenderer.sharedMaterials != null && skinnedMeshRenderer.sharedMaterials.Length > 0)
                                                {
                                                    Material originalMaterial = skinnedMeshRenderer.sharedMaterials[0];
                                                    if (originalMaterial != null)
                                                    {
                                                        int instanceID = originalMaterial.GetInstanceID();
                                                        if (prefabMaterials.ContainsKey(instanceID))
                                                            skinnedMeshRenderer.sharedMaterials = new Material[] { prefabMaterials[instanceID] };
                                                        else
                                                        {
                                                            Material duplicate = SaveMaterialAsset(GameObject.Instantiate(originalMaterial), Path.Combine(prefabSaveDirectory, $"{prefabName} {originalMaterial.name} ({instanceID})"));

                                                            skinnedMeshRenderer.sharedMaterials = new Material[] { duplicate };
                                                            prefabMaterials.Add(instanceID, duplicate);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                    break;

                                case EditorSettings.Enum.MeshCombineType.Everything:
                                    {
                                        List<Material> listMaterials = new List<Material>();
                                        List<MeshRenderer> listMeshRenderers = new List<MeshRenderer>();

                                        //If meshes are using only one material, duplicate only it. Otherwise create new material.
                                        foreach (var meshFilter in gameObject.GetComponentsInChildren<MeshFilter>(true))
                                        {
                                            if (meshFilter != null && meshFilter.sharedMesh != null)
                                            {
                                                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                                                if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                                                {
                                                    listMeshRenderers.Add(meshRenderer);

                                                    foreach (var mat in meshRenderer.sharedMaterials)
                                                    {
                                                        if (mat != null && listMaterials.Contains(mat) == false)
                                                            listMaterials.Add(mat);
                                                    }
                                                }
                                            }
                                        }

                                        Material material;
                                        if (listMaterials.Count == 1)
                                            material = SaveMaterialAsset(GameObject.Instantiate(listMaterials[0]), Path.Combine(prefabSaveDirectory, prefabName));
                                        else
                                            material = SaveMaterialAsset(CreateDefaultMaterial(), Path.Combine(prefabSaveDirectory, prefabName));

                                        foreach (var meshRenderer in listMeshRenderers)
                                        {
                                            meshRenderer.sharedMaterials = new Material[] { material };
                                        }

                                        prefabMaterials.Add(0, material);
                                    }
                                    break;

                                default:
                                    break;
                            }
                        }
                        break;

                    default:
                        break;
                }
            }


            if (prefabMaterials.Count == 0)
                prefabMaterials = null;
        }
        static Material CreateDefaultMaterial()
        {
            if (editorSettings.generateDefaultShader == null)
                return null;

            return new Material(editorSettings.generateDefaultShader);
        }

        static GameObject SaveMainPrefab(GameObject gameObject, string saveDirectory, string prefabName)
        {
            //Remove missing scripts
            EditorUtilities.RemoveMissingScripts(gameObject);


            string prefabPath = Path.Combine(saveDirectory, prefabName + ".prefab");


            EditorWindow.active.lastSavedFilePath = prefabPath;


            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, prefabPath, InteractionMode.AutomatedAction);

            WireframeShaderConversionDetails conversionDetails = prefab.GetComponent<WireframeShaderConversionDetails>();
            if (conversionDetails == null)
                conversionDetails = prefab.AddComponent<WireframeShaderConversionDetails>();

            conversionDetails.prefabProjectPath = AssetDatabase.GetAssetPath(prefab);
            PrefabUtility.SavePrefabAsset(prefab);


            return prefab;
        }
        static Material SaveMaterialAsset(Material material, string savePath)
        {
            if (material != null)
            {
                savePath += ".mat";

                EditorWindow.active.lastSavedFilePath = savePath;


                if (File.Exists(savePath))
                {
                    //Rewrite
                    Material oldMaterial = (Material)AssetDatabase.LoadAssetAtPath(savePath, typeof(Material));

                    if (oldMaterial.shader != material.shader)
                    {
                        oldMaterial.shader = material.shader;
                    }

                    oldMaterial.CopyPropertiesFromMaterial(material);

                    UnityEditor.EditorUtility.SetDirty(material);

                    return oldMaterial;
                }
                else
                {
                    AssetDatabase.CreateAsset(material, savePath);

                    return material;
                }
            }

            return null;
        }
        static UnityAssetFile CreateRootUnityMeshAsset(string saveDirectory, string prefabName, string fileName)
        {
            string assetSavePath = Path.Combine(saveDirectory, prefabName + fileName + ".asset");

            EditorWindow.active.lastSavedFilePath = assetSavePath;


            if (File.Exists(assetSavePath))
            {
                AssetDatabase.DeleteAsset(assetSavePath);
                AssetDatabase.Refresh();
            }


            UnityAssetFile rootObject = ScriptableObject.CreateInstance<UnityAssetFile>();
            rootObject.name = prefabName + fileName;

            AssetDatabase.CreateAsset(rootObject, assetSavePath);
            AssetDatabase.Refresh();

            return rootObject;
        }
        static void AddMeshToRootUnityAsset(Mesh mesh, UnityEngine.Object assetObject, ModelImporterMeshCompression meshCompression)
        {
            if (mesh != null)
            {
                //Compress
                if (meshCompression != ModelImporterMeshCompression.Off)
                    MeshUtility.SetMeshCompression(mesh, meshCompression);


                AssetDatabase.AddObjectToAsset(mesh, assetObject);
            }
        }

        static void RemoveAttributesFromMesh(ref Mesh mesh)
        {
            if (editorSettings.saveUseDefaultFlagsForVertexAttribute == false)
            {
                VertexAttributePopup.AttributeFlags combiedAttributes = editorSettings.GetRequiredVertexAttributes() | editorSettings.saveVertexAttributeFlags;

                bool needUV0 = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.UV0);
                bool needUV1 = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.UV1);
                bool needUV2 = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.UV2);
                bool needUV3 = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.UV3);
                bool needUV4 = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.UV4);
                bool needUV5 = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.UV5);
                bool needUV6 = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.UV6);
                bool needUV7 = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.UV7);
                bool needNormal = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.Normal);
                bool needTangent = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.Tangent);
                bool needColor = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.Color);
                bool needSkin = combiedAttributes.HasFlag(VertexAttributePopup.AttributeFlags.Skin);


                if (needUV0 == false)
                    mesh.uv = null;

                if (needUV1 == false)
                    mesh.uv2 = null;

                if (needUV2 == false)
                    mesh.uv3 = null;

                if (needUV3 == false)
                    mesh.uv4 = null;

                if (needUV4 == false)
                    mesh.uv5 = null;

                if (needUV5 == false)
                    mesh.uv6 = null;

                if (needUV6 == false)
                    mesh.uv7 = null;

                if (needUV7 == false)
                    mesh.uv8 = null;

                if (needNormal == false)
                    mesh.normals = null;

                if (needTangent == false)
                    mesh.tangents = null;

                if (needColor == false)
                    mesh.colors = null;

                if (needSkin == false)
                {
                    mesh.bindposes = null;
                    mesh.boneWeights = null;
                    mesh.ClearBlendShapes();
                }
            }
        }
        static Mesh SaveMeshAsset(Mesh mesh, string assetSavePath)
        {
            if (mesh != null)
            {
                EditorWindow.active.lastSavedFilePath = assetSavePath;


                //Make sure name is correct
                mesh.name = Path.GetFileNameWithoutExtension(assetSavePath);


                //Compress
                if (editorSettings.saveMeshCompression != ModelImporterMeshCompression.Off)
                    MeshUtility.SetMeshCompression(mesh, editorSettings.saveMeshCompression);


                //Save
                if (File.Exists(assetSavePath))
                {
                    //Rewrite
                    Mesh oldMesh = (Mesh)AssetDatabase.LoadAssetAtPath(assetSavePath, typeof(Mesh));

                    if (oldMesh != null)
                    {
                        oldMesh.Clear(false);
                        oldMesh.vertices = null;
                        oldMesh.triangles = null;
                        oldMesh.tangents = null;
                        oldMesh.normals = null;
                        oldMesh.vertices = null;
                        oldMesh.bindposes = null;
                        oldMesh.boneWeights = null;

                        UnityEditor.EditorUtility.CopySerialized(mesh, oldMesh);
                        UnityEditor.EditorUtility.SetDirty(oldMesh);
                        UnityEditor.AssetDatabase.SaveAssets();

                        return oldMesh;
                    }
                    else
                    {
                        AssetDatabase.DeleteAsset(assetSavePath);

                        AssetDatabase.CreateAsset(mesh, assetSavePath);

                        return mesh;
                    }
                }
                else
                {
                    AssetDatabase.CreateAsset(mesh, assetSavePath);

                    return mesh;
                }
            }

            return null;
        }

        static bool CombineMeshesAndSave(GameObject gameObject, Dictionary<int, Material> prefabMainMaterials, string prefabSaveDirectory, string prefabName)
        {
            List<Material> subMeshesMaterials;
            Mesh combinedMesh = WireframeShaderUtilities.CombineGameObjects(gameObject, editorSettings.generateMeshCombineType == EditorSettings.Enum.MeshCombineType.OneMeshWithSubmeshes, out subMeshesMaterials, prefabName);

            if (editorSettings.GeneratingLightmapUVs())
                Unwrapping.GenerateSecondaryUVSet(combinedMesh);


            //Save 
            if (editorSettings.saveMeshFormat == EditorSettings.Enum.MeshFormat.UnityAsset)
            {
                UnityAssetFile rootUnityAsset = CreateRootUnityMeshAsset(prefabSaveDirectory, prefabName, string.Empty);
                AddMeshToRootUnityAsset(combinedMesh, rootUnityAsset, editorSettings.saveMeshCompression);
            }
            else
            {
                combinedMesh = SaveMeshAsset(combinedMesh, Path.Combine(prefabSaveDirectory, prefabName + " " + combinedMesh.name + ".asset"));
            }


            //Destroy all childs
            while (gameObject.transform.childCount > 0)
            {
                WireframeShaderUtilities.DestroyUnityObject(gameObject.transform.GetChild(0).gameObject);
            }


            //Assign meshes
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = combinedMesh;



            //Assign material
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            if (editorSettings.generateMeshCombineType == EditorSettings.Enum.MeshCombineType.OneMeshWithSubmeshes)
            {
                switch (editorSettings.generateMaterialType)
                {
                    case EditorSettings.Enum.MaterialType.UseOriginal:
                        {
                            meshRenderer.sharedMaterials = subMeshesMaterials.ToArray();
                        }
                        break;

                    case EditorSettings.Enum.MaterialType.CreateNew:
                        {
                            meshRenderer.sharedMaterials = subMeshesMaterials.ToArray();
                        }
                        break;

                    case EditorSettings.Enum.MaterialType.CreateDuplicate:
                        {
                            meshRenderer.sharedMaterials = subMeshesMaterials.ToArray();
                        }
                        break;

                    default:
                        break;
                }
            }
            else
            {
                meshRenderer.sharedMaterials = new Material[] { prefabMainMaterials[0] };
            }


            return true;
        }
        static void SaveMeshesSeparetly(GameObject gameObject, string prefabSaveDirectory, string prefabName)
        {
            if (editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.OverwriteOriginalMesh)
                return;


            List<Mesh> allMeshes = new List<Mesh>();
            Dictionary<int, List<MeshFilter>> dictionaryMeshFilters = new Dictionary<int, List<MeshFilter>>();
            Dictionary<int, List<SkinnedMeshRenderer>> dictionarySkinnedMeshRenderer = new Dictionary<int, List<SkinnedMeshRenderer>>();

            foreach (var meshFilter in gameObject.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    if (allMeshes.Contains(meshFilter.sharedMesh) == false)
                    {
                        meshFilter.sharedMesh.name = EditorUtilities.RemoveInvalidCharacters(meshFilter.sharedMesh.name);

                        allMeshes.Add(meshFilter.sharedMesh);
                    }


                    int instanceID = meshFilter.sharedMesh.GetInstanceID();
                    if (dictionaryMeshFilters.ContainsKey(instanceID))
                        dictionaryMeshFilters[instanceID].Add(meshFilter);
                    else
                        dictionaryMeshFilters.Add(instanceID, new List<MeshFilter>() { meshFilter });
                }
            }

            foreach (var skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                {
                    if (allMeshes.Contains(skinnedMeshRenderer.sharedMesh) == false)
                    {
                        skinnedMeshRenderer.sharedMesh.name = EditorUtilities.RemoveInvalidCharacters(skinnedMeshRenderer.sharedMesh.name);

                        allMeshes.Add(skinnedMeshRenderer.sharedMesh);
                    }


                    int instanceID = skinnedMeshRenderer.sharedMesh.GetInstanceID();
                    if (dictionarySkinnedMeshRenderer.ContainsKey(instanceID))
                        dictionarySkinnedMeshRenderer[instanceID].Add(skinnedMeshRenderer);
                    else
                        dictionarySkinnedMeshRenderer.Add(instanceID, new List<SkinnedMeshRenderer>() { skinnedMeshRenderer });
                }
            }


            //Save and re-assign meshes
            if (editorSettings.saveMeshFormat == EditorSettings.Enum.MeshFormat.UnityAsset)
            {
                UnityAssetFile rootUnityAsset = CreateRootUnityMeshAsset(prefabSaveDirectory, prefabName, string.Empty);
                for (int i = 0; i < allMeshes.Count; i++)
                {
                    AddMeshToRootUnityAsset(allMeshes[i], rootUnityAsset, editorSettings.saveMeshCompression);
                }
            }
            else
            {
                for (int i = 0; i < allMeshes.Count; i++)
                {
                    int instanceID = allMeshes[i].GetInstanceID();
                    Mesh savedMesh = SaveMeshAsset(allMeshes[i], Path.Combine(prefabSaveDirectory, prefabName + " " + allMeshes[i].name + ".asset"));

                    if (dictionaryMeshFilters.ContainsKey(instanceID))
                    {
                        foreach (var item in dictionaryMeshFilters[instanceID])
                        {
                            item.sharedMesh = allMeshes[i];
                        }
                    }

                    if (dictionarySkinnedMeshRenderer.ContainsKey(instanceID))
                    {
                        foreach (var item in dictionarySkinnedMeshRenderer[instanceID])
                        {
                            item.sharedMesh = allMeshes[i];
                        }
                    }
                }
            }
        }
        static void SetupColliders(GameObject gameObject)
        {
            if (editorSettings.generateReplaceMeshColliders)
            {
                foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>())
                {
                    MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                    SkinnedMeshRenderer skinnedMeshRenderer = null;
                    if (meshFilter == null)
                        skinnedMeshRenderer = renderer.GetComponent<SkinnedMeshRenderer>();

                    if (meshFilter == null && skinnedMeshRenderer == null)
                        continue;


                    Collider collider = renderer.gameObject.GetComponent<Collider>();
                    if (collider != null)
                    {
                        if (collider is MeshCollider)
                        {
                            ((MeshCollider)collider).sharedMesh = meshFilter != null ? meshFilter.sharedMesh : skinnedMeshRenderer.sharedMesh;
                        }
                        else
                        {
                            MeshCollider meshCollider = renderer.gameObject.AddComponent<MeshCollider>();

                            meshCollider.sharedMesh = meshFilter != null ? meshFilter.sharedMesh : skinnedMeshRenderer.sharedMesh;
                            meshCollider.sharedMaterial = collider.sharedMaterial;


                            WireframeShaderUtilities.DestroyUnityObject(collider);
                        }
                    }
                    else
                    {
                        MeshCollider meshCollider = renderer.gameObject.AddComponent<MeshCollider>();

                        meshCollider.sharedMesh = meshFilter != null ? meshFilter.sharedMesh : skinnedMeshRenderer.sharedMesh;
                    }
                }
            }
        }
        static void SetFlags(GameObject mainPrefab)
        {
            //Flags
            if (editorSettings.saveUseStaticEditorFlags)
            {
                if (editorSettings.saveUseStaticEditorFlagsForHierarchy)
                {
                    foreach (Transform transform in mainPrefab.GetComponentsInChildren<Transform>(true))
                    {
                        GameObjectUtility.SetStaticEditorFlags(transform.gameObject, editorSettings.saveStaticEditorFlags);
                    }
                }
                else
                {
                    GameObjectUtility.SetStaticEditorFlags(mainPrefab, editorSettings.saveStaticEditorFlags);
                }
            }
            if (editorSettings.saveUseTag)
            {
                if (editorSettings.saveUseTagForHierarchy)
                {
                    foreach (Transform transform in mainPrefab.GetComponentsInChildren<Transform>(true))
                    {
                        transform.gameObject.tag = editorSettings.saveTag;
                    }
                }
                else
                {
                    mainPrefab.tag = editorSettings.saveTag;
                }
            }
            if (editorSettings.saveUseLayer)
            {
                if (editorSettings.saveUseLayerForHierarchy)
                {
                    foreach (Transform transform in mainPrefab.GetComponentsInChildren<Transform>(true))
                    {
                        transform.gameObject.layer = editorSettings.saveLayer;
                    }
                }
                else
                {
                    mainPrefab.layer = editorSettings.saveLayer;
                }
            }
        }

        static bool IsSaveLocationProjectRelative()
        {
            switch (editorSettings.prefabSaveIn)
            {
                case EditorSettingsBase.Enum.SaveLocation.SameFolder:
                case EditorSettingsBase.Enum.SaveLocation.SameSubfolder:
                    return true;

                case EditorSettingsBase.Enum.SaveLocation.CustomFolder:
                    return EditorUtilities.IsPathProjectRelative(editorSettings.saveFolderCustomPath);


                default: return false;
            }
        }
        static int CountPrefabReferencesInThisScene(GameObject prefab)
        {
            int counter = 0;

            GameObject[] sceneRootGameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            for (int i = 0; i < sceneRootGameObjects.Length; i++)
            {
                WireframeShaderConversionDetails[] conversionDetails = sceneRootGameObjects[i].GetComponentsInChildren<WireframeShaderConversionDetails>(true);

                for (int j = 0; j < conversionDetails.Length; j++)
                {
                    if (conversionDetails[j] != null &&
                        string.IsNullOrEmpty(conversionDetails[j].prefabProjectPath) == false &&
                        conversionDetails[j].prefabProjectPath == AssetDatabase.GetAssetPath(prefab) &&
                        PrefabUtility.GetPrefabInstanceStatus(conversionDetails[j].gameObject) == PrefabInstanceStatus.Connected)
                    {
                        counter += 1;

                        PrefabUtility.RevertPrefabInstance(conversionDetails[j].gameObject, InteractionMode.AutomatedAction);
                    }
                }
            }

            return counter;
        }

        static void CallBackConversionyProgress(string meshName)
        {
            progressBarCurrentMeshIndex += 1;


            float totalPercentDone = ((float)progressBarCurrentMeshIndex / progressBarTotalMeshCount);
            totalPercentDone = Mathf.Clamp01(totalPercentDone);

            if (progressBarTotalMeshCount == 1)
                progressBarCanceled = UnityEditor.EditorUtility.DisplayCancelableProgressBar("Calculating Wireframe", meshName, totalPercentDone);
            else
                progressBarCanceled = UnityEditor.EditorUtility.DisplayCancelableProgressBar("Calculating Wireframe", $"[{progressBarCurrentMeshIndex} / {progressBarTotalMeshCount}] {meshName}", totalPercentDone);
        }
    }
}