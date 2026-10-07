// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>

using System.Linq;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor.WireframeMeshGenerator
{
    internal class EditorSettings : EditorSettingsBase
    {
        new public static class Enum
        {
            public enum MeshCombineType { Nothing, Submeshes, OneMeshWithSubmeshes, Everything }
            public enum MeshFormat { UnityAsset, UnityMesh }
            public enum AssetSaveType { Prefab, MeshOnly, OverwriteOriginalMesh }
            public enum MaterialType { UseOriginal, CreateNew, CreateDuplicate }
        }


        static public string defaultShaderName
        {
            get
            {
                switch (WireframeShaderUtilities.GetCurrentRenderPipeline())
                {
                    case AmazingAssets.WireframeShader.WireframeShaderEnum.RenderPipeline.Universal: return "Amazing Assets/Wireframe Shader/Standard";
                    case AmazingAssets.WireframeShader.WireframeShaderEnum.RenderPipeline.HighDefinition: return "Amazing Assets/Wireframe Shader/Standard";

                    default: return "Amazing Assets/Wireframe Shader/Physically Based";
                }
            }
        }



        #region Solver
        public bool solverNormalizeEdges = true;
        public bool solverTryQuad = false;
        public WireframeShaderEnum.VertexAttribute solverStoreInsideVertexAttribute = WireframeShaderEnum.VertexAttribute.UV3;

        public Enum.AssetSaveType generateAssetSaveType = Enum.AssetSaveType.Prefab;
        public Enum.MeshCombineType generateMeshCombineType = Enum.MeshCombineType.Nothing;        
        public bool generateReplaceMeshColliders = false;
        public bool generateLightmapUVs = false;
        public Enum.MaterialType generateMaterialType = Enum.MaterialType.CreateNew;
        public Shader generateDefaultShader = Shader.Find(defaultShaderName);

        public Enum.MeshFormat saveMeshFormat = Enum.MeshFormat.UnityAsset;
        public ModelImporterMeshCompression saveMeshCompression = ModelImporterMeshCompression.Low;
        public bool saveUseDefaultFlagsForVertexAttribute = true;
        public VertexAttributePopup.AttributeFlags saveVertexAttributeFlags = (VertexAttributePopup.AttributeFlags)~0;

        public bool saveUseStaticEditorFlags = false;
        public StaticEditorFlags saveStaticEditorFlags = 0;
        public bool saveUseStaticEditorFlagsForHierarchy = true;        
        public bool saveUseTag = false;
        public string saveTag = "Untagged";
        public bool saveUseTagForHierarchy = true;        
        public bool saveUseLayer = false;
        public int saveLayer;
        public bool saveUseLayerForHierarchy = true;                
        #endregion


        #region Tabs
        public bool drawTabSolver = true;
        public bool drawTabGenerate = true;
        public bool drawTabSave = true;
        #endregion


        public EditorSettings()
            : base()
        {

        }
        public override string GetEditorPreferencesName()
        {
            return WireframeShaderAbout.name.RemoveWhiteSpace() + "MeshGenerator";
        }

        protected override void DrawCustomSettings(Rect collumnRect1, Rect collumnRect2, Rect collumnRect3, Rect collumnRect4)
        {
            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
            {
                drawTabSolver = EditorGUILayout.Foldout(drawTabSolver, "Solver", false, EditorResources.GUIStyleFoldoutBold);

                if (drawTabSolver)
                {
                    using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
                    {
                        Rect rect = EditorGUILayout.GetControlRect();
                        collumnRect1.yMin = collumnRect2.yMin = collumnRect3.yMin = collumnRect4.yMin = rect.yMin;
                        collumnRect1.height = collumnRect2.height = collumnRect3.height = collumnRect4.height = rect.height;

                        solverNormalizeEdges = EditorGUIHelper.ToggleAsButton(collumnRect2, solverNormalizeEdges, "Normalize Edges");
                        solverTryQuad = EditorGUIHelper.ToggleAsButton(collumnRect3, solverTryQuad, "Try Quad");

                        using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(75))
                        {
                            using (new EditorGUIHelper.GUIBackgroundColor(solverStoreInsideVertexAttribute == WireframeShaderEnum.VertexAttribute.UV3 ? Color.white : Color.yellow))
                            {
                                using (new EditorGUIHelper.GUIBackgroundColor((solverStoreInsideVertexAttribute == WireframeShaderEnum.VertexAttribute.UV1 && GeneratingLightmapUVs()) ? Color.red : Color.white))
                                {
                                    solverStoreInsideVertexAttribute = (WireframeShaderEnum.VertexAttribute)EditorGUI.EnumPopup(collumnRect4, new GUIContent("Store Inside", "By default wireframe data is saved inside UV3 and all package included shaders and tools render wireframe based on the data saved inside UV3."), solverStoreInsideVertexAttribute);
                                }
                            }
                        }
                    }
                }
            }


            DrawAdditionalSettings(collumnRect1, collumnRect2, collumnRect3, collumnRect4);
        }
        void DrawAdditionalSettings(Rect collumnRect1, Rect collumnRect2, Rect collumnRect3, Rect collumnRect4)
        {
            GUILayout.Space(5);
            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
            {
                drawTabGenerate = EditorGUILayout.Foldout(drawTabGenerate, "Generate", false, EditorResources.GUIStyleFoldoutBold);

                if (drawTabGenerate)
                {
                    using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
                    {
                        Rect rect = AddRow(ref collumnRect1, ref collumnRect2, ref collumnRect3, ref collumnRect4);


                        using (new EditorGUIHelper.GUIBackgroundColor(generateAssetSaveType == Enum.AssetSaveType.OverwriteOriginalMesh ? Color.yellow : Color.white))
                        {
                            generateAssetSaveType = (Enum.AssetSaveType)EditorGUI.EnumPopup(new Rect(collumnRect1.xMin, collumnRect1.yMin, 120, collumnRect1.height), generateAssetSaveType, EditorStyles.toolbarPopup);
                        }

                        if (generateAssetSaveType == Enum.AssetSaveType.OverwriteOriginalMesh)
                        {
                            GUI.DrawTexture(new Rect(collumnRect2.xMin, collumnRect2.yMin, 20, 20), EditorResources.IconError);

                            EditorGUI.LabelField(new Rect(collumnRect2.xMin + 20, collumnRect2.yMin, (collumnRect4.xMax - collumnRect2.xMin), collumnRect2.height), "Only mesh files in .asset format will be overwritten and it cannot be Undo.");
                        }

                        if (generateAssetSaveType == Enum.AssetSaveType.Prefab || generateAssetSaveType == Enum.AssetSaveType.MeshOnly)
                        {
                            using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(75))
                            {
                                generateMeshCombineType = (Enum.MeshCombineType)EditorGUI.EnumPopup(collumnRect2, "Combine", generateMeshCombineType);
                            }

                            if (generateMeshCombineType == Enum.MeshCombineType.OneMeshWithSubmeshes || generateMeshCombineType == Enum.MeshCombineType.Everything)
                                generateLightmapUVs = EditorGUIHelper.ToggleAsButton(collumnRect3, generateLightmapUVs, "Generate Lightmap UVs");
                        }

                        if (generateAssetSaveType == Enum.AssetSaveType.Prefab)
                        {
                            if (generateMeshCombineType == Enum.MeshCombineType.OneMeshWithSubmeshes || generateMeshCombineType == Enum.MeshCombineType.Everything)
                                generateReplaceMeshColliders = EditorGUIHelper.ToggleAsButton(collumnRect4, generateReplaceMeshColliders, "Add Mesh Collider");
                            else
                                generateReplaceMeshColliders = EditorGUIHelper.ToggleAsButton(collumnRect4, generateReplaceMeshColliders, "Replace Mesh Colliders");


                            rect = AddRow(ref collumnRect1, ref collumnRect2, ref collumnRect3, ref collumnRect4);

                            using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(75))
                            {
                                generateMaterialType = (Enum.MaterialType)EditorGUI.EnumPopup(collumnRect2, "Material", generateMaterialType);
                            }


                            if (generateMaterialType == Enum.MaterialType.CreateNew || generateMeshCombineType == Enum.MeshCombineType.Everything)
                            {
                                using (new EditorGUIHelper.GUIBackgroundColor(generateDefaultShader == null ? Color.red : Color.white))
                                {
                                    generateDefaultShader = (Shader)EditorGUI.ObjectField(new Rect(collumnRect3.xMin, collumnRect3.yMin, collumnRect3.width - 30, collumnRect3.height), generateDefaultShader, typeof(Shader), true);
                                }

                                if (GUI.Button(new Rect(collumnRect3.xMin + (collumnRect3.width - 25), collumnRect3.yMin, 25, collumnRect3.height), "..."))
                                {
                                    ShaderSelectionDropdown shaderSelection = new ShaderSelectionDropdown(CallbackDefaultShaderSelection, generateDefaultShader == null ? string.Empty : generateDefaultShader.name);
                                    shaderSelection.Show(collumnRect3);
                                }
                            }
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
                        Rect rect = AddRow(ref collumnRect1, ref collumnRect2, ref collumnRect3, ref collumnRect4);

                        EditorGUI.LabelField(collumnRect1, "Mesh");
                        using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(75))
                        {
                            saveMeshFormat = (Enum.MeshFormat)EditorGUI.EnumPopup(collumnRect2, "Format", saveMeshFormat);
                        }

                        saveMeshCompression = (ModelImporterMeshCompression)EditorGUI.EnumPopup(collumnRect3, string.Empty, saveMeshCompression, GUIStyle.none);
                        EditorGUI.LabelField(collumnRect3, "Compression:  " + saveMeshCompression.ToString(), EditorStyles.popup);


                        EditorGUI.LabelField(collumnRect4, "Attributes");
                        {
                            VertexAttributePopup.AttributeFlags usedAttributes = GetRequiredVertexAttributes();

                            string buttonName = saveUseDefaultFlagsForVertexAttribute ? "Default" : VertexAttributePopup.GetLabelName(saveVertexAttributeFlags, usedAttributes);

                            if (GUI.Button(new Rect(collumnRect4.xMin + 77, collumnRect4.yMin, collumnRect4.width - 77, collumnRect4.height), buttonName, EditorStyles.popup))
                            {
                                PopupWindow.Show(new Rect(collumnRect4.xMin + 77, collumnRect4.yMin, collumnRect4.width - 77, collumnRect4.height), new VertexAttributePopup(usedAttributes));
                            }
                        }


                        if (generateAssetSaveType == Enum.AssetSaveType.Prefab)
                        {
                            rect = AddRow(ref collumnRect1, ref collumnRect2, ref collumnRect3, ref collumnRect4);

                            EditorGUI.LabelField(collumnRect1, "Prefab Flags");
                            using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(75))
                            {
                                saveUseStaticEditorFlags = EditorGUI.ToggleLeft(new Rect(collumnRect2.xMin, collumnRect2.yMin, 55, collumnRect2.height), "Static", saveUseStaticEditorFlags);
                                using (new EditorGUIHelper.GUIEnabled(saveUseStaticEditorFlags))
                                {
                                    saveStaticEditorFlags = (StaticEditorFlags)EditorGUI.EnumFlagsField(new Rect(collumnRect2.xMin, collumnRect2.yMin, collumnRect2.width - (saveUseStaticEditorFlags ? 30 : 0), collumnRect2.height), " ", saveStaticEditorFlags);

                                    if (saveUseStaticEditorFlags)
                                    {
                                        using (new EditorGUIHelper.GUIBackgroundColor(EditorGUIHelper.GetToggleButtonColor(saveUseStaticEditorFlagsForHierarchy)))
                                        {
                                            saveUseStaticEditorFlagsForHierarchy = GUI.Toggle(new Rect(collumnRect2.xMin + (collumnRect2.width - 25), collumnRect2.yMin, 25, collumnRect2.height), saveUseStaticEditorFlagsForHierarchy, EditorResources.GUIContentPrefabFlags, "Button");
                                        }
                                    }
                                }


                                saveUseTag = EditorGUI.ToggleLeft(new Rect(collumnRect3.xMin, collumnRect3.yMin, 60, collumnRect3.height), "Tag", saveUseTag);
                                using (new EditorGUIHelper.GUIEnabled(saveUseTag))
                                {
                                    saveTag = EditorGUI.TagField(new Rect(collumnRect3.xMin, collumnRect3.yMin, collumnRect3.width - (saveUseTag ? 30 : 0), collumnRect3.height), " ", saveTag);

                                    if (saveUseTag)
                                    {
                                        using (new EditorGUIHelper.GUIBackgroundColor(EditorGUIHelper.GetToggleButtonColor(saveUseTagForHierarchy)))
                                        {
                                            saveUseTagForHierarchy = GUI.Toggle(new Rect(collumnRect3.xMin + (collumnRect3.width - 25), collumnRect3.yMin, 25, collumnRect3.height), saveUseTagForHierarchy, EditorResources.GUIContentPrefabFlags, "Button");
                                        }
                                    }
                                }


                                saveUseLayer = EditorGUI.ToggleLeft(new Rect(collumnRect4.xMin, collumnRect4.yMin, 60, collumnRect4.height), "Layer", saveUseLayer);
                                using (new EditorGUIHelper.GUIEnabled(saveUseLayer))
                                {
                                    saveLayer = EditorGUI.LayerField(new Rect(collumnRect4.xMin, collumnRect4.yMin, collumnRect4.width - (saveUseLayer ? 30 : 0), collumnRect4.height), " ", saveLayer);

                                    if (saveUseLayer)
                                    {
                                        using (new EditorGUIHelper.GUIBackgroundColor(EditorGUIHelper.GetToggleButtonColor(saveUseLayerForHierarchy)))
                                        {
                                            saveUseLayerForHierarchy = GUI.Toggle(new Rect(collumnRect4.xMin + (collumnRect4.width - 25), collumnRect4.yMin, 25, collumnRect4.height), saveUseLayerForHierarchy, EditorResources.GUIContentPrefabFlags, "Button");
                                        }
                                    }
                                }
                            }
                        }


                        if (generateAssetSaveType == Enum.AssetSaveType.Prefab || generateAssetSaveType == Enum.AssetSaveType.MeshOnly)
                        {
                            DrawSaveOptions(collumnRect1, collumnRect2, collumnRect3, collumnRect4, 75);
                        }
                    }
                }
            }
        }
        public override void Reset()
        {
            base.Reset();

            solverNormalizeEdges = true;
            solverTryQuad = false;
            solverStoreInsideVertexAttribute = WireframeShaderEnum.VertexAttribute.UV3;

            generateAssetSaveType = Enum.AssetSaveType.Prefab;
            generateMeshCombineType = Enum.MeshCombineType.Nothing;
            generateReplaceMeshColliders = false;
            generateLightmapUVs = false;
            generateMaterialType = Enum.MaterialType.CreateNew;
            generateDefaultShader = Shader.Find(defaultShaderName);

            saveUseStaticEditorFlags = false;
            saveStaticEditorFlags = 0;
            saveUseStaticEditorFlagsForHierarchy = true;
            saveUseTag = false;
            saveTag = "Untagged";
            saveUseTagForHierarchy = true;
            saveUseLayer = false;
            saveLayer = 0;
            saveUseLayerForHierarchy = true;

            saveMeshFormat = Enum.MeshFormat.UnityAsset;
            saveMeshCompression = ModelImporterMeshCompression.Low;
            saveUseDefaultFlagsForVertexAttribute = true;
            saveVertexAttributeFlags = (VertexAttributePopup.AttributeFlags)~0;
        }
        public override void LoadEditorData()
        {
            base.LoadEditorData();
            
            
            if (generateDefaultShader == null || generateDefaultShader.GetType().Equals(typeof(Shader)) == false)
                generateDefaultShader = Shader.Find(defaultShaderName);
            else
            {
                int ID = generateDefaultShader.GetInstanceID();
                generateDefaultShader = EditorUtilities.InstanceIDToObject(ID) as Shader;

                if (generateDefaultShader == null)
                    generateDefaultShader = Shader.Find(defaultShaderName);
            }
        }
        override public bool IsReady()
        {
            if (solverStoreInsideVertexAttribute == WireframeShaderEnum.VertexAttribute.UV1 && GeneratingLightmapUVs())
                return false;

            if (generateAssetSaveType == Enum.AssetSaveType.Prefab || generateAssetSaveType == Enum.AssetSaveType.MeshOnly)
            {
                if (base.IsReady() == false)
                    return false;

                if (generateAssetSaveType == Enum.AssetSaveType.Prefab)
                {
                    if (generateMaterialType == Enum.MaterialType.CreateNew && generateDefaultShader == null)
                        return false;
                }
            }

            if (generateAssetSaveType == Enum.AssetSaveType.OverwriteOriginalMesh)
            {
                if (EditorWindow.active.listBatchObjects.All(c => c.isMeshAssetFormat == BatchObject.OptionsState.No))
                    return false;
            }


            return true;
        }

        public VertexAttributePopup.AttributeFlags GetRequiredVertexAttributes()
        {
            VertexAttributePopup.AttributeFlags usedAttributes = 0;
            switch (solverStoreInsideVertexAttribute)
            {
                case WireframeShaderEnum.VertexAttribute.UV0: usedAttributes = VertexAttributePopup.AttributeFlags.UV0; break;
                case WireframeShaderEnum.VertexAttribute.UV1: usedAttributes = VertexAttributePopup.AttributeFlags.UV1; break;
                case WireframeShaderEnum.VertexAttribute.UV2: usedAttributes = VertexAttributePopup.AttributeFlags.UV2; break;
                case WireframeShaderEnum.VertexAttribute.UV3: usedAttributes = VertexAttributePopup.AttributeFlags.UV3; break;
                case WireframeShaderEnum.VertexAttribute.UV4: usedAttributes = VertexAttributePopup.AttributeFlags.UV4; break;
                case WireframeShaderEnum.VertexAttribute.UV5: usedAttributes = VertexAttributePopup.AttributeFlags.UV5; break;
                case WireframeShaderEnum.VertexAttribute.UV6: usedAttributes = VertexAttributePopup.AttributeFlags.UV6; break;
                case WireframeShaderEnum.VertexAttribute.UV7: usedAttributes = VertexAttributePopup.AttributeFlags.UV7; break;
                case WireframeShaderEnum.VertexAttribute.Normal: usedAttributes = VertexAttributePopup.AttributeFlags.Normal; break;
                case WireframeShaderEnum.VertexAttribute.Tangent: usedAttributes = VertexAttributePopup.AttributeFlags.Tangent; break;
            }

            if (GeneratingLightmapUVs())
                usedAttributes |= VertexAttributePopup.AttributeFlags.UV1;


            return usedAttributes;
        }
        public bool GeneratingLightmapUVs()
        {
            if ((generateAssetSaveType == Enum.AssetSaveType.Prefab || generateAssetSaveType == Enum.AssetSaveType.MeshOnly) &&
                (generateMeshCombineType == Enum.MeshCombineType.OneMeshWithSubmeshes || generateMeshCombineType == Enum.MeshCombineType.Everything))
            {
                return generateLightmapUVs;
            }
            else
            {
                return false;
            }
        }
        void CallbackDefaultShaderSelection(object obj)
        {
            if (obj == null)
                return;

            string shaderName = obj.ToString();
            if (string.IsNullOrEmpty(shaderName))
                return;

            generateDefaultShader = Shader.Find(shaderName);
        }
    }
}