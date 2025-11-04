// REFACTORED PLAYER CONTROLLER USING FINITE STATE MACHINE

using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1f;
    [SerializeField] private float jumpForce = 16f;
    private float xAxis;

    [Header("Gravity")]
    [SerializeField] private float fallMultiplier = 4.5f;
    [SerializeField] private float lowJumpMultiplier = 6f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Wall Check")]
    [SerializeField] private Transform wallCheckLeft;
    [SerializeField] private Transform wallCheckRight;
    [SerializeField] private float wallCheckRadius = 0.5f;
    [SerializeField] private LayerMask wallLayer;

    [Header("Wall Slide")]
    [SerializeField] private float wallStickTime = 1f;
    [SerializeField] private float wallSlideMinSpeed = 0.5f;
    [SerializeField] private float maxFallSpeed = 10f;
    [SerializeField] private float wallSlideAcceleration = 5f;
    [SerializeField] private float wallLatchTime = 0.2f;

    [Header("Wall Jump")]
    [SerializeField] private float wallJumpPush = 10f;
    [SerializeField] private float wallJumpOverrideTime = 0.15f;

    private float wallStickCounter;
    private float wallSlideTimer;
    private float wallJumpTimer;
    private float jumpTimeCounter;
    private float wallLatchCounter;

    private bool isJumping;
    private bool isTouchingWallLeft;
    private bool isTouchingWallRight;
    private float lastWallTime;
    private int lastWallDir;

    private enum PlayerState { Grounded, Jumping, Falling, WallSliding, WallJumping }
    private PlayerState currentState;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentState = PlayerState.Falling;
    }

    private void Update()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
        CheckWallTouch();
        HandleJumpInput();
    }

    private void FixedUpdate()
    {

   
        UpdateState();

        switch (currentState)
        {
            case PlayerState.Grounded:
                Move();
                break;
            case PlayerState.WallSliding:
                WallSlide();
                Move();
                break;
            case PlayerState.WallJumping:
                wallJumpTimer -= Time.fixedDeltaTime;
                break;
            case PlayerState.Falling:
            case PlayerState.Jumping:
                Move();
                break;
        }

        ApplyGravityControl();
    }

    private void UpdateState()
    {
        bool grounded = IsGrounded();
        bool onWall = IsOnWall();

        if (wallJumpTimer > 0)
        {
            currentState = PlayerState.WallJumping;
        }
        else if (grounded)
        {
            currentState = PlayerState.Grounded;
        }
        else if (onWall && rb.linearVelocity.y < 0)
        {
            // Only reset latch timer when transitioning *into* WallSliding
            if (currentState != PlayerState.WallSliding)
            {
                wallLatchCounter = wallLatchTime;
            }

            currentState = PlayerState.WallSliding;
        }
        else if (rb.linearVelocity.y > 0)
        {
            currentState = PlayerState.Jumping;
        }
        else
        {
            currentState = PlayerState.Falling;
        }
    }

    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (IsOnWall() && !IsGrounded())
            {
                WallJump();
            }
            else if (IsGrounded())
            {
                isJumping = true;
                jumpTimeCounter = 0.2f;
                Jump();
            }
        }

        if (Input.GetKey(KeyCode.Space) && isJumping)
        {
            if (jumpTimeCounter > 0)
            {
                Jump();
                jumpTimeCounter -= Time.deltaTime;
            }
            else
            {
                isJumping = false;
            }
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            isJumping = false;
        }
    }

    private void Move()
    {
        rb.linearVelocity = new Vector2(walkSpeed * xAxis, rb.linearVelocity.y);
    }

    private void WallSlide()
    {
        bool pressingIntoWall = (isTouchingWallLeft && xAxis < 0) || (isTouchingWallRight && xAxis > 0);

        if (pressingIntoWall)
        {
            wallStickCounter = wallStickTime;
            wallSlideTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            Debug.Log("Wall Slide: Holding onto wall (stick time)");
        }
        else if (wallLatchCounter > 0)
        {
            wallLatchCounter -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            Debug.Log($"Wall Slide: Latching phase ({wallLatchCounter:F2}s left)");
        }
        else if (wallStickCounter > 0)
        {
            wallStickCounter -= Time.fixedDeltaTime;
            wallSlideTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            Debug.Log($"Wall Slide: Grace period active ({wallStickCounter:F2}s left)");
        }
        else
        {
            wallSlideTimer += Time.fixedDeltaTime;

            // Accelerate sliding downward over time
            float slideSpeed = Mathf.Lerp(wallSlideMinSpeed, maxFallSpeed, wallSlideTimer * wallSlideAcceleration);
            slideSpeed = Mathf.Min(slideSpeed, maxFallSpeed);

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -slideSpeed);
            Debug.Log($"Wall Slide: Sliding down – speed = {slideSpeed:F2}");
        }
    }

    private void WallJump()
    {
        float jumpDir = lastWallDir;
        if (jumpDir == 0) return;

        float jumpX = jumpDir * wallJumpPush;
        float jumpY = jumpForce * 1.6f;

        rb.linearVelocity = new Vector2(jumpX, jumpY);
        wallJumpTimer = wallJumpOverrideTime;
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void ApplyGravityControl()
    {
        if (currentState == PlayerState.WallSliding && (wallLatchCounter > 0 || wallStickCounter > 0))
            return; // freeze gravity during latch or grace period

        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetKey(KeyCode.Space))
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private void CheckWallTouch()
    {
        isTouchingWallLeft = Physics2D.OverlapCircle(wallCheckLeft.position, wallCheckRadius, wallLayer);
        isTouchingWallRight = Physics2D.OverlapCircle(wallCheckRight.position, wallCheckRadius, wallLayer);

        if (isTouchingWallLeft) lastWallDir = 1;
        else if (isTouchingWallRight) lastWallDir = -1;

        if (isTouchingWallLeft || isTouchingWallRight)
        {
            lastWallTime = Time.time;

            // Reset wall latch only on first wall contact
            if (currentState != PlayerState.WallSliding)
            {
                wallLatchCounter = wallLatchTime;
                Debug.Log($"Wall latch started: {wallLatchTime}s");
            }
        }
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private bool IsOnWall()
    {
        return isTouchingWallLeft || isTouchingWallRight;
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (wallCheckLeft != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(wallCheckLeft.position, wallCheckRadius);
        }
        if (wallCheckRight != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(wallCheckRight.position, wallCheckRadius);
        }
    }
}
