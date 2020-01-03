using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttractScript : MonoBehaviour
{
    public float gravPower = 5f;
    // Start is called before the first frame update
    void Start()
    {
        Cursor.visible = false;
       
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerStay(Collider other)
    {   
        if (other.gameObject.tag == "Player")
        {
            // get the direciton vector
           Vector3 distance = gameObject.transform.position - other.gameObject.transform.position;
           Vector3 direction = distance.normalized;
            other.gameObject.GetComponent<Rigidbody>().AddForce(direction * gravPower);

        }
    }
}
