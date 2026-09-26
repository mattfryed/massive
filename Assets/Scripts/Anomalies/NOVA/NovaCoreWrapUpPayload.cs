using System;
using UnityEngine;

[Serializable]
public sealed class NovaCoreWrapUpPayload
{
    public int lightTeamIndex;
    public int darkTeamIndex;

    public int lightParticles;
    public int darkParticles;

    public float lightMass;
    public float darkMass;

    // Exact receipts from accepted match-service awards; never a second score balance.
    public long lightAwardedMilliElectronVolts;
    public long darkAwardedMilliElectronVolts;
    public bool universalScoringApplied;

    public float TotalMass => lightMass + darkMass;
}
