namespace Dypsloom.RhythmTimeline.Midi.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Dypsloom.RhythmTimeline.Core;
    using Dypsloom.RhythmTimeline.Core.Generators;
    using Dypsloom.RhythmTimeline.Core.Notes;
    using Dypsloom.RhythmTimeline.Core.Playables;
    using Melanchall.DryWetMidi.Core;
    using Melanchall.DryWetMidi.Interaction;
    using UnityEditor;
    using UnityEditor.Timeline;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.Timeline;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    [CustomEditor(typeof(MidiToTimelineSettings), true)]
    public class MidiToTimelineAssetInspector : Editor
    {
        private static BasicNoteClipGenerator s_CachedBasicGenerator;
        
        private MidiToTimelineSettings m_MidiToTimelineSettings;

        ObjectField m_RhythmTimelineAssetField;
        ObjectField m_MidiToTimelineSettingsField;
        
        private VisualElement m_HelpBoxContainer;
        private HelpBox m_HelpBox;
        
        private HelpBox m_WarningHelpBox;
        
        private VisualElement m_ContainerDropdowns;
        private DropdownField m_MidiTrackDropdownField;
        private List<string> m_MidiTracksChoices = new List<string>();
        
        private VisualElement m_ListContainer;
        private MidiToNoteListView m_MidiToNoteListView;

        private VisualElement m_ButtonsContainer;
        private Button m_GenerateNotesForAllMidiTracks;  
        private Button m_GenerateNoteForSelectedMidiTrack;

        public MidiToTimelineSettings MidiToTimelineSettings => m_MidiToTimelineSettings;
        public MidiFileAsset MidiFileAsset => m_MidiToTimelineSettings.MidiFileAsset;
        public RhythmTimelineAsset RhythmTimelineAsset => m_MidiToTimelineSettings.RhythmTimelineAsset;

        private void OnEnable()
        {
            m_MidiToTimelineSettings = target as MidiToTimelineSettings;

        }

        public override VisualElement CreateInspectorGUI()
        {

            var root = new VisualElement();

            root.Add(new Label("Midi To Rhythm Timeline"));
            
            m_HelpBoxContainer = new VisualElement();
            root.Add(m_HelpBoxContainer);
            
            m_HelpBox = new HelpBox("To convert Midi To Rhythm Timeline you will need a MdiFile and Rhythm Timeline and a MidiToTimelineSettings", HelpBoxMessageType.Info);

            m_WarningHelpBox = new HelpBox("Make sure to have a Midi File and a Rhythm Timeline Asset.", HelpBoxMessageType.Warning);
            
            m_HelpBoxContainer.Add(m_HelpBox);

            var midiFileAssetField = new ObjectField("Midi File Asset");
            midiFileAssetField.objectType = typeof(MidiFileAsset);
            midiFileAssetField.SetValueWithoutNotify(MidiFileAsset);
            midiFileAssetField.RegisterValueChangedCallback(OnMidiFileAssetChanged);
            root.Add(midiFileAssetField);

            var generateRhythmTimelineButton = new Button(GenerateNewTimeline);
            generateRhythmTimelineButton.text = "Create New Timeline";
            root.Add(generateRhythmTimelineButton);

            m_RhythmTimelineAssetField = new ObjectField("Rhythm Timeline Asset");
            m_RhythmTimelineAssetField.objectType = typeof(RhythmTimelineAsset);
            m_RhythmTimelineAssetField.SetValueWithoutNotify(RhythmTimelineAsset);
            m_RhythmTimelineAssetField.RegisterValueChangedCallback(OnRhythmTimelineAssetChanged);
            root.Add(m_RhythmTimelineAssetField);
            
            //InspectorElement.FillDefaultInspector(root, serializedObject, this);

            m_ContainerDropdowns = new VisualElement();
            root.Add(m_ContainerDropdowns);
            
            m_MidiTrackDropdownField = new DropdownField("Midi Track");
            m_MidiTrackDropdownField.RegisterValueChangedCallback(OnMidiSelectedTrackChange);
            m_ContainerDropdowns.Add(m_MidiTrackDropdownField);

            m_ListContainer = new VisualElement();
            root.Add(m_ListContainer);
            
            m_MidiToNoteListView = new MidiToNoteListView();
            m_MidiToNoteListView.Setup(this);
            
            m_ListContainer.Add(m_MidiToNoteListView);
            
            m_ButtonsContainer = new VisualElement();
            m_ButtonsContainer.style.flexDirection = FlexDirection.Row;
            m_ButtonsContainer.style.justifyContent = Justify.Center;
            m_ButtonsContainer.style.marginTop = 10;
            root.Add(m_ButtonsContainer);
            
            m_GenerateNotesForAllMidiTracks = new Button(GenerateNoteForAllMidiTracks);
            m_GenerateNotesForAllMidiTracks.text = "Generate Notes For All Midi Tracks";
            
            m_GenerateNoteForSelectedMidiTrack = new Button(GenerateNoteForSelectedMidiTrack);
            m_GenerateNoteForSelectedMidiTrack.text = "Generate Notes For Selected Midi Track";
            
            m_ButtonsContainer.Add(m_GenerateNotesForAllMidiTracks);
            m_ButtonsContainer.Add(m_GenerateNoteForSelectedMidiTrack);
            
            Refresh();

            return root;
        }

        private void GenerateNoteForAllMidiTracks()
        {
            foreach (var midiTrack in MidiFileAsset.m_MidiTracks) {
                GenerateNoteForMidiTrack(midiTrack);
            }
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved | RefreshReason.WindowNeedsRedraw);
        }  

        private void GenerateNoteForSelectedMidiTrack()
        {
            var index = m_MidiTrackDropdownField.index;
            var midiTracks = MidiFileAsset.m_MidiTracks;
            
            if (index < 0 ||
                index >= midiTracks.Count) {
                Debug.LogWarning($"Index out of Range {index}/{midiTracks.Count}.");
                return;
            }

            GenerateNoteForMidiTrack(midiTracks[index]);
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved | RefreshReason.WindowNeedsRedraw);
        }
        
        private void GenerateNoteForMidiTrack(MidiFileAsset.MidiTrack midiTrack)
        {
            if (s_CachedBasicGenerator == null) {
                s_CachedBasicGenerator = ScriptableObject.CreateInstance<BasicNoteClipGenerator>();
            }
            
            if (!TryGetMidiToNoteTrack(midiTrack.TrackName, out var midiTrackToRhythmTrack)) {
                Debug.LogWarning($"Could not find Midi To Rhythm Track: {midiTrack.TrackName}");
                return;
            }
            
            var midiFile = MidiFile.Read(AssetDatabase.GetAssetPath(MidiFileAsset));
            var tempoMap = midiFile.GetTempoMap();

            foreach (var midiNote in midiTrack.Notes) {
                var midiGenerator = midiTrackToRhythmTrack.GetOrCreateMidiNoteGenerator(midiNote.NoteNumber);
                if(midiGenerator.NoteDefinition == null) { continue; }
                    
                var clipGenerator = midiGenerator.NoteClipGenerator ?? s_CachedBasicGenerator;

                if (RhythmTimelineAsset.TryGetRhythmTackWithID(midiGenerator.TrackID, out var rhythmTrack) == false) {
                    Debug.LogWarning($"Could not find Rhythm Timeline Track with ID: {midiGenerator.TrackID}");
                    continue;
                }
                
                //Midi time is in ticks. so we need to convert to seconds
                MetricTimeSpan startTime = TimeConverter.ConvertTo<MetricTimeSpan>(midiNote.Time, tempoMap);
                MetricTimeSpan duration = TimeConverter.ConvertTo<MetricTimeSpan>(midiNote.length, tempoMap);
                
                clipGenerator.GenerateClip(rhythmTrack,startTime.TotalSeconds, duration.TotalSeconds, midiGenerator.NoteDefinition, GenerateClipOverrideOption.ReplaceOnOverlap);
            }
        }

        private void OnMidiSelectedTrackChange(ChangeEvent<string> evt)
        {
            Refresh();
        }
    
        private void OnSettingsTrackChange(ChangeEvent<string> evt)
        {
            Refresh();
        }

        private void OnMidiFileAssetChanged(ChangeEvent<Object> evt)
        {
            m_MidiToTimelineSettings.MidiFileAsset = evt.newValue as MidiFileAsset;
            Refresh();
        }

        private void OnRhythmTimelineAssetChanged(ChangeEvent<Object> evt)
        {
            m_MidiToTimelineSettings.RhythmTimelineAsset = evt.newValue as RhythmTimelineAsset;
        }

        private void GenerateNewTimeline()
        {
            var asset = ScriptableObject.CreateInstance<RhythmTimelineAsset>();

            asset.CreateTrack<AudioTrack>();
            for (int i = 0; i < 4; i++) {
                var rhythmTrack = asset.CreateTrack<RhythmTrack>();
                rhythmTrack.SetID(i);
            }

            var assetPath = MidiFileAsset == null
                ? Application.dataPath
                : Path.GetDirectoryName(AssetDatabase.GetAssetPath(MidiFileAsset));

            var path = EditorUtility.SaveFilePanelInProject(
                "Create Rhythm Timeline Asset",
                $"{MidiFileAsset?.name}_Timeline",
                "asset",
                "Create a Rhythm Timeline Asset.",
                assetPath);

            path = AssetDatabase.GenerateUniqueAssetPath(path);

            if (string.IsNullOrWhiteSpace(path)) { return; }

            AssetDatabase.CreateAsset(asset, path);

            m_MidiToTimelineSettings.RhythmTimelineAsset = asset;
            m_RhythmTimelineAssetField.SetValueWithoutNotify(RhythmTimelineAsset);
            
            Refresh();
        }

        private void Refresh()
        {
            m_ListContainer.Clear();
            
            if (MidiFileAsset == null) {
                m_MidiTrackDropdownField.choices = new List<string>(){"NULL"};
            } else {
                m_MidiTrackDropdownField.choices = MidiFileAsset.m_MidiTracks.Select(track => track.TrackName).ToList();
            }

            if (m_MidiTrackDropdownField.index < 0 && m_MidiTrackDropdownField.choices.Count > 0) {
                m_MidiTrackDropdownField.index = 0;
            }
            
            var midiFileTrack = GetMidiFileTrack();
            
            if (midiFileTrack == null) {
                m_ListContainer.Add(m_WarningHelpBox);
                return;
            }
            
            m_ListContainer.Add(m_MidiToNoteListView);
            
            
            m_MidiToNoteListView.Refresh();
        }

        public MidiFileAsset.MidiTrack GetMidiFileTrack()
        {
            if (MidiFileAsset == null || MidiFileAsset.m_MidiTracks == null)
            {
                return null;
            }
            var index = m_MidiTrackDropdownField.index;
            if (index < 0 || index >= MidiFileAsset.m_MidiTracks.Count) {
                Debug.LogWarning($"Index out of Range {index}/{MidiFileAsset.m_MidiTracks.Count}.");
                return null;
            }
            return MidiFileAsset.m_MidiTracks[index];
        }
        
        public bool TryGetMidiToNoteTrack(string trackName, out MidiToTimelineSettings.MidiToRhythmTrack rhythmTrack)
        {
            return MidiToTimelineSettings.TryGetMidiToRhythmTrack(trackName, out rhythmTrack);
        }
    }


    public class MidiToNoteListView : VisualElement
    {
        private MidiToTimelineAssetInspector m_EditorWindow;

        private ListView m_ListView;
        private List<MidiToTimelineSettings.MidiNoteToNoteClipGenerator> m_MidiNoteToGenerators;

        private Dictionary<int, int> m_NoteNumberCount;

        public MidiToTimelineAssetInspector EditorWindow => m_EditorWindow;
        public ListView ListView => m_ListView;
        public List<MidiToTimelineSettings.MidiNoteToNoteClipGenerator> MMidiNoteToGenerators => m_MidiNoteToGenerators;
        public Dictionary<int, int> NoteNumberCount => m_NoteNumberCount;
        public static string DisabledUssClassName => disabledUssClassName;

        private VisualElement m_Header;

        public void Setup(MidiToTimelineAssetInspector midiToTimelineEditorWindow)
        {
            m_EditorWindow = midiToTimelineEditorWindow;
            m_NoteNumberCount = new Dictionary<int, int>();

            m_MidiNoteToGenerators = new List<MidiToTimelineSettings.MidiNoteToNoteClipGenerator>();

            m_Header = new VisualElement()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 10,
                    marginTop = 20,
                    
                }
            };
            
            var noteNumberLabel = new Label("ID"); 
            noteNumberLabel.style.width = 50;
            m_Header.Add(noteNumberLabel);
            
            var noteNameLabel = new Label("Name"); 
            noteNameLabel.style.width = 50;
            m_Header.Add(noteNameLabel);
            
            var noteCountLabel = new Label("Count"); 
            noteCountLabel.style.width = 50;
            m_Header.Add(noteCountLabel);
            
            var trackIDLabel = new Label("TrackID"); 
            trackIDLabel.style.width = 50;
            trackIDLabel.style.marginRight = 10;
            m_Header.Add(trackIDLabel);
            
            var noteDefinitionLabel = new Label("Note Definition"); 
            noteDefinitionLabel.style.flexGrow = 1;
            noteDefinitionLabel.style.marginRight = 10;
            m_Header.Add(noteDefinitionLabel);
            
            var clipGeneratorLabel = new Label("Clip Generator"); 
            clipGeneratorLabel.style.flexGrow = 1;
            m_Header.Add(clipGeneratorLabel);
            
            Add(m_Header);
            

            m_ListView = new ListView(m_MidiNoteToGenerators, -1, MakeItem, BindItem);
            
            Add(m_ListView);
        }

        private void BindItem(VisualElement container, int index)
        {
            var listElement = container as ListElement;
            listElement.Set(m_MidiNoteToGenerators, index);
        }

        private VisualElement MakeItem()
        {
            return new ListElement(this);
        }

        public void Refresh()
        {
            m_MidiNoteToGenerators.Clear();
            m_NoteNumberCount.Clear();

            var midiFileTrack = m_EditorWindow.GetMidiFileTrack();
            if (m_EditorWindow.TryGetMidiToNoteTrack(midiFileTrack.TrackName, out var midiTrackToRhythmTrack) == false) {
                midiTrackToRhythmTrack = new MidiToTimelineSettings.MidiToRhythmTrack();
                midiTrackToRhythmTrack.Name = midiFileTrack.TrackName;
                m_EditorWindow.MidiToTimelineSettings.AddMidiToRhythmTrack(midiTrackToRhythmTrack);
            }

            foreach (var midiNote in midiFileTrack.Notes) {
                if (m_NoteNumberCount.ContainsKey(midiNote.NoteNumber)) {
                    m_NoteNumberCount[midiNote.NoteNumber]++;
                } else {
                    m_NoteNumberCount.Add(midiNote.NoteNumber, 1);

                    var midiGenerator = midiTrackToRhythmTrack.GetOrCreateMidiNoteGenerator(midiNote.NoteNumber);
                    
                    m_MidiNoteToGenerators.Add(midiGenerator);
                }
            }

            m_MidiNoteToGenerators.Sort((x, y) => x.NoteNumber.CompareTo(y.NoteNumber));
            m_ListView.RefreshItems();
        }

        public class ListElement : VisualElement
        {
            private MidiToNoteListView m_MidiToNoteListView;
            private MidiToTimelineSettings.MidiNoteToNoteClipGenerator m_Value;

            private Label m_IDLabel;
            private Label m_NameLabel;
            private Label m_CountLabel;
            private IntegerField m_TrackID;
            private ObjectField m_NoteDefinitionField;
            private ObjectField m_ClipGeneratorField;


            public ListElement(MidiToNoteListView listView)
            {
                m_MidiToNoteListView = listView;
                style.flexDirection = FlexDirection.Row;

                m_IDLabel = new Label();
                m_IDLabel.style.width = 50;
                Add(m_IDLabel);
                
                m_NameLabel = new Label();
                m_NameLabel.style.width = 50;
                Add(m_NameLabel);
                
                m_CountLabel = new Label();
                m_CountLabel.style.width = 50;
                Add(m_CountLabel);

                m_TrackID = new IntegerField();
                m_TrackID.RegisterValueChangedCallback(OnTrackIDValueChanged);
                m_TrackID.style.width = 50;
                m_TrackID.style.marginRight = 10;
                Add(m_TrackID);

                m_NoteDefinitionField = new ObjectField();
                m_NoteDefinitionField.objectType = typeof(NoteDefinition);
                m_NoteDefinitionField.RegisterValueChangedCallback(OnNoteDefinitionValueChanged);
                m_NoteDefinitionField.style.flexGrow = 1;
                m_NoteDefinitionField.style.flexBasis = 0;
                m_NoteDefinitionField.style.marginRight = 10;
                Add(m_NoteDefinitionField);

                m_ClipGeneratorField = new ObjectField();
                m_ClipGeneratorField.objectType = typeof(NoteClipGenerator);
                m_ClipGeneratorField.RegisterValueChangedCallback(OnClipGeneratorValueChanged);
                m_ClipGeneratorField.style.flexGrow = 1;
                m_ClipGeneratorField.style.flexBasis = 0;
                Add(m_ClipGeneratorField);

            }

            private void OnClipGeneratorValueChanged(ChangeEvent<Object> evt)
            {
                m_Value.NoteClipGenerator = evt.newValue as NoteClipGenerator;
                Refresh();
            }

            private void OnNoteDefinitionValueChanged(ChangeEvent<Object> evt)
            {
                m_Value.NoteDefinition = evt.newValue as NoteDefinition;
                Refresh();
            }

            private void OnTrackIDValueChanged(ChangeEvent<int> evt)
            {
                m_Value.TrackID = evt.newValue;
                Refresh();
            }

            private void OnNoteValueChanged(ChangeEvent<int> evt)
            {
                m_Value.NoteNumber = evt.newValue;
                Refresh();
            }

            public void Set(List<MidiToTimelineSettings.MidiNoteToNoteClipGenerator> list, int index)
            {
                if (index < 0 || index >= list.Count) {
                    m_Value = default;
                    Refresh();
                    return;
                }

                m_Value = list[index];
                Refresh();
            }

            public void Refresh()
            {
                if (m_Value == null) {
                    m_IDLabel.text = "NULL";
                    m_NameLabel.text = "N/A";
                    m_CountLabel.text = "0";
                    return;
                }

                m_MidiToNoteListView.NoteNumberCount.TryGetValue(m_Value.NoteNumber, out var count);
                
                m_IDLabel.text = m_Value.NoteNumber.ToString();
                m_NameLabel.text = NoteNumberToName(m_Value.NoteNumber);
                m_CountLabel.text = count.ToString();
                m_TrackID.SetValueWithoutNotify(m_Value.TrackID);
                m_NoteDefinitionField.SetValueWithoutNotify(m_Value.NoteDefinition);
                m_ClipGeneratorField.SetValueWithoutNotify(m_Value.NoteClipGenerator);
            }
            
            private string NoteNumberToName(int valueNoteNumber)
            {
                var octave = (valueNoteNumber - 1) / 12;
                if (valueNoteNumber == 0) { octave = -1; }

                var mod = valueNoteNumber % 12;

                string noteName;

                switch (mod) {
                    case 0:
                        noteName = "C";
                        break;
                    case 1:
                        noteName = "C#";
                        break;
                    case 2:
                        noteName = "D";
                        break;
                    case 3:
                        noteName = "D#";
                        break;
                    case 4:
                        noteName = "E";
                        break;
                    case 5:
                        noteName = "F";
                        break;
                    case 6:
                        noteName = "F#";
                        break;
                    case 7:
                        noteName = "G";
                        break;
                    case 8:
                        noteName = "G#";
                        break;
                    case 9:
                        noteName = "A";
                        break;
                    case 10:
                        noteName = "A#";
                        break;
                    case 11:
                        noteName = "B";
                        break;
                    default:
                        noteName = "Unpitched";
                        break;
                }

                return $"{noteName} {octave}";
            }
        }
    }
}