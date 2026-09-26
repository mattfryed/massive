namespace Massive.Scoring
{
    /// <summary>Optional level sequence between regulation expiry and match resolution.</summary>
    public interface IMatchFinale
    {
        void BeginMatchFinale();
    }
}
