using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldScript : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Sword")
        {

            other.gameObject.transform.parent.parent.BroadcastMessage("Stun");
        }
    }
}
