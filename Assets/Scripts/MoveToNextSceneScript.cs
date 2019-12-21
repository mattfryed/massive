using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveToNextSceneScript : MonoBehaviour
{

    public float delayToNextScene = 5f;
    public int nextSceneNum;
    private bool movingToNextScene = false;
    private Rewired.Player p1;
    private Rewired.Player p2;
    private Rewired.Player p3;
    private Rewired.Player p4;

    void Awake()
    {
        p1 = Rewired.ReInput.players.GetPlayer(0); // get the player by id
        p2 = Rewired.ReInput.players.GetPlayer(1); // get the player by id
        p3 = Rewired.ReInput.players.GetPlayer(2); // get the player by id
        p4 = Rewired.ReInput.players.GetPlayer(3); // get the player by id
    }


    void Update()
    {

        if (p1.GetButtonDown("Sword") || p2.GetButtonDown("Sword") || p3.GetButtonDown("Sword")|| p4.GetButtonDown("Sword") || Input.GetButtonDown("Jump_P1_Xbox") || Input.GetButtonDown("Jump_P2_Xbox") || Input.GetButtonDown("Jump_P3_Xbox") || Input.GetButtonDown("Jump_P4_Xbox") || Input.GetButtonDown("Jump_P1") || Input.GetButtonDown("Jump_P2") || Input.GetButtonDown("Jump_P3") || Input.GetButtonDown("Jump_P4"))
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
