using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordContainerScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = transform.parent.Find("Body").gameObject.transform.position;
        transform.rotation = Quaternion.LookRotation(transform.parent.Find("Body").gameObject.GetComponent<Rigidbody>().linearVelocity);
    }
}
