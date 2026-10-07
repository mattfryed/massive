// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>

using System.Collections.Generic;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor.WireframeTextureGenerator
{
    internal class EditorSettings : EditorSettingsBase
    {
        new public static class Enum
        {
            public enum SaveFileFormat { JPG, PNG, TGA }
            public enum SaveFileResolution { _16, _32, _64, _128, _256, _512, _1024, _2048, _4096, _8192 }
        }

        #region Solver
        public WireframeShaderEnum.Solver solverType = WireframeShaderEnum.Solver.Dynamic;
        public bool solverNormalizeEdges = true;
        public bool solverTryQuad = false;
        public WireframeShaderEnum.VertexAttribute solverReadFrom = WireframeShaderEnum.VertexAttribute.UV3;
        public float solverThickness = 0;
        public float solverSmoothness = 0;

        public Enum.SaveFileResolution saveFileResolution = Enum.SaveFileResolution._1024;
        public Enum.SaveFileFormat saveFileFormat = Enum.SaveFileFormat.PNG;
        public bool combineSubmesh = false;
        #endregion


        #region Tabs
        public bool drawTabSettings = true;
        public bool drawTabPreview = false;
        public bool drawTabSave = true;
        #endregion

        #region PreviewTexture
        public int previewRectSize = 200;
        static public Rect previewTextureDrawRect;
        static Texture2D[] previewTextures;
        static Texture2D previewTextureCombined;
        int previewTexturesIndex = 0;
        bool previewCombine;
        #endregion

        public EditorSettings()
            : base()
        {

        }
        public override string GetEditorPreferencesName()
        {
            return WireframeShaderAbout.name.RemoveWhiteSpace() + "TextureGenerator";
        }

        protected override void DrawCustomSettings(Rect collumnRect1, Rect collumnRect2, Rect collumnRect3, Rect collumnRect4)
        {
            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
            {
                drawTabSettings = EditorGUILayout.Foldout(drawTabSettings, "Solver", false, EditorResources.GUIStyleFoldoutBold);

                if (drawTabSettings)
                {
                    using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
                    {
                        Rect rect = EditorGUILayout.GetControlRect();
                        collumnRect1.yMin = collumnRect2.yMin = collumnRect3.yMin = collumnRect4.yMin = rect.yMin;
                        collumnRect1.height = collumnRect2.height = collumnRect3.height = collumnRect4.height = rect.height;


                        EditorGUI.BeginChangeCheck();
                        {
                            solverType = (WireframeShaderEnum.Solver)EditorGUI.EnumPopup(new Rect(collumnRect1.xMin, collumnRect1.yMin, 130, collumnRect1.height), solverType, EditorStyles.toolbarPopup);

                            solverNormalizeEdges = EditorGUIHelper.ToggleAsButton(collumnRect2, solverNormalizeEdges, "Normalized Edges");
                            solverTryQuad = EditorGUIHelper.ToggleAsButton(collumnRect3, solverTryQuad, "Try Quad");

                            if (solverType == WireframeShaderEnum.Solver.Prebaked)
                            {
                                using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(75))
                                {
                                    using (new EditorGUIHelper.GUIBackgroundColor(solverReadFrom == WireframeShaderEnum.VertexAttribute.UV0 ? Color.red : Color.white))
                                    {
                                        solverReadFrom = (WireframeShaderEnum.VertexAttribute)EditorGUI.EnumPopup(collumnRect4, new GUIContent("Read From", "Can be any vertex buffer containing baked wireframe data, except UV0.\nUV0 channel should contain mesh default UVs."), solverReadFrom);
                                    }
                                }
                            }


                            rect = EditorGUILayout.GetControlRect();
                            collumnRect1.yMin = collumnRect2.yMin = collumnRect3.yMin = collumnRect4.yMin = rect.yMin;
                            collumnRect1.height = collumnRect2.height = collumnRect3.height = collumnRect4.height = rect.height;

                            using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(75))
                            {
                                solverThickness = EditorGUI.Slider(collumnRect2, "Thickness", solverThickness, 0, 1);
                                solverSmoothness = EditorGUI.Slider(collumnRect3, "Smoothness", solverSmoothness, 0, 1);
                            }
                        }
                        if (EditorGUI.EndChangeCheck())
                        {
                            RenderPreviewTextures();
                        }
                    }
                }
            }



            GUILayout.Space(5);
            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
            {
                drawTabSave = EditorGUILayout.Foldout(drawTabSave, "Save", false, EditorResources.GUIStyleFoldoutBold);

                if (drawTabSave)
                {
                    using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
                    {

                        Rect rect = EditorGUILayout.GetControlRect();
                        collumnRect1.yMin = collumnRect2.yMin = collumnRect3.yMin = collumnRect4.yMin = rect.yMin;
                        collumnRect1.height = collumnRect2.height = collumnRect3.height = collumnRect4.height = rect.height;


                        EditorGUI.LabelField(collumnRect1, "Resolution");
                        using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(75))
                        {
                            float enumRectWidth = UnityEditor.EditorGUIUtility.fieldWidth;

                            int value = (int)saveFileResolution;
                            EditorGUI.BeginChangeCheck();
                            value = (int)GUI.HorizontalSlider(new Rect(collumnRect2.xMin, collumnRect2.yMin, collumnRect2.width - enumRectWidth - 5, collumnRect2.height), value, 0, 9);
                            if (EditorGUI.EndChangeCheck())
                            {
                                saveFileResolution = (Enum.SaveFileResolution)value;
                            }

                            saveFileResolution = (Enum.SaveFileResolution)EditorGUI.EnumPopup(new Rect(collumnRect2.xMax - enumRectWidth, collumnRect2.yMin, enumRectWidth, collumnRect2.height), saveFileResolution);


                            saveFileResolution = (Enum.SaveFileResolution)EditorGUI.EnumPopup(new Rect(collumnRect2.xMax - 50, collumnRect2.yMin, 50, collumnRect2.height), saveFileResolution);


                            saveFileFormat = (Enum.SaveFileFormat)EditorGUI.EnumPopup(collumnRect3, "Format", saveFileFormat);

                            combineSubmesh = EditorGUIHelper.ToggleAsButton(collumnRect4, combineSubmesh, "Combine Submeshes");
                        }

                        DrawSaveOptions(collumnRect1, collumnRect2, collumnRect3, collumnRect4, 75);
                    }
                }
            }

            GUILayout.Space(5);
            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
            {
                if (EditorWindow.active.listBatchObjects == null || EditorWindow.active.listBatchObjects.Count == 0)
                {
                    using (new EditorGUIHelper.GUIEnabled(false))
                    {
                        EditorGUILayout.Foldout(false, "Preview", false, EditorResources.GUIStyleFoldoutBold);
                    }
                }
                else
                {
                    drawTabPreview = EditorGUILayout.Foldout(drawTabPreview, "Preview", false, EditorResources.GUIStyleFoldoutBold);

                    if (drawTabPreview)
                    {
                        using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
                        {
                            DrawPreview(collumnRect1, collumnRect2, collumnRect3, collumnRect4);
                        }
                    }
                }
            }
        }
        public void DrawPreview(Rect collumnRect1, Rect collumnRect2, Rect collumnRect3, Rect collumnRect4)
        {
            List<BatchObject> listBatchObjects = EditorWindow.active.listBatchObjects;


            if (previewRectSize < 100) previewRectSize = 100;
            Rect rect = EditorGUILayout.GetControlRect(false, previewRectSize);



            if (listBatchObjects != null && listBatchObjects.Count > 0 &&
                EditorWindow.active.selectedBatchObjectIndex >= 0 && EditorWindow.active.selectedBatchObjectIndex < listBatchObjects.Count &&
                listBatchObjects[EditorWindow.active.selectedBatchObjectIndex].mesh != null &&
                previewTextures == null)
            {
                RenderPreviewTextures();
            }


            if (listBatchObjects == null || listBatchObjects.Count == 0 || EditorWindow.active.selectedBatchObjectIndex < 0 || EditorWindow.active.selectedBatchObjectIndex >= listBatchObjects.Count ||
                (EditorWindow.active.selectedBatchObjectIndex >= 0 && EditorWindow.active.selectedBatchObjectIndex < listBatchObjects.Count && listBatchObjects[EditorWindow.active.selectedBatchObjectIndex].mesh == null))
            {
                DestroyPreviewTextures();
            }


            bool isPreviewValid = (previewTextures != null && previewTextures.Length > 0);


            {
                collumnRect1.y = rect.y + 5;
                collumnRect2.y = rect.y + 5;
                collumnRect3.y = rect.y + 5;
                Rect rectSubmesh = new Rect(collumnRect3.xMin, rect.yMin + 5, collumnRect3.width - (isPreviewValid && previewTextures.Length > 1 ? 26 : 0), 18);



                using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(80))
                {
                    EditorGUI.LabelField(collumnRect2, "Preview Size");
                    previewRectSize = (int)GUI.HorizontalSlider(new Rect(collumnRect2.xMin + 80, collumnRect2.yMin, collumnRect2.width - 80, collumnRect2.height), previewRectSize, 100, 800);
                }

                if (isPreviewValid && previewTextures.Length > 1)
                {
                    using (new EditorGUIHelper.GUIEnabled(previewCombine ? false : true))
                    {
                        using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(65))
                        {
                            using (new EditorGUIHelper.EditorGUIUtilityFieldWidth(25))
                            {
                                previewTexturesIndex = EditorGUI.IntSlider(rectSubmesh, "  Submesh", previewTexturesIndex, 0, previewTextures.Length - 1);
                            }
                        }
                    }

                    previewCombine = GUI.Toggle(new Rect(rectSubmesh.xMax + 2, rectSubmesh.yMin, 25, rectSubmesh.height), previewCombine, new GUIContent("∑", "Combine Submeshes"), "Button");
                }
                else
                {
                    using (new EditorGUIHelper.GUIEnabled(false))
                    {
                        using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(65))
                        {
                            EditorGUI.IntSlider(rectSubmesh, "  Submesh", 0, 0, 1);
                        }
                    }
                }


                previewTextureDrawRect = new Rect(collumnRect1.xMin, rect.yMin + 30, collumnRect4.xMax - collumnRect1.xMin, rect.height - 35);

                if (previewTextureDrawRect.width > previewTextureDrawRect.height)
                    previewTextureDrawRect.width = previewTextureDrawRect.height;
                else if (previewTextureDrawRect.height > previewTextureDrawRect.width)
                    previewTextureDrawRect.height = previewTextureDrawRect.width;

                previewTextureDrawRect.x += (collumnRect4.xMax - collumnRect1.xMin) / 2 - previewTextureDrawRect.width / 2;

                if (isPreviewValid)
                {
                    //Draw preview texture
                    if (previewTextures.Length > 1 && previewCombine)
                    {
                        EditorGUI.DrawPreviewTexture(previewTextureDrawRect, previewTextureCombined);
                    }
                    else
                    {
                        previewTexturesIndex = Mathf.Clamp(previewTexturesIndex, 0, previewTextures.Length - 1);

                        if (previewTextures[previewTexturesIndex] == null)
                            EditorGUI.DrawPreviewTexture(previewTextureDrawRect, Texture2D.blackTexture);
                        else
                            EditorGUI.DrawPreviewTexture(previewTextureDrawRect, previewTextures[previewTexturesIndex]);
                    }
                }
                else
                {
                    EditorGUI.DrawTextureTransparent(previewTextureDrawRect, Texture2D.blackTexture);

                    Rect warningRect = new Rect(collumnRect1.xMin, previewTextureDrawRect.yMin + 20, 300, 40);

                    EditorGUI.DrawRect(warningRect, UnityEditor.EditorGUIUtility.isProSkin ? Color.black : Color.grey);

                    if (listBatchObjects == null || listBatchObjects.Count == 0 || EditorWindow.active.selectedBatchObjectIndex < 0 || EditorWindow.active.selectedBatchObjectIndex >= listBatchObjects.Count)
                    {
                        EditorGUI.HelpBox(warningRect, "Select mesh from the list below to see its wireframe.", MessageType.Warning);
                    }
                    else if (previewTextures == null)
                    {
                        EditorGUI.HelpBox(warningRect, "Selected mesh has no UV0 or baked wireframe.", MessageType.Error);
                    }
                }
            }
        }
        public override void Reset()
        {
            base.Reset();

            solverType = WireframeShaderEnum.Solver.Dynamic;
            solverNormalizeEdges = true;
            solverTryQuad = false;
            solverReadFrom = WireframeShaderEnum.VertexAttribute.UV3;
            solverThickness = 0f;
            solverSmoothness = 0;


            saveFileFormat = Enum.SaveFileFormat.PNG;
            saveFileResolution = Enum.SaveFileResolution._1024;
            combineSubmesh = false;
        }
        override public bool IsReady()
        {
            //Cannot read wireframe from UV0. It should contain default UV for mesh mapping.
            if (solverReadFrom == WireframeShaderEnum.VertexAttribute.UV0)
                return false;


            return base.IsReady();
        }

        public void RenderPreviewTextures()
        {
            DestroyPreviewTextures();

            List<BatchObject> listBatchObjects = EditorWindow.active.listBatchObjects;


            if (listBatchObjects != null && listBatchObjects.Count > 0 &&
               EditorWindow.active.selectedBatchObjectIndex >= 0 && EditorWindow.active.selectedBatchObjectIndex < listBatchObjects.Count &&
               listBatchObjects[EditorWindow.active.selectedBatchObjectIndex].invalidMeshData == false &&
               CanRenderPreviewTexture(listBatchObjects[EditorWindow.active.selectedBatchObjectIndex].mesh))
            {
                previewTextures = new Texture2D[listBatchObjects[EditorWindow.active.selectedBatchObjectIndex].mesh.subMeshCount];

                for (int i = 0; i < listBatchObjects[EditorWindow.active.selectedBatchObjectIndex].mesh.subMeshCount; i++)
                {
                    previewTextures[i] = listBatchObjects[EditorWindow.active.selectedBatchObjectIndex].mesh.WireframeShader().GenerateWireframeTexture(1024, solverType, solverReadFrom, i, solverNormalizeEdges, solverTryQuad, solverThickness, solverSmoothness);

                    if(previewTextures[i] != null)
                        previewTextures[i].filterMode = FilterMode.Bilinear;
                }

                if (previewTextures.Length > 1)
                {
                    previewTextureCombined = listBatchObjects[EditorWindow.active.selectedBatchObjectIndex].mesh.WireframeShader().GenerateWireframeTexture(1024, solverType, solverReadFrom, -1, solverNormalizeEdges, solverTryQuad, solverThickness, solverSmoothness);
                    
                    if(previewTextureCombined != null)
                        previewTextureCombined.filterMode = FilterMode.Bilinear;
                }
            }
        }
        void DestroyPreviewTextures()
        {
            if (previewTextures != null)
            {
                for (int i = 0; i < previewTextures.Length; i++)
                    WireframeShaderUtilities.DestroyUnityObject(previewTextures[i]);

                previewTextures = null;
            }

            WireframeShaderUtilities.DestroyUnityObject(previewTextureCombined);
        }
        bool CanRenderPreviewTexture(Mesh mesh)
        {
            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0) == false)
                return false;

            EditorSettings editorSettings = EditorWindow.active.editorSettings;
            if (editorSettings.solverType == WireframeShaderEnum.Solver.Prebaked)
            {
                if (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV0)
                    return false;

                if ((editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV0 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV1 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV2 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV3 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord3) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV4 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord4) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV5 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord5) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV6 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord6) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.UV7 && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord7) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.Normal && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal) == false) ||
                    (editorSettings.solverReadFrom == WireframeShaderEnum.VertexAttribute.Tangent && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent) == false))

                {
                    return false;
                }
            }

            return true;
        }
    }
}