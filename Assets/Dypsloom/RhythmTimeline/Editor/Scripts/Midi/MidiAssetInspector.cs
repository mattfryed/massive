namespace Dypsloom.RhythmTimeline.Midi.Editor
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    [CustomEditor(typeof(MidiFileAsset), true)]
    public class MidiAssetInspector : Editor
    {
        private MidiFileAsset m_MidiFileAsset;

        private FloatField m_BpmField;
        private FloatField m_SongTimeField;
        
        public MidiFileAsset MidiFileAsset => m_MidiFileAsset;

        ObjectField m_RhythmTimelineAssetField;
        ObjectField m_MidiToTimelineSettingsField;

        private MidiToNoteListView m_MidiToNoteListView;

        private void OnEnable()
        {
            m_MidiFileAsset = target as MidiFileAsset;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            
            var generateSettingsButton = new Button(GenerateMidiToTimelineSettings);
            generateSettingsButton.text = "Create Midi To Timeline Asset";
            root.Add(generateSettingsButton);
            
            return root;
        }
        
        private void GenerateMidiToTimelineSettings()
        {
            var asset = ScriptableObject.CreateInstance<MidiToTimelineSettings>();
            asset.MidiFileAsset = m_MidiFileAsset;

            var assetPath = m_MidiFileAsset == null
                ? Application.dataPath
                : Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_MidiFileAsset));
        
            var path = EditorUtility.SaveFilePanelInProject(
                "Create MidiToTimeline Asset",
                $"{m_MidiFileAsset?.name}_MidiToTimeline",
                "asset",
                "Create a Midi To Timeline Asset.",
                assetPath);

            path = AssetDatabase.GenerateUniqueAssetPath(path);

            if (string.IsNullOrWhiteSpace(path)) {
                return;
            }
            
            AssetDatabase.CreateAsset(asset, path);
        
            //Select the asset in the project window
            Selection.activeObject = asset;
            
            //m_MidiToTimelineSettings = asset;
            //m_MidiToTimelineSettingsField.SetValueWithoutNotify(m_RhythmTimelineAsset);
        }
    }
}   