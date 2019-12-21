using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreboardManagerScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateScoreboard(float newPercentage)
    {
        float newDisplay = Mathf.Round(newPercentage * 100f);
        string scoreString = newDisplay.ToString();
        for (int i = 1; i <= scoreString.Length; i += 1)
        {
            scoreString = scoreString.Insert(i, " ");
            i++;
        }
        // TODO: Make this dynamic based on digits. EDIT: Done!
        string precedingZeroes = "0 0 ";
        if (newDisplay < 10f)
        {
            precedingZeroes = "0 0 0 ";
        } else if (newDisplay < 100f) {
            precedingZeroes = "0 0 ";
        } else
        {
            precedingZeroes = "0 ";
        }
        gameObject.GetComponent<TextMesh>().text = precedingZeroes + scoreString + " / 0 1 0 0";
    }
}
