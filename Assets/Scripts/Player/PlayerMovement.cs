using System;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    private CharacterController controller;

    public float speed = 12f;
    public float gravity = -9.81f;
    public float jumpheight = 3f;

    public Transform groundcheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    Vector3 velocity;

    bool isGrounded;
    bool isMoving;

    private Vector3 lastPosition = new Vector3(0f, 0f, 0f);

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        //GroundCheck
        isGrounded = Physics.CheckSphere(groundcheck.position, groundDistance, groundMask);
        //Resetting the default velocity
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        //Getting the inputs
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        //Creating the moving vector
        Vector3 move = transform.right * x + transform.forward * z; //right - red axis, forward - blue axis

        //Actually moving the player
        controller.Move(move * speed * Time.deltaTime);

        //Check if player can jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            //Going up
            velocity.y = Mathf.Sqrt(jumpheight * -2f * gravity);
        }

        //Falling down
        velocity.y += gravity * Time.deltaTime;

        //Executing the jump
        controller.Move(velocity * Time.deltaTime);

        if (lastPosition != gameObject.transform.position && isGrounded == true)
        {
            isMoving = true;
            //for later use
        }

        else
        {
            isMoving = false;
            //for later use
        }

        lastPosition = gameObject.transform.position;

    }

}
