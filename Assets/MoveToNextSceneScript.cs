using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveToNextSceneScript : MonoBehaviour
{

    public float delayToNextScene = 5f;
    public int nextSceneNum;
    private bool movingToNextScene = false;

    void Start()
    {
        
    }


    void Update()
    {

        if (Input.GetButtonDown("Jump_P1_Xbox") || Input.GetButtonDown("Jump_P2_Xbox") || Input.GetButtonDown("Jump_P3_Xbox") || Input.GetButtonDown("Jump_P4_Xbox") || Input.GetButtonDown("Jump_P1") || Input.GetButtonDown("Jump_P2") || Input.GetButtonDown("Jump_P3") || Input.GetButtonDown("Jump_P4"))
        {
            Debug.Log("A button was pressed");
            if (!movingToNextScene)
            {
                movingToNextScene = true;
                Invoke("MoveToNextScene", delayToNextScene);
            }
        }
    }

    void MoveToNextScene()
    {
        Application.LoadLevel(nextSceneNum);
    }
}
