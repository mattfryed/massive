using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DSScript : MonoBehaviour
{
    private bool goingToCenter = false;
    private bool scrollingOff = false;
    private GameObject gameManager;
    // Start is called before the first frame update
    void Start()
    {
        gameManager = GameObject.FindWithTag("GameManager");
    }

    // Update is called once per frame
    void Update()
    {
        if (goingToCenter)
        {
            transform.Translate(Vector3.back * Time.deltaTime * 20f);
            Vector3 distToCenter = transform.localPosition - new Vector3(0f, 0f, 0f);
            if (distToCenter.magnitude < 25f)
            {
                goingToCenter = false;
                gameManager.BroadcastMessage("ShowEndScreen");
            }
        }
        if (scrollingOff)
        {
            transform.Translate(Vector3.back * Time.deltaTime * 20f);
        }
    }
    public void Engage()
    {
        goingToCenter = true;

    }
    public void ScrollOff()
    {
        scrollingOff = true;
    }
}
