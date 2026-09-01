using System.Collections.Generic;
using Dypsloom.RhythmTimeline.Core.Input;
using Dypsloom.RhythmTimeline.Core.Notes;
using Dypsloom.RhythmTimeline.Core.Playables;
using UnityEngine;
using UnityEngine.Serialization;

namespace Dypsloom.RhythmTimeline.Demos.OSU.Scripts
{
    using System;
    using Dypsloom.RhythmTimeline.Core.Managers;
    using Dypsloom.Shared;
    using TMPro;
    using UnityEngine.Rendering;

    public class OSUNoteBase : Note, INoteRhythmClipOnSceneGUIChange
    {
        [Header("Outline")]
        [SerializeField] protected Transform m_TimingOutline;
        [SerializeField] protected float m_OutlineSizeSpeed = .1f;
        [SerializeField] protected Vector3 m_TimingOutlineEndSize = new Vector3(0.5f,0.5f,0.5f);
        
        [Header("Visuals")]
        [Tooltip("If true the Number Text will be set to the order of the note, the number gets auto reset when switching tracks.")]
        [SerializeField] protected bool m_AutoSetOrderTextPerTrack = true;
        [Tooltip("The text showing the number of the note")]
        [SerializeField] protected TMP_Text m_NumberText;
        
        [Tooltip("Set the note color to match the track primary color.")]
        [SerializeField] protected bool m_SetColorPerTrack = true;
        [FormerlySerializedAs("m_MainSprite")]
        [FormerlySerializedAs("m_ColorSprite")]
        [Tooltip("The main sprite used for coloring per track")]
        [SerializeField] protected SpriteRenderer m_MainSpriteRenderer;
        [FormerlySerializedAs("m_OutlineSprite")]
        [Tooltip("The main sprite used for coloring per track")]
        [SerializeField] protected SpriteRenderer m_OutlineSpriteRenderer;
        
        [Tooltip("The color used for coloring per track")]
        [SerializeField] protected int m_SortLayer;
        [Tooltip("The sort order of the canvases, appears on top of sprite renderers. Their sort is set dynamically to always appear behind previous notes")]
        [SerializeField] protected Canvas[] m_SortCanvas = Array.Empty<Canvas>();
        [Tooltip("The sort order of the sprite renderers. Their sort is set dynamically to always appear behind previous notes")]
        [SerializeField] protected Renderer[] m_SortOrderSpriteRenderers = Array.Empty<Renderer>();
       
        
        
        /// <summary>
        /// The note is initialized when it is added to the top of a track.
        /// </summary>
        /// <param name="rhythmClipData">The rhythm clip data.</param>
        public override void Initialize(RhythmClipData rhythmClipData)
        {
            base.Initialize(rhythmClipData);
            InitializeNoteVisuals(rhythmClipData);
        }

        protected virtual void InitializeNoteVisuals(RhythmClipData rhythmClipData)
        {
            if (m_SetColorPerTrack) {
                if (m_MainSpriteRenderer != null) {
                    m_MainSpriteRenderer.color = rhythmClipData.TrackObject.PrimaryColor;
                }

                if (m_OutlineSpriteRenderer != null) {
                    m_OutlineSpriteRenderer.color = rhythmClipData.TrackObject.PrimaryColor;
                }
            }
            
            var allRhythmClips = rhythmClipData.RhythmDirector.AllRhythmClips;
            var indexOfThisNote = allRhythmClips.IndexOf(rhythmClipData.RhythmClip);
            
            if (indexOfThisNote == -1) {
                Debug.LogWarning("Something went wrong, the rhythm clip was not found in the RhythmDirector array.");
                return;
            }

            if (m_AutoSetOrderTextPerTrack) {
                
                var count = 0;
                for (int i = indexOfThisNote; i >= 0; i--) {
                    var previousNoteClipData = allRhythmClips[i].RhythmClipData;

                    if (previousNoteClipData.TrackID == rhythmClipData.TrackID) {
                        count++;
                    } else {
                        break;
                    }
                }
                m_NumberText.text = count.ToString();
            }

            if (m_SortOrderSpriteRenderers.Length >= 16) {
                Debug.LogError("OSU Notes do not support more than 16 sprite renderers");
            }

            var indexSortNumber = indexOfThisNote<<4;

            //SortOrder is a signed 16 bit integer.
            var sortOrder = Int16.MaxValue-indexSortNumber;
            
            //Canvas always on top.
            for (int i = 0; i < m_SortCanvas.Length; i++) {
                var canvas = m_SortCanvas[i];
                canvas.sortingLayerID = m_SortLayer;
                canvas.sortingOrder = sortOrder;
                sortOrder--;
            }
            
            for (int i = 0; i < m_SortOrderSpriteRenderers.Length; i++) {
                var spriteRenderer = m_SortOrderSpriteRenderers[i];
                spriteRenderer.sortingLayerID = m_SortLayer;
                spriteRenderer.sortingOrder = sortOrder;
                sortOrder--;
            }

            
        }

        /// <summary>
        /// The note needs to be deactivated when it is out of range from being triggered.
        /// This usually happens when the clip ends.
        /// </summary>
        protected override void DeactivateNote()
        {
            base.DeactivateNote();

            //Only send the trigger miss event during play mode.
            if (Application.isPlaying == false)
            {
                return;
            }

            gameObject.SetActive(false);
            if (m_IsTriggered == false)
            {
                InvokeNoteTriggerEventMiss();
            }
        }

        /// <summary>
        /// An input was triggered on this note.
        /// The input event data has the information about what type of input was triggered.
        /// </summary>
        /// <param name="inputEventData">The input event data.</param>
        public override void OnTriggerInput(InputEventData inputEventData)
        {
            //Since this is a tap note, only deal with tap inputs.
            if (!inputEventData.Tap)
            {
                return;
            }

            //The gameobject can be set to active false. It is returned to the pool automatically when reset.
            m_IsTriggered = true;

            //You may compute the perfect time anyway you want.
            //In this case the perfect time is half of the clip.
            var perfectTime = m_RhythmClipData.RealDuration / 2f;
            var timeDifference = TimeFromActivate - perfectTime;
            var timeDifferencePercentage = Mathf.Abs((float)(100f * timeDifference)) / perfectTime;

            //Send a trigger event such that the score system can listen to it.
            InvokeNoteTriggerEvent(inputEventData, timeDifference, (float)timeDifferencePercentage);

            DeactivateNote();
        }

        /// <summary>
        /// Hybrid Update is updated both in play mode, by update or timeline, and edit mode by the timeline. 
        /// </summary>
        /// <param name="timeFromStart">The time from reaching the start of the clip.</param>
        /// <param name="timeFromEnd">The time from reaching the end of the clip.</param>
        protected override void HybridUpdate(double timeFromStart, double timeFromEnd)
        {
            //Compute the perfect timing.
            var perfectTime = m_RhythmClipData.RealDuration / 2f;
            var deltaT = (float)(timeFromStart - perfectTime);

            //Compute the position of the note using the delta T from the perfect timing.
            //Here we use the direction of the track given at delta T.
            //You can easily curve all your notes to any trajectory, not just straight lines, by customizing the TrackObjects.
            //Here the target position is found using the track object end position.
            var distance = deltaT * m_RhythmClipData.RhythmDirector.NoteSpeed;
            var targetSize = m_TimingOutlineEndSize;

            distance = Mathf.Max(-distance, 0);
            
            var newTimingSize = targetSize + Vector3.one * (m_OutlineSizeSpeed * distance);
            
            m_TimingOutline.localScale = newTimingSize;

            //Using those parameters we can easily compute the new position of the note at any time.
            var clipOffset = m_RhythmClipData.ClipParameters.Vector2Parameter;
            var notePosition = RhythmClipData.TrackObject.EndPoint.position + new Vector3(clipOffset.x,clipOffset.y,0);
            transform.position = notePosition;
            
            
        }
        
        
#if UNITY_EDITOR
        public virtual bool RhythmClipOnSceneGUIChange(UnityEditor.SceneView sceneView, RhythmClip mainSelectedClip,
            List<RhythmClip> selectedClips)
        {
            var rhythmClipData = mainSelectedClip.RhythmClipData;
            if (rhythmClipData.IsValid == false) { return false;}
            
            var trackObject = rhythmClipData.TrackObject;
            if (trackObject == null) { return false; }
            
            UnityEditor.EditorGUI.BeginChangeCheck();
            var clipOffset = mainSelectedClip.ClipParameters.Vector2Parameter;
            
            var noteOriginalPosition = trackObject.EndPoint.position + new Vector3(clipOffset.x,clipOffset.y,0);
            
            Vector3 newTargetPosition = UnityEditor.Handles.PositionHandle(noteOriginalPosition, Quaternion.identity);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                var deltaPos = newTargetPosition - noteOriginalPosition;

                if (deltaPos == Vector3.zero)
                {
                    return false;
                }
                
                foreach (var otherRhythmClip in selectedClips)
                {
                    UnityEditor.Undo.RecordObject(otherRhythmClip, "Change Target Position");
                    otherRhythmClip.ClipParameters.Vector2Parameter += new Vector2(deltaPos.x, deltaPos.y);
                }

                return false;
            }

            return false;
        }
#endif
    }
}