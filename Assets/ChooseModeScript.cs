using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChooseModeScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void MoveToNextScene(bool is2v2)
    {
        // set a flag here
        GameObject dm = GameObject.FindWithTag("DataManager");
        dm.GetComponent<DataManagerScript>().UpdateMode(is2v2);
        Application.LoadLevel(6);
    }
}
