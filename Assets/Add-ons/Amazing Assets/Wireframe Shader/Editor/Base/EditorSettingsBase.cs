// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;
using System.IO;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor
{
    internal abstract class EditorSettingsBase
    {
        public static class Enum
        {
            public enum SaveLocation { SameFolder, SameSubfolder, CustomFolder }
        }


        #region Settings
        public Enum.SaveLocation prefabSaveIn = Enum.SaveLocation.SameFolder;
        public bool useParentAssetNameAsSubfolder = true;
        public string saveFolderCustomPath = string.Empty;
        public string filePrefix = string.Empty;
        public string fileSuffix = string.Empty;
        public string subfolderName = string.Empty;
        public bool saveFolderStructure = false;
        #endregion

        #region Tabs
        bool drawTabHelp = false;
        #endregion


        public EditorSettingsBase()
        {
            LoadEditorData();
        }


        public void Draw()
        {
            int labelWidth = 152;

            Rect controlRect = EditorGUILayout.GetControlRect();
            controlRect.xMin += 10;
            controlRect.width -= 10;

            float controlsWidth = (controlRect.width - labelWidth) / 3f;

            Rect collumnRect1, collumnRect2, collumnRect3, collumnRect4;
            collumnRect1 = new Rect(controlRect.xMin, controlRect.yMin, labelWidth, controlRect.height);
            collumnRect2 = new Rect(controlRect.xMin + labelWidth, controlRect.yMin, controlsWidth, controlRect.height);
            collumnRect3 = new Rect(controlRect.xMin + labelWidth + controlsWidth + 10, controlRect.yMin, controlsWidth - 10, controlRect.height);
            collumnRect4 = new Rect(controlRect.xMin + labelWidth + controlsWidth + controlsWidth + 10, controlRect.yMin, controlsWidth - 10, controlRect.height);


            GUILayout.Space(-15);
            DrawCustomSettings(collumnRect1, collumnRect2, collumnRect3, collumnRect4);



            GUILayout.Space(5);
            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
            {
                drawTabHelp = EditorGUILayout.Foldout(drawTabHelp, "Help", false, EditorResources.GUIStyleFoldoutBold);

                if (drawTabHelp)
                {
                    using (new EditorGUIHelper.EditorGUILayoutBeginHorizontal(EditorStyles.helpBox))
                    {
                        int buttonHeight = 30;

                        if (GUILayout.Button(new GUIContent("Documentation", EditorResources.IconManual), GUILayout.MaxHeight(buttonHeight)))
                        {
                            Application.OpenURL(WireframeShaderAbout.documentationURL);
                        }

                        if (GUILayout.Button(new GUIContent("Forum", EditorResources.IconForum), GUILayout.MaxHeight(buttonHeight)))
                        {
                            Application.OpenURL(WireframeShaderAbout.forumURL);
                        }

                        if (GUILayout.Button(new GUIContent("Support & Bug Report", EditorResources.IconSupport, WireframeShaderAbout.supportMail + "\nRight click to copy to the clipboard"), GUILayout.MaxHeight(buttonHeight)))
                        {
                            if (Event.current.button == 1)   //Right click
                            {
                                TextEditor te = new TextEditor();
                                te.text = WireframeShaderAbout.supportMail;
                                te.SelectAll();
                                te.Copy();



                                StackTraceLogType save = Application.GetStackTraceLogType(LogType.Log);
                                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

                                WireframeShaderDebug.Log(WireframeShaderAbout.supportMail);

                                Application.SetStackTraceLogType(LogType.Log, save);
                            }
                            else
                            {
                                Application.OpenURL("mailto:" + WireframeShaderAbout.supportMail);
                            }
                        }


                        if (GUILayout.Button(new GUIContent("Rate Asset", EditorResources.IconRate), GUILayout.MaxHeight(buttonHeight)))
                        {
                            Application.OpenURL(WireframeShaderAbout.storeURL);
                        }

                        if (GUILayout.Button(new GUIContent("More Assets ", EditorResources.IconMore), GUILayout.MaxHeight(buttonHeight)))
                        {
                            Application.OpenURL(WireframeShaderAbout.publisherPage);
                        }
                    }

                    EditorGUILayout.LabelField(string.Format("{0} ({1})", WireframeShaderAbout.name, WireframeShaderAbout.version), EditorStyles.centeredGreyMiniLabel);
                }
            }
        }
        public void DrawSaveOptions(Rect collumnRect1, Rect collumnRect2, Rect collumnRect3, Rect collumnRect4, int nameLabelWidth)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            collumnRect1.yMin = collumnRect2.yMin = collumnRect3.yMin = collumnRect4.yMin = rect.yMin;
            collumnRect1.height = collumnRect2.height = collumnRect3.height = collumnRect4.height = rect.height;

            EditorGUI.LabelField(collumnRect1, "Name");


            bool bothAreInvalid = false;
            if (prefabSaveIn == Enum.SaveLocation.SameFolder && useParentAssetNameAsSubfolder == false && string.IsNullOrWhiteSpace(filePrefix) && string.IsNullOrWhiteSpace(fileSuffix))
                bothAreInvalid = true;

            using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(nameLabelWidth))
            {
                using (new EditorGUIHelper.GUIBackgroundColor((EditorUtilities.ContainsInvalidFileNameCharacters(filePrefix) || bothAreInvalid) ? Color.red : Color.white))
                {
                    EditorGUI.BeginChangeCheck();
                    filePrefix = EditorGUI.TextField(collumnRect2, "Prefix", filePrefix);
                    if (EditorGUI.EndChangeCheck())
                        filePrefix = filePrefix.TrimStart();
                }
            }
            using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(nameLabelWidth))
            {
                using (new EditorGUIHelper.GUIBackgroundColor((EditorUtilities.ContainsInvalidFileNameCharacters(fileSuffix) || bothAreInvalid) ? Color.red : Color.white))
                {
                    EditorGUI.BeginChangeCheck();
                    fileSuffix = EditorGUI.TextField(collumnRect3, "Suffix", fileSuffix);
                    if (EditorGUI.EndChangeCheck())
                        fileSuffix = fileSuffix.TrimEnd();
                }
            }



            rect = EditorGUILayout.GetControlRect();
            collumnRect1.yMin = collumnRect2.yMin = collumnRect3.yMin = collumnRect4.yMin = rect.yMin;
            collumnRect1.height = collumnRect2.height = collumnRect3.height = collumnRect4.height = rect.height;

            EditorGUI.LabelField(collumnRect1, "Location");
            prefabSaveIn = (Enum.SaveLocation)EditorGUI.EnumPopup(new Rect(collumnRect2.xMin, collumnRect2.yMin, collumnRect2.width - 51, collumnRect2.height), string.Empty, prefabSaveIn);



            Rect rectUseParentAsset = new Rect(collumnRect2.xMax - 50, collumnRect2.yMin, 50, collumnRect2.height);

            Texture iconUseParentAsset = EditorResources.IconNone;
            if (useParentAssetNameAsSubfolder) iconUseParentAsset = EditorResources.IconFolder;

            if (GUI.Button(rectUseParentAsset, new GUIContent(iconUseParentAsset, "Use parent asset's name as subfolder?")))
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("As Subfolder"), useParentAssetNameAsSubfolder, CallbackUseParentAsset, true);
                menu.AddItem(new GUIContent("Do Not Use"), !useParentAssetNameAsSubfolder, CallbackUseParentAsset, false);

                menu.DropDown(rectUseParentAsset);
            }



            if (prefabSaveIn == Enum.SaveLocation.SameSubfolder)
            {
                using (new EditorGUIHelper.GUIBackgroundColor(EditorUtilities.ContainsInvalidFileNameCharacters(subfolderName) ? Color.red : Color.white))
                {
                    subfolderName = EditorGUI.TextField(collumnRect3, string.Empty, subfolderName);
                }

                if (string.IsNullOrEmpty(subfolderName.Trim()))
                {
                    using (new EditorGUIHelper.GUIColor(UnityEditor.EditorGUIUtility.isProSkin ? (Color.white * 0.5f) : (Color.white * 0.25f)))
                    {
                        EditorGUI.TextField(collumnRect3, string.Empty, WireframeShaderAbout.name);
                    }
                }
            }
            else if (prefabSaveIn == Enum.SaveLocation.CustomFolder)
            {
                using (new EditorGUIHelper.GUIBackgroundColor((Directory.Exists(saveFolderCustomPath) == false) ? Color.red : (EditorUtilities.IsPathProjectRelative(saveFolderCustomPath) ? EditorResources.projectRelatedPathColor : Color.white)))
                {
                    saveFolderCustomPath = EditorGUI.TextField(new Rect(collumnRect3.xMin, collumnRect3.yMin, collumnRect3.width - 25, collumnRect3.height), saveFolderCustomPath);
                }

                if (GUI.Button(new Rect(collumnRect3.xMax - 24, collumnRect3.yMin, 25, collumnRect3.height), "..."))
                {
                    GUIUtility.keyboardControl = 0;


                    string newPathName = saveFolderCustomPath;
                    newPathName = UnityEditor.EditorUtility.OpenFolderPanel("Select Folder", Directory.Exists(saveFolderCustomPath) ? saveFolderCustomPath : Application.dataPath, string.Empty);
                    if (string.IsNullOrEmpty(newPathName) == false)
                    {
                        if (EditorUtilities.IsPathProjectRelative(newPathName))
                            newPathName = EditorUtilities.ConvertPathToProjectRelative(newPathName);

                        saveFolderCustomPath = newPathName;
                    }
                }

                saveFolderStructure = EditorGUIHelper.ToggleAsButton(collumnRect4, saveFolderStructure, "Save Folder Structure");
            }
        }
        protected virtual void DrawCustomSettings(Rect collumnRect1, Rect collumnRect2, Rect collumnRect3, Rect collumnRect4) { }
        public Rect AddRow(ref Rect collumnRect1, ref Rect collumnRect2, ref Rect collumnRect3, ref Rect collumnRect4)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            collumnRect1.yMin = collumnRect2.yMin = collumnRect3.yMin = collumnRect4.yMin = rect.yMin;
            collumnRect1.height = collumnRect2.height = collumnRect3.height = collumnRect4.height = rect.height;

            return rect;
        }

        public virtual void Reset()
        {
            prefabSaveIn = Enum.SaveLocation.SameFolder;
            useParentAssetNameAsSubfolder = true;
            saveFolderCustomPath = string.Empty;
            filePrefix = string.Empty;
            fileSuffix = string.Empty;
            subfolderName = string.Empty;
            saveFolderStructure = true;


            drawTabHelp = false;    //Hide help tab
        }
        public virtual bool IsReady()
        {
            if (EditorUtilities.ContainsInvalidFileNameCharacters(filePrefix) || EditorUtilities.ContainsInvalidFileNameCharacters(fileSuffix))
                return false;

            if (prefabSaveIn == Enum.SaveLocation.SameSubfolder && EditorUtilities.ContainsInvalidFileNameCharacters(subfolderName))
                return false;

            if (prefabSaveIn == Enum.SaveLocation.CustomFolder && (Directory.Exists(saveFolderCustomPath) == false))
                return false;

            if (prefabSaveIn == Enum.SaveLocation.SameFolder && useParentAssetNameAsSubfolder == false && string.IsNullOrWhiteSpace(filePrefix) && string.IsNullOrWhiteSpace(fileSuffix))
                return false;


            return true;
        }

        public virtual string SaveEditorData()
        {
            //Convert to Json
            string jsonData = JsonUtility.ToJson(this);

            //Save Json string
            EditorPrefs.SetString(GetEditorPreferencesPath(), jsonData);

            return jsonData;
        }
        public virtual void LoadEditorData()
        {
            //Reset first
            Reset();

            //Load saved Json
            string jsonData = EditorPrefs.GetString(GetEditorPreferencesPath());

            //Read Json
            if (string.IsNullOrEmpty(jsonData) == false)
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(jsonData, this);
                }
                catch
                {
                    WireframeShaderDebug.Log(LogType.Warning, "Can not load settings.", null);
                }
            }
        }
        public void LoadEditorDataFromCopyBuffer()
        {
            if (string.IsNullOrEmpty(GUIUtility.systemCopyBuffer))
            {
                WireframeShaderDebug.Log(LogType.Warning, "GUIUtility.systemCopyBuffer is empty.", null);
                return;
            }

            EditorPrefs.SetString(GetEditorPreferencesPath(), GUIUtility.systemCopyBuffer);

            LoadEditorData();
        }

        public abstract string GetEditorPreferencesName();
        protected string GetEditorPreferencesPath()
        {
            return string.Format("{0}_{1}_{2}", WireframeShaderAbout.name.RemoveWhiteSpace(), GetEditorPreferencesName(), Application.dataPath.GetHashCode());
        }

        public string GetSaveSubfolderName()
        {
            if (string.IsNullOrWhiteSpace(subfolderName))
                return WireframeShaderAbout.name;
            else
                return subfolderName;
        }
        public string GetAssetSaveDirectory(UnityEngine.Object obj, bool useTempFolder, bool useExtension)
        {
            string parentAssetPath = GetParentAssetPath(obj);

            string parentAssetName = Path.GetFileName(parentAssetPath);


            string finalPath = string.Empty;
            switch (prefabSaveIn)
            {
                case Enum.SaveLocation.SameFolder:
                    {
                        finalPath = Path.GetDirectoryName(parentAssetPath);
                    }
                    break;

                case Enum.SaveLocation.SameSubfolder:
                    {
                        finalPath = Path.Combine(Path.Combine(Path.GetDirectoryName(parentAssetPath), GetSaveSubfolderName()));
                    }
                    break;

                case Enum.SaveLocation.CustomFolder:
                    {
                        if (EditorUtilities.IsPathProjectRelative(saveFolderCustomPath))
                        {
                            if (saveFolderCustomPath.IndexOf("Assets") == 0)
                            {
                                finalPath = saveFolderCustomPath;
                            }
                            else
                            {
                                string projectPath = Application.dataPath.Replace("\\", "/").Replace("//", "/");
                                string assetSavePath = saveFolderCustomPath.Replace("\\", "/").Replace("//", "/");

                                finalPath = "Assets" + assetSavePath.Replace(projectPath, string.Empty);
                            }
                        }
                        else
                        {
                            finalPath = useTempFolder ? EditorUtilities.GetAssetTempFolder() : saveFolderCustomPath;
                        }


                        if (saveFolderStructure)
                            finalPath = Path.Combine(finalPath, Path.GetDirectoryName(parentAssetPath));
                    }
                    break;

                default:
                    goto case Enum.SaveLocation.SameFolder;
            }


            if (useParentAssetNameAsSubfolder)
            {
                finalPath = Path.Combine(finalPath, AddPrefixAndSuffix(Path.GetFileNameWithoutExtension(parentAssetName)));
            }


            return finalPath;
        }
        public string GetSaveAssetName(UnityEngine.Object obj, bool useExtension)
        {
            string assetName = EditorUtilities.RemoveInvalidCharacters(obj.name).Trim();
            if (string.IsNullOrEmpty(assetName))
                assetName = "ID " + obj.GetInstanceID();


            //Add prefix & suffix
            assetName = AddPrefixAndSuffix(assetName);

            return assetName;
        }
        public string AddPrefixAndSuffix(string name)
        {
            name = (string.IsNullOrWhiteSpace(filePrefix) ? string.Empty : filePrefix.TrimStart()) +
                   name +
                   (string.IsNullOrWhiteSpace(fileSuffix) ? string.Empty : fileSuffix.TrimEnd());

            return name;
        }
        public string GetParentAssetPath(UnityEngine.Object obj)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);

            //Is it a scene object
            if (string.IsNullOrEmpty(assetPath))
            {
                UnityEngine.Object prefab = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
                if (prefab != null)
                {
                    prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);
                    assetPath = AssetDatabase.GetAssetPath(prefab);
                }
            }

            //Asset does not exist on HDD or can not be loaded
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(Path.GetExtension(assetPath)))
            {
                string name = string.IsNullOrEmpty(obj.name) ? ($"ID {obj.GetInstanceID()}") : (obj.name + $" (ID {obj.GetInstanceID()})");

                assetPath = Path.Combine("Assets", "Generated Assets", WireframeShaderAbout.name, name) + ".asset";
            }


            return assetPath;
        }


        void CallbackUseParentAsset(object obj)
        {
            useParentAssetNameAsSubfolder = (bool)obj;
        }
        internal static void CatchDragAndDrop(Rect dropArea, Action LoadEditorDataFromCopyBuffer)
        {
            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    {
                        if (!dropArea.Contains(evt.mousePosition))
                            return;

                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                        if (evt.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();


                            //Read drop
                            if (DragAndDrop.paths != null && DragAndDrop.paths.Length == 1)
                            {
                                if (Path.GetFileName(DragAndDrop.paths[0]) == (WireframeShaderAbout.name.RemoveWhiteSpace() + "EditorSettings.txt"))
                                {
                                    GUIUtility.systemCopyBuffer = File.ReadAllText(DragAndDrop.paths[0]);

                                    LoadEditorDataFromCopyBuffer();
                                }
                            }
                        }
                    }
                    break;

                default: break;
            }
        }
    }
}