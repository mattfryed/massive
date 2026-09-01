using Dypsloom.RhythmTimeline.Core.Input;
using Dypsloom.RhythmTimeline.Core.Notes;
using Dypsloom.RhythmTimeline.Core.Playables;
using UnityEngine;

namespace Dypsloom.RhythmTimeline.Demos.OSU.Scripts
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.Events;


    /// <summary>
    /// A OSU note with a Spline. Hold the note down and follow the spline released at an end point.
    /// The inner spline shape controller also controls the outline spline shape controller.
    /// </summary>
    public class OSUSplineNote : OSUNoteBase, INoteOnSceneGUIChange
    {
        [Header("OSU Spline Settings")]
        
        [Tooltip("Should the hold target come back from end to start?")]
        [SerializeField] protected bool m_PingPong = false;
        [Tooltip("Should the hold target come back from end to start?")]
        [SerializeField] protected GameObject m_EnableIfPingPong;
        [Tooltip("Should the hold target come back from end to start?")]
        [SerializeField] protected GameObject m_DisableIfPingPong;
        
        [Header("Sprite Shape Splines")]
        [Tooltip("The main spline that controls the additional splines")]
        [SerializeField] protected SpriteShapeSplineData m_MainSpline = new SpriteShapeSplineData(1f);
        
        [Header("Moving Point")]
        [Tooltip("The transform that follow the the spline, which the player need to hold over time. ")]
        [SerializeField] protected Transform m_MovingHoldTransform;
        [Tooltip("The transform that follow the the spline, should follow the spline tangent? ")]
        [SerializeField] protected bool m_MovingHoldMatchSplineOrientation;
        [Tooltip("The sprite renderer for the moving part.")]
        [SerializeField] protected SpriteRenderer m_MovingHoldSpriteRenderer;
        
        [Header("End Point")]  
        [Tooltip("The note End, automatically placed at the end of the spline.")]
        [SerializeField] protected Transform m_NoteEnd;
        [Tooltip("The note end sprite renderer use for coloring.")]
        [SerializeField] protected SpriteRenderer m_NoteEndSpriteRenderer;
        
        
        [Header("Other Settings")]
        [Tooltip("The hold not will automatically release when it reaches perfect.")]
        [SerializeField] protected bool m_AutoPerfectRelease;
        [Tooltip("Destroy the not if it was missed, if not the note will still be holdable to get some points at the release.")]
        [SerializeField] protected bool m_RemoveNoteIfMissed = true;

        [Header("Events")]
        [Tooltip("Event when the player starts holding the note")]
        [SerializeField] protected UnityEvent m_OnStartHold;
        [Tooltip("Event when the player starts holding the note")]
        [SerializeField] protected UnityEvent m_OnStopHold;
        
        
        protected bool m_Holding;
        protected Color m_StartLineColor;

        protected double m_StartHoldTimeOffset;
        
        SpriteShapeControllerSynchronizer m_SpriteShapeControllerSynchronizer;
        
        
        /// <summary>
        /// This function returns the RhythmClipExtraNoteData Type to assign to the RhythmClip when added to the timeline.
        /// </summary>
        public override Type RhythmClipExtraNoteDataType => typeof(OsuSplineNoteRhythmClipExtraData);

        public bool PingPong { get => m_PingPong; set => m_PingPong = value; }
        public SpriteShapeSplineData MainSpline { get => m_MainSpline; set => m_MainSpline = value; }

        private void OnValidate()
        {
            if (m_EnableIfPingPong != null) {
                m_EnableIfPingPong.SetActive(m_PingPong);
            }

            if (m_DisableIfPingPong != null) {
                m_DisableIfPingPong.SetActive(!m_PingPong);
            }
        }

        /// <summary>
        /// Initialize the note.
        /// </summary>
        /// <param name="rhythmClipData">The rhythm Clip Data.</param>
        public override void Initialize(RhythmClipData rhythmClipData)
        {
            base.Initialize(rhythmClipData);

            if (m_SpriteShapeControllerSynchronizer == null) {
                m_SpriteShapeControllerSynchronizer = GetComponentInChildren<SpriteShapeControllerSynchronizer>();
            }
            m_Holding = false;
            
            m_StartHoldTimeOffset = 0;

            if (rhythmClipData.ClipParameters
                    .RhythmClipExtraNoteData is OsuSplineNoteRhythmClipExtraData extraNoteData) {
                
                PingPong = extraNoteData.PingPong;
                SpriteShapeControllerSynchronizer.SyncSplineData(extraNoteData.spline, m_MainSpline.ShapeController.spline);
                m_MainSpline.ShapeController.BakeMesh();

                if (m_SpriteShapeControllerSynchronizer != null) {
                    m_SpriteShapeControllerSynchronizer.ForceDraw();
                }
            }
            
            if (m_EnableIfPingPong != null) {
                m_EnableIfPingPong.SetActive(m_PingPong);
            }

            if (m_DisableIfPingPong != null) {
                m_DisableIfPingPong.SetActive(!m_PingPong);
            }
        }

        protected override void InitializeNoteVisuals(RhythmClipData rhythmClipData)
        {
            base.InitializeNoteVisuals(rhythmClipData);
            if (m_SetColorPerTrack) {
                if (m_MainSpline.ShapeRenderer != null) {
                    m_MainSpline.ShapeRenderer.color = rhythmClipData.TrackObject.PrimaryColor;
                }

                if (m_NoteEndSpriteRenderer != null) {
                    m_NoteEndSpriteRenderer.color = rhythmClipData.TrackObject.PrimaryColor;
                }

                if (m_MovingHoldSpriteRenderer != null) {
                    m_MovingHoldSpriteRenderer.color = rhythmClipData.TrackObject.PrimaryColor;
                }
            }
            
        }

        /// <summary>
        /// Trigger an input on the note. Detect both tap and release inputs.
        /// </summary>
        /// <param name="inputEventData">The input event data.</param>
        public override void OnTriggerInput(InputEventData inputEventData)
        {
            if (inputEventData.Tap) {
                if (m_Holding == false) {
                    m_OnStartHold?.Invoke();
                }
                
                m_Holding = true;
            
                var perfectTime = m_RhythmClipData.RhythmDirector.HalfCrochet;
                var timeDifference = TimeFromActivate - perfectTime;

                m_StartHoldTimeOffset = timeDifference;
            }

            if (m_Holding && inputEventData.Release) {
            
                m_OnStopHold?.Invoke();
                gameObject.SetActive(false);
                m_IsTriggered = true;
            
                var perfectTime = m_RhythmClipData.RhythmDirector.HalfCrochet;
                var timeDifference = TimeFromDeactivate + perfectTime;

                var averageTotalTimeDifference = (m_StartHoldTimeOffset + timeDifference)/2f;
                var timeDifferencePercentage =  Mathf.Abs((float)(100f*averageTotalTimeDifference)) / perfectTime;
                
                InvokeNoteTriggerEvent(inputEventData, timeDifference, (float) timeDifferencePercentage);
                RhythmClipData.TrackObject.RemoveActiveNote(this);
            }
        
        }
    
        /// <summary>
        /// Hybrid update works both in play and edit mode.
        /// </summary>
        /// <param name="timeFromStart">The offset before the start.</param>
        /// <param name="timeFromEnd">The offset before the end.</param>
        protected override void HybridUpdate(double timeFromStart, double timeFromEnd)
        {
            var deltaTStart = (float)(timeFromStart - m_RhythmClipData.RhythmDirector.HalfCrochet);
            var deltaTEnd =  (float)(timeFromEnd + m_RhythmClipData.RhythmDirector.HalfCrochet);

            //Compute the position of the note using the delta T from the perfect timing.
            //Here we use the direction of the track given at delta T.
            //You can easily curve all your notes to any trajectory, not just straight lines, by customizing the TrackObjects.
            //Here the target position is found using the track object end position.
            var distance = deltaTStart * m_RhythmClipData.RhythmDirector.NoteSpeed;
            var targetSize = m_TimingOutlineEndSize;

            distance = Mathf.Max(-distance, 0);
            
            var newTimingSize = targetSize + Vector3.one * (m_OutlineSizeSpeed * distance);
            
            m_TimingOutline.localScale = newTimingSize;

            //Using those parameters we can easily compute the new position of the note at any time.
            var clipOffset = m_RhythmClipData.ClipParameters.Vector2Parameter;
            var originPosition = RhythmClipData.TrackObject.EndPoint.position + new Vector3(clipOffset.x,clipOffset.y,0);
            transform.position = originPosition;
            
            
            
            if(Application.isPlaying && (m_ActiveState == ActiveState.PostActive || m_ActiveState == ActiveState.Disabled)){return;}
            
            
            
            GetNotePositionAndTangent(deltaTStart, deltaTEnd, m_PingPong, out var notePosition, out var tangent);
            
            m_MovingHoldTransform.position = notePosition;
            if (m_MovingHoldMatchSplineOrientation) {
                m_MovingHoldTransform.right = tangent;
            }
           

            if (m_Holding == false) {

                if (Application.isPlaying) {
                    if (m_RemoveNoteIfMissed && timeFromStart > m_RhythmClipData.RhythmDirector.Crochet) {
                        //Force a miss.
                        DeactivateNote();
                        gameObject.SetActive(false);
                    }
                }
            }

            if (m_AutoPerfectRelease && Application.isPlaying && m_Holding && deltaTEnd > 0) {
                //Trigger a release input within code.
                OnTriggerInput(new InputEventData(RhythmClipData.TrackID,1));
                DeactivateNote();
            }
        }
    
        /// <summary>
        /// Get the position of the Note for the delta time.
        /// </summary>
        /// <param name="deltaT">The delta time.</param>
        /// <returns>The position of the note.</returns>
        protected void GetNotePositionAndTangent(float deltaTStart, float deltaTEnd, bool pingPong, out Vector3 notePosition, out Vector3 tangentPosition)
        {
            var mainSpline = m_MainSpline.ShapeController.spline;
            var splinePos = Vector3.zero;
            
            if (deltaTStart <= 0) 
            {
                splinePos = mainSpline.GetPosition(0);
                tangentPosition = mainSpline.GetRightTangent(0);
            }
            else if (deltaTEnd > 0) 
            {
                if (pingPong) {
                    splinePos = mainSpline.GetPosition(0);
                    tangentPosition = mainSpline.GetRightTangent(0);
                } else {
                    splinePos = mainSpline.GetPosition(mainSpline.GetPointCount()-1);
                    tangentPosition = -mainSpline.GetLeftTangent(mainSpline.GetPointCount()-1);
                }
               
            }
            else 
            {
                var normalizedT = 1f - (deltaTEnd / (deltaTEnd - deltaTStart));
                
                if (pingPong) {
                    normalizedT *= 2f;
                    if (normalizedT > 1f) {
                        normalizedT = 2f - normalizedT;
                    }
                    mainSpline.Evaluate(normalizedT, out splinePos, out tangentPosition, out var upVector);
                    tangentPosition = -tangentPosition;
                } else {
                    mainSpline.Evaluate(normalizedT, out splinePos, out tangentPosition, out var upVector);
                }

               
            }
            
            var shapeControllerTransform = m_MainSpline.ShapeController.transform;
            var worldPos =  shapeControllerTransform.position + Vector3.Scale(splinePos,shapeControllerTransform.localScale);
        
            notePosition = worldPos;
            return;
        }
        
#if UNITY_EDITOR
        public bool OnSceneGUIChange(UnityEditor.SceneView sceneView)
        {
            if (m_RhythmClipData.IsValid == false) { return false;}
            
            if (m_RhythmClipData.ClipParameters
                    .RhythmClipExtraNoteData is OsuSplineNoteRhythmClipExtraData extraNoteData) {
                
                extraNoteData.PingPong = PingPong;
                SpriteShapeControllerSynchronizer.SyncSplineData(m_MainSpline.ShapeController.spline, extraNoteData.spline);
               
                UnityEditor.EditorUtility.SetDirty(extraNoteData);
            }

            return false;
        }
        
        public override bool RhythmClipOnSceneGUIChange(UnityEditor.SceneView sceneView, RhythmClip mainSelectedClip,
            List<RhythmClip> selectedClips)
        {
           var change = base.RhythmClipOnSceneGUIChange(sceneView, mainSelectedClip, selectedClips);
           
           if (m_RhythmClipData.ClipParameters
                   .RhythmClipExtraNoteData is OsuSplineNoteRhythmClipExtraData extraNoteData) {
                
               extraNoteData.PingPong = PingPong;
               SpriteShapeControllerSynchronizer.SyncSplineData(m_MainSpline.ShapeController.spline, extraNoteData.spline);
               
               UnityEditor.EditorUtility.SetDirty(extraNoteData);
           }
           return change;
        }
        
#endif


        
    }
}