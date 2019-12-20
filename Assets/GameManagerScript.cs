using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagerScript : MonoBehaviour
{
    public GameObject Score_1;
    public GameObject Score_2;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Score_1.transform.localScale.x >= 11f) //TODO: Make this a variable
        {
            // Game over, team 1 wins
            Application.LoadLevel(0);
        }

        if (Score_2.transform.localScale.x >= 11f) //TODO: Make this a variable
        {
            // Game over, team 2 wins
            Application.LoadLevel(0);
        }
    }
}
