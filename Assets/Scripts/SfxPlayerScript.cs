using UnityEngine;

public class SfxPlayerScript : MonoBehaviour
{
    [Header("Legacy fallback clips (optional)")]
    // Keep these so nothing breaks in the inspector if they were already assigned.
    // They will ONLY be used if AudioSystem isn't available.
    public AudioClip testSFX;
    public AudioClip stunnedSFX;
    public AudioClip diedSFX;
    public AudioClip massNuggetSFX;
    public AudioClip struckSFX;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        // AudioSource is only for fallback. Your new system does not need it.
    }

    /// <summary>
    /// Legacy entrypoint used by existing gameplay code.
    /// We keep this signature so you don't have to rewrite gameplay right now.
    /// </summary>
    public void SafePlay(string sfxName)
    {
        // 1) Prefer the new centralized system (AudioDatabase -> AudioCue -> AudioSystem pool)
        if (AudioSystem.I != null && AudioSystem.I.database != null)
        {
            if (TryMapLegacyKeyToEventId(sfxName, out var id))
            {
                // Global SFX = 2D
                AudioSystem.I.Play2D(id);
                return;
            }

            // If a key comes in that we haven't mapped yet, do nothing (or log if you want).
            // Debug.LogWarning($"Unmapped SFX key: {sfxName}");
            return;
        }

        // 2) Fallback (only if AudioSystem isn't available)
        FallbackPlayOneShot(sfxName);
    }

    private bool TryMapLegacyKeyToEventId(string sfxName, out AudioEventId id)
    {
        // Map your EXISTING string keys to your centralized AudioEventId enum.
        // These enum values exist in your AudioEventId.cs (eg Player_SwordAttack, etc.)
        switch (sfxName)
        {
            case "struckSFX":
                id = AudioEventId.Player_SwordAttack;
                return true;

            case "massNuggetSFX":
                id = AudioEventId.Player_MassNugget;
                return true;

            case "diedSFX":
                id = AudioEventId.Player_Death;
                return true;

            case "stunnedSFX":
                id = AudioEventId.Player_Stunned;
                return true;

            case "testSFX":
                id = AudioEventId.Player_Charge;
                return true;

            default:
                id = default;
                return false;
        }
    }

    private void FallbackPlayOneShot(string sfxName)
    {
        if (_audioSource == null) return;

        switch (sfxName)
        {
            case "testSFX":
                if (testSFX) _audioSource.PlayOneShot(testSFX);
                break;

            case "stunnedSFX":
                if (stunnedSFX) _audioSource.PlayOneShot(stunnedSFX);
                break;

            case "diedSFX":
                if (diedSFX) _audioSource.PlayOneShot(diedSFX);
                break;

            case "massNuggetSFX":
                if (massNuggetSFX) _audioSource.PlayOneShot(massNuggetSFX);
                break;

            case "struckSFX":
                if (struckSFX) _audioSource.PlayOneShot(struckSFX);
                break;
        }
    }
}