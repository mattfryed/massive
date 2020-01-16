using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SfxPlayerScript : MonoBehaviour
{
    // List SFX a player is likely to use here
    public AudioClip testSFX;
    public AudioClip stunnedSFX;
    public AudioClip diedSFX;
    public AudioClip massNuggetSFX;
    public AudioClip struckSFX;

    private AudioSource AudioS;

    void Start()
    {
        AudioS = GetComponent<AudioSource>();
    }

    // This is kinda dumb to do it this way but w/e
    public void SafePlay(string sfxName)
    {
        switch (sfxName)
        {
            case "testSFX":
                if (testSFX)
                {
                    AudioS.PlayOneShot(testSFX);
                }
                break;
            case "stunnedSFX":
                if (stunnedSFX)
                {
                    AudioS.PlayOneShot(stunnedSFX);
                }
                break;
            case "diedSFX":
                if (diedSFX)
                {
                    AudioS.PlayOneShot(diedSFX);
                }
                break;
            case "massNuggetSFX":
                if (massNuggetSFX)
                {
                    AudioS.PlayOneShot(massNuggetSFX);
                }
                break;
            case "struckSFX":
                if (struckSFX)
                {
                    AudioS.PlayOneShot(struckSFX);
                }
                break;
            default:
            //    Debug.Log("No sfx found by that key");
                break;

        }
        }

    }
