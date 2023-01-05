using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControllerScript : MonoBehaviour
{
    private Rewired.Player player;

    public int playerID;
    public int teamID;
    private string xboxString = "_Xbox";
    private float movePower = 8f;
    public float dashPower = 50f;
    private Rigidbody rb;
    private Vector3 startingPosition;

    public GameObject goalZone;
    private GameObject sm;

    public GameObject massBlobPrefab;
    public GameObject explosionPrefab;
    public GameObject respawnPrefab;

    private float timeUntilNextShrink = .1f;
    private float timeOfLastShrink = 0f;
    private float shieldSlowdownFactor = .3f;
    private float massAddedOnGrow = .1f;
    private float massRemovedOnShrink = .05f;
    private float massRemovedOnGoalShrink = .0045f;
    private float sizeChangeOnHit = .15f;
    private float sizeChangeOnGrow = .2f;
    private float sizeChangeOnShrink = .15f;
    private float sizeChangeOnGoalHit = .01f;
    private float maxScale = 3.0f;
    private float actionDelay = .25f;
    public float stunTime = 1.25f;
    private bool isStunned = false;
    private float minScale = .5f;
    private float timeToReturn = 5f;
    private GameObject dm;
    public bool isActive = true;
    private float idleTime = 60f;
    public float timeSinceLastActivity;
    public float lastActivityTime;
    public bool isBlinking = false;
    // references to other objects that are part of the player
    public GameObject sword;
    public GameObject shield;
    private GameObject stunEffect;
    // flags
    public bool gamepadMode = false;
    public bool canUserTakeAction = true;
    public bool canUserControlMovement = true;
    public bool shieldOn = false;
    public bool temporarilyEliminated = false;

    public float moveHorizontal;
    public float moveVertical;
    public Vector3 movement;
    public Vector3 lastMovement;
    private Quaternion lookRotation;

    public bool didPlayerTapActionThisFrame = false;
    public bool didPlayerReleaseActionThisFrame = false;
    public float timeLastPressed;
    
    private GameObject gameplayObjects;
    private MeshRenderer mr;

    private void Awake()
    {
        // assign a new 'rewired' player.
        player = Rewired.ReInput.players.GetPlayer(playerID); // get the player by id
        lastActivityTime = Time.time;
        gameplayObjects = GameObject.FindWithTag("GameplayObjects");

    }
    void Start()
    {
       // body = transform.Find("Body").gameObject;
        rb = GetComponent<Rigidbody>();
        startingPosition = transform.position;
        dm = GameObject.FindWithTag("GameManager");
        sm = gameObject.transform.Find("SfxModule").gameObject;
        mr = transform.Find("Ring").gameObject.GetComponent<MeshRenderer>();
        stunEffect = gameObject.transform.Find("StunnedEffect").gameObject;

        if (teamID == 1)
        {
            goalZone = GameObject.Find("TEAM 1");
        }
        else
        {
            goalZone = GameObject.Find("TEAM 2");
        }

    }

    private void Update()
    {
        //Store the current horizontal input in the float moveHorizontal.
         moveHorizontal = player.GetAxis("MoveH");

        //Store the current vertical input in the float moveVertical.
         moveVertical = player.GetAxis("MoveV");


        //Use the two store floats to create a new Vector2 variable movement.
         movement = new Vector3(moveHorizontal, 0f, moveVertical);

        shieldOn = player.GetButton("Shield");

        if (player.GetButton("Sword"))
        {

        }

      
        didPlayerTapActionThisFrame = player.GetButtonDown("Sword");
        if (didPlayerTapActionThisFrame)
        {
            // Start recording how long the button is held
            timeLastPressed = Time.time;
        }

        if (player.GetButtonUp("Sword"))
        {
            didPlayerReleaseActionThisFrame = true;
         }



        if (rb.mass < .1f)
        {
            rb.mass = 1f;
        }

        if (didPlayerTapActionThisFrame || shieldOn)
        {
            lastActivityTime = Time.time;
        }

        timeSinceLastActivity = Time.time - lastActivityTime;
        if (timeSinceLastActivity > idleTime)
        {
          //  isActive = false;
        }
        else
        {
            isActive = true;
        }

        ManageBlink();
    }

    void FixedUpdate()
    {
        if (!isStunned && !temporarilyEliminated)
        {
            // rotate sword container to proper direction
            if (movement != Vector3.zero)
            {
                lookRotation = Quaternion.LookRotation(movement.normalized);
                lastMovement = movement;
            }
            sword.transform.parent.transform.rotation = lookRotation;
            

            //Call the AddForce function of our Rigidbody2D rb2d supplying movement multiplied by speed to move our player.
            if (canUserControlMovement && canUserTakeAction)
            {
                if (playerID == 3)
                {
                    //Debug.Log(movement.x);
             //      Debug.Log(movement.magnitude);
                }

                if (Mathf.Abs(movement.magnitude) > .15f)
                {
                    // Legacy 'force-based' movement
                    //  rb.AddForce(movement * movePower * shieldSlowdownFactor);
                    float massFactor = 1f;
                    if (rb.mass > 1.0f)
                    {
                        massFactor = 1 / (1 + (rb.mass - 1f));
                    } 
                    if (rb.mass < 1.0f)
                    {
                        massFactor = 1 + (1f - rb.mass);
                    }
                    rb.velocity = movement * movePower * shieldSlowdownFactor * massFactor;
                }
            }
            else
            {
            //    if (Mathf.Abs(movement.magnitude) > .15f)
           //     {
           //         float massFactor = 1f;
           //        if (rb.mass > 1.0f)
           //         {
          //              massFactor = 1 / (1 + (rb.mass - 1f));
            //        }
             //       if (rb.mass < 1.0f)
              //      {
               //         massFactor = 1 + (1f - rb.mass);
                //    }
                 //   rb.velocity = movement * movePower * shieldSlowdownFactor * massFactor;
               // }
            }

            if (didPlayerReleaseActionThisFrame && sword.activeInHierarchy == false && canUserTakeAction == true && !shieldOn)
            {
               
                // rotate sword container to proper direction
                if (movement != Vector3.zero)
                {
                    lookRotation = Quaternion.LookRotation(movement.normalized);
                }
                sword.transform.parent.transform.rotation = lookRotation;
                sword.SetActive(true);
                // Calculate how long button was held
                float totalTimeHeld = Time.time - timeLastPressed;
                Dash(totalTimeHeld);
                didPlayerTapActionThisFrame = false;
                didPlayerReleaseActionThisFrame = false;
                canUserTakeAction = false;
                canUserControlMovement = false;
                Invoke("SwordWithdraw", .5f);
                if (totalTimeHeld < .5f)
                {
                    Invoke("EndActionCooldown", .1f);
                } else
                {
                    Invoke("EndActionCooldown", actionDelay);
                }

            }

            if (shieldOn)
            {
                // only make shield appear if sword is gone
                if (sword.activeInHierarchy == false)
                {
                    if (movement != Vector3.zero)
                    {
                        lookRotation = Quaternion.LookRotation(movement.normalized);
                    }
                    shield.transform.rotation = lookRotation;
                    shield.SetActive(true);
                    shieldSlowdownFactor = .1f;
                    // decrease size and mass by a very small amount. 
                    // NOTE: Decrease factor was / 6f.
                    float decreaseFactor = 4f;
                    if (transform.localScale.x > minScale)
                    {
                        transform.localScale -= new Vector3(sizeChangeOnGoalHit / decreaseFactor, sizeChangeOnGoalHit / decreaseFactor, sizeChangeOnGoalHit / decreaseFactor);
                        rb.mass = rb.mass - massRemovedOnGoalShrink / decreaseFactor;
                    }
                }

            }
            else
            {
                // player is charging an attack... keep the slowdown factor
                if (player.GetButton("Sword"))
                {
                    shieldSlowdownFactor = .55f;
                }
                else
                {
                    shieldSlowdownFactor = 1f;
                }
                shield.SetActive(false);
                   
            }
        }
        // Make sure the y-axis for position is locked because it's acting weird.
    }
    void ManageBlink()
    {
        if (player.GetButton("Sword") && Time.time - timeLastPressed > 0.5f)
        {
            if (!isBlinking)
            {
                StartCoroutine("Blink");
                isBlinking = true;
            }
        }
        else
        {
            if (isBlinking)
            {
                StopCoroutine("Blink");
                isBlinking = false;
                mr.enabled = true;
            }
        }
    }
    void Dash(float timeButtonWasHeld)
    {
        Debug.Log("Button was held: " + timeButtonWasHeld.ToString());
        float computedDashPower = dashPower;
        if (timeButtonWasHeld > 0.5f)
        {
            if (timeButtonWasHeld > 3f)
            {
                timeButtonWasHeld = 3f;
            }
            computedDashPower = dashPower + 30f * (timeButtonWasHeld / 3f);
        }


        // Play dash SFX
        playSFX("attackSFX");
      //rb.AddForce(lastMovement.normalized * computedDashPower);
        // Using velocity for the moment.
       rb.velocity += lastMovement.normalized * computedDashPower;
        if (transform.localScale.x > minScale)
        {
            transform.localScale -= new Vector3(sizeChangeOnGoalHit*3f, sizeChangeOnGoalHit*3f, sizeChangeOnGoalHit*3f);
            rb.mass = rb.mass - massRemovedOnGoalShrink*3f;
        }
        //eject some blobs to signify mass lossage
        EjectBlob(null);
        EjectBlob(null);
        EjectBlob(null);
        EjectBlob(null);
        EjectBlob(null);
        EjectBlob(null);
    }

    public void playSFX(string sfxName)
    {
   //     Debug.Log("trying to play sfx");
        if (sm != null)
        {
            sm.GetComponent<SfxPlayerScript>().SafePlay(sfxName);
        }
        else
        {
            Debug.Log("SFX Module not found");
        }
    }

    void EndActionCooldown()
    {
        canUserTakeAction = true;
    }

    void SwordWithdraw()
    {
        canUserControlMovement = true;
        if (sword.activeInHierarchy == true)
        {
            sword.SetActive(false);
        }
    }
    public void Shrink(GameObject target)
    {
        if (Time.time - timeOfLastShrink > timeUntilNextShrink)
        {
           // Debug.Log("SHRINKING!");
            transform.localScale -= new Vector3(sizeChangeOnShrink, sizeChangeOnShrink, sizeChangeOnShrink);
            rb.mass = rb.mass - massRemovedOnShrink;
//            Debug.Log(target);
            if (target != null)
            {

                for (int i = 0; i < 5; i++)
                {
                    EjectBlob(target);
                }

            }
            if (transform.localScale.x < minScale)
            {
                // Temporarily eliminate this player.
                temporarilyEliminated = true;
                // fire a death explosion;
                GameObject exp = Instantiate(explosionPrefab);
                exp.transform.position = gameObject.transform.position;
                // play a sfx
                playSFX("diedSFX");
                transform.localScale = new Vector3(1f, 1f, 1f);
                rb.mass = 1f;
                RespawnEffect();
                Invoke("Return", timeToReturn);
                // just.... uh... move the player somewhere very very far away.
                transform.position = new Vector3(1200f, 1200f, 1200f);

                //Broadcast to that player's goal that they should lose mass.
                goalZone.BroadcastMessage("LoseScore", teamID);
            }
            timeOfLastShrink = Time.time;
        }
    }
    void RespawnEffect()
    {
        GameObject re = Instantiate(respawnPrefab, gameplayObjects.transform);
        re.transform.position = startingPosition;
    }
    void Return()
    {
        transform.position = startingPosition;
        temporarilyEliminated = false;
    }

    void UnStun()
    {
        isStunned = false;
        stunEffect.GetComponent<ParticleSystem>().Stop();
    }

    public void Stun(Vector3 shieldPosition)
    {
      //  Destroy(gameObject);
        // Knock back the player in the opposite direction of the opposing shield
        Debug.Log("Player " + playerID.ToString() + " was stunned!");
        // play a sfx
        playSFX("StunnedSFX");
        Vector3 knockDirection = transform.position - shieldPosition;
        rb.AddForce(knockDirection.normalized * movePower*30f);
        shield.SetActive(false);
        sword.SetActive(false);
        isStunned = true;
        stunEffect.GetComponent<ParticleSystem>().Play();
        Invoke("UnStun", stunTime);
    }

    IEnumerator Blink()

    {
        while (true)
        {
            yield return new WaitForSeconds(.05f);
            mr.enabled = !mr.enabled;
        }
    }

        public void SwordClash()
    {
        Debug.Log("Sword clash");
        Debug.Log("I am player:");
        Debug.Log(playerID);
        canUserTakeAction = true;
        canUserControlMovement = true;
        // apply a force in the opposite direction of the sword
        Vector3 direction = transform.position - sword.transform.position;
        rb.AddForce(direction * 200f);

    }
    public void ShrinkSlow(GameObject target)
    {
        if (transform.localScale.x > minScale)
        {
            Debug.Log("shrinkslow!");
            transform.localScale -= new Vector3(sizeChangeOnGoalHit, sizeChangeOnGoalHit, sizeChangeOnGoalHit);
            rb.mass = rb.mass - massRemovedOnGoalShrink;
            EjectBlob(target);

        }
       
    }
    public bool GoalShrink()
    {
        if (transform.localScale.x > minScale)
        {
            Debug.Log("GOAL SHRINKING!");
            transform.localScale -= new Vector3(sizeChangeOnGoalHit, sizeChangeOnGoalHit, sizeChangeOnGoalHit);
            rb.mass = rb.mass - massRemovedOnGoalShrink;
            EjectBlob(goalZone.gameObject.transform.Find("Score Sphere").gameObject);
            return true;
        }
        else
        {
            return false;
        }
    }

    void Grow()
    {
        if (transform.localScale.x < maxScale)
        {
        //   Debug.Log("GROWING!");
            transform.localScale += new Vector3(sizeChangeOnGrow, sizeChangeOnGrow, sizeChangeOnGrow);
            rb.mass = rb.mass + massAddedOnGrow;
        }
    }

    void EjectBlob(GameObject newTarget)
    {
   //    Debug.Log(newTarget);
        GameObject newBlob = Instantiate(massBlobPrefab);
        newBlob.transform.position = transform.position;
        newBlob.GetComponent<SmallMassBlobScript>().target = newTarget;
    }
}
