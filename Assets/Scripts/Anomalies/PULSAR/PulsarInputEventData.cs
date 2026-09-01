// NOTE: If your compiler can't find these types, adjust the namespace to match
// Rhythm Timeline 2 in your project (often Dypsloom.RhythmTimeline).
using Dypsloom.RhythmTimeline;
using Dypsloom.RhythmTimeline.Core.Playables;
    using Dypsloom.RhythmTimeline.Core.Notes;
    using Dypsloom.RhythmTimeline.Core.Input;




public enum PulsarPromptType
{
    Sword = 0,
    Shield = 1,
    Both = 2
}

public class PulsarInputEventData : InputEventData
{
    public int PlayerIndex;
    public double DspTime;
    public PulsarPromptType AttemptType;
}
