using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControllerScript : MonoBehaviour
{
    private Rewired.Player player;

    public int playerID;
    private string xboxString = "_Xbox";
    public float movePower = 10f;
    public float dashPower = 500f;
    private Rigidbody rb;

    public float massAddedOnGrow = .1f;
    public float massRemovedOnShrink = .1f;
    public float massRemovedOnGoalShrink = .1f;
    public float sizeChangeOnHit = .1f;
    public float sizeChangeOnGoalHit = .00001f;
    public float actionDelay = 1.5f;

    private float minScale = .5f;

    // references to other objects that are part of the player
    public GameObject sword;
    public GameObject shield;

    // flags
    public bool gamepadMode = false;
    public bool canUserTakeAction = true;
    public bool canUserControlMovement = true;
    public bool shieldOn = false;

    public float moveHorizontal;
    public float moveVertical;
    public Vector3 movement;
    private Quaternion lookRotation;

    public bool didPlayerTapActionThisFrame = false;

    private void Awake()
    {
        // assign a new 'rewired' player.
        player = Rewired.ReInput.players.GetPlayer(playerID); // get the player by id
    }
    void Start()
    {
       // body = transform.Find("Body").gameObject;
        rb = GetComponent<Rigidbody>();
      
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
            Debug.Log("BUTTON!");
        }
    }

    void FixedUpdate()
    {

        //Call the AddForce function of our Rigidbody2D rb2d supplying movement multiplied by speed to move our player.
        if (canUserControlMovement)
        {
            rb.AddForce(movement.normalized * movePower);
        }

        if (didPlayerTapActionThisFrame && sword.activeInHierarchy == false && canUserTakeAction == true && !shieldOn)
        {
            // rotate sword container to proper directio
            lookRotation = Quaternion.LookRotation(movement.normalized);
            sword.transform.parent.transform.rotation = lookRotation;
            sword.SetActive(true);
            Dash();
            didPlayerTapActionThisFrame = false;
            canUserTakeAction = false;
            canUserControlMovement = false;
            Invoke("SwordWithdraw", .550f);
            Invoke("EndActionCooldown", actionDelay);

        }

        if (shieldOn)
        {
            // only make shield appear if sword is gone
            if (sword.activeInHierarchy == false)
            {
                lookRotation = Quaternion.LookRotation(movement.normalized);
                shield.transform.parent.transform.rotation = lookRotation;
                shield.SetActive(true);
            }

        } else
        {
            shield.SetActive(false);
        }

        // Make sure the y-axis for position is locked because it's acting weird.
    }

    void Dash()
    {
        Debug.Log("Dashing now");
   
        rb.AddForce(movement.normalized * dashPower);
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
    void Shrink()
    {
        Debug.Log("SHRINKING!");
        transform.localScale -= new Vector3(sizeChangeOnHit, sizeChangeOnHit, sizeChangeOnHit);
        rb.mass = rb.mass - massRemovedOnShrink;
    }

    public void Stun(Vector3 shieldPosition)
    {
        // Knock back the player in the opposite direction of the opposing shield
        Vector3 knockDirection = shieldPosition - transform.position;
        rb.AddForce(knockDirection.normalized * movePower*5f);
    }

    public bool GoalShrink()
    {
        if (transform.localScale.x > minScale)
        {
            Debug.Log("GOAL SHRINKING!");
            transform.localScale -= new Vector3(sizeChangeOnGoalHit, sizeChangeOnGoalHit, sizeChangeOnGoalHit);
            rb.mass = rb.mass - massRemovedOnGoalShrink;
            return true;
        }
        else
        {
            return false;
        }
    }

    void Grow()
    {
        Debug.Log("GROWING!");
        transform.localScale += new Vector3(sizeChangeOnHit, sizeChangeOnHit, sizeChangeOnHit);
        rb.mass = rb.mass + massAddedOnGrow;
    }
}
