namespace Massive.Resonance
{
    /// <summary>An optional presentation on a VectorGridGPU may consume Resonance
    /// attraction instead of injecting it into the spring simulation.</summary>
    public interface IResonanceGridPresentation
    {
        // Return false to retain the ordinary simulated response. Called before
        // late-order grid presentations draw; samples are valid for this frame only.
        bool TryPresentResonance(ResonancePatternController pattern);
    }
}
