using System;
using UnityEngine;

[Serializable]
public struct PlayerHitResult
{
    public bool accepted;
    public bool causedDeath;
    public bool disruptsScoreChain;
    public PlayerControllerScript victim;
    public PlayerControllerScript attacker;
    public GameObject source;
    public float requestedScale01;
    public float appliedScale01;
    public float massLost01;
    public int victimLifeSequence;
    public Vector3 worldPosition;
}

[Serializable]
public struct PlayerDeathContext
{
    public PlayerControllerScript victim;
    public PlayerControllerScript killer;
    public GameObject source;
    public int victimLifeSequence;
    public Vector3 worldPosition;
}
