using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatterNuggetScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            other.gameObject.BroadcastMessage("playSFX", "massNuggetSFX");
            other.gameObject.BroadcastMessage("Grow");
            other.gameObject.BroadcastMessage("Grow");
            other.gameObject.BroadcastMessage("Grow");
            Destroy(transform.gameObject);
        }
    }
}
