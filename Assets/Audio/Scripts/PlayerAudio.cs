using System;
using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Event IDs (assigned via inspector or Apply Suggested Defaults)")]
    [SerializeField] private AudioEventId spawnId;
    [SerializeField] private AudioEventId hitId;
    [SerializeField] private AudioEventId deathId;
    [SerializeField] private AudioEventId stunnedId;
    [SerializeField] private AudioEventId massNuggetId;
    [SerializeField] private AudioEventId parryId;
    [SerializeField] private AudioEventId shieldUpId;
    [SerializeField] private AudioEventId shieldBreakId;

    private void Play(AudioEventId id)
    {
        if (AudioSystem.I == null) return;
        AudioSystem.I.Play(id, transform.position);
    }

    public void PlaySpawn()        => Play(spawnId);
    public void PlayHit()          => Play(hitId);
    public void PlayDeath()        => Play(deathId);
    public void PlayStunned()      => Play(stunnedId);
    public void PlayMassNugget()   => Play(massNuggetId);
    public void PlayParry()        => Play(parryId);
    public void PlayShieldUp()     => Play(shieldUpId);
    public void PlayShieldBreak()  => Play(shieldBreakId);

    [ContextMenu("Apply Suggested Defaults (by name)")]
    private void ApplySuggestedDefaults()
    {
        // These will succeed after the enum is regenerated to include these names.
        TryParseAssign(ref spawnId, "Player_Spawn");
        TryParseAssign(ref hitId, "Player_Hit");
        TryParseAssign(ref deathId, "Player_Death");
        TryParseAssign(ref stunnedId, "Player_Stunned");
        TryParseAssign(ref massNuggetId, "Player_MassNugget");
        TryParseAssign(ref parryId, "Player_Parry");
        TryParseAssign(ref shieldUpId, "Player_ShieldUp");
        TryParseAssign(ref shieldBreakId, "Player_ShieldBreak");
    }

    private static void TryParseAssign(ref AudioEventId field, string name)
    {
        if (Enum.TryParse(name, out AudioEventId id))
            field = id;
    }
}