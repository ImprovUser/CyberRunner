using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;

    [Header("Movement")]
    private float walkSpeed = 4f;
    private float runSpeed = 9f;
    private float sprintSpeed = 13.5f;
    private float currentMoveSpeed;
    private float jumpForce = 21f;
    private float xAxis;

    [Header("Gravity")]
    private float fallMultiplier = 4.5f;
    private float lowJumpMultiplier = 6f;

    [Header("Ground Check")]

    [SerializeField] private LayerMask groundLayer;
    private Vector2 groundCheckSize = new Vector2(0.92f, 0.42f);
    private float castDistance = 1f;

    [Header("Wall Check")]
    private Vector2 wallLCheckSize = new Vector2(0.54f, 1.76f);
    private Vector2 wallRCheckSize = new Vector2(0.54f, 1.76f);
    [SerializeField] private LayerMask wallLayer;
    private float wallLCastDistance = 0.5f;
    private float wallRCastDistance = 0.5f;

    [Header("Wall Slide")]
    private float wallStickTime = 0.5f;
    private float wallSlideMinSpeed = 0.5f;
    private float maxFallSpeed = 10f;
    private float wallSlideAcceleration = 5f;
    private float wallLatchTime = 0.2f;

    [Header("Wall Jump")]
    [SerializeField] private float wallJumpPush = 10f;
    [SerializeField] private float wallJumpOverrideTime = 0.15f;

    private float wallStickCounter;
    private float wallSlideTimer;
    private float wallJumpTimer;
    private float jumpTimeCounter;
    private float wallLatchCounter;

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
        UpdateMovementSpeed();
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
        
        if (wallJumpTimer > 0)
        {
            currentState = PlayerState.WallJumping;
        }
        else if (IsGrounded())
        {
            currentState = PlayerState.Grounded;
        }
        else if (IsOnWall() && !IsGrounded() && rb.linearVelocity.y <= 0)
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
        Debug.Log("Current State: " + currentState);
    }

    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentState == PlayerState.WallSliding)
            {
                WallJump();
            }
            else if (currentState == PlayerState.Grounded)
            {
                
                jumpTimeCounter = 0.2f;
                Jump();
            }
        }

        if (Input.GetKey(KeyCode.Space) && currentState == PlayerState.Jumping)
        {
            if (jumpTimeCounter > 0)
            {
                Jump();
                jumpTimeCounter -= Time.deltaTime;
            }
        }
    }
    private void UpdateMovementSpeed()
    {
        if (Input.GetKey(KeyCode.LeftControl))
        {
            currentMoveSpeed = walkSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            currentMoveSpeed = sprintSpeed;
        }
        else
        {
            currentMoveSpeed = runSpeed;
        }
    }
    private void Move()
    {
        rb.linearVelocity = new Vector2(currentMoveSpeed * xAxis, rb.linearVelocity.y);
    }

    private bool LatchingWall()
    {
        return (isTouchingWallLeft && xAxis < 0) || (isTouchingWallRight && xAxis > 0);
    }

    private void WallSlide()
    {
        if (LatchingWall())
        {
            wallStickCounter = wallStickTime;
            wallSlideTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
           // Debug.Log("Wall Slide: Holding onto wall (stick time)");
        }
        else if (wallLatchCounter > 0)
        {
            wallLatchCounter -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
           // Debug.Log($"Wall Slide: Latching phase ({wallLatchCounter:F2}s left)");
        }
        else if (wallStickCounter > 0)
        {
            wallStickCounter -= Time.fixedDeltaTime;
            wallSlideTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
           // Debug.Log($"Wall Slide: Grace period active ({wallStickCounter:F2}s left)");
        }
        else
        {
            wallSlideTimer += Time.fixedDeltaTime;

            // Accelerate sliding downward over time
            float slideSpeed = Mathf.Lerp(wallSlideMinSpeed, maxFallSpeed, wallSlideTimer * wallSlideAcceleration);
            slideSpeed = Mathf.Min(slideSpeed, maxFallSpeed);

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -slideSpeed);
           // Debug.Log($"Wall Slide: Sliding down – speed = {slideSpeed:F2}");
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
        isTouchingWallLeft = Physics2D.BoxCast(transform.position, wallLCheckSize, 0, -transform.right, wallLCastDistance, wallLayer);
        isTouchingWallRight = Physics2D.BoxCast(transform.position, wallRCheckSize, 0, transform.right, wallRCastDistance, wallLayer);

        if (isTouchingWallLeft) lastWallDir = 1;
        else if (isTouchingWallRight) lastWallDir = -1;

        if (isTouchingWallLeft || isTouchingWallRight)
        {
            lastWallTime = Time.time;

            // Reset wall latch only on first wall contact
            if (currentState != PlayerState.WallSliding)
            {
                wallLatchCounter = wallLatchTime;
                //Debug.Log($"Wall latch started: {wallLatchTime}s");
            }
        }
    }

    private bool IsGrounded()
    {
        return Physics2D.BoxCast(transform.position,groundCheckSize, 0, -transform.up, castDistance, groundLayer);
    }

    private bool IsOnWall()
    {
        return isTouchingWallLeft || isTouchingWallRight;
    }
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position-transform.up * castDistance, groundCheckSize);
        Gizmos.DrawWireCube(transform.position - Vector3.right * wallLCastDistance, wallLCheckSize);
        Gizmos.DrawWireCube(transform.position + Vector3.right * wallRCastDistance, wallRCheckSize);

    }

}
