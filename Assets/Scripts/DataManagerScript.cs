using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataManagerScript : MonoBehaviour
{
    public bool is2v2;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject.transform);
    }

    public void UpdateMode(bool is4player)
    {
        is2v2 = is4player;
    }
}
