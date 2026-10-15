namespace Massive.Enemies
{
    /// <summary>Feedback cadence is independent of damage source, health and scoring.</summary>
    public enum EnemyDamageDelivery { Impact = 0, Sustained = 1 }

    public enum EnemyDamageSource
    {
        Unknown = 0,
        Sword = 1,
        Projectile = 2,
        ShieldReflect = 3,
        Environmental = 4,
        LifetimeExpired = 5,
        Repulsor = 6,
        AmplifierShot = 7,
        Decoherence = 8,
    }
}
