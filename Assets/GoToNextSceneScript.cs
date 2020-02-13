using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoToNextSceneScript : MonoBehaviour
{
    private GameObject tmm;
    private GameObject dm;
    public float delayTime;
    // Start is called before the first frame update
    void Start()
    {
        tmm = GameObject.FindWithTag("TitleMusicManager");
        tmm.GetComponent<MusicManagerScript>().StopMusic();
        dm = GameObject.FindWithTag("DataManager");
        Invoke("GoToLevel", delayTime);
    }

    void GoToLevel()
    {
        Application.LoadLevel(dm.GetComponent<DataManagerScript>().levelToLoad);
    }

}
