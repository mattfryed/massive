using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControllerScript : MonoBehaviour
{
    // Start is called before the first frame update

    public int playerID;
    private string xboxString = "_Xbox";
    public float movePower = 10;
    private Rigidbody rb;

    public float massAddedOnGrow = .1f;
    public float massRemovedOnShrink = .1f;
    public float sizeChangeOnHit = .1f;

    public GameObject sword;
    public bool gamepadMode = false;


    void Start()
    {
        rb = gameObject.GetComponent<Rigidbody>();
        if (gamepadMode)
        {
            xboxString = "_Xbox";
        }
        else
        {
            xboxString = "";
        }
    }


    void FixedUpdate()
    {
        //Store the current horizontal input in the float moveHorizontal.
        float moveHorizontal = Input.GetAxis("Horizontal_P" + playerID.ToString() + xboxString);

        //Store the current vertical input in the float moveVertical.
        float moveVertical = Input.GetAxis("Vertical_P" + playerID.ToString() + xboxString);

        //Store the current horizontal input in the float moveHorizontal.
        // float aimHorizontal = Input.GetAxis("Horizontal_RS_P" + playerID.ToString() + xboxString);

        //Store the current vertical input in the float moveVertical.
        // float aimVertical = Input.GetAxis("Vertical_RS_P" + playerID.ToString() + xboxString);
        float aimHorizontal = 0f;
        float aimVertical = 0f;
        //Use the two store floats to create a new Vector2 variable movement.
        Vector3 movement = new Vector3(moveHorizontal, 0f, moveVertical);
        if (playerID == 4)
        {
            Debug.Log(moveHorizontal);
        }

        Vector3 aimDirection = new Vector3(aimHorizontal, 0, aimVertical);

        if (aimDirection.magnitude <= .1f)
        {
            sword.SetActive(false);
        }
        else
        {
            sword.SetActive(true);
        }

        transform.rotation = Quaternion.LookRotation(aimDirection);

        //Call the AddForce function of our Rigidbody2D rb2d supplying movement multiplied by speed to move our player.
        rb.AddForce(movement.normalized * movePower);
    }

    void Shrink()
    {
        Debug.Log("SHRINKING!");
        transform.localScale -= new Vector3(sizeChangeOnHit, sizeChangeOnHit, sizeChangeOnHit);
        rb.mass = rb.mass - massRemovedOnShrink;
    }

    void Grow()
    {
        Debug.Log("GROWING!");
        transform.localScale += new Vector3(sizeChangeOnHit, sizeChangeOnHit, sizeChangeOnHit);
        rb.mass = rb.mass + massAddedOnGrow;
    }
}
