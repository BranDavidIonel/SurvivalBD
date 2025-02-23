using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public FixedJoystick joystick;

    public float speed = 12f;
    public float gravity = -9.81f * 2;
    public float jumpHeight = 3f;

    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    Vector3 velocity;

    bool isGrounded;

    private Vector3 lastPosition = new Vector3(0f, 0f, 0f);
    public bool isMoving;

    public GameObject movementScreenUI;
    bool moveUp = false;
    bool moveDown = false;
    bool moveLeft = false;
    bool moveRight = false;

    Button buttonUp;
    Button buttonDown;
    Button buttonLeft;
    Button buttonRight;
    private void Start()
    {
        buttonUp = movementScreenUI.transform.Find("ButtonUp").GetComponent<Button>();
        buttonUp.onClick.AddListener(delegate { MovePlayerWithButtons("up"); });

        buttonDown = movementScreenUI.transform.Find("ButtonDown").GetComponent<Button>();
        buttonDown.onClick.AddListener(delegate { MovePlayerWithButtons("down"); });

        buttonLeft = movementScreenUI.transform.Find("ButtonLeft").GetComponent<Button>();
        buttonLeft.onClick.AddListener(delegate { MovePlayerWithButtons("left"); });

        buttonRight = movementScreenUI.transform.Find("ButtonRight").GetComponent<Button>();
        buttonRight.onClick.AddListener(delegate { MovePlayerWithButtons("right"); });
    }
    void MovePlayerWithButtons(string typeMove)
    {
        if(typeMove == "up")
        {
            moveUp = true;
        }
        if (typeMove == "down")
        {
            moveDown = true;
        }
        if (typeMove == "left")
        {
            moveLeft = true;
        }
        if (typeMove == "right")
        {
            moveRight = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(StorageManager.Instance.storageUIOpen == false && CampfireUIManager.Instance.isUiOpen == false)
        {
            Movement();
        }
    }
    private void Movement()
    {
        //checking if we hit the ground to reset our falling velocity, otherwise we will fall faster the next time
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        //float x= joystick.Horizontal;
        //float z = joystick.Vertical;


        if (moveUp)
        {
            z += speed;
            moveUp = false;
        }
        if (moveDown)
        {
            z -= speed;
            moveDown = false;
        }
        if (moveLeft)
        {
            x -= speed;
            moveLeft = false;
        }
        if (moveRight)
        {
            x += speed;
            moveRight = false;
        }

        //right is the red Axis, foward is the blue axis
        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * speed * Time.deltaTime);

        //check if the player is on the ground so he can jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            //the equation for jumping
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
        if (lastPosition != gameObject.transform.position && isGrounded == true)
        {
            isMoving = true;
            SoundManager.Instance.PlaySound(SoundManager.Instance.grassWalkSound);
        }
        else
        {
            isMoving = false;
            SoundManager.Instance.grassWalkSound.Stop();
        }
        lastPosition = gameObject.transform.position;
    }
}
