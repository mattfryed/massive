namespace Dypsloom.RhythmTimeline.Core.Input
{
    using Dypsloom.RhythmTimeline.Core.Notes;
    using UnityEngine;

    /// <summary>
    /// Input event data tracks the input type.
    /// </summary>
    public class InputEventData
    {
        public Note Note;
        public int InputID;
        public int TrackID;
        public Vector2 Direction;

        public virtual bool Tap => InputID == 0;
        public virtual bool Release => InputID == 1;
        public virtual bool Swipe => InputID == 2;
        
        public InputEventData() { }

        public InputEventData(int trackID, int inputID)
        {
            TrackID = trackID;
            InputID = inputID;
        }

    }
}