using UnityEngine;

[CreateAssetMenu(menuName = "MASSIVE/Audio/Player SFX Set", fileName = "PlayerSfxSet")]
public class PlayerSfxSet : ScriptableObject
{
    public AudioCue spawn;
    public AudioCue death;
    public AudioCue hit;
    public AudioCue parry;
    public AudioCue shieldUp;
    public AudioCue shieldBreak;
}
