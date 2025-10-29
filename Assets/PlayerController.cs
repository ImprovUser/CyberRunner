using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;


    [SerializeField] private float walkSpeed = 1f;
    [SerializeField] private float jumpForce = 15f;

    private float xAxis;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Settings")]
    [SerializeField] private float jumpTimeMax = 0.3f;
    [SerializeField] private float fallMultiplier = 3.5f;
    [SerializeField] private float lowJumpMultiplier = 5f;

    [Header("Wall Check")]
    [SerializeField] private Transform wallCheckLeft;
    [SerializeField] private Transform wallCheckRight;
    [SerializeField] private float wallCheckDistance = 0.1f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallSlideSpeed = 0.5f;
    [SerializeField] private float wallJumpPush = 10f;
    [SerializeField] private float wallCheckRadius = 0.1f;


    private bool isTouchingWallLeft;
    private bool isTouchingWallRight;
    private float jumpTimeCounter;
    private bool isJumping;
    private float wallJumpGraceTime = 0.2f;
    private float lastWallTime;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        GetControls();
        CheckJump();
        CheckWallTouch();

        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("Manual Wall Jump Test");
            WallJump();
        }

    }

    void FixedUpdate()
    {
        Move();
        WallSlide();
        ApplyGravityControl();
    }

    void GetControls()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
    }

    void CheckJump()
    {
        if (Input.GetButtonDown("Jump") && (isGrounded() || Time.time - lastWallTime < wallJumpGraceTime))
        {
            isJumping = true;
            jumpTimeCounter = jumpTimeMax;

            if (IsOnWall() && !isGrounded())
            {
                // If we're on a wall, perform a wall jump (push away horizontally + vertical)
                WallJump();
            }
            else
            {
                // Normal ground jump
                Jump();
            }
        }

        if(Input.GetButton("Jump") && isJumping)
        {
            if(jumpTimeCounter > 0)
            {
                Jump();
                jumpTimeCounter -= Time.deltaTime;
            }

            else
            {
                isJumping = false;
            }
        }

        if(Input.GetButtonUp("Jump"))
        {
            isJumping = false;
        }
    }

    void CheckWallTouch()
    {
        // Debug ray draw
        Debug.DrawRay(wallCheckLeft.position, Vector2.left * wallCheckDistance, Color.red);
        Debug.DrawRay(wallCheckRight.position, Vector2.right * wallCheckDistance, Color.blue);

        isTouchingWallLeft = Physics2D.OverlapCircle(wallCheckLeft.position, wallCheckRadius, wallLayer);
        isTouchingWallRight = Physics2D.OverlapCircle(wallCheckRight.position, wallCheckRadius, wallLayer);
    }


    private void Move()
    {
        rb.linearVelocity = new Vector2(walkSpeed * xAxis, rb.linearVelocity.y);
    }

    private void WallSlide()
    {
        bool pushingIntoLeftWall = isTouchingWallLeft && xAxis < 0;
        bool pushingIntoRightWall = isTouchingWallRight && xAxis > 0;

        if ((pushingIntoLeftWall || pushingIntoRightWall) && !isGrounded() && rb.linearVelocity.y < 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed));
        }
    }

    private void WallJump()
    {
        float horizontalDir = 0f;

        if (isTouchingWallLeft) horizontalDir = 1f;
        else if (isTouchingWallRight) horizontalDir = -1f;

        Debug.Log($"Wall Jump! Dir: {horizontalDir}, TouchLeft: {isTouchingWallLeft}, TouchRight: {isTouchingWallRight}");

        // Failsafe: if no wall detected, abort
        if (horizontalDir == 0f)
            return;

        rb.linearVelocity = new Vector2(horizontalDir * wallJumpPush, jumpForce);
    }


    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private bool isGrounded()
    {
        Collider2D hit = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        //Debug.Log(hit ? $"Grounded on: {hit.name}" : "Not grounded");
        return hit != null;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    private void ApplyGravityControl()
    {
        if (rb.linearVelocity.y < 0)
        {
            // Faster fall
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            // Cut jump short if released early
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private bool IsOnWall()
    {
        return isTouchingWallLeft || isTouchingWallRight;
    }

    private void OnDrawGizmos()
    {
        if (wallCheckLeft != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(wallCheckLeft.position, wallCheckLeft.position + Vector3.left * wallCheckDistance);
        }

        if (wallCheckRight != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wallCheckRight.position, wallCheckRight.position + Vector3.right * wallCheckDistance);
        }
    }


}
