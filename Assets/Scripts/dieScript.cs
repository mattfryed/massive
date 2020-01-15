using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dieScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
       Invoke("Die", 10f); 
    }

    void Die()
    {
        Destroy(gameObject);
    }
}
