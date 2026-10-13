namespace Massive.Emergence
{
    /// <summary>Shared mass/score rules with a level-owned bounded pool.</summary>
    public sealed class EmergenceMassNugget : MatterNuggetScript
    {
        protected override void FinishDespawn() => gameObject.SetActive(false);
    }
}
