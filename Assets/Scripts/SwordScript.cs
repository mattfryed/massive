using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordScript : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            transform.parent.BroadcastMessage("Grow");
            other.gameObject.BroadcastMessage("Shrink");
        }
    }
}
