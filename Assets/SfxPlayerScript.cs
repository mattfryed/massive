using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SfxPlayerScript : MonoBehaviour
{
    // List SFX a player is likely to use here
    public AudioClip testSFX;
    private AudioSource AudioS;

    void Start()
    {
        AudioS = GetComponent<AudioSource>();
    }

    void SafePlay(string sfxName)
    {
        switch (sfxName)
        {
            case "testSFX":
                if (testSFX)
                {
                    AudioS.PlayOneShot(testSFX);
                }
                break;
        }
        }

    }
