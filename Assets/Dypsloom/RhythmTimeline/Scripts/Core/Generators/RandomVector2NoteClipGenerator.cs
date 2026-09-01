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
    [CreateAssetMenu(fileName = "RandomVector2NoteClipGenerator", menuName="Dypsloom/Rhythm Timeline/Note Generator/RandomVector2NoteClipGenerator")]
    public class RandomVector2NoteClipGenerator : BasicNoteClipGenerator
    {
        public Vector2 MinValues;
        public Vector2 MaxValues;

        public override RhythmClip GenerateClip(RhythmTrack rhythmTrack, double clipStart, double clipDuration, NoteDefinition noteDefinition, GenerateClipOverrideOption overrideOption = GenerateClipOverrideOption.ClearPreviousNotes)
        {
            var rhythmClip = base.GenerateClip(rhythmTrack, clipStart, clipDuration, noteDefinition,overrideOption);
            
            rhythmClip.ClipParameters.Vector2Parameter = new Vector2(Random.Range(MinValues.x, MaxValues.x), Random.Range(MinValues.y, MaxValues.y));
            return rhythmClip;
            
        }
    }
}