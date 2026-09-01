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

    public float TotalMass => lightMass + darkMass;
}
