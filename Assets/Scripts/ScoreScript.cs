using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreScript : MonoBehaviour
{
    // Start is called before the first frame update

    public GameObject Goal;



    private void OnTriggerStay(Collider other)
    {
        Debug.Log("Trigger entered");
        if (other.gameObject.tag == "Body")
        {
            Debug.Log("Player identified");
            other.gameObject.transform.parent.gameObject.BroadcastMessage("GoalShrink");
            transform.parent.BroadcastMessage("Grow");
        }
    }


}
