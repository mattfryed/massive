using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControllerScript : MonoBehaviour
{
    private Rewired.Player player;

    public int playerID;
    public int teamID;
    private string xboxString = "_Xbox";
    public float movePower = 10f;
    public float dashPower = 500f;
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
    private float sizeChangeOnShrink = .1f;
    private float sizeChangeOnGoalHit = .01f;
    private float maxScale = 3.0f;
    private float actionDelay = .3f;
    public float stunTime = 1f;
    private bool isStunned = false;
    private float minScale = .5f;
    private float timeToReturn = 5f;
    private GameObject dm;
    public bool isActive = true;
    private float idleTime = 60f;
    public float timeSinceLastActivity;
    public float lastActivityTime;
    // references to other objects that are part of the player
    public GameObject sword;
    public GameObject shield;

    // flags
    public bool gamepadMode = false;
    public bool canUserTakeAction = true;
    public bool canUserControlMovement = true;
    public bool shieldOn = false;
    public bool temporarilyEliminated = false;

    public float moveHorizontal;
    public float moveVertical;
    public Vector3 movement;
    private Quaternion lookRotation;

    public bool didPlayerTapActionThisFrame = false;

    private GameObject gameplayObjects;

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

        didPlayerTapActionThisFrame = player.GetButtonDown("Sword");
        if (didPlayerTapActionThisFrame)
        {
       //     Debug.Log("BUTTON!");
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
            isActive = false;
        }
        else
        {
            isActive = true;
        }
    }

    void FixedUpdate()
    {
        if (!isStunned && !temporarilyEliminated)
        {
           

            //Call the AddForce function of our Rigidbody2D rb2d supplying movement multiplied by speed to move our player.
            if (canUserControlMovement)
            {
                if (playerID == 3)
                {
                    //Debug.Log(movement.x);
             //      Debug.Log(movement.magnitude);
                }

                if (Mathf.Abs(movement.magnitude) > .15f)
                {
                    rb.AddForce(movement * movePower * shieldSlowdownFactor);
                }
            }
            else
            {
                if (Mathf.Abs(movement.magnitude) > .15f)
                {
                    rb.AddForce(movement * movePower * .33f * shieldSlowdownFactor);
                }
            }

            if (didPlayerTapActionThisFrame && sword.activeInHierarchy == false && canUserTakeAction == true && !shieldOn)
            {
                // rotate sword container to proper directio
                if (movement != Vector3.zero)
                {
                    lookRotation = Quaternion.LookRotation(movement.normalized);
                }
                sword.transform.parent.transform.rotation = lookRotation;
                sword.SetActive(true);
                Dash();
                didPlayerTapActionThisFrame = false;
                canUserTakeAction = false;
                canUserControlMovement = false;
                Invoke("SwordWithdraw", .5f);
                Invoke("EndActionCooldown", actionDelay);

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
                    shieldSlowdownFactor = .4f;
                    // decrease size and mass by a very small amount
                    if (transform.localScale.x > minScale)
                    {
                        transform.localScale -= new Vector3(sizeChangeOnGoalHit / 6f, sizeChangeOnGoalHit / 6f, sizeChangeOnGoalHit / 6f);
                        rb.mass = rb.mass - massRemovedOnGoalShrink / 6f;
                    }
                }

            }
            else
            {
                shieldSlowdownFactor = 1f;
                shield.SetActive(false);
            }
        }
        // Make sure the y-axis for position is locked because it's acting weird.
    }

    void Dash()
    {
//        Debug.Log("Dashing now");
        // Play dash SFX
        playSFX("attackSFX");
        rb.AddForce(movement.normalized * dashPower);
        if (transform.localScale.x > minScale)
        {
            transform.localScale -= new Vector3(sizeChangeOnGoalHit*3f, sizeChangeOnGoalHit*3f, sizeChangeOnGoalHit*3f);
            rb.mass = rb.mass - massRemovedOnGoalShrink*3f;
        }
        //eject some blobs to signify mass lossage
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
    }

    public void Stun(Vector3 shieldPosition)
    {
      //  Destroy(gameObject);
        // Knock back the player in the opposite direction of the opposing shield
        Debug.Log("Player " + playerID.ToString() + " was stunned!");
        // play a sfx
        playSFX("StunnedSFX");
        Vector3 knockDirection = transform.position - shieldPosition;
        rb.AddForce(knockDirection.normalized * movePower*50f);
        shield.SetActive(false);
        sword.SetActive(false);
        isStunned = true;
        Invoke("UnStun", stunTime);
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
