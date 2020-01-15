using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyGameManagerScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {   
        GameObject gm = GameObject.FindWithTag("GameManager");
        if (gm != null)
        {
            Destroy(gm);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
