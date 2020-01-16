using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmallMassBlobScript : MonoBehaviour
{
    public GameObject target;
    private bool targetOn = false;

    private GameObject child;

    // Start is called before the first frame update
    void Start()
    {
        child = transform.Find("Particle System").gameObject;
        // Launch in a random direction
        float ejectPowerX = Random.Range(-250f, 250f);
        float ejectPowerY = Random.Range(-250f, 250f);
        if (ejectPowerX >= 0f)
        {
            ejectPowerX += 20f;
        }
        else
        {
            ejectPowerX -= 20f;
        }
        if (ejectPowerY >= 0f)
        {
            ejectPowerY += 20f;
        }
        else
        {
            ejectPowerY -= 20f;
        }
        gameObject.GetComponent<Rigidbody>().AddForce(ejectPowerX, 0f, ejectPowerY);
        float timeToTarget = Random.Range(.25f, .75f);
        Invoke("GoToTarget", timeToTarget);
    }

    void GoToTarget()
    {
        targetOn = true;
    }
    // Update is called once per frame
    void Update()
    {
     //  child.GetComponent<ParticleSystem>().GetComponent<Renderer>().material.SetColor("_Color", new Color(0f, 1f, 0f, .1f));
        if (target != null && targetOn)
        {
            // get direction vector between opbject and target
            Vector3 direction = target.transform.position - transform.position;

            gameObject.GetComponent<Rigidbody>().AddForce(direction.normalized * 25f);

            // if distance is close enough to target, destroy this blob
            if (direction.magnitude < .48f)
            {
                Destroy(gameObject);
            }
        }
    }
}
