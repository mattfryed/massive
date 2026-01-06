using UnityEngine;

public class SfxQuickTest : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            AudioSystem.I?.Play2D(AudioEventId.Player_SwordAttack);
            Debug.Log("Test: Player_SwordAttack");
        }
    }
}