/// ---------------------------------------------
/// Rhythm Timeline
/// Copyright (c) Dyplsoom. All Rights Reserved.
/// https://www.dypsloom.com
/// ---------------------------------------------

namespace Dypsloom.RhythmTimeline.Core.Playables
{
    using UnityEngine;
    using UnityEngine.Playables;
    using System;
    using Dypsloom.RhythmTimeline.Core.Notes;

    [Serializable]
    public class RhythmBehaviour : PlayableBehaviour
    {
        [Tooltip("The note definition.")]
        [SerializeField] protected NoteDefinition m_NoteDefinition;

        public NoteDefinition NoteDefinition => m_NoteDefinition;

        // These are populated by the Timeline system / clips.
        public RhythmClip RhythmClip { get; set; }

        // NOTE: RhythmClipData is NOT nullable. Avoid comparing it to null.
        public RhythmClipData RhythmClipData
        {
            get
            {
                // Guard: Timeline can call behaviour methods before RhythmClip is assigned in some editor flows.
                if (RhythmClip == null) { return default; }
                return RhythmClip.RhythmClipData;
            }
        }

        protected bool m_IsNoteSpawned;
        protected Note m_Note;
        protected Note m_NotePrefab;

        protected bool m_MissingDefinition;

        public override void OnPlayableCreate(Playable playable)
        { }

        public void SetNoteDefinition(NoteDefinition noteDefinition)
        {
            m_NoteDefinition = noteDefinition;
        }

        /// <summary>
        /// Takes care of validating references when the graph starts.
        /// Timeline preview/edit-time can call this with incomplete data; be defensive.
        /// </summary>
        public override void OnGraphStart(Playable playable)
        {
            // Determine whether we have a valid NoteDefinition + prefab + Note component.
            m_MissingDefinition = true;
            m_NotePrefab = null;

            if (m_NoteDefinition == null)
            {
                Debug.LogWarning("RhythmBehaviour: Missing NoteDefinition on a RhythmClip. (Clip will be ignored until assigned.)");
                return;
            }

            if (m_NoteDefinition.NotePrefab == null)
            {
                Debug.LogWarning($"RhythmBehaviour: NoteDefinition '{m_NoteDefinition.name}' has no NotePrefab assigned.");
                return;
            }

            var noteComponent = m_NoteDefinition.NotePrefab.GetComponent<Note>();
            if (noteComponent == null)
            {
                Debug.LogWarning($"RhythmBehaviour: NoteDefinition '{m_NoteDefinition.name}' prefab '{m_NoteDefinition.NotePrefab.name}' is missing a Note component.");
                return;
            }

            // All good.
            m_NotePrefab = noteComponent;
            m_MissingDefinition = false;
        }

        protected virtual void SpawnNote()
        {
            if (m_MissingDefinition) { return; }
            if (m_IsNoteSpawned) { return; }

            // Guard: clip can be null in certain editor preview situations.
            if (RhythmClip == null) { return; }

            var clipData = RhythmClipData;

            // Guard: bindings / director / processor may not exist yet.
            if (clipData.RhythmDirector == null) { return; }
            if (clipData.RhythmDirector.RhythmProcessor == null) { return; }

            m_Note = clipData.RhythmDirector.RhythmProcessor.CreateNewNote(m_NoteDefinition, RhythmClip);
            m_IsNoteSpawned = (m_Note != null);
        }

        protected virtual void RemoveNote()
        {
            if (!m_IsNoteSpawned) { return; }
            if (m_Note == null)
            {
                m_IsNoteSpawned = false;
                return;
            }

            // Guard: clip can be null in certain editor preview situations.
            if (RhythmClip == null)
            {
                m_IsNoteSpawned = false;
                m_Note = null;
                return;
            }

            var clipData = RhythmClipData;
            if (clipData.RhythmDirector == null || clipData.RhythmDirector.RhythmProcessor == null)
            {
                // Can't route destroy safely; just drop the reference.
                m_IsNoteSpawned = false;
                m_Note = null;
                return;
            }

            clipData.RhythmDirector.RhythmProcessor.DestroyNote(m_Note);
            m_IsNoteSpawned = false;
            m_Note = null;
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (!m_IsNoteSpawned) { return; }
            if (m_Note == null) { return; }

            m_Note.OnClipStart();
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (!Application.isPlaying) { return; }
            if (!m_IsNoteSpawned) { return; }
            if (m_Note == null) { return; }

            var duration = playable.GetDuration();
            var time = playable.GetTime();
            var count = time + info.deltaTime;

            if ((info.effectivePlayState == PlayState.Paused && count > duration) ||
                Mathf.Approximately((float)time, (float)duration))
            {
                m_Note.OnClipStop();
            }
        }

        public void MixerProcessFrame(Playable thisPlayable, FrameData info, object playerData, double timelineCurrentTime)
        {
            if (m_MissingDefinition) { return; }
            if (RhythmClip == null) { return; }

#if UNITY_EDITOR
            // Update the BPM in case it was changed in the inspector.
            var cd = RhythmClipData;
            if (cd.RhythmDirector != null)
            {
                cd.RhythmDirector.RefreshBpm();
            }
#endif

            var clipData = RhythmClipData;

            // Hard guard: if director/processor isn't wired yet, do nothing.
            if (clipData.RhythmDirector == null) { return; }
            if (clipData.RhythmDirector.RhythmProcessor == null) { return; }

            // Another guard: Note prefab cache might still be missing if definition was fixed after graph start.
            if (m_NotePrefab == null)
            {
                var def = m_NoteDefinition;
                if (def != null && def.NotePrefab != null)
                {
                    m_NotePrefab = def.NotePrefab.GetComponent<Note>();
                }

                if (m_NotePrefab == null) { return; }
            }

            /* Calculate the clip time starting from the actual Timeline time
               the only reason why we need this is because we need it to be able to be negative or past the clip's duration,
               so we can handle bullets also after the clip ends
               thisPlayable.GetTime() only gives time constrained to the clip duration */

            var globalClipStartTime = timelineCurrentTime - clipData.ClipStart;
            var globalClipEndTime = timelineCurrentTime - clipData.ClipEnd;

            var timeRange = clipData.RhythmDirector.SpawnTimeRange;
            var offsets = new Vector2(m_NotePrefab.NoteClipStartOffset, m_NotePrefab.NoteClipEndOffset);

            if (!(globalClipStartTime >= -timeRange.x - offsets.x) || !(globalClipEndTime < timeRange.y + offsets.y))
            {
                // hide note.
                if (m_IsNoteSpawned) { RemoveNote(); }
                return;
            }

            // show note.
            if (!m_IsNoteSpawned) { SpawnNote(); }

            if (m_IsNoteSpawned && m_Note != null)
            {
                m_Note.TimelineUpdate(globalClipStartTime, globalClipEndTime);
            }
        }

        public override void OnGraphStop(Playable playable)
        { }

        // Takes care of destroying the GameObject, if it still exists
        public override void OnPlayableDestroy(Playable playable)
        {
            RemoveNote();
        }
    }
}