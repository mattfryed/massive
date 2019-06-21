using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wormhole2 : MonoBehaviour
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
            other.gameObject.transform.position = new Vector3(-8.5f, 0f, 0f);

        }
    }
}
