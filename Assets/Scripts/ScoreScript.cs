using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreScript : MonoBehaviour
{
    // Start is called before the first frame update

    public GameObject Goal;


    void Update()
    {
        transform.position = transform.parent.Find("Body").gameObject.transform.position;
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            other.gameObject.BroadcastMessage("Shrink");
        }
    }


}
