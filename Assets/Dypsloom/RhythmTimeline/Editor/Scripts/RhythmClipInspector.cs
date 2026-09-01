using System.Collections.Generic;
using Dypsloom.RhythmTimeline.Core.Notes;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace Dypsloom.RhythmTimeline.Editor
{
    using Dypsloom.RhythmTimeline.Core.Playables;
    using UnityEditor.UIElements;
    using UnityEngine.U2D;
    using UnityEngine.UIElements;
    using Editor = UnityEditor.Editor;

    [CustomEditor(typeof(Note), true)]
    [CanEditMultipleObjects]
    public class NoteInspector : Editor
    {
        protected virtual void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        protected virtual void OnSceneGUI(SceneView sceneView)
        {
            Note note = target as Note;

            if (note is INoteOnSceneGUIChange onsceneviewchange) {
                onsceneviewchange.OnSceneGUIChange(sceneView);
            }
        }
        
        protected virtual void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }

    [CustomEditor(typeof(RhythmClip), true)]
    [CanEditMultipleObjects]
    public class RhythmClipInspector : Editor
    {
        private static List<RhythmClip> s_SelectedRhythmClips;
        private static Note s_SelectedNoteInstance;

        private VisualElement m_Root;
        
        protected virtual void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            
            var selectNoteInstanceButton = new Button()
            {
                text = "Select Note",
            };
            selectNoteInstanceButton.clicked += () =>
            {
                if (s_SelectedNoteInstance != null) {
                    Selection.activeObject = s_SelectedNoteInstance;
                }
            };
            root.Add(selectNoteInstanceButton);
            
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            
            RhythmClip rhythmClip = (RhythmClip)target;
            var extraDataObject = rhythmClip.ClipParameters.RhythmClipExtraNoteData;
            if (extraDataObject != null) {
                root.Add(new Label(extraDataObject.GetType().Name));
                
                var nestedInspector = new InspectorElement(extraDataObject);
                root.Add(nestedInspector);
            }

            m_Root = root;
            
            return root;
        }
        
        protected virtual void OnSceneGUI(SceneView sceneView)
        {
            if (s_SelectedRhythmClips == null)
            {
                s_SelectedRhythmClips = new List<RhythmClip>();
            }
            else
            {
                s_SelectedRhythmClips.Clear();
            }
            
            TimelineClip[] clips = UnityEditor.Timeline.TimelineEditor.selectedClips;
            foreach (var clip in clips)
            {
                if (clip.asset is RhythmClip ohterRhythmClip)
                {
                    s_SelectedRhythmClips.Add(ohterRhythmClip);
                }
            }
            
            RhythmClip rhythmClip = (RhythmClip)target;
            
            INoteRhythmClipOnSceneGUIChange noteOnSceneGuiChange;

            //bool usingPrefab = false;
            var noteInstance = rhythmClip.RhythmClipData.NoteInstance;
            if (s_SelectedNoteInstance != noteInstance) {
                
                if (s_SelectedNoteInstance is INoteRhythmClipOnNoteInstanceChange previousNoteInstanceChange) {
                    previousNoteInstanceChange.RhythmClipOnNoteInstanceChange(s_SelectedNoteInstance, noteInstance, m_Root, rhythmClip, s_SelectedRhythmClips);
                }
                if (noteInstance is INoteRhythmClipOnNoteInstanceChange noteInstanceChange) {
                    noteInstanceChange.RhythmClipOnNoteInstanceChange(s_SelectedNoteInstance, noteInstance, m_Root, rhythmClip, s_SelectedRhythmClips);
                }

                s_SelectedNoteInstance = noteInstance;
            }
            
            if (noteInstance != null) {
                //usingPrefab = false;
                noteOnSceneGuiChange = noteInstance.GetComponent<INoteRhythmClipOnSceneGUIChange>();
            } else {
                //usingPrefab = true;
                noteOnSceneGuiChange = rhythmClip.RhythmPlayableBehaviour?.NoteDefinition?.NotePrefab?.GetComponent<INoteRhythmClipOnSceneGUIChange>();
            }
            
            
            //Get the note prefab to get the function to call for creating handles for custom notes.
            if (noteOnSceneGuiChange == null)
            {
                return;
            }
            
            if ( noteOnSceneGuiChange.RhythmClipOnSceneGUIChange( sceneView, rhythmClip, s_SelectedRhythmClips ) )
            {
                Reevaluate();
                Repaint();
            }
            
        }
        void Reevaluate()
        {
            UnityEditor.Timeline.TimelineEditor.Refresh( RefreshReason.ContentsModified 
                                                         | RefreshReason.WindowNeedsRedraw);
			
        }
        
        
        protected virtual void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }
}
