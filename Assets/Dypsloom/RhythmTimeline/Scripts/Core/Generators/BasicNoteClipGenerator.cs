namespace Dypsloom.RhythmTimeline.Core.Generators
{
    using Dypsloom.RhythmTimeline.Core.Notes;
    using Dypsloom.RhythmTimeline.Core.Playables;
    using UnityEngine;

    /// <summary>
    /// This object is used to generate a note clips inside the rhythm timeline.
    /// Inherit this class to make your own note clip generator.
    /// You can use this generator in the Midi to Rhythm Timeline tool.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicNoteClipGenerator", menuName="Dypsloom/Rhythm Timeline/Note Generator/BasicNoteClipGenerator")]
    public class BasicNoteClipGenerator : NoteClipGenerator
    {
        public RhythmTimelineAsset RhythmTimelineAsset;
        public int TrackID;
        public NoteDefinition NoteDefinition;
        public GenerateClipOverrideOption OverrideOption = GenerateClipOverrideOption.ClearPreviousNotes;
        public double bpm = 120;
        [Tooltip("Set to -1 to generate nodes with for each beat of the bpm")]
        public double step = -1;
        public double noteSpacing = 0;
        public double noteLength = 0.1f;
        public double startTime = 0;
        public double endTime = 100;

        public override void GenerateNotes()
        {
            GenerateNotes(RhythmTimelineAsset, TrackID);
        }

        public override void GenerateNotes(RhythmTrack rhythmTrack)
        {
            var localStep = step;
            if (localStep < 0) {
                localStep = 60d / bpm;
            }

            if (startTime < 0) {
                startTime = 0;
            }
            if (startTime > endTime)
            {
                Debug.LogError("End Time cannot be smaller than the start time.");
                return;
            }
            if (noteSpacing < 0)
            {
                Debug.LogError("NoteSpacing cannot be negative");
                return;
            }
            if (noteLength < 1)
            {
                Debug.LogError("Length cannot be 0 or negative");
                return;
            }
            
            GenerateClips(rhythmTrack, NoteDefinition, startTime, endTime, localStep, noteLength, noteSpacing, OverrideOption);
        }
    }
}