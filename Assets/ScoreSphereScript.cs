using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreSphereScript : MonoBehaviour
{
    public float sizeChangeOnGoalHit = .00001f;

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

        if (other.gameObject.tag == "Player")
        {
            Debug.Log("something is happening");
            // TODO: Should probably check what team the player is on, but for now, just tell em to shrink and lets grow the score acceptor
           // other.gameObject.BroadcastMessage("GoalShrink");
            transform.localScale += new Vector3(sizeChangeOnGoalHit, sizeChangeOnGoalHit, sizeChangeOnGoalHit);
        }
    }
}
