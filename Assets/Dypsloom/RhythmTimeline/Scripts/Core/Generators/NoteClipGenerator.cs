namespace Dypsloom.RhythmTimeline.Core.Generators
{
    using System;
    using System.Collections.Generic;
    using Dypsloom.RhythmTimeline.Core.Notes;
    using Dypsloom.RhythmTimeline.Core.Playables;
    using UnityEngine;
    using UnityEngine.Serialization;
    using UnityEngine.Timeline;

    [Serializable]
    public enum GenerateClipOverrideOption
    {
        ClearPreviousNotes,
        AddOnTop,
        ReplaceOnOverlap,
        DontReplaceOnOverlap
    }
    
    /// <summary>
    /// This object is used to generate a note clips inside the rhythm timeline.
    /// Inherit this class to make your own note clip generator.
    /// You can use this generator in the Midi to Rhythm Timeline tool.
    /// </summary>
    public abstract class NoteClipGenerator : ScriptableObject
    {
        protected List<TimelineClip> m_CachedClipsInRange = new List<TimelineClip>();

        public virtual void GenerateNotes(RhythmTimelineAsset rhythmTimelineAsset, int trackID)
        {
            if (rhythmTimelineAsset.TryGetRhythmTackWithID(trackID, out var track)) {
                GenerateNotes(track);
            }else{
                Debug.LogError("Track not found");
            }
        }
        
        [ContextMenu("Generate Notes")]
        public abstract void GenerateNotes();
        public abstract void GenerateNotes(RhythmTrack rhythmTrack);

        protected virtual void GenerateClips(RhythmTrack rhythmTrack, NoteDefinition noteDefinition, 
            double startTime, double endTime, double step, double noteLength, double noteSpacing,
            GenerateClipOverrideOption overrideOption = GenerateClipOverrideOption.ClearPreviousNotes)
        {
            var stepCount = (endTime - startTime) / (step * (noteSpacing + noteLength));
            
            //fill notes by beat spacing
            for (double i = 0; i < stepCount; i++) {
                var clipStart = (step * i * (noteSpacing + noteLength)) + startTime;
                var clipDuration = noteLength * step;
                
                var clips = GetClipsInRange(rhythmTrack, clipStart, clipStart + clipDuration);
                
                if (clips.Count != 0) {
                    bool skip = false;
                    
                    switch (overrideOption) {
                        
                        case GenerateClipOverrideOption.ClearPreviousNotes:
                            for (int j = 0; j < clips.Count; j++) {
                                rhythmTrack.timelineAsset.DeleteClip(clips[j]);
                            }
                            break;
                        case GenerateClipOverrideOption.AddOnTop:
                            break;
                        case GenerateClipOverrideOption.ReplaceOnOverlap:
                            for (int j = 0; j < clips.Count; j++) {
                                rhythmTrack.timelineAsset.DeleteClip(clips[j]);
                            }
                            break;
                        case GenerateClipOverrideOption.DontReplaceOnOverlap:
                            skip = true;
                            break;
                    }
                    if (skip) {
                        continue;
                    }
                    
                    for (int j = 0; j < clips.Count; j++) {
                        rhythmTrack.timelineAsset.DeleteClip(clips[j]);
                    }
                }
                
                GenerateClip(rhythmTrack, clipStart, clipDuration, noteDefinition,overrideOption);
            }
        }

        
        
        public virtual RhythmClip GenerateClip(RhythmTrack rhythmTrack, double clipStart, double clipDuration,
            NoteDefinition noteDefinition, GenerateClipOverrideOption overrideOption = GenerateClipOverrideOption.ClearPreviousNotes)
        {
            var newClip = rhythmTrack.CreateClip<RhythmClip>();
            //By default set the name to an hidden character
            newClip.displayName = " ";
            newClip.start = clipStart;
            newClip.duration = clipDuration;
            
            
            var rhythmClip = newClip.asset as RhythmClip;
            if (rhythmClip == null) {
                Debug.LogError("Rhythm Clip is null");
                return null;
            }

            rhythmClip.SetNoteDefinition(noteDefinition);
            return rhythmClip;
        }

        public List<TimelineClip> GetClipsInRange(RhythmTrack rhythmTrack, double start, double end)
        {
            m_CachedClipsInRange.Clear();
            
            var clips = rhythmTrack.GetClips();
 
            foreach (var clip in clips)
            {
                if (clip.end < start || clip.start > end) {
                    continue;
                }
                m_CachedClipsInRange.Add(clip);
            }

            return m_CachedClipsInRange;
        }
    }
}