using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackHoleScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

 
    private void OnCollisionEnter(Collision collision)
    {
       // Debug.Log("Hitting black ho0le");
        if (collision.gameObject.tag == "Player")
        {
            collision.gameObject.BroadcastMessage("ShrinkSlow", transform.gameObject);
           
       
        }
    }
    private void OnCollisionStay(Collision collision)
    {
        //Debug.Log("Hitting black ho0le");
        if (collision.gameObject.tag == "Player")
        {
            collision.gameObject.BroadcastMessage("ShrinkSlow", transform.gameObject);
        }
    }
}
