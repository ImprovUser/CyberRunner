using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;

    [Header("Movement")]
    private float walkSpeed = 4f;
    private float runSpeed = 9f;
    private float sprintSpeed = 13.5f;
    private float roofClimbspeed = 6f;
    private float currentMoveSpeed;
    private float jumpForce = 21f;
    private float xAxis;


    private float fallMultiplier = 4.5f;
    private float lowJumpMultiplier = 6f;

    [Header("Layer Association")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask roofClimbLayer;
    [SerializeField] private LayerMask ledgeLayer;

    private Vector2 groundCheckSize = new Vector2(0.92f, 0.42f);
    private float castDistance = 1f;

    [SerializeField] private Vector2 roofClimableCheckSize = new Vector2(1.09f, 0.33f);
    [SerializeField] private float roofClimableCastDistance = 1.16f;

    [SerializeField] private float roofJumpOverrideTime = 0.2f;
    private float roofJumpTimer;

    private Vector2 wallLCheckSize = new Vector2(0.54f, 1.46f);
    private Vector2 wallRCheckSize = new Vector2(0.54f, 1.46f);
    private float wallLCastDistance = 0.5f;
    private float wallRCastDistance = 0.5f;


    private float wallStickTime = 0.5f;
    private float wallSlideMinSpeed = 0.5f;
    private float maxFallSpeed = 10f;
    private float wallSlideAcceleration = 5f;
    private float wallLatchTime = 0.2f;

    [Header("Wall Jump")]
    [SerializeField] private float wallJumpPush = 10f;
    [SerializeField] private float wallJumpOverrideTime = 0.15f;

    [Header("Game Feel Tweaks")]
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    [Header("Ledge Climb")]
    [SerializeField] private Vector2 ledgeCheckSize = new Vector2(0.4f, 0.6f);
    [SerializeField] private float ledgeCheckDistance = 0.5f;
    // --- THIS CONTROLS THE SPEED ---
    [SerializeField] private float ledgeClimbDuration = 0.5f; // Set this to 0.2 in Inspector for a fast climb!
    [SerializeField] private float ledgeHangYOffset = 0.8f;
    [SerializeField] private float ledgeHangXOffset = 0.25f;
    private bool isLedgeClimbing = false;
    private Transform ledgeSnapPoint;
    private float facingDirectionOnLedge;


    private float wallStickCounter;
    private float wallSlideTimer;
    private float wallJumpTimer;
    private float jumpTimeCounter;
    private float wallLatchCounter;

    private bool isTouchingWallLeft;
    private bool isTouchingWallRight;
    private float lastWallTime;
    private int lastWallDir; // 1 for left, -1 for right

    private enum PlayerState { Grounded, Jumping, Falling, WallSliding, WallJumping, RoofClimbing, RoofJumping, LedgeGrabbing, LedgeClimbing }
    private PlayerState currentState;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentState = PlayerState.Falling;
    }

    private void Update()
    {
        if (isLedgeClimbing) return;

        xAxis = Input.GetAxisRaw("Horizontal");
        UpdateMovementSpeed();
        CheckWallTouch();
        HandleJumpInput();

        if (Input.GetKeyDown(KeyCode.S) && currentState == PlayerState.LedgeGrabbing)
        {
            currentState = PlayerState.Falling;
            rb.gravityScale = 9.5f;
        }
    }

    private void FixedUpdate()
    {
        UpdateState();

        if (isLedgeClimbing || currentState == PlayerState.LedgeGrabbing)
        {
            rb.linearVelocity = Vector2.zero; // Stop all momentum when grabbing
            return;
        }

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
                break;
            case PlayerState.Falling:
            case PlayerState.Jumping:
                Move();
                break;
            case PlayerState.RoofClimbing:
                RoofClimb();
                break;
            case PlayerState.RoofJumping:
                break;
            case PlayerState.LedgeGrabbing:
            case PlayerState.LedgeClimbing:
                break;
        }

        ApplyGravityControl();
    }

    private void UpdateState()
    {
        if (isLedgeClimbing)
        {
            currentState = PlayerState.LedgeClimbing;
            return;
        }

        if (roofJumpTimer > 0) roofJumpTimer -= Time.fixedDeltaTime;
        if (wallJumpTimer > 0) wallJumpTimer -= Time.fixedDeltaTime;
        if (coyoteTimeCounter > 0) coyoteTimeCounter -= Time.fixedDeltaTime;
        if (jumpBufferCounter > 0) jumpBufferCounter -= Time.fixedDeltaTime;

        // Priority 1: Roof Jumping (Override)
        if (roofJumpTimer > 0)
        {
            currentState = PlayerState.RoofJumping;
        }
        // Priority 2: Wall Jumping (Override)
        else if (wallJumpTimer > 0)
        {
            currentState = PlayerState.WallJumping;
        }
        // Priority 3: Ledge Grabbing 
        else if (CanGrabLedge())
        {
            if (currentState == PlayerState.Falling && rb.linearVelocity.y == 0)
            {
                // This is a special case if we just pressed 'S'
            }
            else
            {
                currentState = PlayerState.LedgeGrabbing;
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;

                // Store the direction we were facing when we grabbed
                facingDirectionOnLedge = Mathf.Sign(xAxis);

                float snapX = ledgeSnapPoint.position.x - (facingDirectionOnLedge * ledgeHangXOffset);
                float snapY = ledgeSnapPoint.position.y - ledgeHangYOffset;
                rb.position = new Vector2(snapX, snapY);
            }
        }
        // Priority 4: Roof Climbing (Sticky State)
        else if (IsOnClimableRoof())
        {
            currentState = PlayerState.RoofClimbing;
            coyoteTimeCounter = 0f;
        }
        // Priority 5: Wall Sliding (Sticky State)
        else if (IsOnWall() && !IsGrounded() && rb.linearVelocity.y <= 0)
        {
            if (currentState != PlayerState.WallSliding)
            {
                wallLatchCounter = wallLatchTime;
            }
            currentState = PlayerState.WallSliding;
            coyoteTimeCounter = 0f;
        }
        // Priority 6: Grounded (Normal State)
        else if (IsGrounded())
        {
            coyoteTimeCounter = coyoteTime;

            if (currentState != PlayerState.Grounded)
            {
                if (jumpBufferCounter > 0)
                {
                    GroundedJump();
                }
            }

            if (jumpBufferCounter <= 0)
            {
                currentState = PlayerState.Grounded;
            }
        }
        // Priority 7: Jumping (Aerial State)
        else if (rb.linearVelocity.y > 0)
        {
            currentState = PlayerState.Jumping;
            coyoteTimeCounter = 0f;
        }
        // Priority 8: Falling (Default Aerial State)
        else
        {
            currentState = PlayerState.Falling;
        }
    }

    // --- THIS IS THE ROBUST CanGrabLedge() USING X-INPUT ---
    private bool CanGrabLedge()
    {
        // Can't grab if already climbing, grounded, wallsliding, or moving up
        if (isLedgeClimbing || IsGrounded() || currentState == PlayerState.WallSliding || rb.linearVelocity.y > 0.1f)
        {
            // Exception: if we are *already* grabbing, we can "stick" to it
            if (currentState == PlayerState.LedgeGrabbing)
            {
                // But if we press away, let go
                if (xAxis != 0 && Mathf.Sign(xAxis) == -facingDirectionOnLedge)
                {
                    currentState = PlayerState.Falling;
                    rb.gravityScale = 9.5f; // Restore gravity
                    return false;
                }
                return true; // Keep grabbing
            }
            return false; // Not in a grabbable state
        }

        // We must be pressing towards a wall
        float facingDir = Mathf.Sign(xAxis);
        if (facingDir == 0) return false;

        // Check from "hands" level (tweak Y offset as needed)
        Vector2 checkOrigin = (Vector2)transform.position + new Vector2(0f, 0.5f);

        // Check in the direction of our input (xAxis)
        RaycastHit2D ledgeHit = Physics2D.BoxCast(checkOrigin, ledgeCheckSize, 0, new Vector2(facingDir, 0), ledgeCheckDistance, ledgeLayer);

        if (ledgeHit.collider != null)
        {
            // We hit a ledge! Store its snap point.
            ledgeSnapPoint = ledgeHit.transform;
            return true;
        }

        return false;
    }


    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentState == PlayerState.WallSliding)
            {
                WallJump();
            }
            else if (currentState == PlayerState.RoofClimbing)
            {
                RoofJump();
            }
            else if (currentState == PlayerState.LedgeGrabbing)
            {
                StartCoroutine(ClimbLedge());
            }
            else if (coyoteTimeCounter > 0)
            {
                GroundedJump();
            }
            else
            {
                jumpBufferCounter = jumpBufferTime;
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

    // --- THIS IS THE "SPASM-FIX" L-SHAPED CLIMB ---
    private IEnumerator ClimbLedge()
    {
        isLedgeClimbing = true;

        // --- THIS IS THE FIX ---
        // Turn off physics so it doesn't fight the coroutine
        rb.isKinematic = true;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        Vector2 startPos = rb.position;
        Vector2 endPos = ledgeSnapPoint.position;

        // Part 1: Move UP
        Vector2 intermediatePos = new Vector2(startPos.x, endPos.y);
        float climbUpTime = ledgeClimbDuration * 0.5f; // Half the time to move up
        float timer = 0f;

        while (timer < climbUpTime)
        {
            rb.position = Vector2.Lerp(startPos, intermediatePos, timer / climbUpTime);
            timer += Time.deltaTime;
            yield return null;
        }

        rb.position = intermediatePos;

        // Part 2: Move OVER
        float climbOverTime = ledgeClimbDuration * 0.5f; // Half the time to move over
        timer = 0f;

        while (timer < climbOverTime)
        {
            rb.position = Vector2.Lerp(intermediatePos, endPos, timer / climbOverTime);
            timer += Time.deltaTime;
            yield return null;
        }

        // --- RE-ENABLE PHYSICS ---
        rb.isKinematic = false;
        rb.position = endPos;
        isLedgeClimbing = false;
        currentState = PlayerState.Grounded;
        rb.gravityScale = 9.5f;
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
        else if (currentState == PlayerState.RoofClimbing)
        {
            currentMoveSpeed = roofClimbspeed;
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

    private void RoofClimb()
    {
        rb.linearVelocity = new Vector2(currentMoveSpeed * xAxis, 0f);
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
        }
        else if (wallLatchCounter > 0)
        {
            wallLatchCounter -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
        else if (wallStickCounter > 0)
        {
            wallStickCounter -= Time.fixedDeltaTime;
            wallSlideTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
        else
        {
            wallSlideTimer += Time.fixedDeltaTime;

            float slideSpeed = Mathf.Lerp(wallSlideMinSpeed, maxFallSpeed, wallSlideTimer * wallSlideAcceleration);
            slideSpeed = Mathf.Min(slideSpeed, maxFallSpeed);

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -slideSpeed);
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
        currentState = PlayerState.WallJumping;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }

    private void GroundedJump()
    {
        jumpTimeCounter = 0.2f;
        Jump();
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        currentState = PlayerState.Jumping;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }

    private void RoofJump()
    {
        float jumpY = -jumpForce * 0.25f;
        float jumpX = currentMoveSpeed * xAxis;

        rb.gravityScale = 9.5f;
        rb.linearVelocity = new Vector2(jumpX, jumpY);

        currentState = PlayerState.RoofJumping;
        roofJumpTimer = roofJumpOverrideTime;
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }

    private void ApplyGravityControl()
    {
        if (currentState == PlayerState.LedgeGrabbing || isLedgeClimbing)
        {
            rb.gravityScale = 0f;
            return;
        }

        if (currentState == PlayerState.WallSliding && (wallLatchCounter > 0 || wallStickCounter > 0))
            return;

        if (currentState == PlayerState.RoofClimbing)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
        else
        {
            rb.gravityScale = 9.5f;
        }

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
        else
        {
            lastWallDir = 0;
        }

        if (isTouchingWallLeft || isTouchingWallRight)
        {
            lastWallTime = Time.time;

            if (currentState != PlayerState.WallSliding)
            {
                wallLatchCounter = wallLatchTime;
            }
        }
    }

    private bool IsGrounded()
    {
        return Physics2D.BoxCast(transform.position, groundCheckSize, 0, -transform.up, castDistance, groundLayer);
    }

    private bool IsOnWall()
    {
        return isTouchingWallLeft || isTouchingWallRight;
    }

    private bool IsOnClimableRoof()
    {
        return Physics2D.BoxCast(transform.position, roofClimableCheckSize, 0, Vector2.up, roofClimableCastDistance, roofClimbLayer);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position - transform.up * castDistance, groundCheckSize);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position - Vector3.right * wallLCastDistance, wallLCheckSize);
        Gizmos.DrawWireCube(transform.position + Vector3.right * wallRCastDistance, wallRCheckSize);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + transform.up * roofClimableCastDistance, roofClimableCheckSize);

        Gizmos.color = Color.yellow;
        float facingDir = Mathf.Sign(xAxis);
        if (facingDir != 0)
        {
            Vector2 checkOrigin = (Vector2)transform.position + new Vector2(0f, 0.5f) + new Vector2(facingDir, 0) * ledgeCheckDistance;
            Gizmos.DrawWireCube(checkOrigin, ledgeCheckSize);
        }
    }
}