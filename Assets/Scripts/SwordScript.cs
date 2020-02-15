using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordScript : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Sword")
        {
            Debug.Log("two swords collided");
            Debug.Log("as reported by ");
            Debug.Log(transform.parent.parent.parent.GetComponent<PlayerControllerScript>().playerID);
            transform.parent.parent.parent.BroadcastMessage("SwordClash");


        }

        if (other.gameObject.tag == "Shield")
        {
            Debug.Log("shield hit, stunning attacker");
          
            transform.parent.parent.parent.BroadcastMessage("Stun", other.gameObject.transform.position);


        }

        else if (other.gameObject.tag == "Player")
        {

                 Debug.Log("Sword colliding with player");
            if (!other.gameObject.GetComponent<PlayerControllerScript>().shieldOn)
            {
                transform.parent.parent.parent.BroadcastMessage("Grow");
                other.gameObject.BroadcastMessage("Shrink", transform.parent.parent.gameObject);
                other.gameObject.BroadcastMessage("playSFX", "struckSFX");
            }
        }
    }
}
