namespace Dypsloom.RhythmTimeline.Demos.OSU.Scripts
{
    using System;
    using Dypsloom.RhythmTimeline.Core.Input;
    using Dypsloom.RhythmTimeline.Core.Managers;
    using Dypsloom.RhythmTimeline.Core.Notes;
    using UnityEngine;

    public class OSURhythmInputEventData : InputEventData
    {
        public int KeyInputID;
        public RaycastHit2D RaycastHit;
    }
    
    /// <summary>
    /// OSU Rhythm Input Manager will use the mouse position to send a raycast to find the note when the button is clicked
    /// </summary>
    public class OSURhythmInputManager : MonoBehaviour
    {
        [Tooltip("The Rhythm Processor.")]
        [SerializeField] protected RhythmProcessor m_RhythmProcessor;
        [Tooltip("The Note Collider layer mask to detect with the raycast")]
        [SerializeField] protected LayerMask m_NoteColliderLayerMask = 0;
        [Tooltip("Inputs used to know when to raycast to find notes under the mouse.")]
        [SerializeField] protected SimpleInputActionKey[] m_Inputs = Array.Empty<SimpleInputActionKey>();

        private OSURhythmInputEventData[] m_InputEventDataArrays;

        private Camera m_Camera;
        //If you have a lot of overlapping notes you may want to change this limit to more than 10.
        private RaycastHit2D[] m_CachedRaycastHits = new RaycastHit2D[10];
        
        private void Awake()
        {
            m_Camera = Camera.main;
            
            m_InputEventDataArrays = new OSURhythmInputEventData[m_Inputs.Length];
            for (var i = 0; i < m_InputEventDataArrays.Length; i++) {
                m_InputEventDataArrays[i] = new OSURhythmInputEventData();
                m_InputEventDataArrays[i].KeyInputID = i;
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < m_Inputs.Length; i++) {
                var input = m_Inputs[i];
                input.Enable();
            }
        }
        
        private void OnDisable()
        {
            for (int i = 0; i < m_Inputs.Length; i++) {
                var input = m_Inputs[i];
                input.Disable();
            }
        }

        private void Update()
        {
            for (int i = 0; i < m_Inputs.Length; i++) {
                var input = m_Inputs[i];
                
                if (input.GetInputDown()) {
                    var inputEventData = m_InputEventDataArrays[i];

                    if (TryGetMouseRaycastAndNote(out var raycastHit, out var note)) {
                        if (TryPrepareEventFor(raycastHit, inputEventData) == false) { continue; }

                        inputEventData.InputID = 0;
                        TriggerInput(inputEventData);
                    }
                }
                
                if (input.GetInputUp()) {
                    var inputEventData = m_InputEventDataArrays[i];
                    
                    if (TryGetMouseRaycastAndNote(out var raycastHit, out var note)) {
                        if (TryPrepareEventFor(raycastHit, inputEventData) == false) { continue; }

                        inputEventData.InputID = 1;
                        TriggerInput(inputEventData);
                    }
                }
            }
        }

        public bool TryGetMouseRaycastAndNote(out RaycastHit2D raycastHit2D, out Note note)
        {
            
            Vector3 mousePosition = Input.mousePosition;
            
            //must specify a depth for screen to world point to work.
            mousePosition.z = 10f;

            // Convert the mouse position to world space
            mousePosition = m_Camera.ScreenToWorldPoint(mousePosition);

            // Set the z-coordinate to 0 (since we are working in 2D)
            mousePosition.z = 0;
            
            // Perform a raycast from the mouse position in the world
            var numberOfHits = Physics2D.RaycastNonAlloc(mousePosition, Vector2.zero,m_CachedRaycastHits, 100, m_NoteColliderLayerMask);

            if (numberOfHits <= 0) {
                note = null;
                raycastHit2D = new RaycastHit2D();
                return false;
            }
            
            // In case some notes overlap we need to find the best match.
            raycastHit2D = m_CachedRaycastHits[0];
            TryGetNoteFromRayCast(raycastHit2D, out note);
            for (int i = 0; i < numberOfHits; i++) {
                var otherRaycastHit2D = m_CachedRaycastHits[i];
                Note otherNote = null;
                
                // Couldn't find note.
                if (TryGetNoteFromRayCast(otherRaycastHit2D, out otherNote) == false) {
                    continue;
                }

                if (note == null) {
                    raycastHit2D = otherRaycastHit2D;
                    note = otherNote;
                    continue;
                }
                
                
                // Check which note appeared first.
                if (note.RhythmClipData.ClipStart > otherNote.RhythmClipData.ClipStart) {
                    raycastHit2D = otherRaycastHit2D;
                    note = otherNote;
                }
            }

            if (note == null) {
                return false;
            }

            return raycastHit2D;
        }

        private bool TryGetNoteFromRayCast(RaycastHit2D raycastHit, out Note note)
        {
            note = null;
            if (raycastHit.collider == null) {
                return false;
            }
            
            if (raycastHit.rigidbody == null) {
                note = raycastHit.collider.GetComponentInParent<Note>();
                return note != null;
            }
            
            note = raycastHit.rigidbody.GetComponent<Note>();
            if (note != null) {
                return true;
            }
            
            note = raycastHit.rigidbody.GetComponentInParent<Note>();
            return note != null;
        }

        private bool TryPrepareEventFor(RaycastHit2D raycastHit, OSURhythmInputEventData osuEventData)
        {
            if (TryGetNoteFromRayCast(raycastHit, out var note) == false) {
                return false;
            }
            
            osuEventData.TrackID = note.RhythmClipData.TrackID;
            osuEventData.Note = note;
            osuEventData.RaycastHit = raycastHit;

            return true;
        }

        protected virtual void TriggerInput(InputEventData trackInputEventData)
        {
            m_RhythmProcessor.TriggerInput(trackInputEventData);
        }
    }
}