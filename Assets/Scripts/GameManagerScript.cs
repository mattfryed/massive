using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagerScript : MonoBehaviour
{
    public GameObject Score_1;
    public GameObject Score_2;
    public float finalScore_1;
    public float finalScore_2;
    public float winScale = 11f;
    public string levelName = "NOVA";
    private bool isGameOver = false;
    public string levelNumber = "003";
    private GameObject dm;

    // Start is called before the first frame update
    void Start()
    {
        dm = GameObject.FindWithTag("DataManager");
        DontDestroyOnLoad(gameObject.transform);

        if (dm != null)
        {
            // deactivate players based on dm setting
            if (!dm.GetComponent<DataManagerScript>().is2v2)
            {
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

            if (Score_1.transform.localScale.x >= winScale)
            {
                Debug.Log("Game is now over");
                StoreFinalScores();
                // Game over, team 1 wins
                Application.LoadLevel("levelendnewscene");
            }

            if (Score_2.transform.localScale.x >= winScale)
            {
                Debug.Log("Game is now over");
                StoreFinalScores();
                // Game over, team 2 wins
                Application.LoadLevel("levelendnewscene");
            }
        }
    }

    public void StoreFinalScores()
    {
        isGameOver = true; 
        finalScore_1 = Score_1.transform.localScale.x;
        finalScore_2 = Score_2.transform.localScale.x;
    }
}
