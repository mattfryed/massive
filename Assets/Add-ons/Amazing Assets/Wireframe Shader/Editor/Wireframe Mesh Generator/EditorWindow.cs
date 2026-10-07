// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System.IO;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor.WireframeMeshGenerator
{
    internal class EditorWindow : UnityEditor.EditorWindow
    {
        internal enum ContextMenuOption { OpenCustomSaveFolder, Reset, SelectLastSavedFile, Reload, ExportSelectedBatchObject, DeveloperLoadSettingsFromSystemMemory, DeveloperOpenThisAssetProjectFolder, DeveloperLoadAllProjectPrefabs, DeveloperToggleIcons, DeveloperMakeBatchObjectMeshesReadable, DeveloperMakeBatchObjectMeshesNotReadable }

        internal static EditorWindow active;



        internal EditorSettings editorSettings;

        internal List<BatchObject> listBatchObjects;
        internal BatchObject problematicBatchObject;

        static GameObject objectPicker;
        protected internal int selectedBatchObjectIndex = -1;



        Vector2 scrollSettings;
        readonly int objectPickerID = 443101;
        internal string lastSavedFilePath;
        static readonly int windowMinWidth = 650;


        [MenuItem("Window/Amazing Assets/Wireframe Shader/Wireframe Mesh Generator", false, 3101)]
        static public void ShowWindow()
        {
            EditorWindow window = GetWindow<EditorWindow>("Wireframe Mesh Generator");

            BatchObjectsDrawer.ResetSort();

            window.Show();
        }


        void OnEnable()
        {
            active = this;

            LoadEditorData();
        }
        void OnDisable()
        {
            active = this;

            SaveEditorData();
        }
        void OnDestroy()
        {
            active = this;

            if (listBatchObjects != null)
                listBatchObjects.Clear();


            SaveEditorData();
        }        
        void OnFocus()
        {
            active = this;

            LoadEditorData();

            UnityEditor.EditorUtility.ClearProgressBar();
        }        
        void OnLostFocus()
        {
            active = this;

            SaveEditorData();
        }
        void OnGUI()
        {
            if (WireframeTextureGenerator.EditorWindow.AreRequiedFilesInstalled() == false)
                return;


            LoadResources();

            scrollSettings = EditorGUILayout.BeginScrollView(scrollSettings);
            {
                //This will force to display horizontal scrollbar after window width becomes less than 'minwidth' pixels.
                EditorGUILayout.GetControlRect(false, 1, GUILayout.MinWidth(windowMinWidth));


                editorSettings.Draw();

                DrawBatchObjectsArray();
            }
            EditorGUILayout.EndScrollView();

            DrawRunButton();



            CatchInput();

            CatchContextMenu();

            BatchObjectsDrawer.CatchDragAndDrop();
            EditorSettingsBase.CatchDragAndDrop(new Rect(0, 0, position.width, position.height), editorSettings.LoadEditorDataFromCopyBuffer);

            if (objectPicker != null)
            {
                BatchObjectsDrawer.AddBatchObjectToArray(objectPicker, false);
                SaveEditorData();
                objectPicker = null;
                BatchObjectsDrawer.SortBatchObjects();
                Repaint();
            }

            CatchObjectPicker();
        }


        void DrawBatchObjectsArray()
        {
            GUILayout.Space(5);


            using (new EditorGUIHelper.EditorGUILayoutBeginHorizontal())
            {
                int buttonHeight = 30;

                using (new EditorGUIHelper.GUIEnabled((Selection.transforms != null && Selection.transforms.Length > 0)))
                {
                    if (GUILayout.Button("Add Selected", GUILayout.Height(buttonHeight)))
                    {
                        for (int i = 0; i < Selection.transforms.Length; i++)
                        {
                            if (Selection.transforms[i] != null)
                                BatchObjectsDrawer.AddBatchObjectToArray(Selection.transforms[i].gameObject, false);
                        }

                        SaveEditorData();
                        BatchObjectsDrawer.SortBatchObjects();
                    }
                }

                if (GUILayout.Button("Add Custom Prefab", GUILayout.Height(buttonHeight)))
                {
                    UnityEditor.EditorGUIUtility.ShowObjectPicker<GameObject>(null, true, string.Empty, objectPickerID);
                }


                using (new EditorGUIHelper.GUIEnabled(listBatchObjects != null && listBatchObjects.Count > 0))
                {
                    if (GUILayout.Button("Remove All", GUILayout.Height(buttonHeight)))
                    {
                        if (listBatchObjects != null)
                            listBatchObjects.Clear();

                        listBatchObjects = null;
                        selectedBatchObjectIndex = -1;

                        problematicBatchObject = null;

                        SaveEditorData();

                        Repaint();
                    }
                }
            }



            if (listBatchObjects == null || listBatchObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("Drag and drop GameObjects and Prefabs from Hierarchy and Project windows here.", MessageType.Warning);
            }
            else
            {
                BatchObjectsDrawer.Draw();
            }
        }
        void DrawRunButton()
        {
            GUILayout.Space(10);
            Rect controlRect = EditorGUILayout.GetControlRect(false, 70);
            GUILayout.Space(5);

            string buttonName = "Run";
            if (listBatchObjects != null && listBatchObjects.Count > 0)
                buttonName += " (" + listBatchObjects.Count + " Objects / " + listBatchObjects.Sum(c => (c.meshInfo == null ? 0 : c.meshInfo.Count)) + " Meshes)";


            using (new EditorGUIHelper.GUIEnabled(IsReadeToGenerate()))
            {
                if (GUI.Button(new Rect(controlRect.xMin + 2, this.position.height - 54, controlRect.width - 4 - (problematicBatchObject == null ? 0 : 150), 45), buttonName))
                {
                    Run.RunConverter();
                }

                if (problematicBatchObject != null)
                {
                    using (new EditorGUIHelper.GUIBackgroundColor(Color.red))
                    {
                        if (GUI.Button(new Rect(controlRect.xMax - 147, this.position.height - 54, 147, 45), "Export"))
                        {
                            EditorUtilities.ExportObjectInfoToPackage(problematicBatchObject.gameObject, editorSettings.SaveEditorData(), problematicBatchObject.exception);

                            problematicBatchObject = null;
                        }
                    }
                }
            }
        }

        void LoadResources()
        {
            active = this;


            if (listBatchObjects == null)
                listBatchObjects = new List<BatchObject>();

            if (editorSettings == null)
                editorSettings = new EditorSettings();
        }
        void SaveEditorData()
        {
            BatchObjectsDrawer.SaveEditorData();

            if (editorSettings != null)
                editorSettings.SaveEditorData();
        }
        void LoadEditorData()
        {
            if (editorSettings == null)
                editorSettings = new EditorSettings();


            BatchObjectsDrawer.LoadEditorData();
        }

        bool IsReadeToGenerate()
        {
            if (listBatchObjects == null || listBatchObjects.Count == 0)
                return false;

            return editorSettings.IsReady();
        }

        void CatchInput()
        {
            if (listBatchObjects != null && listBatchObjects.Count > 0 && Event.current != null)
            {
                if (Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.UpArrow)
                    {
                        if (selectedBatchObjectIndex > 0)
                        {
                            selectedBatchObjectIndex -= 1;

                            Repaint();
                        }
                        else if (selectedBatchObjectIndex < 0)
                        {
                            selectedBatchObjectIndex = listBatchObjects.Count - 1;

                            Repaint();
                        }
                    }

                    if (Event.current.keyCode == KeyCode.DownArrow)
                    {
                        if (selectedBatchObjectIndex < listBatchObjects.Count - 1)
                        {
                            selectedBatchObjectIndex += 1;

                            Repaint();
                        }
                    }

                    if (Event.current.keyCode == KeyCode.Delete)
                    {
                        BatchObjectsDrawer.RemoveSelectedBatchObject(selectedBatchObjectIndex, true);
                    }
                }
            }
        }
        void CatchContextMenu()
        {
            var evt = Event.current;
            var contextRect = new Rect(10, 10, this.position.width, this.position.height);
            if (evt != null && evt.type == EventType.ContextClick)
            {
                var mousePos = evt.mousePosition;
                if (contextRect.Contains(mousePos))
                {
                    GenericMenu menu = new GenericMenu();

                    if (listBatchObjects != null && listBatchObjects.Count >= 0)
                    {
                        if (editorSettings.prefabSaveIn == EditorSettingsBase.Enum.SaveLocation.CustomFolder)
                        {
                            if (string.IsNullOrEmpty(editorSettings.saveFolderCustomPath) == false && Directory.Exists(editorSettings.saveFolderCustomPath))
                                menu.AddItem(new GUIContent("Open Save Folder"), false, CallbackContextMenu, ContextMenuOption.OpenCustomSaveFolder);
                            else
                                menu.AddDisabledItem(new GUIContent("Open Save Folder"));
                        }

                        if (string.IsNullOrEmpty(lastSavedFilePath) == false && File.Exists(lastSavedFilePath))
                            menu.AddItem(new GUIContent("Highlight Last Saved File"), false, CallbackContextMenu, ContextMenuOption.SelectLastSavedFile);
                        else
                            menu.AddDisabledItem(new GUIContent("Highlight Last Saved File"));

                        menu.AddSeparator(string.Empty);
                        if (selectedBatchObjectIndex >= 0 && selectedBatchObjectIndex < listBatchObjects.Count)
                            menu.AddItem(new GUIContent("Export Selected Object"), false, CallbackContextMenu, ContextMenuOption.ExportSelectedBatchObject);
                        else
                            menu.AddDisabledItem(new GUIContent("Export Selected Object"));


                        menu.AddSeparator(string.Empty);
                    }

                    menu.AddItem(new GUIContent("Reset"), false, CallbackContextMenu, ContextMenuOption.Reset);


                    //
                    if (evt.control && evt.shift)
                    {
                        menu.AddSeparator(string.Empty);
                        menu.AddItem(new GUIContent("Developer/Load Settings From System Copy Buffer"), false, CallbackContextMenu, ContextMenuOption.DeveloperLoadSettingsFromSystemMemory);
                        menu.AddItem(new GUIContent("Developer/Open This Asset Project Folder"), false, CallbackContextMenu, ContextMenuOption.DeveloperOpenThisAssetProjectFolder);
                        menu.AddItem(new GUIContent("Developer/Load All Project Prefabs"), false, CallbackContextMenu, ContextMenuOption.DeveloperLoadAllProjectPrefabs);
                        menu.AddItem(new GUIContent("Developer/Toggle Asset Icons"), false, CallbackContextMenu, ContextMenuOption.DeveloperToggleIcons);
                        menu.AddItem(new GUIContent("Developer/Make Batch Object Meshes Readable"), false, CallbackContextMenu, ContextMenuOption.DeveloperMakeBatchObjectMeshesReadable);
                        menu.AddItem(new GUIContent("Developer/Make Batch Object Meshes Not Readable"), false, CallbackContextMenu, ContextMenuOption.DeveloperMakeBatchObjectMeshesNotReadable);
                    }


                    menu.ShowAsContext();
                }
            }
        }
        void CatchObjectPicker()
        {
            if (Event.current.commandName == "ObjectSelectorUpdated")
            {
                if (UnityEditor.EditorGUIUtility.GetObjectPickerControlID() == objectPickerID)
                {
                    if (UnityEditor.EditorGUIUtility.GetObjectPickerObject() != null)
                    {
                        objectPicker = UnityEditor.EditorGUIUtility.GetObjectPickerObject() as GameObject;
                    }
                }
            }
        }
        internal void CallbackContextMenu(object obj)
        {
            if (obj == null)
                return;

            switch ((ContextMenuOption)obj)
            {
                case ContextMenuOption.Reset:
                    {
                        editorSettings.Reset();
                        BatchObjectsDrawer.ResetSort();
                        Repaint();
                        SceneView.RepaintAll();
                    }
                    break;

                case ContextMenuOption.SelectLastSavedFile:
                    {
                        EditorUtilities.SelectFile(lastSavedFilePath);
                    }
                    break;

                case ContextMenuOption.OpenCustomSaveFolder:
                    {
                        EditorUtilities.OpenFolder(editorSettings.saveFolderCustomPath);
                    }
                    break;

                case ContextMenuOption.Reload:
                    {
                        BatchObjectsDrawer.ResetSort();

                        SaveEditorData();

                        if (listBatchObjects != null)
                            listBatchObjects.Clear();

                        problematicBatchObject = null;

                        LoadEditorData();
                        Repaint();
                        SceneView.RepaintAll();
                    }
                    break;

                case ContextMenuOption.ExportSelectedBatchObject:
                    {
                        if (listBatchObjects != null &&
                            listBatchObjects.Count > 0 &&
                            selectedBatchObjectIndex >= 0 &&
                            selectedBatchObjectIndex < listBatchObjects.Count)
                        {
                            EditorUtilities.ExportObjectInfoToPackage(listBatchObjects[selectedBatchObjectIndex].gameObject,
                                                                      editorSettings.SaveEditorData(),
                                                                      listBatchObjects[selectedBatchObjectIndex].exception);
                        }
                    }
                    break;

                case ContextMenuOption.DeveloperLoadSettingsFromSystemMemory:
                    {
                        editorSettings.LoadEditorDataFromCopyBuffer();
                    }
                    break;

                case ContextMenuOption.DeveloperOpenThisAssetProjectFolder:
                    {
                        EditorUtilities.PingObject(EditorUtilities.GetThisAssetProjectPath());
                    }
                    break;

                case ContextMenuOption.DeveloperLoadAllProjectPrefabs:
                    {
                        string[] guids = AssetDatabase.FindAssets("t:Prefab t:Model", null);

                        for (int i = 0; i < guids.Length; i++)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                            UnityEditor.EditorUtility.DisplayProgressBar("Hold On", path, (float)i / guids.Length);

                            if (string.IsNullOrEmpty(path) == false && EditorUtilities.IsPathProjectRelative(path))
                                BatchObjectsDrawer.AddBatchObjectToArray((GameObject)AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)), false);
                        }


                        BatchObjectsDrawer.SortBatchObjects();
                        BatchObjectsDrawer.SaveEditorData();


                        UnityEditor.EditorUtility.ClearProgressBar();
                    }
                    break;

                case ContextMenuOption.DeveloperToggleIcons:
                    {
                        string iconFolderPath = Path.Combine(EditorUtilities.GetThisAssetProjectPath(), "Editor", "Icons");

                        string[] iconFiles = Directory.GetFiles(iconFolderPath, "*.png");
                        if (iconFiles.Length > 0)
                        {
                            foreach (string file in iconFiles)
                            {
                                string newFileName = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
                                File.Move(file, newFileName);
                            }
                        }
                        else
                        {
                            foreach (string file in Directory.GetFiles(iconFolderPath))
                            {
                                if (string.IsNullOrEmpty(Path.GetExtension(file))) // Check if the file has no extension
                                {
                                    string newFileName = file + ".png"; // Append .png
                                    File.Move(file, newFileName);
                                }
                            }
                        }

                        AssetDatabase.Refresh();
                    }
                    break;

                case ContextMenuOption.DeveloperMakeBatchObjectMeshesReadable:
                    {
                        Run.MakeMeshesReadable(true);
                    }
                    break;

                case ContextMenuOption.DeveloperMakeBatchObjectMeshesNotReadable:
                    {
                        Run.MakeMeshesReadable(false);
                    }
                    break;

                default:
                    break;
            }
        }
    }
}