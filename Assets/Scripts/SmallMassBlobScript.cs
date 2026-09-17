using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Massive.Player;

public class SmallMassBlobScript : MonoBehaviour
{
    public GameObject target;
    private bool targetOn = false;

    private GameObject child;
    private float timeToAutoKill = 30f;
    private float bornTime;
    private Vector3 authoredVisualScale;
    private bool visualScaleCaptured;
    private float visualSize = 1f;
    // Start is called before the first frame update


    private void Awake()
    {
        bornTime = Time.time;
        CaptureVisualScale();
    }

    /// <summary>Sets a launch-time visual size without changing travel forces or lifetime.</summary>
    public void ConfigureVisualSize(float size)
    {
        CaptureVisualScale();
        visualSize = Mathf.Max(0.01f, size);
        transform.localScale = authoredVisualScale * visualSize;
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    private void CaptureVisualScale()
    {
        if (visualScaleCaptured) return;
        authoredVisualScale = transform.localScale;
        visualScaleCaptured = true;
    }
    void Start()
    {
        child = transform.Find("Particle System").gameObject;
        // Launch in a random direction
        if (target != null)
        {
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
        } else {
            float ejectPowerX = Random.Range(-50f, 50f);
            float ejectPowerY = Random.Range(-50f, 50f);
           
            gameObject.GetComponent<Rigidbody>().AddForce(ejectPowerX, 0f, ejectPowerY);
        }
        if (target != null)
        {
            float timeToTarget = Random.Range(.25f, .75f);
            Invoke("GoToTarget", timeToTarget);
        }
    }

    void GoToTarget()
    {
        targetOn = true;
    }
    // Update is called once per frame
    void Update()
    {   
        if (Time.time >= bornTime + timeToAutoKill)
        {
            Destroy(gameObject);
        }
        //  child.GetComponent<ParticleSystem>().GetComponent<Renderer>().material.SetColor("_Color", new Color(0f, 1f, 0f, .1f));
        if (target != null && targetOn)
        {
            // get direction vector between opbject and target
            Vector3 direction = target.transform.position - transform.position;

            gameObject.GetComponent<Rigidbody>().AddForce(direction.normalized * 25f);

            // if distance is close enough to target, destroy this blob
            PlayerControllerScript targetPlayer = target.GetComponentInParent<PlayerControllerScript>();
            float arrivalDistance = .48f;
            if (targetPlayer != null)
            {
                float bodyRadius = PlayerScaleAdjuster.BodyRadiusOf(targetPlayer);
                if (bodyRadius > 0f) arrivalDistance = bodyRadius * .96f;
            }
            if (direction.magnitude < arrivalDistance)
            {
                Destroy(gameObject);
            }


            Vector3 distanceToTarget = transform.position - target.transform.position;

            if (distanceToTarget.magnitude > 100f)
            {
                // target is dead, choose a new target. That player's goal sounds good. 
                GameObject newTarget = target.gameObject.GetComponent<PlayerControllerScript>().goalZone.gameObject.transform.Find("Score Sphere").gameObject;
                if (newTarget != null)
                {
                    target = newTarget;
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        } else if (target == null)
        {
            // just fade out until ya gone
            transform.localScale *= .97f;
            if (transform.localScale.x < .1f * visualSize)
            {
                Destroy(gameObject);
            }
        }
    }
}
