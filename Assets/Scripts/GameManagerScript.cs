using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagerScript : MonoBehaviour
{
    public GameObject Score_1;
    public GameObject Score_2;
    public float finalScore_1;
    public float finalScore_2;
    public float winScale = 10.9f;
    public string levelName = "NOVA";
    private bool isGameOver = false;
    public string levelNumber = "003";
    private GameObject dm;
    private GameObject gmm;
    public GameObject sss;
    private GameObject gameplayObjects;
    private GameObject deathSphere;
    private bool is2v2;

    // Start is called before the first frame update
    void Start()
    {
        dm = GameObject.FindWithTag("DataManager");
        gmm = GameObject.FindWithTag("GameMusicManager");
        deathSphere = GameObject.FindWithTag("DeathSphere");
        gameplayObjects = GameObject.FindWithTag("GameplayObjects");
      //  sss = GameObject.FindWithTag("sss");

        DontDestroyOnLoad(gmm);
        DontDestroyOnLoad(gameObject.transform);

        if (dm != null)
        {
            // deactivate players based on dm setting
            if (!dm.GetComponent<DataManagerScript>().is2v2)
            {
                is2v2 = dm.GetComponent<DataManagerScript>().is2v2;
                GameObject p2 = GameObject.Find("Players/Player 2");
                GameObject p4 = GameObject.Find("Players/Player 4");
                p2.SetActive(false);
                p4.SetActive(false);
            }
            Destroy(dm);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!isGameOver)
        {
            // check for inactive players
            CheckForInactivePlayers();
            if (Score_1.transform.localScale.x >= winScale || Score_1.transform.localScale.x <= 0f || Score_2.transform.localScale.x >= winScale || Score_2.transform.localScale.x <= 0f)
            {
                Debug.Log("Game is now over");
                isGameOver = true;
                StoreFinalScores();
                // Game over
               
              
            }

        }
    }

    public void CheckForInactivePlayers()
    {
        GameObject p1 = GameObject.Find("Players/Player 1");
        GameObject p2 = GameObject.Find("Players/Player 2");
        GameObject p3 = GameObject.Find("Players/Player 3");
        GameObject p4 = GameObject.Find("Players/Player 4");
    //    Debug.Log("Checking for inactive players");
        if (!is2v2)
        {
            if (!p1.GetComponent<PlayerControllerScript>().isActive && !p3.GetComponent<PlayerControllerScript>().isActive)
            {
                // end the game prematurely!
                EndGamePrematurely();
            }
        }
        else
        {
            if (!p1.GetComponent<PlayerControllerScript>().isActive && !p2.GetComponent<PlayerControllerScript>().isActive && !p3.GetComponent<PlayerControllerScript>().isActive && !p4.GetComponent<PlayerControllerScript>().isActive)
            {
                EndGamePrematurely();
            }
        }
    }

    public void EndGame()
    {
        //Application.LoadLevel("levelendnewscene");
        //activate death sphere!
        deathSphere.GetComponent<DSScript>().Engage();
        //gameplayObjects.SetActive(false);
        //sss.SetActive(true);
    }

    public void ShowEndScreen()
    {
        gameplayObjects.SetActive(false);
        sss.SetActive(true);
        deathSphere.GetComponent<DSScript>().ScrollOff();

    }
    public void EndGamePrematurely()

    {
        Debug.Log("Ending game prematurely");
        StoreFinalScores();
        Application.LoadLevel(0);

    }

    public void StoreFinalScores()
    {
        isGameOver = true; 

        finalScore_1 = Score_1.transform.localScale.x;
        finalScore_2 = Score_2.transform.localScale.x;

        gmm.GetComponent<MusicManagerScript>().StopMusic();
        if (finalScore_1 < 0f)
        {
            finalScore_1 = 0f;
        }

        if (finalScore_2 < 0f)
        {
            finalScore_2 = 0f;
        }
        EndGame();
    }
}
