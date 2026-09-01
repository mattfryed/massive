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
    internal class BatchObjectsDrawer
    {
        enum SortBy { None, ObjectData, MeshCount, AssetFormat, SubmeshCount, VertexAndTriangleCountOfOriginalMesh, VertexCountOfWireframeMesh, IndexFormat }
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
            Rect rectMeshCount = new Rect();
            Rect rectAssetFormat = new Rect();      bool needRectAssetFormat = false;
            Rect rectSubmeshCount = new Rect();
            Rect rectVertexAndTriangleCountOfOriginalMesh = new Rect();
            Rect rectVertexCountOfWireframeMesh = new Rect();
            Rect rectIndexFormat = new Rect();
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
                            SplitControlRect(currentRowRect, out rectObjectData, out rectMeshCount,
                                             out needRectAssetFormat, out rectAssetFormat,
                                             out rectSubmeshCount,
                                             out rectVertexAndTriangleCountOfOriginalMesh,
                                             out rectVertexCountOfWireframeMesh,
                                             out rectIndexFormat,
                                             out rectRefresh);


                        if (currentBatchObject == null || currentBatchObject.gameObject == null || currentBatchObject.meshInfo == null || currentBatchObject.meshInfo.Count == 0)
                        {
                            using (new EditorGUIHelper.GUIBackgroundColor(Color.yellow))
                            {
                                EditorGUI.ObjectField(new Rect(rectObjectData.xMin + 16, currentRowRect.yMin, rectObjectData.width - 16, currentRowRect.height), null, typeof(GameObject), false);
                            }

                            needRepaint = true;
                        }
                        else
                        {
                            ScrollViewItemVisibility isRowVisible = IsRectVisibleInsideScrollView(toolbarRect, currentRowRect, scrollBatchObjects);
                            if (isRowVisible == ScrollViewItemVisibility.Visible)
                            {
                                visibleBatchObjectsCount += 1;


                                //Foldout
                                EditorGUI.BeginChangeCheck();
                                currentBatchObject.expanded = EditorGUI.Foldout(new Rect(rectObjectData.xMin + 18, currentRowRect.yMin, 18, currentRowRect.height), currentBatchObject.expanded, string.Empty);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    if (Event.current.alt)
                                    {
                                        listBatchObjects.ForEach(c => c.expanded = currentBatchObject.expanded);
                                    }
                                }

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
                                }

                                if (EditorWindow.active.selectedBatchObjectIndex == i)
                                    EditorGUI.DrawRect(new Rect(rectObjectData.xMax + 5, currentRowRect.yMin, rectRefresh.xMin - rectObjectData.xMax - 6, currentRowRect.height), Color.green * 0.2f);


                                //Visibility
                                if (currentBatchObject.gameObject.scene != null && currentBatchObject.gameObject.scene.isLoaded)
                                {
                                    bool visibility = currentBatchObject.gameObject.activeSelf;
                                    if (GUI.Button(new Rect(rectObjectData.xMin, currentRowRect.yMin + 2, 14, 14), visibility ? EditorResources.GUIContentVisibilityOn : EditorResources.GUIContentVisibilityOff, GUIStyle.none))
                                    {
                                        visibility = !visibility;

                                        currentBatchObject.gameObject.SetActive(visibility);

                                        if (Event.current.alt)
                                            listBatchObjects.ForEach(c => c.gameObject.SetActive(visibility));
                                    }
                                }
                                else
                                {
                                    using (new EditorGUIHelper.GUIEnabled(false))
                                    {
                                        GUI.Box(new Rect(rectObjectData.xMin, currentRowRect.yMin + 2, 14, 14), EditorResources.GUIContentVisibilityOff, GUIStyle.none);
                                    }
                                }

                                //GameObject or ProjectObject
                                Color backgroundColor = Color.white;
                                if (!(currentBatchObject.gameObject.scene != null && currentBatchObject.gameObject.scene.isLoaded))
                                    backgroundColor = EditorResources.projectRelatedPathColor;
                                if (EditorWindow.active.problematicBatchObject != null && EditorWindow.active.problematicBatchObject.gameObject == currentBatchObject.gameObject)
                                    backgroundColor = Color.red;
                                if (currentBatchObject.hasMeshProblems)
                                    backgroundColor = Color.red;


                                using (new EditorGUIHelper.GUIBackgroundColor(backgroundColor))
                                {
                                    EditorGUI.ObjectField(new Rect(rectObjectData.xMin + 34, currentRowRect.yMin, rectObjectData.width - 34, currentRowRect.height), currentBatchObject.gameObject, typeof(GameObject), false);
                                }


                                //Mesh count
                                EditorGUI.LabelField(new Rect(rectMeshCount.xMin, currentRowRect.yMin, rectMeshCount.width, currentRowRect.height), currentBatchObject.meshInfo.Count.ToString("N0"), EditorResources.GUIStyleCenteredGreyMiniLabel);


                                //.asset format
                                if (needRectAssetFormat)
                                {
                                    Rect assetFormatDrawRect = new Rect(rectAssetFormat.xMin + rectAssetFormat.width / 2 - 6, currentRowRect.yMin + 2, 12, 12);
                                    switch (currentBatchObject.isMeshAssetFormat)
                                    {
                                        case BatchObject.OptionsState.Yes: GUI.DrawTexture(assetFormatDrawRect, EditorResources.IconYes); break;
                                        case BatchObject.OptionsState.No: GUI.DrawTexture(assetFormatDrawRect, EditorResources.IconNo); break;
                                        case BatchObject.OptionsState.Mixed: EditorGUI.LabelField(assetFormatDrawRect, "-", EditorResources.GUIStyleCenteredGreyMiniLabel); break;
                                    }
                                }


                                //Submesh
                                EditorGUI.LabelField(new Rect(rectSubmeshCount.xMin, currentRowRect.yMin, rectSubmeshCount.width, currentRowRect.height), currentBatchObject.submeshCount, EditorResources.GUIStyleCenteredGreyMiniLabel);


                                //Vertex Count
                                EditorGUI.LabelField(new Rect(rectVertexAndTriangleCountOfOriginalMesh.xMin, currentRowRect.yMin, rectVertexAndTriangleCountOfOriginalMesh.width, currentRowRect.height), currentBatchObject.vertexAndTriangleCountOfOriginalMesh, EditorResources.GUIStyleCenteredGreyMiniLabel);
                                EditorGUI.LabelField(new Rect(rectVertexCountOfWireframeMesh.xMin, currentRowRect.yMin, rectVertexCountOfWireframeMesh.width, currentRowRect.height), currentBatchObject.vertexCountOfWireframeMesh, EditorResources.GUIStyleCenteredGreyMiniLabel);


                                //Index Format
                                EditorGUI.DrawRect(new Rect(rectIndexFormat.xMin, currentRowRect.yMin + 3, rectIndexFormat.width, currentRowRect.height - 5), Color.gray * 0.1f);
                                EditorGUI.LabelField(new Rect(rectIndexFormat.xMin, currentRowRect.yMin + 3, rectIndexFormat.width, currentRowRect.height - 5), currentBatchObject.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 ? "32 Bits" : "16 Bits",
                                                                                                                                                                currentBatchObject.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 ? EditorResources.GUIStyleMeshIndex : EditorResources.GUIStyleCenteredGreyMiniLabel);


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
                                if (!(i == listBatchObjects.Count - 1 && currentBatchObject.expanded == false))
                                    EditorGUI.DrawRect(new Rect(rectObjectData.xMax + 5, currentRowRect.yMax, rectRefresh.xMax - rectObjectData.xMax - 5, 1), Color.gray);
                            }
                            else if (isRowVisible == ScrollViewItemVisibility.BelowDrawArea)
                                break;


                            if (currentBatchObject.expanded)
                            {
                                for (int m = 0; m < currentBatchObject.meshInfo.Count; m++)
                                {
                                    BatchObjectMeshInfo meshInfo = currentBatchObject.meshInfo[m];

                                    currentRowRect = EditorGUILayout.GetControlRect();

                                    isRowVisible = IsRectVisibleInsideScrollView(toolbarRect, currentRowRect, scrollBatchObjects);
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
                                        }

                                        if (EditorWindow.active.selectedBatchObjectIndex == i)
                                            EditorGUI.DrawRect(new Rect(rectObjectData.xMax + 5, currentRowRect.yMin - 1, rectRefresh.xMin - rectObjectData.xMax - 6, currentRowRect.height + 1), EditorResources.groupHighLightColor);


                                        using (new EditorGUIHelper.GUIEnabled(false))
                                        {
                                            //MeshFilter / SkinnedMeshRenderer
                                            if (meshInfo.meshFilter != null)
                                                EditorGUI.ObjectField(new Rect(rectObjectData.xMin + 34, currentRowRect.yMin, (rectObjectData.width - 46) * 0.5f, currentRowRect.height), meshInfo.meshFilter, typeof(MeshFilter), false);
                                            else if (meshInfo.skinnedMeshRenderer != null)
                                                EditorGUI.ObjectField(new Rect(rectObjectData.xMin + 34, currentRowRect.yMin, (rectObjectData.width - 46) * 0.5f, currentRowRect.height), meshInfo.skinnedMeshRenderer, typeof(SkinnedMeshRenderer), false);
                                            else
                                            {
                                                needRepaint = true;

                                                break;
                                            }


                                            //Mesh
                                            using (new EditorGUIHelper.GUIBackgroundColor(meshInfo.invalidMeshData ? Color.red : Color.white))
                                            {
                                                EditorGUI.ObjectField(new Rect(rectObjectData.xMin + 28 + (rectObjectData.width - 24) * 0.5f, currentRowRect.yMin, (rectObjectData.width - 32) * 0.5f, currentRowRect.height), meshInfo.mesh, typeof(Mesh), false);
                                            }


                                            //.asset format
                                            if (needRectAssetFormat)
                                                GUI.DrawTexture(new Rect(rectAssetFormat.xMin + rectAssetFormat.width / 2 - 6, currentRowRect.yMin + 2, 12, 12), meshInfo.isMeshAssetFormat ? EditorResources.IconYes : EditorResources.IconNo);


                                            //Submesh
                                            if (meshInfo.mesh == null)
                                                EditorGUI.LabelField(new Rect(rectSubmeshCount.xMin, currentRowRect.yMin, rectSubmeshCount.width, currentRowRect.height), "0", EditorResources.GUIStyleCenteredGreyMiniLabel);
                                            else
                                                EditorGUI.LabelField(new Rect(rectSubmeshCount.xMin, currentRowRect.yMin, rectSubmeshCount.width, currentRowRect.height), meshInfo.mesh.subMeshCount.ToString(), EditorResources.GUIStyleCenteredGreyMiniLabel);
                                                                                                                                 

                                            //Vertex Count
                                            EditorGUI.LabelField(new Rect(rectVertexAndTriangleCountOfOriginalMesh.xMin, currentRowRect.yMin, rectVertexAndTriangleCountOfOriginalMesh.width, currentRowRect.height), meshInfo.vertexAndTriangleCountOfOriginalMesh, EditorResources.GUIStyleCenteredGreyMiniLabel);
                                            EditorGUI.LabelField(new Rect(rectVertexCountOfWireframeMesh.xMin, currentRowRect.yMin, rectVertexCountOfWireframeMesh.width, currentRowRect.height), meshInfo.vertexCountOfWireframeMesh, EditorResources.GUIStyleCenteredGreyMiniLabel);


                                            //Index Format 
                                            EditorGUI.DrawRect(new Rect(rectIndexFormat.xMin, currentRowRect.yMin + 3, rectIndexFormat.width, currentRowRect.height - 5), Color.gray * 0.1f);
                                            if (meshInfo.mesh == null)
                                                EditorGUI.LabelField(new Rect(rectIndexFormat.xMin, currentRowRect.yMin + 3, rectIndexFormat.width, currentRowRect.height - 5), "-", EditorResources.GUIStyleCenteredGreyMiniLabel);
                                            else
                                                EditorGUI.LabelField(new Rect(rectIndexFormat.xMin, currentRowRect.yMin + 3, rectIndexFormat.width, currentRowRect.height - 5), meshInfo.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 ? "32 Bits" : "16 Bits",
                                                                                                                                                                                meshInfo.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 ? EditorResources.GUIStyleMeshIndex : EditorResources.GUIStyleCenteredGreyMiniLabel);
                                        }


                                        //Draw line
                                        if (!(i == listBatchObjects.Count - 1 && m == currentBatchObject.meshInfo.Count - 1))
                                            EditorGUI.DrawRect(new Rect(rectObjectData.xMax + 5, currentRowRect.yMax, rectRefresh.xMin - rectObjectData.xMax - 6, 1), Color.gray * (UnityEditor.EditorGUIUtility.isProSkin ? 0.8f : 0.2f));
                                    }
                                    else if (isRowVisible == ScrollViewItemVisibility.BelowDrawArea)
                                        break;
                                }
                            }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            if (needRepaint)
            {
                listBatchObjects = listBatchObjects.Where(c => c.gameObject != null).ToList();
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
                DrawSortByButton(new Rect(rectMeshCount.xMin + offset, toolbarRect.yMin, rectMeshCount.width, singleLineHeight), "Mesh", SortBy.MeshCount);

                if (needRectAssetFormat)
                    DrawSortByButton(new Rect(rectAssetFormat.xMin + offset, toolbarRect.yMin, rectAssetFormat.width, singleLineHeight), ".asset", SortBy.AssetFormat);

                DrawSortByButton(new Rect(rectSubmeshCount.xMin + offset, toolbarRect.yMin, rectSubmeshCount.width, singleLineHeight), "Submesh", SortBy.SubmeshCount);

                DrawSortByButton(new Rect(rectVertexAndTriangleCountOfOriginalMesh.xMin + offset, toolbarRect.yMin, rectVertexAndTriangleCountOfOriginalMesh.width, singleLineHeight), "Source", SortBy.VertexAndTriangleCountOfOriginalMesh);
                DrawSortByButton(new Rect(rectVertexCountOfWireframeMesh.xMin + offset, toolbarRect.yMin, rectVertexCountOfWireframeMesh.width, singleLineHeight), "Wireframe", SortBy.VertexCountOfWireframeMesh);
                DrawSortByButton(new Rect(rectIndexFormat.xMin + offset, toolbarRect.yMin, rectIndexFormat.width, singleLineHeight), "Index", SortBy.IndexFormat);


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
            for (int i = 0; i < EditorWindow.active.listBatchObjects.Count; i++)
            {
                if (EditorWindow.active.listBatchObjects[i].meshInfo != null && EditorWindow.active.listBatchObjects[i].meshInfo.Count > 0 && EditorWindow.active.listBatchObjects[i].expanded == true)
                {
                    renderObjectsCount += EditorWindow.active.listBatchObjects[i].meshInfo.Count;
                }
            }

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
                    if (listBatchObjects[i] != null && listBatchObjects[i].gameObject != null)
                    {
                        batchObjectsData += string.Format("{0},{1}", listBatchObjects[i].expanded ? 1 : 0, listBatchObjects[i].gameObject.GetInstanceID()) + ";";
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


                    string[] values = editorSettings[i].Split(',');

                    bool mainExpand = false;
                    if (int.TryParse(values[0], out iValue))
                        mainExpand = iValue == 1;


                    if (int.TryParse(values[1], out iValue))
                    {
                        UnityEngine.Object uObject = EditorUtilities.InstanceIDToObject(iValue);
                        if (uObject != null)
                        {
                            AddBatchObjectToArray(uObject as GameObject, mainExpand);
                        }
                    }
                }

                SortBatchObjects();
            }
        }
        static string GetEditorPreferencesPath()
        {
            return WireframeShaderAbout.name.RemoveWhiteSpace() + "WireframeMeshGenerator_ObjectsID_" + Application.dataPath.GetHashCode();
        }


        static void SplitControlRect(Rect controlRect, out Rect rectObjectData, out Rect rectMeshCount, out bool needRectAssetFormat, out Rect rectAssetFormat, out Rect rectSubmeshCount, out Rect rectVertexAndTriangleCountOfOriginalMesh, out Rect rectVertexCountOfWireframeMesh, out Rect rectIndexFormat, out Rect rectRefresh)
        {
            EditorSettings editorSettings = EditorWindow.active.editorSettings;

            float controlWidth = controlRect.width - 20;    //20 is for refresh button

            rectRefresh = new Rect(controlRect);
            rectRefresh.xMin = controlRect.xMax - 20;
            rectRefresh.width = 20;


            float width;
            float xMin = controlWidth;

            width = Mathf.Min(controlWidth * 0.06f, 58);
            xMin -= width;
            {
                rectIndexFormat = new Rect(controlRect);
                rectIndexFormat.xMin = xMin;
                rectIndexFormat.width = width;
            }

            width = Mathf.Min(controlWidth * 0.15f, 140);
            xMin -= width;
            {
                rectVertexCountOfWireframeMesh = new Rect(controlRect);
                rectVertexCountOfWireframeMesh.xMin = xMin;
                rectVertexCountOfWireframeMesh.width = width;
            }

            width = Mathf.Min(controlWidth * 0.15f, 140);
            xMin -= width;
            {
                rectVertexAndTriangleCountOfOriginalMesh = new Rect(controlRect);
                rectVertexAndTriangleCountOfOriginalMesh.xMin = xMin;
                rectVertexAndTriangleCountOfOriginalMesh.width = width;
            }

            width = Mathf.Min(controlWidth * 0.09f, 100);
            xMin -= width;
            {
                rectSubmeshCount = new Rect(controlRect);
                rectSubmeshCount.xMin = xMin;
                rectSubmeshCount.width = width;
            }

            rectAssetFormat = new Rect(controlRect);
            needRectAssetFormat = (editorSettings.generateAssetSaveType == EditorSettings.Enum.AssetSaveType.OverwriteOriginalMesh);
            if (needRectAssetFormat)
            {
                width = Mathf.Min(controlWidth * 0.06f, 62);
                xMin -= width;
                {
                    rectAssetFormat.xMin = xMin;
                    rectAssetFormat.width = width;
                }
            }

            width = controlWidth * 0.07f;
            xMin -= width;
            {
                rectMeshCount = new Rect(controlRect);
                rectMeshCount.xMin = xMin;
                rectMeshCount.width = width;
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
                case SortBy.ObjectData: SortBatchObjects(r => r.gameObjectSortName, null, c => c.meshSortName, null); break;
                case SortBy.MeshCount: SortBatchObjects(r => r.meshInfo.Count, r => r.gameObjectSortName, c => c.meshSortName, null); break;
                case SortBy.AssetFormat: SortBatchObjects(r => r.isMeshAssetFormat, r => r.gameObjectSortName, c => c.isMeshAssetFormat, c => c.meshSortName); break;
                case SortBy.SubmeshCount: SortBatchObjects(r => r.submeshCount, r => r.gameObjectSortName, c => c.mesh.subMeshCount.ToString(), c => c.meshSortName); break;
                case SortBy.VertexAndTriangleCountOfOriginalMesh:
                    SortBatchObjects(r => EditorUtilities.PadNumbers(r.vertexAndTriangleCountOfOriginalMesh.Replace(N0, string.Empty)),
                                     r => r.gameObjectSortName,
                                     r => EditorUtilities.PadNumbers(r.vertexAndTriangleCountOfOriginalMesh.Replace(N0, string.Empty)),
                                     c => c.meshSortName); break;

                case SortBy.VertexCountOfWireframeMesh:
                    SortBatchObjects(r => EditorUtilities.PadNumbers(r.vertexCountOfWireframeMesh.Replace(N0, string.Empty)),
                                                                     r => r.gameObjectSortName,
                                                                     c => c.vertexCountOfWireframeMesh,
                                                                     c => c.meshSortName); break;

                case SortBy.IndexFormat:
                    SortBatchObjects(r => r.indexFormat, r => r.gameObjectSortName, c => c.mesh.indexFormat, c => c.meshSortName); break;

                default:
                    break;
            }
        }
        static void SortBatchObjects(Func<BatchObject, System.Object> orderRoot, Func<BatchObject, System.Object> orderRootThen, Func<BatchObjectMeshInfo, System.Object> orderChild, Func<BatchObjectMeshInfo, System.Object> orderChildThen)
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

                if (orderChild != null)
                {
                    for (int i = 0; i < EditorWindow.active.listBatchObjects.Count; i++)
                    {
                        if (orderChildThen != null)
                            EditorWindow.active.listBatchObjects[i].meshInfo = EditorWindow.active.listBatchObjects[i].meshInfo.OrderBy(orderChild).ThenBy(orderChildThen).ToList();
                        else
                            EditorWindow.active.listBatchObjects[i].meshInfo = EditorWindow.active.listBatchObjects[i].meshInfo.OrderBy(orderChild).ToList();
                    }
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

                if (orderChild != null)
                {
                    for (int i = 0; i < EditorWindow.active.listBatchObjects.Count; i++)
                    {
                        if (orderChildThen != null)
                            EditorWindow.active.listBatchObjects[i].meshInfo = EditorWindow.active.listBatchObjects[i].meshInfo.OrderByDescending(orderChild).ThenBy(orderChildThen).ToList();
                        else
                            EditorWindow.active.listBatchObjects[i].meshInfo = EditorWindow.active.listBatchObjects[i].meshInfo.OrderByDescending(orderChild).ToList();
                    }
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

                if (gameObject != null)
                {
                    AddBatchObjectToArray(gameObject, false);
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
        internal static BatchObject AddBatchObjectToArray(GameObject gameObject, bool expand)
        {
            if (EditorWindow.active == null)
                return null;


            if (EditorWindow.active.listBatchObjects == null)
                EditorWindow.active.listBatchObjects = new List<BatchObject>();

            if (EditorWindow.active.editorSettings == null)
                EditorWindow.active.editorSettings = new EditorSettings();



            if (gameObject != null && EditorWindow.active.listBatchObjects.Any(x => x.gameObject.GetInstanceID() == gameObject.GetInstanceID()) == false)
            {
                string assetName = EditorWindow.active.editorSettings.GetSaveAssetName(gameObject, true);
                string assetSaveDirectory = EditorWindow.active.editorSettings.GetAssetSaveDirectory(gameObject, true, true);
                string savePath = Path.Combine(assetSaveDirectory, assetName);


                if (string.IsNullOrEmpty(savePath) == true ||
                    EditorWindow.active.listBatchObjects.Any(x => x.savePath == savePath) == false)
                {
                    BatchObject batchObject = new BatchObject(gameObject, expand);

                    if (batchObject.meshInfo.Count > 0)
                    {
                        EditorWindow.active.listBatchObjects.Add(batchObject);

                        SceneView.RepaintAll();
                        return batchObject;
                    }
                }
            }

            return null;
        }
        internal static void AddBatchObjectToArray(string folderPath, bool sort)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab t:Model", string.IsNullOrEmpty(folderPath) ? null : new string[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                UnityEditor.EditorUtility.DisplayProgressBar("Hold On", path, (float)i / guids.Length);

                if (string.IsNullOrEmpty(path) == false && EditorUtilities.IsPathProjectRelative(path))
                    AddBatchObjectToArray((GameObject)AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)), false);
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