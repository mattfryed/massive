// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor.WireframeTextureGenerator
{
    internal static class Run
    {
        static List<BatchObject> listBatchObjects;
        static EditorSettings editorSettings;


        static int progressBarTotalMeshCount;
        static int progressBarCurrentMeshIndex;
        static bool progressBarCanceled;


        static public void RunConverter()
        {
            listBatchObjects = EditorWindow.active.listBatchObjects;
            editorSettings = EditorWindow.active.editorSettings;

            BatchObject currentBatchObject = null;


            progressBarTotalMeshCount = editorSettings.combineSubmesh ? listBatchObjects.Count : listBatchObjects.Sum(c => c.mesh.subMeshCount);
            progressBarCurrentMeshIndex = 0;
            progressBarCanceled = false;



            //Delete temp directory
            EditorUtilities.DeleteTempDirectory();

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
                if (currentBatchObject == null || currentBatchObject.mesh == null)
                    WireframeShaderDebug.Log(LogType.Error, "Encountered an unknown error.", null);
                else
                {
                    WireframeShaderDebug.Log(LogType.Error, string.Format("Encountered an error with mesh '{0}'.\n{1}", currentBatchObject.mesh, e.Message), null);

                    EditorWindow.active.problematicBatchObject = currentBatchObject;
                    EditorWindow.active.problematicBatchObject.exception = e.Message;
                }
            }

            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();

            UnityEditor.EditorUtility.ClearProgressBar();

            AssetDatabase.Refresh();
        }
        static void RunLoopConverter(int loopIndex)
        {
            if (listBatchObjects[loopIndex] == null || listBatchObjects[loopIndex].mesh == null)
                return;


            Mesh currentMesh = listBatchObjects[loopIndex].mesh;

            string prefabName;
            string prefabSaveDirectory;
            if (GetPrefabNameAndSaveDirectory(currentMesh, out prefabName, out prefabSaveDirectory) == false)
                return;


            //Save list of all generated assets
            List<string> generatedAssetsPath = new List<string>();

            int resolution = (int)Mathf.Pow(2, (int)editorSettings.saveFileResolution + 4);

            ExportTexture(currentMesh, resolution, prefabSaveDirectory, prefabName, ref generatedAssetsPath);


            //Select last saved file 
            if (generatedAssetsPath.Count == 0)
                EditorWindow.active.lastSavedFilePath = string.Empty;
            else
            {
                EditorWindow.active.lastSavedFilePath = generatedAssetsPath[generatedAssetsPath.Count - 1];

                GeneratedAssetsImporter.ReimportTextures(generatedAssetsPath.ToArray(), resolution);
            }
        }
        static bool GetPrefabNameAndSaveDirectory(Mesh currentMesh, out string prefabName, out string prefabSaveDirectory)
        {
            prefabName = editorSettings.GetSaveAssetName(currentMesh, true);
            prefabSaveDirectory = editorSettings.GetAssetSaveDirectory(currentMesh, false, true);

            if (EditorUtilities.IsPathWithinStreamingAssetsFolder(prefabSaveDirectory))
            {
                WireframeShaderDebug.Log(LogType.Warning, $"Cannot convert/save '{currentMesh.name}' files into streaming assets folder '{prefabSaveDirectory}'");
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

            return true;
        }
        static public void MakeMeshesReadable(bool readable)
        {
            List<BatchObject> listBatchObjects = EditorWindow.active.listBatchObjects;

            List<ModelImporter> modelImporters = new List<ModelImporter>();


            for (int i = 0; i < listBatchObjects.Count; i++)
            {
                if (listBatchObjects[i] != null && listBatchObjects[i].mesh != null)
                {
                    UnityEditor.EditorUtility.DisplayProgressBar("Updating Mesh State", listBatchObjects[i].mesh.name, (float)i / listBatchObjects.Count);

                    ModelImporter modelImporter = EditorUtilities.MakeMeshReadable(listBatchObjects[i].mesh, readable);
                    if (modelImporter != null && modelImporters.Contains(modelImporter) == false)
                        modelImporters.Add(modelImporter);
                }
            }

            for (int i = 0; i < modelImporters.Count; i++)
            {
                modelImporters[i].SaveAndReimport();
            }

            UnityEditor.AssetDatabase.Refresh();

            UnityEditor.EditorUtility.ClearProgressBar();
        }

        static void ExportTexture(Mesh mesh, int resolution, string saveDirectory, string prefabName, ref List<string> generatedAssetsPath)
        {
            if (editorSettings.combineSubmesh)
            {
                ++progressBarCurrentMeshIndex;

                progressBarCanceled = UnityEditor.EditorUtility.DisplayCancelableProgressBar("Hold On", mesh.name, (float)progressBarCurrentMeshIndex / progressBarTotalMeshCount);


                Texture2D texture = mesh.WireframeShader().GenerateWireframeTexture(resolution, editorSettings.solverType, editorSettings.solverReadFrom , - 1, editorSettings.solverNormalizeEdges, editorSettings.solverTryQuad, editorSettings.solverThickness, editorSettings.solverSmoothness);

                SaveTexture(texture, editorSettings.saveFileFormat, saveDirectory, prefabName, ref generatedAssetsPath);


                if (progressBarCanceled)
                    return; 
            }
            else
            {
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    ++progressBarCurrentMeshIndex;

                    progressBarCanceled = UnityEditor.EditorUtility.DisplayCancelableProgressBar("Hold On", mesh.name, (float)progressBarCurrentMeshIndex / progressBarTotalMeshCount);
                        

                    Texture2D texture = mesh.WireframeShader().GenerateWireframeTexture(resolution, editorSettings.solverType, editorSettings.solverReadFrom, s, editorSettings.solverNormalizeEdges, editorSettings.solverTryQuad, editorSettings.solverThickness, editorSettings.solverSmoothness);

                    string submeshName = string.Empty;
                    if (mesh.subMeshCount > 1)
                        submeshName = " (" + s + ")";

                    SaveTexture(texture, editorSettings.saveFileFormat, saveDirectory, prefabName + submeshName, ref generatedAssetsPath);


                    if (progressBarCanceled)
                        return;
                }
            }

        }
        static void SaveTexture(Texture2D texture, EditorSettings.Enum.SaveFileFormat fileFormat, string saveDirectory, string fileName, ref List<string> generatedAssetsPath)
        {
            if (texture == null)
                return;


            byte[] bytes = null;

            switch (fileFormat)
            {
                case EditorSettings.Enum.SaveFileFormat.JPG: bytes = texture.EncodeToJPG(100); break;
                case EditorSettings.Enum.SaveFileFormat.PNG: bytes = texture.EncodeToPNG(); break;
                case EditorSettings.Enum.SaveFileFormat.TGA: bytes = texture.EncodeToTGA(); break;

                default:
                    break;
            }


            if (bytes != null)
            {
                string saveFileName = Path.Combine(saveDirectory, fileName + "." + fileFormat.ToString().ToLower());
                generatedAssetsPath.Add(saveFileName);

                File.WriteAllBytes(saveFileName, bytes);
            }


            WireframeShaderUtilities.DestroyUnityObject(texture);
        }
    }
}
