using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniMassBlobScript : MonoBehaviour
{
    public GameObject target;
    public Rigidbody2D rb;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Launch in a random direction
        rb.AddForce(new Vector3(10f, 10f, 10f));
    }

    // Update is called once per frame
    void Update()
    {
        // after a delay, move toward target

    }
}
