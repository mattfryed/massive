using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WinLoseScreenManagerScript : MonoBehaviour
{
    public float rawScore_1;
    public float rawScore_2;
    public float winScale;
    public string whichLevel;
    public string stageNum;
    private GameObject gm;

    // Text objects to update
    public GameObject whichLevelText;
    public GameObject score1Text;
    public GameObject score2Text;
    public GameObject winningTeamText;
    public GameObject levelNumText;

    // graphic elements to update
    public GameObject winnerEmitter;
    public GameObject loserEmitter;
    public GameObject winnerSphere;

    // Start is called before the first frame update
    void Start()
    {
        // load how many games have already been played
        int newNumber = PlayerPrefs.GetInt("gamesPlayed");
        Debug.Log("Number of games played was " + newNumber.ToString());
        // add one to it
        newNumber = newNumber + 1;
        // save it as the new number of games played
        PlayerPrefs.SetInt("gamesPlayed", newNumber);
        PlayerPrefs.Save();
        Debug.Log("number of games played is now " + newNumber.ToString());
        // get variables from game manager and destroy it.
        gm = GameObject.FindWithTag("GameManager");

        if (gm != null)
        {
            rawScore_1 = gm.GetComponent<GameManagerScript>().finalScore_1;
            rawScore_2 = gm.GetComponent<GameManagerScript>().finalScore_2;
            whichLevel = gm.GetComponent<GameManagerScript>().levelName;
            stageNum = "STAGE_" + gm.GetComponent<GameManagerScript>().levelNumber;
            winScale = gm.GetComponent<GameManagerScript>().winScale;
            Destroy(gm);
            UpdateText();
        }


    }

    void UpdateText()
    {
        //update level name 
        score1Text.GetComponent<TextMesh>().text = FormatScores(rawScore_1/winScale);
        score2Text.GetComponent<TextMesh>().text = FormatScores(rawScore_2/winScale);
        whichLevelText.GetComponent<TextMesh>().text = whichLevel;
        levelNumText.GetComponent<TextMesh>().text = stageNum;

        if (rawScore_1 > rawScore_2)
        {
            winningTeamText.GetComponent<TextMesh>().text = "LIGHT\nWINS";
            winnerSphere.transform.position = new Vector3(-17.4982f, 0f, 0f);
            winnerEmitter.transform.position = new Vector3(-17.45763f, 0f, 0f);
            loserEmitter.transform.position = new Vector3(17.3f, 0f, 0f);

        }
        else
        {
            winningTeamText.GetComponent<TextMesh>().text = "DARK\nWINS";
            winnerSphere.transform.position = new Vector3(17.4982f, 0f, 0f);
            winnerEmitter.transform.position = new Vector3(17.45763f, 0f, 0f);
            winnerEmitter.transform.rotation = Quaternion.Euler(0, 0, 0);
            loserEmitter.transform.position = new Vector3(-17.3f, 0f, 0f);
        }
    }

    string FormatScores(float rawScore)
    {
        float newDisplay = Mathf.Round(rawScore * 100f);
        string scoreString = newDisplay.ToString();
        for (int i = 1; i <= scoreString.Length; i += 1)
        {
            scoreString = scoreString.Insert(i, " ");
            i++;
        }
       
        string precedingZeroes = "0 0 ";
        if (newDisplay < 10f)
        {
            precedingZeroes = "0 0 0 ";
        }
        else if (newDisplay < 100f)
        {
            precedingZeroes = "0 0 ";
        }
        else
        {
            precedingZeroes = "0 ";
        }

        return(precedingZeroes + scoreString + "/ 0 1 0 0");

    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
