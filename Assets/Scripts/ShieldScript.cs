using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldScript : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
    
        if (collision.gameObject.tag == "Sword")
        {
            Debug.Log(transform.parent.parent.position);
            collision.gameObject.transform.parent.parent.BroadcastMessage("Stun", transform.parent.parent.position);
        }
    }
}
