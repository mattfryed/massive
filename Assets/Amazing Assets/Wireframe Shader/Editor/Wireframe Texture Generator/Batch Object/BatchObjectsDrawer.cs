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
    internal class BatchObjectsDrawer
    {
        enum SortBy { None, ObjectData, SubmeshCount, UV0, VertexAttribute }
		enum ScrollViewItemVisibility { Visible, AboveDrawArea, BelowDrawArea }


        static SortBy sortBy = SortBy.None;
        static Vector2 scrollBatchObjects;
        static bool sortByAscending = true;    //true - OrderBy, false - OrderByDescending


        static int singleLineHeight = 20;


        internal static void Draw()
        {
            CatchKeyboard();


            List<BatchObject> listBatchObjects = EditorWindow.active.listBatchObjects;
            EditorSettings editorSettings = EditorWindow.active.editorSettings;


            Rect rectObjectData = new Rect();
            Rect rectSubmeshCount = new Rect();
            Rect rectUV0 = new Rect();
            Rect rectVertexAttribute = new Rect(); bool needRectVertexAttribute = false;
            Rect rectRefresh = new Rect();

            
            bool needRepaint = false;
            Rect toolbarRect;


            int visibleBatchObjectsCount = 0;
            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox, GUILayout.MaxHeight(GetScrolViewHeight())))
            {
                toolbarRect = EditorGUILayout.GetControlRect(false, singleLineHeight);


                scrollBatchObjects = EditorGUILayout.BeginScrollView(scrollBatchObjects);
                {
                    for (int i = 0; i < listBatchObjects.Count; i++)
                    {
                        BatchObject currentBatchObject = listBatchObjects[i];

                        //Rect for current raw
                        Rect currentRowRect = EditorGUILayout.GetControlRect();


                        if (i == 0)
                            SplitControlRect(currentRowRect, out rectObjectData, out rectSubmeshCount, out rectUV0, out needRectVertexAttribute, out rectVertexAttribute, out rectRefresh);


                        if (currentBatchObject == null || currentBatchObject.mesh == null)
                        {
                            using (new EditorGUIHelper.GUIBackgroundColor(Color.yellow))
                            {
                                EditorGUI.ObjectField(new Rect(rectObjectData.xMin, currentRowRect.yMin, rectObjectData.width, currentRowRect.height), null, typeof(Mesh), false);
                            }

                            needRepaint = true;
                        }
                        else
                        {
                            ScrollViewItemVisibility isRowVisible = IsRectVisibleInsideScrollView(toolbarRect, currentRowRect, scrollBatchObjects);
                            if (isRowVisible == ScrollViewItemVisibility.Visible)
                            {
                                visibleBatchObjectsCount += 1;


                                //Higlight selected row
                                EditorGUI.BeginChangeCheck();
                                using (new EditorGUIHelper.GUIBackgroundColor(Color.clear))
                                {
                                    EditorGUI.Foldout(new Rect(rectObjectData.xMax, currentRowRect.yMin, rectRefresh.xMin - rectObjectData.xMax, currentRowRect.height), false, GUIContent.none, true);
                                }
                                if (EditorGUI.EndChangeCheck())
                                {
                                    if (EditorWindow.active.selectedBatchObjectIndex == i)
                                        EditorWindow.active.selectedBatchObjectIndex = -1;
                                    else
                                        EditorWindow.active.selectedBatchObjectIndex = i;

                                    editorSettings.RenderPreviewTextures();
                                }

                                if (EditorWindow.active.selectedBatchObjectIndex == i)
                                    EditorGUI.DrawRect(new Rect(rectObjectData.xMax + 5, currentRowRect.yMin, rectRefresh.xMin - rectObjectData.xMax - 6, currentRowRect.height), Color.green * 0.2f);


                                //Gameobject or ProjectObject
                                Color backgroundColor = Color.white;
                                if (EditorWindow.active.problematicBatchObject != null && EditorWindow.active.problematicBatchObject.mesh == currentBatchObject.mesh)
                                    backgroundColor = Color.red;


                                Rect rectObjectField = new Rect(rectObjectData.xMin, currentRowRect.yMin, rectObjectData.width, currentRowRect.height);
                                using (new EditorGUIHelper.GUIBackgroundColor(backgroundColor))
                                {
                                    EditorGUI.ObjectField(rectObjectField, currentBatchObject.mesh, typeof(Mesh), false);
                                }


                                //Submesh
                                EditorGUI.LabelField(new Rect(rectSubmeshCount.xMin, currentRowRect.yMin, rectSubmeshCount.width, currentRowRect.height), currentBatchObject.mesh.subMeshCount.ToString(), EditorResources.GUIStyleCenteredGreyMiniLabel);


                                //UV0                                
                                GUI.DrawTexture(new Rect(rectUV0.xMin + rectUV0.width / 2 - 6, currentRowRect.yMin + 2, 12, 12), currentBatchObject.hasUV0 ? EditorResources.IconYes : EditorResources.IconNo);


                                if (needRectVertexAttribute)
                                {
                                    Rect vertexAttributeDrawRect = new Rect(rectVertexAttribute.xMin + rectVertexAttribute.width / 2 - 6, currentRowRect.yMin + 2, 12, 12);
                                    switch (editorSettings.solverReadFrom)
                                    {
                                        case WireframeShaderEnum.VertexAttribute.UV0: /*Never used*/ break;

                                        case WireframeShaderEnum.VertexAttribute.UV1: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasUV1 ? EditorResources.IconYes : EditorResources.IconNo); break;
                                        case WireframeShaderEnum.VertexAttribute.UV2: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasUV2 ? EditorResources.IconYes : EditorResources.IconNo); break;
                                        case WireframeShaderEnum.VertexAttribute.UV3: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasUV3 ? EditorResources.IconYes : EditorResources.IconNo); break;
                                        case WireframeShaderEnum.VertexAttribute.UV4: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasUV4 ? EditorResources.IconYes : EditorResources.IconNo); break;
                                        case WireframeShaderEnum.VertexAttribute.UV5: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasUV5 ? EditorResources.IconYes : EditorResources.IconNo); break;
                                        case WireframeShaderEnum.VertexAttribute.UV6: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasUV6 ? EditorResources.IconYes : EditorResources.IconNo); break;
                                        case WireframeShaderEnum.VertexAttribute.UV7: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasUV7 ? EditorResources.IconYes : EditorResources.IconNo); break;
                                        case WireframeShaderEnum.VertexAttribute.Normal: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasNormal ? EditorResources.IconYes : EditorResources.IconNo); break;
                                        case WireframeShaderEnum.VertexAttribute.Tangent: GUI.DrawTexture(vertexAttributeDrawRect, currentBatchObject.hasTangent ? EditorResources.IconYes : EditorResources.IconNo); break;

                                        default:
                                            break;
                                    }
                                }


                                //Remove
                                if (GUI.Button(new Rect(rectRefresh.xMin, currentRowRect.yMin - 1, rectRefresh.width, currentRowRect.height - 1), EditorResources.GUIContentRemoveButton, EditorStyles.toolbarButton))
                                {
                                    if (Event.current.button == 1)
                                    {
                                        if (Event.current.control)
                                            RemoveAllElementsBelow(i);
                                        else
                                            RemoveAllBatchObjectsExceptSelected(i);
                                    }
                                    else
                                    {
                                        if (Event.current.control)
                                            RemoveAllElementsAbove(i);
                                        else
                                            RemoveSelectedBatchObject(i, false);
                                    }

                                    break;
                                }


                                //Draw line
                                if (!(i == listBatchObjects.Count - 1))
                                    EditorGUI.DrawRect(new Rect(rectObjectData.xMax + 5, currentRowRect.yMax, rectRefresh.xMax - rectObjectData.xMax - 5, 1), Color.gray);
                            }
                            else if (isRowVisible == ScrollViewItemVisibility.BelowDrawArea)
                                break;
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            if (needRepaint)
            {
                listBatchObjects = listBatchObjects.Where(c => c.mesh != null).ToList();
                EditorWindow.active.Repaint();
            }


            //Toolbar
            if (visibleBatchObjectsCount == 0)
            {
                EditorGUI.LabelField(toolbarRect, "Draw area is too small. Expand window size to see objects list.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                int offset = 7;

                GUI.Box(toolbarRect, string.Empty, EditorStyles.toolbar);


                DrawSortByButton(new Rect(rectObjectData.xMin + offset, toolbarRect.yMin, rectObjectData.width, singleLineHeight), "Target", SortBy.ObjectData);
                DrawSortByButton(new Rect(rectSubmeshCount.xMin + offset, toolbarRect.yMin, rectSubmeshCount.width, singleLineHeight), "Submesh", SortBy.SubmeshCount);
                DrawSortByButton(new Rect(rectUV0.xMin + offset, toolbarRect.yMin, rectUV0.width, singleLineHeight), "UV0", SortBy.UV0);

                if (needRectVertexAttribute)
                    DrawSortByButton(new Rect(rectVertexAttribute.xMin + offset, toolbarRect.yMin, rectVertexAttribute.width, singleLineHeight), editorSettings.solverReadFrom.ToString(), SortBy.VertexAttribute);

                if (GUI.Button(new Rect(rectRefresh.xMin + offset, toolbarRect.yMin, rectRefresh.width, singleLineHeight), " ", EditorStyles.toolbarButton))
                {
                    EditorWindow.active.CallbackContextMenu(EditorWindow.ContextMenuOption.Reload);
                }
                GUI.Label(new Rect(rectRefresh.xMin + offset, toolbarRect.yMin, rectRefresh.width, singleLineHeight), new GUIContent("↻", "Reload"), EditorResources.GUIStyleCenteredGreyMiniLabel);
            }
        }

        static int GetScrolViewHeight()
        {
            int renderObjectsCount = EditorWindow.active.listBatchObjects.Count + 1;

            return renderObjectsCount * singleLineHeight + 12;
        }
        internal static void SaveEditorData()
        {
            List<BatchObject> listBatchObjects = EditorWindow.active.listBatchObjects;


            if (listBatchObjects != null)
            {
                string batchObjectsData = $"{(int)sortBy},{(sortByAscending ? 1 : 0)}|";
                for (int i = 0; i < listBatchObjects.Count; i++)
                {
                    if (listBatchObjects[i] != null && listBatchObjects[i].mesh != null)
                    {
                        batchObjectsData += listBatchObjects[i].mesh.GetInstanceID() + ";";
                    }
                }

                EditorPrefs.SetString(GetEditorPreferencesPath(), batchObjectsData);
            }
        }
        internal static void LoadEditorData()
        {
            if (EditorWindow.active.listBatchObjects != null)
                EditorWindow.active.listBatchObjects.Clear();


            string editorPrefs = EditorPrefs.GetString(GetEditorPreferencesPath());
            if (string.IsNullOrEmpty(editorPrefs) == false)
            {
                string[] editorSettings = editorPrefs.Split('|')[0].Split(',');
                if (int.TryParse(editorSettings[0], out int iValue))
                    sortBy = (SortBy)iValue;
                if (int.TryParse(editorSettings[1], out iValue))
                    sortByAscending = iValue == 1;


                editorSettings = editorPrefs.Split('|')[1].Split(';');

                for (int i = 0; i < editorSettings.Length; i++)
                {
                    if (string.IsNullOrEmpty(editorSettings[i]))
                        continue;


                    if (int.TryParse(editorSettings[i], out iValue))
                    {
                        UnityEngine.Object uObject = EditorUtilities.InstanceIDToObject(iValue);
                        if (uObject != null)
                            AddBatchObjectToArray(uObject as Mesh, false);
                    }
                }

                SortBatchObjects();
            }
        }
        static string GetEditorPreferencesPath()
        {
            return WireframeShaderAbout.name.RemoveWhiteSpace() + "WireframeTextureGenerator_ObjectsID_" + Application.dataPath.GetHashCode();
        }


        static void SplitControlRect(Rect controlRect, out Rect rectObjectData, out Rect rectSubmeshCount, out Rect rectUV0, out bool needRectVertexAttribute, out Rect rectVertexAttribute, out Rect rectRefresh)
        {
            EditorSettings editorSettings = EditorWindow.active.editorSettings;

            float controlWidth = controlRect.width - 20;    //20 is for refresh button

            rectRefresh = new Rect(controlRect);
            rectRefresh.xMin = controlRect.xMax - 20;
            rectRefresh.width = 20;


            float width;
            float xMin = controlWidth;

            rectVertexAttribute = new Rect();
            needRectVertexAttribute = editorSettings.solverType == WireframeShaderEnum.Solver.Prebaked && editorSettings.solverReadFrom != WireframeShaderEnum.VertexAttribute.UV0;
            if (needRectVertexAttribute)
            {
                width = Mathf.Min(controlWidth * 0.09f, 80);
                xMin -= width;
                {
                    rectVertexAttribute = new Rect(controlRect);
                    rectVertexAttribute.xMin = xMin;
                    rectVertexAttribute.width = width;
                }
            }

            width = Mathf.Min(controlWidth * 0.09f, 80);
            xMin -= width;
            {
                rectUV0 = new Rect(controlRect);
                rectUV0.xMin = xMin;
                rectUV0.width = width;
            }

            width = Mathf.Min(controlWidth * 0.12f, 100);
            xMin -= width;
            {
                rectSubmeshCount = new Rect(controlRect);
                rectSubmeshCount.xMin = xMin;
                rectSubmeshCount.width = width;
            }           

            rectObjectData = new Rect(controlRect);
            rectObjectData.width = xMin - controlRect.xMin;


            return;
        }
        static void DrawSortByButton(Rect rect, string label, SortBy sortByOnClickEvent)
        {
            using (new EditorGUIHelper.GUIColor(sortBy == sortByOnClickEvent ? Color.gray : Color.white))
            {
                if (GUI.Button(rect, label, sortBy == sortByOnClickEvent ? EditorResources.GUIStyleToolbarButtonMiddleCenterBold : EditorResources.GUIStyleToolbarButtonMiddleCenter))
                {
                    sortBy = sortByOnClickEvent;
                    sortByAscending = !sortByAscending;

                    SortBatchObjects();

                    SaveEditorData();

                    EditorWindow.active.Repaint();
                }
            }
        }
        internal static void ResetSort()
        {
            sortBy = SortBy.None;
        }
        internal static void SortBatchObjects()
        {
            string N0 = (1000).ToString("N0").Replace("1", string.Empty).Replace("0", string.Empty);


            switch (sortBy)
            {
                case SortBy.ObjectData: SortBatchObjects(r => r.meshSortName, null); break;
                case SortBy.SubmeshCount: SortBatchObjects(r => r.mesh.subMeshCount, r => r.meshSortName); break;
                case SortBy.UV0: SortBatchObjects(r => r.hasUV0, r => r.meshSortName); break;
                case SortBy.VertexAttribute:
                    {
                        switch (EditorWindow.active.editorSettings.solverReadFrom)
                        {
                            case WireframeShaderEnum.VertexAttribute.UV0: /*Never used*/ break;
                            case WireframeShaderEnum.VertexAttribute.UV1: SortBatchObjects(r => r.hasUV1, r => r.meshSortName); break;
                            case WireframeShaderEnum.VertexAttribute.UV2: SortBatchObjects(r => r.hasUV2, r => r.meshSortName); break;
                            case WireframeShaderEnum.VertexAttribute.UV3: SortBatchObjects(r => r.hasUV3, r => r.meshSortName); break;
                            case WireframeShaderEnum.VertexAttribute.UV4: SortBatchObjects(r => r.hasUV4, r => r.meshSortName); break;
                            case WireframeShaderEnum.VertexAttribute.UV5: SortBatchObjects(r => r.hasUV5, r => r.meshSortName); break;
                            case WireframeShaderEnum.VertexAttribute.UV6: SortBatchObjects(r => r.hasUV6, r => r.meshSortName); break;
                            case WireframeShaderEnum.VertexAttribute.UV7: SortBatchObjects(r => r.hasUV7, r => r.meshSortName); break;
                            case WireframeShaderEnum.VertexAttribute.Normal: SortBatchObjects(r => r.hasNormal, r => r.meshSortName); break;
                            case WireframeShaderEnum.VertexAttribute.Tangent: SortBatchObjects(r => r.hasTangent, r => r.meshSortName); break;

                            default:
                                break;
                        }
                    } break;
                default:
                    break;
            }
        }
        static void SortBatchObjects(Func<BatchObject, System.Object> orderRoot, Func<BatchObject, System.Object> orderRootThen)
        {
            if (EditorWindow.active.listBatchObjects == null)
                return;


            //Save batch object and reselect it after sorting;
            BatchObject selectedBatchObject = null;
            if (EditorWindow.active.selectedBatchObjectIndex > -1 && EditorWindow.active.listBatchObjects.Count > EditorWindow.active.selectedBatchObjectIndex)
                selectedBatchObject = EditorWindow.active.listBatchObjects[EditorWindow.active.selectedBatchObjectIndex];


            if (sortByAscending)
            {
                if (orderRoot != null)
                {
                    if (orderRootThen != null)
                        EditorWindow.active.listBatchObjects = EditorWindow.active.listBatchObjects.OrderBy(orderRoot).ThenBy(orderRootThen).ToList();
                    else
                        EditorWindow.active.listBatchObjects = EditorWindow.active.listBatchObjects.OrderBy(orderRoot).ToList();
                }
            }
            else
            {
                if (orderRoot != null)
                {
                    if (orderRootThen != null)
                        EditorWindow.active.listBatchObjects = EditorWindow.active.listBatchObjects.OrderByDescending(orderRoot).ThenBy(orderRootThen).ToList();
                    else
                        EditorWindow.active.listBatchObjects = EditorWindow.active.listBatchObjects.OrderByDescending(orderRoot).ToList();
                }
            }


            //Restore selection
            if (selectedBatchObject != null)
            {
                EditorWindow.active.selectedBatchObjectIndex = EditorWindow.active.listBatchObjects.IndexOf(selectedBatchObject);
            }
        }

        static ScrollViewItemVisibility IsRectVisibleInsideScrollView(Rect topMostRect, Rect localRect, Vector2 scrollPosition)
        {
            float windowSpacePositionY = topMostRect.y + localRect.y - scrollPosition.y;

            if (windowSpacePositionY < topMostRect.yMax - 40)
                return ScrollViewItemVisibility.AboveDrawArea;
            else if (windowSpacePositionY > EditorWindow.active.position.height - 110)
                return ScrollViewItemVisibility.BelowDrawArea;
            else
                return ScrollViewItemVisibility.Visible;
        }

        internal static void AddDrops(UnityEngine.Object[] drops, bool checkDirectory)
        {
            if (drops == null || drops.Length == 0)
                return;


            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i] == null)
                    continue;

                UnityEditor.EditorUtility.DisplayProgressBar("Hold On", drops[i].name, (float)i / drops.Length);


                string dropPath = AssetDatabase.GetAssetPath(drops[i]);
                string dropExtension = Path.GetExtension(dropPath).ToLowerInvariant();

                GameObject gameObject = drops[i] as GameObject;
                Mesh mesh = drops[i] as Mesh;

                if (gameObject != null)
                {
                    AddBatchObjectToArray(gameObject, false);
                }
                else if (mesh != null)
                {
                    AddBatchObjectToArray(mesh, false);
                }
                else if (string.IsNullOrEmpty(dropExtension))
                {
                    //May be it is a folder ?
                    if (checkDirectory)
                        AddBatchObjectToArray(dropPath, false);
                }
            }


            SortBatchObjects();
            SaveEditorData();

            UnityEditor.EditorUtility.ClearProgressBar();
            EditorWindow.active.Repaint();
        }
        internal static void AddBatchObjectToArray(GameObject gameObject, bool sort)
        {
            if (gameObject != null)
            {
                MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(true);
                for (int m = 0; m < meshFilters.Length; m++)
                {
                    if (meshFilters[m] != null && meshFilters[m].sharedMesh != null)
                        AddBatchObjectToArray(meshFilters[m].sharedMesh, false);
                }

                SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int m = 0; m < skinnedMeshRenderers.Length; m++)
                {
                    if (skinnedMeshRenderers[m] != null && skinnedMeshRenderers[m].sharedMesh != null)
                        AddBatchObjectToArray(skinnedMeshRenderers[m].sharedMesh, false);
                }

                if (sort)
                    SortBatchObjects();
            }
        }
        internal static BatchObject AddBatchObjectToArray(Mesh mesh, bool sort)
        {
            if (EditorWindow.active.listBatchObjects == null)
                EditorWindow.active.listBatchObjects = new List<BatchObject>();

            if (mesh != null && EditorWindow.active.listBatchObjects.Any(x => x.mesh.GetInstanceID() == mesh.GetInstanceID()) == false)
            {
                string assetName = EditorWindow.active.editorSettings.GetSaveAssetName(mesh, true);
                string assetSaveDirectory = EditorWindow.active.editorSettings.GetAssetSaveDirectory(mesh, true, true);
                string savePath = Path.Combine(assetSaveDirectory, assetName);


                BatchObject batchObject = new BatchObject(mesh);

                EditorWindow.active.listBatchObjects.Add(batchObject);


                if (sort)
                    SortBatchObjects();


                SceneView.RepaintAll();
                return batchObject;
            }

            return null;
        }
        internal static void AddBatchObjectToArray(string folderPath, bool sort)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab t:Model t:Mesh", string.IsNullOrEmpty(folderPath) ? null : new string[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                UnityEditor.EditorUtility.DisplayProgressBar("Hold On", path, (float)i / guids.Length);

                if (string.IsNullOrEmpty(path) == false && EditorUtilities.IsPathProjectRelative(path))
                {
                    GameObject gameObject = (GameObject)AssetDatabase.LoadAssetAtPath(path, typeof(GameObject));
                    if (gameObject != null)
                        AddBatchObjectToArray(gameObject, false);
                    else
                    {
                        Mesh mesh = (Mesh)AssetDatabase.LoadAssetAtPath(path, typeof(Mesh));
                        if (mesh != null)
                            AddBatchObjectToArray(mesh, false);
                    }
                }

            }

            if (sort)
                SortBatchObjects();

            SaveEditorData();

            UnityEditor.EditorUtility.ClearProgressBar();
        }
        internal static void RemoveSelectedBatchObject(int index, bool selectNext)
        {
            if (index >= 0 && index < EditorWindow.active.listBatchObjects.Count)
            {
                if (EditorWindow.active.problematicBatchObject != null && EditorWindow.active.problematicBatchObject == EditorWindow.active.listBatchObjects[index])
                    EditorWindow.active.problematicBatchObject = null;

                EditorWindow.active.listBatchObjects.Remove(EditorWindow.active.listBatchObjects[index]);

                if (selectNext)
                {
                    if (EditorWindow.active.listBatchObjects.Count > 0)
                        EditorWindow.active.selectedBatchObjectIndex = Mathf.Clamp(EditorWindow.active.selectedBatchObjectIndex, 0, EditorWindow.active.listBatchObjects.Count - 1);
                }
                else
                {
                    if (index == EditorWindow.active.selectedBatchObjectIndex)
                        EditorWindow.active.selectedBatchObjectIndex = -1;
                    else if (index <= EditorWindow.active.selectedBatchObjectIndex)
                        EditorWindow.active.selectedBatchObjectIndex -= 1;
                }

                if (EditorWindow.active.listBatchObjects.Count == 0)
                    EditorWindow.active.selectedBatchObjectIndex -= 1;


                EditorWindow.active.Repaint();
                SceneView.RepaintAll();
            }
        }
        internal static void RemoveAllBatchObjectsExceptSelected(int index)
        {
            BatchObject batchObject = EditorWindow.active.listBatchObjects[index];

            EditorWindow.active.listBatchObjects.Clear();
            EditorWindow.active.listBatchObjects.Add(batchObject);

            if (EditorWindow.active.selectedBatchObjectIndex == index)
                EditorWindow.active.selectedBatchObjectIndex = 0;


            EditorWindow.active.Repaint();
            SceneView.RepaintAll();
        }
        internal static void RemoveAllElementsAbove(int index)
        {
            if (index < 0 || index >= EditorWindow.active.listBatchObjects.Count)
                return;

            EditorWindow.active.listBatchObjects.RemoveRange(0, index);

            EditorWindow.active.Repaint();
            SceneView.RepaintAll();
        }
        internal static void RemoveAllElementsBelow(int index)
        {
            if (index < 0)
            {
                // If index is 0 or less, remove nothing
                return;
            }
            else if (index >= EditorWindow.active.listBatchObjects.Count)
            {
                // If index is out of range, remove all
                EditorWindow.active.listBatchObjects.Clear();
                return;
            }

            EditorWindow.active.listBatchObjects.RemoveRange(index + 1, EditorWindow.active.listBatchObjects.Count - (index + 1));

            EditorWindow.active.Repaint();
            SceneView.RepaintAll();
        }
        internal static void CatchDragAndDrop()
        {
            Rect drop_area = new Rect(0, 0, EditorWindow.active.position.width, EditorWindow.active.position.height);

            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!drop_area.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        AddDrops(DragAndDrop.objectReferences, true);

                        SaveEditorData();
                        EditorWindow.active.editorSettings.SaveEditorData();
                        UnityEditor.EditorUtility.ClearProgressBar();
                        EditorWindow.active.Repaint();
                    }
                    break;
            }
        }
        internal static void CatchKeyboard()
        {
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Home && Event.current.control)
                {
                    scrollBatchObjects.y = 0;
                    EditorWindow.active.Repaint();
                }

                if (Event.current.keyCode == KeyCode.End && Event.current.control)
                {
                    scrollBatchObjects.y = float.MaxValue;
                    EditorWindow.active.Repaint();
                }
            }
        }
    }
}