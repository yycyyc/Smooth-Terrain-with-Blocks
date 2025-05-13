using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    public float walkSpeed = 8f;
    public float flySpeed = 16f;
    public float flyVerticalSpeed = 16f;
    public float jumpForce = 1.5f;
    public float mouseSensitivity = 200f;

    private bool isFlying = false;
    private CharacterController controller;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private Vector3 velocity;
    private bool isGrounded;
    private Transform camTransform;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        camTransform = transform.GetComponentInChildren<Camera>().transform;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            isFlying = !isFlying;
        }

        LookAround();
        MoveCharacter();
    }

    void LookAround()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity * Time.deltaTime;

        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -89f, 89f);
        rotationY += mouseX;


        transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
        camTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }

    void MoveCharacter()
    {
        Vector3 move = Vector3.zero;
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        isGrounded = controller.isGrounded;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float speed = isRunning ? walkSpeed * 2f : walkSpeed;
        float currentFlySpeed = isRunning ? flySpeed * 2f : flySpeed;

        if (isFlying)
        {
            move = new Vector3(moveX, 0, moveZ).normalized * currentFlySpeed;
            float verticalMove = 0;

            if (Input.GetKey(KeyCode.Space))
            {
                verticalMove = flyVerticalSpeed;
            }
            else if (Input.GetKey(KeyCode.LeftControl))
            {
                verticalMove = -flyVerticalSpeed;
            }

            move.y = verticalMove;
        }
        else
        {
            move = new Vector3(moveX, 0, moveZ).normalized * speed;

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            if (isGrounded && Input.GetKeyDown(KeyCode.Space))
            {
                velocity.y = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);
            }

            velocity.y += Physics.gravity.y * Time.deltaTime;
            move.y = velocity.y;
        }

        controller.Move(transform.TransformDirection(move) * Time.deltaTime);
    }
}



