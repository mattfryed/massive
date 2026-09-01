using UnityEngine;

public class AudioDebugHotkeys : MonoBehaviour
{
    public AudioEventId testEvent = AudioEventId.Player_Charge; // change to your sword event

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            if (AudioSystem.I == null)
            {
                Debug.LogError("AudioSystem.I is null (no AudioSystem alive).");
                return;
            }

            Debug.Log($"Playing test SFX: {testEvent} (db null? {AudioSystem.I.database == null})");
            AudioSystem.I.Play2D(testEvent);
        }
    }
}