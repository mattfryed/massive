/// ---------------------------------------------
/// Rhythm Timeline
/// Copyright (c) Dyplsoom. All Rights Reserved.
/// https://www.dypsloom.com
/// ---------------------------------------------

namespace Dypsloom.RhythmTimeline.Core.Playables
{
    using Dypsloom.RhythmTimeline.Core.Managers;
    using System;
    using Dypsloom.RhythmTimeline.Core.Notes;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.Timeline;
    using Object = UnityEngine.Object;


    [Serializable]
    //[DisplayName("Rhythm/Rhythm Clip")]
    public class RhythmClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("The Rhythm Playable Behaviour.")]
        [SerializeField] protected RhythmBehaviour m_RhythmPlayableBehaviour = new RhythmBehaviour();
        [Tooltip("The Rhythm Clip parameters.")]
        [SerializeField] protected RhythmClipParameters m_ClipParameters = new RhythmClipParameters();
        [Tooltip("The rhythm clip data.")]
        [SerializeField] [HideInInspector] protected RhythmClipData m_RhythmClipData = new RhythmClipData();

        public RhythmClipData RhythmClipData { get => m_RhythmClipData; set => m_RhythmClipData = value; }
        public RhythmBehaviour RhythmPlayableBehaviour => m_RhythmPlayableBehaviour;
        public RhythmClipParameters ClipParameters => m_ClipParameters;
    
        public ClipCaps clipCaps
        {
            get { return ClipCaps.None; }
        }
        
        public void SetNoteDefinition(NoteDefinition noteDefinition)
        {
            m_RhythmPlayableBehaviour.SetNoteDefinition(noteDefinition);
            
            var notePrefab = noteDefinition?.NotePrefab?.GetComponent<Note>();
            if (notePrefab == null) {
                Debug.LogWarning("WARNING: Note Prefab is not found");
                return;
            }
            
            var extraNoteDataType = notePrefab.RhythmClipExtraNoteDataType;

            if (extraNoteDataType != null) {
                var otherExtraData = ClipParameters.RhythmClipExtraNoteData;
                if (otherExtraData == null || otherExtraData.GetType() != extraNoteDataType) 
                {
                    if(otherExtraData != null) {
                        
#if UNITY_EDITOR
                
                        // save that scriptable object inside the timeline
                        UnityEditor.AssetDatabase.RemoveObjectFromAsset(otherExtraData);
                    
                        // Serializing the changes in memory to disk
                        UnityEditor.AssetDatabase.SaveAssets();
                        
                        Object.DestroyImmediate(otherExtraData);
#endif
                    }
                    
                    otherExtraData = ScriptableObject.CreateInstance(extraNoteDataType) as RhythmClipExtraNoteData;
                    otherExtraData.name = extraNoteDataType.Name;
                    otherExtraData.Initialize(notePrefab);
                    

                    
                }
                if (otherExtraData != null) 
                {
                    
                    Debug.Log(otherExtraData);
                
                    // Make a copy of the extra Data.
                    var newNoteData = ScriptableObject.Instantiate(otherExtraData);
                    newNoteData.name = otherExtraData.name;
                    ClipParameters.RhythmClipExtraNoteData = newNoteData;

#if UNITY_EDITOR
                
                    // save that scriptable object inside the timeline
                    var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
                    
                    UnityEditor.AssetDatabase.AddObjectToAsset(newNoteData, assetPath);
                    UnityEditor.EditorUtility.SetDirty(this);
                    UnityEditor.EditorUtility.SetDirty(newNoteData);
                    
                    UnityEditor.Undo.RegisterCreatedObjectUndo(newNoteData, "Create Clip");
                    
                    // Serializing the changes in memory to disk
                    UnityEditor.AssetDatabase.SaveAssets();
#endif
                }
            } else {
                ClipParameters.RhythmClipExtraNoteData = null;
            }
        }

        public override Playable CreatePlayable (PlayableGraph graph, GameObject owner)
        {
            m_RhythmPlayableBehaviour.RhythmClip = this;
            var playable = ScriptPlayable<RhythmBehaviour>.Create (graph, m_RhythmPlayableBehaviour);
            return playable;
        }

        public virtual void Copy(RhythmClip otherClip)
        {
            return;
        }
        
        private void OnDestroy()
        {  
#if UNITY_EDITOR
            var extraNoteDataObject = m_ClipParameters.RhythmClipExtraNoteData;

            if (extraNoteDataObject == null) {
                return;
            }
            
            UnityEditor.Undo.DestroyObjectImmediate(extraNoteDataObject);

            if (UnityEditor.AssetDatabase.Contains(extraNoteDataObject)) {
                UnityEditor.AssetDatabase.RemoveObjectFromAsset(extraNoteDataObject);
            }
            
            UnityEditor.AssetDatabase.SaveAssets();
                
#endif
        }
    }

    [Serializable]
    public struct RhythmClipData
    {
        private bool m_IsValid;
        private RhythmDirector m_RhythmDirector;
        private int m_TrackID;
        private double m_ClipStart;
        private double m_RealDuration;
        private RhythmClipParameters m_ClipParameters;
        private RhythmClip m_RhythmClip;
        private Note m_NoteInstance;

        public bool IsValid => m_IsValid;
        public RhythmDirector RhythmDirector=> m_RhythmDirector;
        public int TrackID => m_TrackID;
        public TrackObject TrackObject => m_RhythmDirector?.TrackObjects[m_TrackID];
        public double ClipStart => m_ClipStart;
        public double ClipEnd => m_ClipStart + m_RealDuration;
        public double RealDuration => m_RealDuration;
        public RhythmClipParameters ClipParameters => m_ClipParameters;
        public RhythmClip RhythmClip => m_RhythmClip;
        
        public Note NoteInstance {get => m_NoteInstance; set => m_NoteInstance = value; }

        public RhythmClipData(RhythmClip rhythmClip, RhythmDirector rhythmDirector, int trackID, double clipStart, double realDuration)
        {
            m_IsValid = true;
            m_RhythmClip = rhythmClip;
            m_RhythmDirector = rhythmDirector;
            m_TrackID = trackID;
            m_ClipStart = clipStart;
            m_RealDuration = realDuration;
            m_ClipParameters = m_RhythmClip.ClipParameters;
            m_NoteInstance = null;
        }
    }

    [Serializable]
    public class RhythmClipParameters
    {
        [Tooltip("The integer parameter.")]
        [SerializeField] protected int m_IntParameter;
        [Tooltip("The string parameter.")]
        [SerializeField] protected string m_StringParameter;
        [Tooltip("The float parameter.")]
        [SerializeField] protected float m_FloatParameter;
        [Tooltip("The Vector2 parameter.")]
        [SerializeField] protected Vector2 m_Vector2Parameter;
        [Tooltip("The Vector3 parameter.")]
        [SerializeField] protected Vector3 m_Vector3Parameter;
        [Tooltip("The Vector4 parameter.")]
        [SerializeField] protected Vector4 m_Vector4Parameter;
        [Tooltip("The Color parameter.")]
        [SerializeField] protected Color m_ColorParameter;
        [Tooltip("The Object parameter.")]
        [SerializeField] protected Object m_ObjectReferenceParameter;
        [HideInInspector]
        [Tooltip("The Extra data parameter for more complex setups.")]
        [SerializeField] protected RhythmClipExtraNoteData m_RhythmClipExtraNoteData;
    
        public int IntParameter
        {
            get => m_IntParameter;
            set => m_IntParameter = value;
        }

        public string StringParameter
        {
            get => m_StringParameter;
            set => m_StringParameter = value;
        }

        public float FloatParameter
        {
            get => m_FloatParameter;
            set => m_FloatParameter = value;
        }

        public Vector2 Vector2Parameter
        {
            get => m_Vector2Parameter;
            set => m_Vector2Parameter = value;
        }

        public Vector3 Vector3Parameter
        {
            get => m_Vector3Parameter;
            set => m_Vector3Parameter = value;
        }

        public Vector3 Vector4Parameter
        {
            get => m_Vector4Parameter;
            set => m_Vector4Parameter = value;
        }

        public Color ColorParameter
        {
            get => m_ColorParameter;
            set => m_ColorParameter = value;
        }

        public Object ObjectReferenceParameter
        {
            get => m_ObjectReferenceParameter;
            set => m_ObjectReferenceParameter = value;
        }
        
        public RhythmClipExtraNoteData RhythmClipExtraNoteData
        {
            get => m_RhythmClipExtraNoteData;
            set => m_RhythmClipExtraNoteData = value;
        }
    }
}