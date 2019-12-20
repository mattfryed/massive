using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreSphereScript : MonoBehaviour
{
    public float sizeChangeOnGoalHit = .01f;
    public GameObject sphereGraphic;
    private float maxSize = 11f;
    public GameObject scoreboard;
    public int teamID;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerStay(Collider other)
    {

        if (other.gameObject.tag == "Player" && other.gameObject.GetComponent<PlayerControllerScript>().teamID == teamID)
        {
            Debug.Log("something is happening");
            // TODO: Should probably check what team the player is on, but for now, just tell em to shrink and lets grow the score acceptor
            if (other.gameObject.GetComponent<PlayerControllerScript>().GoalShrink())
            {
                if (sphereGraphic.GetComponent<RectTransform>().localScale.x < maxSize)
                {
                    sphereGraphic.GetComponent<RectTransform>().localScale += new Vector3(sizeChangeOnGoalHit, sizeChangeOnGoalHit, sizeChangeOnGoalHit);
                    scoreboard.GetComponent<ScoreboardManagerScript>().UpdateScoreboard(sphereGraphic.GetComponent<RectTransform>().localScale.x / maxSize);
                }
                else
                {
                    // trigger a game over condition
                }
            }
        }
    }
}
