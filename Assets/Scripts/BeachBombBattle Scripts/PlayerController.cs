using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController instance;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 20f;
    public float gravityScale = 7f;
    public float rotateSpeed = 10f;

    [Header("Acceleration Settings")]
    [Tooltip("How fast the player accelerates to target speed")]
    public float acceleration = 25f;
    [Tooltip("How fast the player decelerates when stopping")]
    public float deceleration = 30f;
    [Tooltip("Air control - multiplier for acceleration when airborne")]
    public float airControlMultiplier = 0.6f;

    private float currentHorizontalSpeed = 0f;
    
    [Header("Ground Detection")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheckPoint;

    [Header("Jump Settings")]
    [Tooltip("Time to buffer jump input before landing")]
    public float jumpBufferTime = 0.2f;
    [Tooltip("The animation frame where the Y position should start changing (at 24 FPS)")]
    public int animationFrameWhereJumpStarts = 3;
    private float jumpDelayTimer = 0f;
    private bool isJumpInitiated = false;
    private bool inPreJumpAnimation = false;
    public float normalJumpForce = 20f;
    public float runningJumpForce = 23f;  // Slightly higher force for running jumps
    public float runningJumpThreshold = 1.0f;
    private bool justPerformedHighJump = false; // Track if we just performed a high jump
    private bool jumpDisabledDueToEarlyProneDown = false;
    
    [Tooltip("Time window after landing where the next jump has no delay")]
    public float consecutiveJumpWindow = 0.4f;
    private float timeSinceLanding = 0f;
    
    [Tooltip("Time window after landing before allowing transition to running state")]
    public float runTransitionDelay = 0.2f;

    [Header("Fall Settings")]
    [Tooltip("Time to fall before triggering highland animation")]
    public float highLandThreshold = 0.6f;
    [Tooltip("Time to fall before triggering splat animation")]
    public float splatThreshold = 0.8f;
    [SerializeField, Tooltip("Optional separate threshold for Ground Pound splat (if 0, uses regular splat threshold)")]
    public float groundPoundSplatThreshold = 0.5f;
    [Tooltip("How long controls are locked during HighLand")]
    public float highLandControlLockDuration = 1.05f;
    private float fallTimer = 0f;
    private float groundPoundFallTimer = 0f;
    private bool groundPoundShouldSplat = false;
    private float groundPoundForceIncrement = 1f; // How much to increase per tick
    private float groundPoundForceIncrementInterval = 0.1f; // Time between increments
    private float groundPoundForceTimer = 0f; // Timer for tracking intervals
    private float currentGroundPoundForce; // Current force value

    [Header("Prone Settings")]
    private bool isProning = false; // Whether we're currently in any prone state
    private bool isTransitioningToProne = false; // Whether we're in ProneDown animation
    private bool isTransitioningFromProne = false; // Whether we're in ProneUp animation

    // Attack system reference
    private AttackController attackController;
    private bool isInAttackState = false;
    private bool isInSpecialFall = false;
    private bool isInSpecialRecovery = false;
    private AttackMove currentAttackMove;

    // Movement variables
    private SlipNFall slipNFallController;
    private Vector3 moveDirection;
    private float verticalVelocity;
    private Vector2 input;
    
    // State tracking
    private bool isGrounded;
    private bool wasGrounded;
    private bool isJumping;
    private bool isFalling;
    private bool isHighLanding = false;
    private bool isSplatting = false;
    private float landingTime = -100f;
    private float skFallStateTimer = 0f;
    private bool wasInSKFallLastFrame = false;
    public bool IsSplatting() => isSplatting;
    public bool IsHighLanding() => isHighLanding;
    
    // Idle animation variables
    private float idleTimer = 0f;
    private float timeUntilStretch = 10f;
    private float stretchChance = 0.2f;
    
    // Blink state control
    private float blinkStateTimer = 0f;
    private float blinkStateDuration = 2f;
    
    // Camera reference
    private Camera theCam;
    
    [Header("Player Reference")]
    public CharacterController charController;
    public GameObject playerModel;
    public Animator anim;
    
    // Previous state tracking
    private bool previousJustLandedState = false;
    private bool proneButtonPressed = false;
    private bool previousProneButtonState = false;
    
    private void Awake()
    {
        instance = this;
    }
    
    private void Start()
    {
        theCam = Camera.main;
        
        // Get or add the attack controller
        attackController = GetComponent<AttackController>();
        if (attackController == null)
        {
            attackController = gameObject.AddComponent<AttackController>();
        }
        
        // Get reference to SlipNFall controller if it exists
        slipNFallController = GetComponent<SlipNFall>();
        
        // Create a ground check point if not assigned
        if (groundCheckPoint == null)
        {
            groundCheckPoint = new GameObject("GroundCheckPoint").transform;
            groundCheckPoint.parent = transform;
            groundCheckPoint.localPosition = new Vector3(0, -charController.height/2, 0);
        }
        
        // Set initial animation states
        anim.SetBool("Grounded", true);
        anim.SetBool("IsIdle", true);
        anim.SetBool("IsJumping", false);
        anim.SetBool("IsFalling", false);
        anim.SetBool("ShouldBlink", false);
        anim.SetBool("ShouldStretch", false);
        anim.SetBool("JustLanded", false);
        anim.SetBool("ShouldHighLand", false);
        anim.SetBool("ShouldSplat", false);
        anim.SetBool("ShouldProne", false);
        anim.SetFloat("Speed", 0f);
        
        // Initialize timer to past the window so it's not active on start
        landingTime = -consecutiveJumpWindow * 2;
    }

    private void OnEnable()
    {
        // Check if jumping works with SlipNFall enabled
        if (GetComponent<SlipNFall>() != null && GetComponent<SlipNFall>().enabled)
        {
            Debug.LogWarning("SlipNFall script detected - checking for jump functionality");

            // Check current jump settings
            Debug.Log($"Current jump settings: moveSpeed={moveSpeed}, jumpForce={jumpForce}, normalJumpForce={normalJumpForce}");
            
            // Force jumpForce and normalJumpForce to safe values
            jumpForce = 20f;
            normalJumpForce = 20f;
            
            // Ensure SlipNFall's settings are safe
            SlipNFall slipScript = GetComponent<SlipNFall>();
            if (slipScript != null)
            {
                // Disable ground override
                var field = slipScript.GetType().GetField("forceGroundedDuringSlip");
                if (field != null)
                {
                    field.SetValue(slipScript, false);
                    Debug.Log("Disabled forceGroundedDuringSlip in SlipNFall");
                }
                
                // Apply other safe settings
                slipScript.slipMovementSpeed = moveSpeed * 0.5f; // 50% of normal movement speed
            }
        }
    }

    void Update()
    {
        // At the start of Update, set ShouldProne directly based on button state
        // This should happen BEFORE any other logic that might manipulate it
        if (GameInputManager.Instance != null)
        {
            try {
                bool proneButtonCurrentlyPressed = GameInputManager.Instance.GetProneInput();
                anim.SetBool("ShouldProne", proneButtonCurrentlyPressed);
            } catch {
                // Fallback if GetProneInput() doesn't exist
                anim.SetBool("ShouldProne", Input.GetKey(KeyCode.LeftShift));
            }
        }
        else
        {
            // Fallback to legacy Input system
            anim.SetBool("ShouldProne", Input.GetKey(KeyCode.LeftShift));
        }
        
        // Check if we're in UI mode
        bool isInUIMode = IsPaused();
        
        // Store previous ground state for edge detection
        bool wasGroundedLastFrame = isGrounded;
        
        // Update ground state
        wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer) || charController.isGrounded;
        
        // Update time since landing
        timeSinceLanding = Time.time - landingTime;
        
        // Update attack state tracking from the attack controller
        if (attackController != null)
        {
            isInAttackState = attackController.IsInAttackState();
            isInSpecialFall = attackController.IsInSpecialFall();
            isInSpecialRecovery = attackController.IsInSpecialRecovery();
        }
        
        // Detect landing
        if (isGrounded && !wasGrounded)
        {
            OnLanding();
        }

        // CRITICAL FIX: Add a safety check for stuck attack animations
        // If we're not in an attack state but still have attack animations active
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (!isInAttackState)
        {
            if (stateInfo.IsName("Punch") && anim.GetBool("Punch"))
            {
                // Force reset the animation parameter
                anim.SetBool("Punch", false);
                Debug.Log("Detected stuck Punch animation - forced reset");
            }
        }
        
        // SAFETY CHECK: If we're in normal states but attack parameters are stuck, reset them
        bool inNormalState = stateInfo.IsName("Idle") || 
                            stateInfo.IsName("IdleToRun") || 
                            stateInfo.IsName("Run") || 
                            stateInfo.IsName("Walk");
        
        if (inNormalState && !isInAttackState && !isInSpecialFall && !isInSpecialRecovery)
        {
            // If we're in a normal state but attack parameters are still active, reset them
            bool needsReset = false;
            
            if (anim.GetBool("SpinKick"))
            {
                anim.SetBool("SpinKick", false);
                Debug.Log("PlayerController safety reset: SpinKick parameter");
                needsReset = true;
            }
            
            if (anim.GetBool("SKFall"))
            {
                anim.SetBool("SKFall", false);
                Debug.Log("PlayerController safety reset: SKFall parameter");
                needsReset = true;
            }
            
            if (anim.GetBool("SKRecover"))
            {
                anim.SetBool("SKRecover", false);
                Debug.Log("PlayerController safety reset: SKRecover parameter");
                needsReset = true;
            }
            
            if (needsReset)
            {
                Debug.Log("PlayerController: Reset stuck animation parameters in normal state");
            }
        }
        
        // Only process gameplay input if we're not in UI mode
        if (!isInUIMode)
        {
            // Get input
            GetInput();
            
            // Check for high land animation interruption
            CheckForHighLandInterruption();
            
            // Track internal prone state but don't set animation parameter
            if (!isInAttackState)
            {
                HandleProneStateInternal();
            }
            
            // Handle jump (if not in any attack state)
            if (!isInAttackState)
            {
                HandleJump();
            }
            
            // Handle movement (modified for attack states)
            HandleMovement();
            
            // Apply movement
            ApplyMovement();
            
            // Handle animations
            UpdateAnimations();
            
            // Track SKFall state for highland/splat transitions
            TrackSKFallState();
            
            // Check for SKFall exit and reset moveSpeed
            CheckForSKFallExit();
        }
        else
        {
            // If in UI mode, only apply gravity
            verticalVelocity += Physics.gravity.y * Time.deltaTime * gravityScale;
            moveDirection = new Vector3(0, verticalVelocity, 0);
            charController.Move(moveDirection * Time.deltaTime);
        }
    }

    private void CheckForHighLandInterruption()
    {
        // If we're in high landing animation but past the control lockout period
        if (anim.GetBool("ShouldHighLand") && !isHighLanding)
        {
            // Check if player is trying to move
            if (input.magnitude > 0.1f)
            {
                // Player wants to move, end the high land animation
                anim.SetBool("ShouldHighLand", false);
                //Debug.Log("High landing animation interrupted by player movement");
            }
        }
    }
    
    private bool IsPaused()
    {
        PauseMenu pauseMenu = FindObjectOfType<PauseMenu>();
        return pauseMenu != null && pauseMenu.IsPaused;
    }
    
    private void GetInput()
    {
        // Get movement input (prioritizing new Input System through GameInputManager)
        if (GameInputManager.Instance != null)
        {
            Vector2 moveInput = GameInputManager.Instance.GetMoveInput();
            input = new Vector2(moveInput.x, moveInput.y);
            
            // Get prone button input
            try {
                proneButtonPressed = GameInputManager.Instance.GetProneInput();
            } catch {
                // Fallback if GetProneInput() doesn't exist
                proneButtonPressed = Input.GetKey(KeyCode.LeftShift);
            }
        }
        else
        {
            // Fallback to legacy Input system
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            
            // Get prone button input (Left Shift on keyboard)
            proneButtonPressed = Input.GetKey(KeyCode.LeftShift);
        }
        
        // Check if we're in early ProneDown animation
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool inProneDownAnimation = stateInfo.IsName("ProneDown");
        float proneDownProgress = inProneDownAnimation ? stateInfo.normalizedTime : 0f;
        
        // If in early ProneDown, disable jump by consuming any jump input
        if (inProneDownAnimation && proneDownProgress < 0.5f)
        {
            // Set flag for tracking
            jumpDisabledDueToEarlyProneDown = true;
            
            // Check if jump was pressed and consume it if necessary
            if (GameInputManager.Instance != null)
            {
                // Call GetAndResetJumpButtonPressed to consume the input without using it
                if (GameInputManager.Instance.GetAndResetJumpButtonPressed())
                {
                    Debug.Log("Jump input consumed during early ProneDown animation");
                }
            }
            else if (Input.GetButtonDown("Jump"))
            {
                // With legacy input we can't consume it, but we set the flag to ignore it
                Debug.Log("Jump input detected and will be ignored during early ProneDown animation");
            }
        }
        else
        {
            // Clear the flag when not in early ProneDown
            jumpDisabledDueToEarlyProneDown = false;
        }
    }
    
    private void HandleProneStateInternal()
    {
        // Cannot enter prone state during jumping, falling
        if (isJumping || isFalling)
        {
            return;
        }
        
        // Don't process prone state changes during highland/splat animations
        if (isHighLanding || isSplatting)
        {
            // But continue tracking the prone button state
            previousProneButtonState = proneButtonPressed;
            return;
        }
        
        // CRITICAL FIX: Only check for prone exit if button is actually released!
        // Handle entering prone state
        if (proneButtonPressed && !previousProneButtonState && !isProning && !isTransitioningToProne && !isTransitioningFromProne)
        {
            // Start transition to prone
            isTransitioningToProne = true;
            isProning = true;  // Set this to true immediately
            Debug.Log("Starting transition to prone");
            
            // Start coroutine to detect when ProneDown animation ends
            StartCoroutine(WaitForProneDownComplete());
        }
        
        // CRITICAL FIX: ONLY check for exiting prone if button is ACTUALLY released
        // Handle exiting prone state - only if button is released AND we're not already transitioning out
        if (!proneButtonPressed && previousProneButtonState && (isProning || isTransitioningToProne) && !isTransitioningFromProne)
        {
            // Start transition from prone to standing
            isProning = false;
            isTransitioningToProne = false;
            isTransitioningFromProne = true;
            Debug.Log("Starting transition from prone to standing");
            
            // Start coroutine to detect when ProneUp animation ends
            StartCoroutine(WaitForProneUpComplete());
        }
        
        // Store current prone button state for next frame
        previousProneButtonState = proneButtonPressed;
    }
    
    private IEnumerator WaitForProneDownComplete()
    {
        // Wait until ProneDown completes and transitions to ProneTo
        while (true)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            
            // Check for either of these states which indicate the transition is complete enough
            bool inProneToAnimation = stateInfo.IsName("ProneTo");
            bool inProneIdleAnimation = stateInfo.IsName("ProneIdle");
            
            // Original check for ProneDown is now redundant since it will transition to ProneTo
            // But keep checking for ProneTo and ProneIdle
            if (inProneToAnimation || inProneIdleAnimation)
            {
                break;
            }
            
            // If prone was canceled early, exit
            if (!proneButtonPressed)
            {
                isTransitioningToProne = false;
                isProning = false;  // Set this back to false if canceled
                yield break;
            }
            
            yield return null;
        }
        
        // Now we've reached ProneTo or ProneIdle, but we're still technically in transition
        // Only set isTransitioningToProne=false when we're fully in ProneIdle
        while (true)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            
            // If we've reached ProneIdle or performed a high jump, we're done
            if (stateInfo.IsName("ProneIdle") || anim.GetBool("IsHighJumping"))
            {
                isTransitioningToProne = false;
                Debug.Log("Fully in prone state or performed high jump");
                break;
            }
            
            // If prone was canceled early, exit
            if (!proneButtonPressed)
            {
                isTransitioningToProne = false;
                isProning = false;
                yield break;
            }
            
            yield return null;
        }
    }
    
    private IEnumerator WaitForProneUpComplete()
    {
        // Wait a small amount to ensure animation has started
        yield return new WaitForSeconds(0.1f);
        
        // Wait until ProneUp animation completes
        while (true)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            
            // If we're in ProneUp state and it's almost done
            if (stateInfo.IsName("ProneUp") && stateInfo.normalizedTime >= 0.9f)
            {
                break;
            }
            
            // If we're already back in Idle, we've completed the transition
            if (stateInfo.IsName("Idle"))
            {
                break;
            }
            
            // If prone was reactivated early, exit
            if (proneButtonPressed)
            {
                isTransitioningFromProne = false;
                isTransitioningToProne = true;
                StartCoroutine(WaitForProneDownComplete());
                yield break;
            }
            
            yield return null;
        }
        
        // Now fully out of prone state
        isTransitioningFromProne = false;
        Debug.Log("Fully out of prone state");
    }
    
    private void HandleMovement()
    {
        // Get current animation state to check for Punch
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool inPunchState = stateInfo.IsName("Punch");
        
        // CRITICAL FIX: If we're in a Punch animation state but not marked as attacking, reset it
        if (inPunchState && !isInAttackState && attackController != null)
        {
            anim.SetBool("Punch", false);
            Debug.Log("PlayerController detected orphaned Punch state - forced reset");
        }
        
        // Keep vertical velocity separate from horizontal movement
        float yStore = verticalVelocity;
        
        // CRITICAL FIX: Check for SpinKick or SKFall states
        bool inSpinKickState = stateInfo.IsName("SpinKick") || anim.GetBool("SpinKick");
        bool inSKFallState = stateInfo.IsName("SKFall") || anim.GetBool("SKFall");
        bool inSKRecoverState = stateInfo.IsName("SKRecover") || anim.GetBool("SKRecover");
        bool inSpinKickOrFall = inSpinKickState || inSKFallState;
        
        // NEW: Check if we're transitioning from SKFall to Highland/Splat
        bool skFallTransitioningToHighlandOrSplat = inSKFallState && 
            (anim.GetBool("ShouldHighLand") || anim.GetBool("ShouldSplat"));
        
        // Special handling for Punch animation - block movement but preserve vertical velocity
        if (inPunchState)
        {
            // Block all horizontal movement during Punch, but maintain gravity
            // ACCELERATION CHANGE: Decelerate to zero instead of instant stop
            currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, 0f, deceleration * Time.deltaTime);
            moveDirection = new Vector3(0, yStore, 0);
            return;
        }
        
        // Check for SlipRecover state
        bool inSlipRecover = slipNFallController != null && slipNFallController.IsInSlipRecover();
        
        // CRITICAL FIX: Treat SlipRecover exactly like Splat and Highland - completely block movement
        if (isSplatting || isHighLanding || inSlipRecover)
        {
            // ACCELERATION CHANGE: Decelerate to zero instead of instant stop
            currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, 0f, deceleration * Time.deltaTime);
            moveDirection = Vector3.zero;
            
            // Log SlipRecover state to confirm it's working
            if (inSlipRecover && input.magnitude > 0.1f)
            {
                Debug.Log("Movement BLOCKED during SlipRecover state");
            }
            
            return;
        }
        
        bool inLimitedMovementRecovery = isInSpecialRecovery && currentAttackMove != null;
        float movementMultiplier = 1.0f;

        // Flag to check if we need to apply a max speed cap
        bool applyMaxSpeedCap = false;
        float maxSpeedCap = 8f; // Default max speed cap

        // Check specifically for GPRecover animation
        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
        bool inGPRecoverAnimation = currentState.IsName("GPRecover");

        if (inGPRecoverAnimation)
        {
            // Strict speed cap for GPRecover animation
            applyMaxSpeedCap = true;
            maxSpeedCap = 3f; // Limit to 3 units during GPRecover
        }
        else if (inLimitedMovementRecovery)
        {
            movementMultiplier = currentAttackMove.recoveryMovementMultiplier;
        }
        // Apply prone movement restriction ONLY IF GROUNDED
        else if (isGrounded && anim.GetBool("ShouldProne"))
        {
            // 40% movement speed while in any prone state AND grounded
            movementMultiplier = 0.4f;
        }
        
        // NEW: SpinKick animation speed caps (but NOT if transitioning to Highland/Splat)
        if (inSpinKickState)
        {
            // SpinKick animation: cap at 8 units
            applyMaxSpeedCap = true;
            maxSpeedCap = 8f;
        }
        else if (inSKFallState && !skFallTransitioningToHighlandOrSplat && !isHighLanding && !isSplatting && stateInfo.IsName("SKFall"))
        {
            // SKFall animation: cap at 8 units (but NOT if transitioning or in other states)
            applyMaxSpeedCap = true;
            maxSpeedCap = 8f;
        }
        else if (inSKRecoverState)
        {
            // SKRecover animation: cap at 5 units
            applyMaxSpeedCap = true;
            maxSpeedCap = 5f;
        }
        
        // Check for Uppercut state
        bool inUppercutState = stateInfo.IsName("Uppercut");
        
        // Check for GroundPound fall state
        bool inGroundPoundFall = isInSpecialFall && currentAttackMove != null && 
                                currentAttackMove.moveName == "GroundPound";
        
        // Check for regular fall states
        bool inRegularFall = isFalling && !isInSpecialFall;
        bool inHighFall = inRegularFall && anim.GetBool("IsHighFalling");
        
        // Apply specific max speed caps for different states
        if (inUppercutState)
        {
            applyMaxSpeedCap = true;
            maxSpeedCap = 6f; // Max speed of 6 for Uppercut
        }
        else if (inGroundPoundFall)
        {
            applyMaxSpeedCap = true;
            maxSpeedCap = 8f; // Max speed of 8 for GroundPound fall
        }
        else if (inHighFall)
        {
            applyMaxSpeedCap = true;
            maxSpeedCap = 4f; // Max speed of 4 for high fall
        }
        else if (inRegularFall)
        {
            applyMaxSpeedCap = true;
            maxSpeedCap = 8f; // Max speed of 8 for regular fall
        }
        
        // If we're airborne in an attack state, apply gravity here since HandleJump is skipped during attacks
        // BUT - make sure we're applying the CORRECT gravity for each state
        if (!isGrounded && isInAttackState && !isJumpInitiated)
        {
            if (isInSpecialRecovery)
            {
                // For recovery, use NORMAL gravity only - no multipliers
                verticalVelocity += Physics.gravity.y * Time.deltaTime * gravityScale;
            }
            else if (isInSpecialFall && currentAttackMove != null && currentAttackMove.moveName == "GroundPound")
            {
                // For GroundPound fall, use the enhanced gravity
                float gravityMultiplier = 2.0f;
                verticalVelocity += Physics.gravity.y * Time.deltaTime * gravityScale * gravityMultiplier;
            }
            else
            {
                // For all other attacks, use normal gravity
                verticalVelocity += Physics.gravity.y * Time.deltaTime * gravityScale;
            }
        }
        
        // Calculate move direction relative to camera
        Vector3 rawMoveDirection = (theCam.transform.forward * input.y) + (theCam.transform.right * input.x);
        rawMoveDirection.y = 0; // Remove any y component from camera

        // ACCELERATION SYSTEM STARTS HERE
        float targetSpeed = 0f;
        Vector3 moveDir = Vector3.zero;
        
        if (rawMoveDirection.magnitude > 0.1f)
        {
            // Get the analog stick magnitude (0.0 to 1.0) BEFORE normalizing
            float analogMagnitude = Mathf.Clamp01(rawMoveDirection.magnitude);
            
            // Normalize to get direction only
            moveDir = rawMoveDirection.normalized;
            
            // Calculate target speed with all modifiers
            targetSpeed = moveSpeed * movementMultiplier * analogMagnitude;
            
            // Special fall still gets its reduction
            if (isInSpecialFall && !(currentAttackMove != null && currentAttackMove.moveName == "SpinKick"))
            {
                targetSpeed *= 0.2f;
            }
            
            // Apply max speed cap to target speed if needed
            if (applyMaxSpeedCap)
            {
                // Enforce prone speed cap for prone states
                if (isGrounded && anim.GetBool("ShouldProne"))
                {
                    float proneSpeedCap = 4f;
                    targetSpeed = Mathf.Min(targetSpeed, proneSpeedCap);
                }
                // Normal speed cap handling for non-prone states
                else
                {
                    targetSpeed = Mathf.Min(targetSpeed, maxSpeedCap);
                }
            }
        }
        else
        {
            // No input - target speed is zero
            targetSpeed = 0f;
        }
        
        // ACCELERATION/DECELERATION LOGIC
        float accelerationRate;
        if (targetSpeed > currentHorizontalSpeed)
        {
            // Accelerating
            accelerationRate = acceleration;
            if (!isGrounded)
            {
                accelerationRate *= airControlMultiplier;
            }
        }
        else
        {
            // Decelerating
            accelerationRate = deceleration;
            if (!isGrounded)
            {
                accelerationRate *= airControlMultiplier;
            }
        }
        
        // Apply acceleration/deceleration
        currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, targetSpeed, accelerationRate * Time.deltaTime);
        
        // Set final movement direction
        if (currentHorizontalSpeed > 0.01f && moveDir != Vector3.zero)
        {
            moveDirection = moveDir * currentHorizontalSpeed;
            
            // Rotation handling (same as before)
            if (!inUppercutState)
            {
                Quaternion newRotation = Quaternion.LookRotation(moveDir);
                playerModel.transform.rotation = Quaternion.Slerp(playerModel.transform.rotation, newRotation, rotateSpeed * Time.deltaTime);
            }
            else
            {
                // Special rotation handling for Uppercut - only allow positive Y rotation
                if (moveDir.magnitude > 0.1f)
                {
                    Vector3 currentEulerAngles = playerModel.transform.eulerAngles;
                    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                    Vector3 targetEulerAngles = targetRotation.eulerAngles;
                    
                    float currentYAngle = currentEulerAngles.y;
                    float targetYAngle = targetEulerAngles.y;
                    float angleDifference = Mathf.DeltaAngle(currentYAngle, targetYAngle);
                    
                    if (angleDifference > 0)
                    {
                        float newYAngle = Mathf.LerpAngle(currentYAngle, targetYAngle, rotateSpeed * Time.deltaTime);
                        playerModel.transform.rotation = Quaternion.Euler(currentEulerAngles.x, newYAngle, currentEulerAngles.z);
                    }
                }
            }
        }
        else
        {
            moveDirection = Vector3.zero;
        }
        
        // Reapply vertical velocity
        moveDirection.y = yStore;
    }
    
    private void HandleJump()
    {
        // Enhanced gravity for ALL slip states
        if (slipNFallController != null && slipNFallController.enabled && slipNFallController.IsSlipping())
        {
            // Check if we're in any slip animation
            AnimatorStateInfo slipStateCheck = anim.GetCurrentAnimatorStateInfo(0);
            bool inAnySlipAnimation = slipStateCheck.IsName("Slip") || 
                                    slipStateCheck.IsName("SlipFall") || 
                                    slipStateCheck.IsName("SlipRecover");
            
            if (inAnySlipAnimation)
            {
                // Apply stronger downward force when airborne during slip
                if (!isGrounded)
                {
                    // Apply extra downward force - adjust this multiplier as needed
                    float slipGravityMultiplier = 2.0f;
                    
                    // Start with a strong initial downward velocity if we just started falling
                    if (verticalVelocity >= 0)
                    {
                        verticalVelocity = -5f; // Initial downward push
                    }
                    
                    // Apply enhanced gravity
                    verticalVelocity += Physics.gravity.y * Time.deltaTime * gravityScale * slipGravityMultiplier;
                    
                    // Optional: Cap maximum fall speed to prevent excessive velocity
                    float maxFallSpeed = -30f;
                    if (verticalVelocity < maxFallSpeed)
                    {
                        verticalVelocity = maxFallSpeed;
                    }
                    
                    // Debug logging (once per second)
                    if (Time.frameCount % 60 == 0)
                    {
                        Debug.Log("Enhanced slip gravity: " + verticalVelocity);
                    }
                }
                else
                {
                    // When grounded, apply small downward force to keep player on slopes
                    verticalVelocity = -2f;
                }
                
                // Block jump attempts during any slip animation
                if (GameInputManager.Instance != null)
                {
                    GameInputManager.Instance.GetAndResetJumpButtonPressed();
                }
                
                // Return to skip other jump processing
                return;
            }
        }
        
        // Important: Remove dependency on slipNFallController for jump blocking
        // Check for slip recover state but don't let it block jumping if check fails
        bool inSlipRecover = false;
        
        // More reliable slipNFallController check
        if (slipNFallController != null && slipNFallController.enabled)
        {
            try
            {
                // OPTIMIZATION: Instead of using IsInSlipRecover which might have bugs,
                // directly access the isInSlipRecover field using reflection
                var field = slipNFallController.GetType().GetField("isInSlipRecover", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                if (field != null)
                {
                    inSlipRecover = (bool)field.GetValue(slipNFallController);
                    if (inSlipRecover)
                    {
                        Debug.Log("Slip recover state detected by direct field access");
                    }
                }
                else
                {
                    // Fallback to method call if field access fails
                    inSlipRecover = slipNFallController.IsInSlipRecover();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error checking slip recover state: " + e.Message);
                // Important: Don't let errors in slip recover check block jumping
                inSlipRecover = false; 
            }
        }
        
        // These conditions block jumping
        if (isSplatting || isHighLanding || isInAttackState || jumpDisabledDueToEarlyProneDown || inSlipRecover)
        {
            if (jumpDisabledDueToEarlyProneDown && verticalVelocity > 0)
            {
                verticalVelocity = 0f;
                Debug.Log("Prevented vertical movement during early ProneDown");
            }
            
            // DEBUGGING: This will help track down when jumping is blocked and why
            if (GameInputManager.Instance != null && GameInputManager.Instance.GetAndResetJumpButtonPressed())
            {
                if (isSplatting) Debug.Log("Jump blocked: Player is in Splat state");
                else if (isHighLanding) Debug.Log("Jump blocked: Player is in HighLand state");
                else if (isInAttackState) Debug.Log("Jump blocked: Player is in Attack state");
                else if (jumpDisabledDueToEarlyProneDown) Debug.Log("Jump blocked: Player is in early ProneDown animation");
                else if (inSlipRecover) Debug.Log("Jump blocked: Player is in SlipRecover state");
                else Debug.Log("Jump blocked for unknown reason");
            }
            
            return;
        }
        
        // JUST FOR DEBUGGING - check if slip recover is blocking jump without us knowing
        if (inSlipRecover && GameInputManager.Instance != null && GameInputManager.Instance.GetAndResetJumpButtonPressed())
        {
            Debug.LogWarning("BYPASSING SLIP RECOVER BLOCK - Allowing jump despite SlipRecover state");
            // Don't return here - continue with jump logic
        }

        // Special case: For attack states, still allow gravity and falling transitions
        if (isInAttackState && !isInSpecialRecovery)
        {
            // Allow gravity application but skip the jump logic
        }
        else if (isInAttackState && isInSpecialRecovery)
        {
            // For recovery, apply normal gravity if not grounded
            if (!isGrounded)
            {
                // IMPORTANT: Initialize vertical velocity if it's a sudden drop
                if (verticalVelocity >= 0)
                {
                    // Initialize with small downward velocity instead of large instant drop
                    verticalVelocity = -2f;
                }
                
                // Then apply normal gravity
                verticalVelocity += Physics.gravity.y * Time.deltaTime * gravityScale;
            }
            return;
        }
        
        // Apply gravity when not in jump initialization phase
        if (!isGrounded && !isJumpInitiated)
        {
            // Normal gravity - ALWAYS APPLIED regardless of attack state
            verticalVelocity += Physics.gravity.y * Time.deltaTime * gravityScale;
        }
        else if (verticalVelocity < 0 && isGrounded && !isJumpInitiated)
        {
            // Small negative value when grounded to keep player on slopes
            verticalVelocity = -2f;
        }
        
        // Get direct jump input
        bool jumpPressed = false;
        if (GameInputManager.Instance != null && GameInputManager.Instance.GetAndResetJumpButtonPressed())
        {
            jumpPressed = true;
        }
        else if (Input.GetButtonDown("Jump"))
        {
            jumpPressed = true;
        }
        
        // Get current animation state
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        
        // Check if we're in any prone-related animation
        bool inProneDownAnimation = stateInfo.IsName("ProneDown");
        bool inProneToAnimation = stateInfo.IsName("ProneTo");  // Add this line
        bool inProneIdleAnimation = stateInfo.IsName("ProneIdle");
        bool inCrawlAnimation = stateInfo.IsName("Crawl");
        bool inProneUpAnimation = stateInfo.IsName("ProneUp");
        bool shouldProne = anim.GetBool("ShouldProne");
        
        // Check if this is a consecutive jump (player jumped recently after landing)
        bool isConsecutiveJump = isGrounded && timeSinceLanding <= consecutiveJumpWindow;
        
        // Check if player is running (has significant horizontal speed)
        float currentSpeed = new Vector2(moveDirection.x, moveDirection.z).magnitude;
        bool isRunning = currentSpeed > runningJumpThreshold;
        
        // HANDLE NORMAL GROUND JUMP (only allow jumping when grounded)
        if (isGrounded && jumpPressed && !isJumping && !isJumpInitiated)
        {
            // ALLOW HIGHJUMP FROM: ProneIdle, Crawl, or ProneTo (new!) AND when prone button is pressed
            if ((inProneIdleAnimation || inCrawlAnimation || inProneToAnimation) && 
                !isTransitioningFromProne && proneButtonPressed)
            {
                // Only these states can do high jumps
                Debug.Log("HighJump from prone state: " + 
                        (inProneIdleAnimation ? "ProneIdle" : 
                        inCrawlAnimation ? "Crawl" : "ProneTo"));
                
                HandleHighJumpFromProne();
                return;
            }
            
            // Block jumping during ProneDown completely
            if (inProneDownAnimation)
            {
                Debug.Log("Jump blocked during ProneDown animation");
                return;
            }
            
            // Block jumping during ProneUp
            if (inProneUpAnimation)
            {
                Debug.Log("Jump blocked during ProneUp animation");
                return;
            }
            
            // If ShouldProne is true but we're not in a recognized prone animation,
            // we must be in a transition state - block jumping
            if (shouldProne && !(inProneIdleAnimation || inCrawlAnimation))
            {
                Debug.Log("Jump blocked during prone transition state");
                return;
            }
            
            // If we get here, we're not in any prone state and can do a regular jump
            
            // Skip delay if it's a consecutive jump OR a running jump
            bool skipJumpDelay = isConsecutiveJump || isRunning;
            
            if (skipJumpDelay)
            {
                // Determine which jump force to use
                float jumpForceToUse = isRunning ? runningJumpForce : normalJumpForce;
                
                // Apply jump force immediately
                verticalVelocity = jumpForceToUse;
                isJumping = true;
                
                // Set animation to jumping
                anim.SetBool("IsJumping", true);
                anim.SetBool("IsFalling", false);
                
                if (isRunning)
                {
                    Debug.Log("Running jump executed - force: " + jumpForceToUse);
                }
                else
                {
                    Debug.Log("Consecutive jump executed - time since landing: " + timeSinceLanding.ToString("F3"));
                }
            }
            else
            {
                // Start the prejump phase
                isJumpInitiated = true;
                jumpDelayTimer = 0f;
                
                // Start the prejump animation
                anim.SetBool("IsJumping", true);
                anim.SetBool("IsFalling", false);
                
                Debug.Log("Standard jump initiated");
            }
        }
        
        // Handle the prejump initialization phase - THIS IS WHERE THE DELAY HAPPENS
        if (isJumpInitiated)
        {
            // CRITICAL: Keep Y position exactly the same by forcing velocity to zero
            verticalVelocity = 0f;
            
            // Increment timer
            jumpDelayTimer += Time.deltaTime;
            
            // Calculate the target delay time based on animation frame rate
            float targetDelayTime = (animationFrameWhereJumpStarts / 24f);
            
            // Check if we've waited enough time
            if (jumpDelayTimer >= targetDelayTime)
            {
                // Use normal jump force for standing jumps
                verticalVelocity = normalJumpForce;
                
                // Transition to jumping state
                isJumping = true;
                isJumpInitiated = false;
                
                //Debug.Log("Jump force applied after " + jumpDelayTimer.ToString("F3") + " seconds delay");
            }
        }
        
        // Handle falling transition - ONLY IF NOT IN AN ATTACK STATE
        if (verticalVelocity < 0 && !isGrounded && !isJumpInitiated && !isInAttackState && !inPreJumpAnimation)
        {
            if (!isFalling)
            {
                // Just started falling
                isFalling = true;
                isJumping = false;
                fallTimer = 0f;
                anim.SetBool("IsJumping", false);
                anim.SetBool("IsFalling", true);
            }
            else
            {
                // Already falling, increment timer
                fallTimer += Time.deltaTime;
                
                // Check if we're falling long enough for a splat AND we're getting close to the ground
                if (fallTimer >= splatThreshold)
                {
                    anim.SetBool("IsHighFalling", true);
                    
                    // Check if we're close to the ground using a raycast
                    RaycastHit hit;
                    if (Physics.Raycast(transform.position, Vector3.down, out hit, 1.0f, groundLayer))
                    {
                        // We're about to hit ground and have been falling long enough for splat
                        Debug.Log("About to splat landing - falling for " + fallTimer.ToString("F2") + " seconds");
                    }
                }
                else if (fallTimer >= highLandThreshold)
                {
                    // We've been falling long enough for highland but not splat
                    // No need to set any specific animation parameter yet
                    // Just log the fall duration
                    if (Mathf.Floor(fallTimer * 4) > Mathf.Floor((fallTimer - Time.deltaTime) * 4))
                    {
                        //Debug.Log("Falling for " + fallTimer.ToString("F2") + " seconds - will trigger HighLand on landing");
                    }
                }
                else
                {
                    // Normal fall, not long enough for special landing yet
                    if (Mathf.Floor(fallTimer * 4) > Mathf.Floor((fallTimer - Time.deltaTime) * 4))
                    {
                        //Debug.Log("Falling for " + fallTimer.ToString("F2") + " seconds - normal landing expected");
                    }
                }
            }
        }
    }

    private void HandleHighJumpFromProne()
    {
        // Set up high jump - this is the special jump from prone position
        float highJumpForce = jumpForce * 1.25f; // 25% more force than regular jump
        
        // Exit prone state
        isProning = false;
        isTransitioningToProne = false;
        isTransitioningFromProne = true;
        
        // Set animation parameters
        anim.SetBool("IsHighJumping", true);
        
        // Important: Make sure we're not in Prone animation anymore
        anim.SetBool("ShouldProne", false);
        
        // Apply the enhanced jump force
        verticalVelocity = highJumpForce;
        isJumping = true;
        
        Debug.Log("Executed HIGH JUMP from prone state with force: " + highJumpForce);
        
        // Start a coroutine to transition from high jump to normal fall
        StartCoroutine(TransitionFromHighJump());
    }

    private IEnumerator ForceResetProneStateAfterHighJump()
    {
        // Wait a frame to ensure other updates have happened
        yield return null;
        
        // Force button state detection by temporarily setting previousProneButtonState to false
        previousProneButtonState = false;
        
        // Allow one frame to pass so HandleProneStateInternal can process this change
        yield return null;
        
        // Now HandleProneStateInternal should detect proneButtonPressed as true
        // and previousProneButtonState as false, triggering the transition to prone
        //Debug.Log("Forced prone state reset complete, should trigger prone transition now");
    }

    private IEnumerator TransitionFromHighJump()
    {
        // Wait until we reach the peak of the jump (when vertical velocity becomes negative)
        while (verticalVelocity > 0)
        {
            yield return null;
        }
        
        // We've reached the peak, transition to falling
        anim.SetBool("IsHighJumping", false);
        anim.SetBool("IsFalling", true);
        isJumping = false;
        isFalling = true;
        
        // CRITICAL: Reset transition state but maintain the high jump flag
        isTransitioningFromProne = false;
        
        // Note: Don't reset justPerformedHighJump here, we need it for OnLanding
        
        Debug.Log("Transitioned from HighJump to falling state");
    }

    public void SuspendGravity()
    {
        // This mimics what happens during jump initiation
        // Temporarily disable gravity effects
        isJumpInitiated = true;
        
        // Force vertical velocity to zero (like in HandleJump when isJumpInitiated is true)
        verticalVelocity = 0f;
        
        // Start a short timer to re-enable gravity
        StartCoroutine(ResumeGravityAfterDelay(0.1f)); // Adjust time as needed
    }

    public void CancelUpwardVelocity()
    {
        // If we have upward velocity, cancel it before applying downward force
        if (verticalVelocity > 0)
        {
            //Debug.Log("Canceling upward velocity from " + verticalVelocity + " to 0");
            verticalVelocity = 0f;
        }
    }

    private IEnumerator ResumeGravityAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Only reset if we're not in another jump initiation
        if (isJumpInitiated && !isJumping)
        {
            isJumpInitiated = false;
        }
    }
    
    private void ApplyMovement()
    {
        // Apply vertical velocity to move direction
        moveDirection.y = verticalVelocity;
        
        // Apply movement using character controller
        charController.Move(moveDirection * Time.deltaTime);
    }
    
    private void OnLanding()
    {
        // CRITICAL FIX: Handle SKFall → Highland/Splat transitions BEFORE the early return
        bool wasInSKFall = false;
        if (skFallStateTimer > 0f)
        {
            Debug.Log("Landing from SKFall after " + skFallStateTimer.ToString("F2") + " seconds");
            wasInSKFall = true;
            
            // Check if we should trigger Highland or Splat based on SKFall time
            if (skFallStateTimer >= splatThreshold && anim.GetBool("ShouldSplat"))
            {
                Debug.Log("SKFall → Splat transition - applying Splat control lock");
                // Apply Splat control lock
                isSplatting = true;
                StartCoroutine(WaitForSplatAnimationComplete(false, false));
            }
            else if (skFallStateTimer >= highLandThreshold && anim.GetBool("ShouldHighLand"))
            {
                Debug.Log("SKFall → Highland transition - applying Highland control lock");
                // Apply Highland control lock
                isHighLanding = true;
                StartCoroutine(ClearHighLandAfterDelay(false, false));
            }
            
            // Reset SKFall timer
            skFallStateTimer = 0f;
        }
        
        // If we're landing from a special fall, handle differently
        if (isInSpecialFall)
        {
            // Let the attack controller handle this
            // Don't process normal landing logic
            isJumping = false;
            isFalling = false;
            isJumpInitiated = false;
            jumpDelayTimer = 0f;
            fallTimer = 0f;
            landingTime = Time.time;
            
            // Reset animations
            anim.SetBool("IsJumping", false);
            anim.SetBool("IsFalling", false);
            anim.SetBool("IsHighFalling", false);
            
            // CRITICAL: Don't return early if we're transitioning to Highland/Splat from SKFall
            if (!wasInSKFall || (!isSplatting && !isHighLanding))
            {
                return;
            }
        }

        // Make sure move speed is reset to default
        moveSpeed = 10f;
        Debug.Log("OnLanding - Reset moveSpeed to 10");
        
        // Tell SlipNFall component to ensure move speed is reset
        if (slipNFallController != null)
        {
            slipNFallController.EnsureMoveSpeedReset();
        }

        // Store the prone state before landing
        bool wasProneBeforeLanding = isProning || isTransitioningToProne;
        bool wasProneBtnPressed = proneButtonPressed;

        // Special handling for landing after a high jump while still holding prone button
        if (justPerformedHighJump && proneButtonPressed)
        {
            // Force prone state reset
            isProning = false;
            isTransitioningToProne = false;
            isTransitioningFromProne = false;
            
            // Reset the flag
            justPerformedHighJump = false;
            
            // This is crucial: we need to temporarily "trick" the system 
            // by setting the previousProneButtonState to false
            // so HandleProneStateInternal will detect a "new" button press
            StartCoroutine(ForceResetProneStateAfterHighJump());
            
            //Debug.Log("Reset prone state after landing from high jump with button still held");
        }
        
        // Determine which landing animation to use based on fall time
        if (fallTimer >= splatThreshold)
        {
            // Long fall - trigger Splat animation
            anim.SetBool("ShouldSplat", true);
            isSplatting = true;
            StartCoroutine(WaitForSplatAnimationComplete(wasProneBeforeLanding, wasProneBtnPressed));
            //Debug.Log("SPLAT landing triggered after falling for " + fallTimer.ToString("F2") + " seconds");
        }
        else if (fallTimer >= highLandThreshold)
        {
            // Medium fall - trigger HighLand animation
            anim.SetBool("ShouldHighLand", true);
            isHighLanding = true;
            StartCoroutine(ClearHighLandAfterDelay(wasProneBeforeLanding, wasProneBtnPressed));
            //Debug.Log("HIGH landing triggered after falling for " + fallTimer.ToString("F2") + " seconds");
        }
        else
        {          
            // For normal landing, handle prone state properly
            if (wasProneBtnPressed)
            {
                // Reset prone state variables to trigger proper transition
                // This ensures that if we're still holding the prone button after a jump,
                // we'll go through the prone state setting process again
                isProning = false;
                isTransitioningToProne = true;
                isTransitioningFromProne = false;
                
                // Start coroutine to detect when ProneDown animation completes
                StartCoroutine(WaitForProneDownComplete());
                //Debug.Log("Reset prone state after landing with prone button still pressed");
            }
        }
        
        // Reset jump states
        isJumping = false;
        isFalling = false;
        isJumpInitiated = false;
        jumpDelayTimer = 0f;
        
        // Record landing time
        landingTime = Time.time;
        
        // Update animations
        anim.SetBool("IsJumping", false);
        anim.SetBool("IsFalling", false);
        anim.SetBool("IsHighFalling", false);
        anim.SetBool("IsHighJumping", false); // Make sure to reset the high jump animation flag
        
        // Reset fall timer
        fallTimer = 0f;

        if (anim.GetBool("ShouldProne"))
        {
            // Reset attack cooldown when landing in prone state
            if (attackController != null)
            {
                attackController.ResetAttackCooldown();
            }
        }

        if (attackController != null)
        {
            attackController.NotifyPlayerLanded();
        }
    }

    public bool IsInControlLock()
    {
        // CRITICAL FIX: Make sure SlipRecover behaves exactly like Splat and Highland
        // Check for SlipNFall control lock in addition to highland and splat
        bool inSlipRecover = false;
        
        // More robust check for SlipRecover state that won't fail silently
        if (slipNFallController != null && slipNFallController.enabled)
        {
            try
            {
                inSlipRecover = slipNFallController.IsInSlipRecover();
                
                // Debug confirmation if slip recover is active
                if (inSlipRecover)
                {
                    // Only log if this is a state change to avoid spam
                    //Debug.Log("Control lock active due to SlipRecover state");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error checking SlipRecover state: " + e.Message);
            }
        }
        
        return isHighLanding || isSplatting || inSlipRecover;
    }

    private IEnumerator ClearHighLandAfterDelay(bool wasProneBeforeLanding, bool wasProneBtnPressed)
    {
        // Wait for the control lock duration
        yield return new WaitForSeconds(highLandControlLockDuration);
        
        // Re-enable controls
        isHighLanding = false;
        Debug.Log("Highland control lock released - movement available");
        
        // Wait a bit longer for animation to finish playing or be interrupted
        yield return new WaitForSeconds(0.5f);
        
        // Clear the animation flag if not already interrupted
        if (anim.GetBool("ShouldHighLand"))
        {
            anim.SetBool("ShouldHighLand", false);
            Debug.Log("HighLand animation flag cleared");
            
            // After highland ends, check if we should restore prone state
            if (wasProneBeforeLanding && proneButtonPressed)
            {
                // Restore prone state
                isProning = true;
                isTransitioningToProne = false;
                isTransitioningFromProne = false;
                Debug.Log("Restored prone state after highland");
            }
        }
    }

    private IEnumerator WaitForSplatAnimationComplete(bool wasProneBeforeLanding, bool wasProneBtnPressed)
    {
        // The transition from HighFall to Splat happens immediately
        // This small delay is just to make sure the animation has started
        yield return new WaitForSeconds(0.1f);
        
        // Monitor the animation until it's complete
        while (true)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            
            // If we're in the Splat state and it's almost done, exit
            if (stateInfo.IsName("Splat") && stateInfo.normalizedTime >= 0.95f)
            {
                break;
            }
            
            // If we're no longer in the Splat state, exit
            if (!stateInfo.IsName("Splat") && stateInfo.normalizedTime > 0)
            {
                break;
            }
            
            yield return null;
        }
        
        // Now the animation is complete, safe to reset
        anim.SetBool("ShouldSplat", false);
        isSplatting = false;
        Debug.Log("Splat animation complete - controls restored");
        
        // After splat ends, check if we should restore prone state
        if (wasProneBeforeLanding && proneButtonPressed)
        {
            // Restore prone state
            isProning = true;
            isTransitioningToProne = false;
            isTransitioningFromProne = false;
            //Debug.Log("Restored prone state after splat");
        }
    }

    // Call this from an animation event at the end of Splat animation
    public void OnSplatAnimationComplete()
    {
        anim.SetBool("ShouldSplat", false);
        isSplatting = false;
        Debug.Log("Splat animation event triggered - movement controls restored");
    }
    
    private void UpdateAnimations()
    {
        // Update ground state animation
        anim.SetBool("Grounded", isGrounded);
        
        // Calculate speed for animation - ONLY based on actual movement input/direction
        float currentSpeed = currentHorizontalSpeed;
        anim.SetFloat("Speed", currentSpeed);
        
        // Handle JustLanded state - true as long as we're within the consecutive jump window
        bool justLanded = timeSinceLanding <= consecutiveJumpWindow;
        
        // Only update previousJustLandedState if justLanded actually changes
        if (justLanded != previousJustLandedState)
        {
            previousJustLandedState = justLanded;
            if (justLanded)
            {
                //Debug.Log("JustLanded became TRUE at " + Time.time);
            }
            else
            {
                //Debug.Log("JustLanded became FALSE at " + Time.time);
            }
        }
        
        // Set JustLanded animation parameter
        anim.SetBool("JustLanded", justLanded);
        
        // Determine idle state - character is stationary regardless of prone state and not in attack
        bool isIdle = isGrounded && currentSpeed < 0.1f && !isJumping && !isInAttackState;
        anim.SetBool("IsIdle", isIdle);
        
        // Handle blinking for both idle and running states (if grounded and not jumping/attacking)
        if (isGrounded && !isJumping && !isInAttackState)
        {
            blinkStateTimer -= Time.deltaTime;
            if (blinkStateTimer <= 0f)
            {
                anim.SetBool("ShouldBlink", Random.value < 0.5f);
                blinkStateTimer = blinkStateDuration;
            }
        }
        else
        {
            // If we're not in a state that can blink, make sure blink is off
            anim.SetBool("ShouldBlink", false);
        }
        
        // Idle-specific animations - only if not in special animations or attack
        if (isIdle && !isSplatting && !isHighLanding && !isInAttackState)
        {
            // Increment idle timer
            idleTimer += Time.deltaTime;
            
            // Handle stretching - only in normal idle, not prone idle
            if (idleTimer >= timeUntilStretch && !isProning && !isTransitioningToProne && !isTransitioningFromProne)
            {
                bool shouldStretch = Random.value < stretchChance;
                anim.SetBool("ShouldStretch", shouldStretch);
                idleTimer = 0f;
            }
        }
        else
        {
            // Reset idle-specific behaviors when not idle
            idleTimer = 0f;
            anim.SetBool("ShouldStretch", false);
        }
    }
    
    // ============ ATTACK SYSTEM INTEGRATION ============
    
    // Called by AttackController to execute an attack move
    public void ExecuteAttackMove(AttackMove attackMove)
    {
        // Store the current attack move
        currentAttackMove = attackMove;
        
        // SPECIAL HANDLING FOR SPINKICK DURING PRE-JUMP
        if (attackMove.moveName == "SpinKick" && isJumpInitiated)
        {
            Debug.Log("SpinKick executed during pre-jump - applying jump force immediately");
            
            // Apply the jump force immediately since we were planning to jump anyway
            verticalVelocity = normalJumpForce;
            
            // Complete the jump transition
            isJumping = true;
            isJumpInitiated = false;
            jumpDelayTimer = 0f;
            
            // Keep the jumping animation since we're now actually jumping + spinning
            anim.SetBool("IsJumping", true);
        }
        
        // For SpinKick, ensure other SpinKick-related bools are reset
        if (attackMove.moveName == "SpinKick")
        {
            // Make sure SKFall and SKRecover are OFF before starting SpinKick
            anim.SetBool("SKFall", false);
            anim.SetBool("SKRecover", false);
            Debug.Log("ExecuteAttackMove: Setting SpinKick=TRUE, ensuring SKFall/SKRecover=FALSE");
        }
        
        // SPECIAL CASE FOR UPPERCUT: Don't change any prone state variables
        if (attackMove.moveName == "Uppercut")
        {
            // Only set the Uppercut parameter
            anim.SetBool(attackMove.moveName, true);
        }
        else
        {
            // For other attacks, we still track internal prone state but don't set animation param
            if (attackMove.requiresProne && !isProning)
            {
                isProning = true;
                isTransitioningToProne = false;
                isTransitioningFromProne = false;
            }
            else if (!attackMove.requiresProne && isProning && !proneButtonPressed)
            {
                isProning = false;
                isTransitioningToProne = false;
                isTransitioningFromProne = false;
            }
            
            // Set animation parameter with the attack name
            anim.SetBool(attackMove.moveName, true);
        }
        
        // If this is a dash-like move with horizontal force (but NOT punch since that's handled in AttackController)
        if (attackMove.moveName != "Punch" && attackMove.horizontalForce > 0 && moveDirection.magnitude > 0.1f)
        {
            // Apply the horizontal force in the movement direction
            moveDirection = moveDirection.normalized * attackMove.horizontalForce;
        }
        
        // Add more detailed logging for SpinKick
        if (attackMove.moveName == "SpinKick")
        {
            Debug.Log("Executing SpinKick attack - jump state: jumping=" + isJumping + ", initiated=" + isJumpInitiated);
        }
        else
        {
            Debug.Log("Executing attack move: " + attackMove.moveName);
        }
    }

    public void ApplyAttackLaunchForce(float force)
    {
        // Safety check - don't allow force application during splat or highland
        if (isSplatting || isHighLanding)
        {
            //Debug.LogWarning("Attack launch force BLOCKED - player is in splat/highland state");
            return;
        }
        
        // Direct assignment exactly like jump
        verticalVelocity = force;
    }

    // Add this method for the Ground Pound
    public void ApplyAttackDownwardForce(float force)
    {
        // Log before applying
        //Debug.Log("BEFORE Ground Pound: verticalVelocity = " + verticalVelocity);
        
        // Apply negative force - direct assignment
        verticalVelocity = -force;
        
        // Set appropriate flags
        isJumpInitiated = false;
        isFalling = true;
        isJumping = false;
        
        // Log after applying
        //Debug.Log("AFTER Ground Pound: verticalVelocity = " + verticalVelocity);
    }
        
    // End an attack animation
    public void EndAttackAnimation(AttackMove attackMove)
    {
        // Reset the animation parameter
        anim.SetBool(attackMove.moveName, false);
        
        // CRITICAL FIX: Only exit prone if L1/Shift is not pressed
        // The previous logic was ignoring the button state when ending attacks
        if (isProning && !attackMove.requiresProne && !proneButtonPressed)
        {
            isProning = false;
            //Debug.Log("Exiting prone after attack because prone button is not pressed");
        }
        else if (proneButtonPressed)
        {
            // If the button is still pressed, ensure we stay in prone
            isProning = true;
            isTransitioningToProne = false;
            isTransitioningFromProne = false;
            //Debug.Log("Staying in prone after attack because prone button is still pressed");
        }
        
        Debug.Log("Attack animation ended: " + attackMove.moveName);
    }

    public void UpdateGroundPoundForce(float deltaTime)
    {
        // Increment the timer
        groundPoundForceTimer += deltaTime;
        
        // Check if it's time to increase the force
        if (groundPoundForceTimer >= groundPoundForceIncrementInterval)
        {
            // Increase the force
            currentGroundPoundForce += groundPoundForceIncrement;
            groundPoundForceTimer = 0f; // Reset the timer
            
            // Apply the current force - only if we're in the special fall state
            if (isInSpecialFall)
            {
                verticalVelocity = -currentGroundPoundForce;
                //Debug.Log("GroundPound force increased to: " + currentGroundPoundForce);
            }
        }
    }
    
    // Start special fall state (for ground pound / spinkick)
    public void StartSpecialFall(AttackMove attackMove)
    {
        // Clear the attack animation
        anim.SetBool(attackMove.moveName, false);
        
        // Set the special fall animation
        if (!string.IsNullOrEmpty(attackMove.specialFallAnimName))
        {
            anim.SetBool(attackMove.specialFallAnimName, true);
        }
        
        // Apply the initial forces based on attack type
        if (attackMove.moveName == "GroundPound" && attackMove.downwardForce > 0)
        {
            // GroundPound-specific logic
            currentGroundPoundForce = attackMove.downwardForce;
            groundPoundForceTimer = 0f;
            verticalVelocity = -attackMove.downwardForce;
            groundPoundFallTimer = 0f;
        }
        else if (attackMove.moveName == "SpinKick")
        {
            // SpinKick-specific fall logic
            // Apply a slight downward force if needed, or just let gravity work
            if (verticalVelocity > 0)
            {
                verticalVelocity = -1f; // Small initial downward push
            }
        }
        
        Debug.Log("Started special fall: " + attackMove.specialFallAnimName);
    }

    public void TrackGroundPoundFallTime(float deltaTime)
    {
        groundPoundFallTimer += deltaTime;
        
        // Log the fall time every half second
        if (Mathf.Floor(groundPoundFallTimer * 2) > Mathf.Floor((groundPoundFallTimer - deltaTime) * 2))
        {
            Debug.Log("Ground Pound falling for " + groundPoundFallTimer.ToString("F2") + 
                    " seconds (Splat threshold: " + splatThreshold + ")");
        }
    }

    public void PrepareGroundPoundForSplat()
    {
        groundPoundShouldSplat = true;
        //Debug.Log("Ground Pound is now prepared to splat on landing");
    }

    public bool ShouldGroundPoundSplat()
    {
        // Use a separate ground pound threshold if set, otherwise use regular threshold
        float thresholdToUse = groundPoundSplatThreshold > 0 ? groundPoundSplatThreshold : splatThreshold;
        
        // Consider both fall time and current force when determining splat
        bool shouldSplat = groundPoundShouldSplat || 
                        groundPoundFallTimer >= thresholdToUse || 
                        (currentGroundPoundForce >= 50f); // If force is very high, always splat
        
        Debug.Log("Ground Pound Splat Check: Fall time = " + groundPoundFallTimer.ToString("F2") + 
                ", Force = " + currentGroundPoundForce +
                ", Threshold = " + thresholdToUse + 
                ", Should splat? " + shouldSplat);
        
        return shouldSplat;
    }

    public float GetSplatThreshold()
    {
        // Use the ground pound specific threshold if set
        return groundPoundSplatThreshold > 0 ? groundPoundSplatThreshold : splatThreshold;
    }

    // Transition from ground pound to splat animation
    public void TransitionToSplat(AttackMove attackMove)
    {
        // Clear the special fall animation
        if (!string.IsNullOrEmpty(attackMove.specialFallAnimName))
        {
            anim.SetBool(attackMove.specialFallAnimName, false);
        }
        
        // Reset ground pound state
        groundPoundShouldSplat = false;
        groundPoundFallTimer = 0f;
        
        // Trigger splat animation
        anim.SetBool("ShouldSplat", true);
        isSplatting = true;
        
        // Start coroutine to wait for splat animation
        StartCoroutine(WaitForSplatAnimationComplete(false, false));
        
        Debug.Log("Transitioned from GroundPound to Splat due to extreme height!");
    }
    
    // End special fall (if interrupted)
    public void EndSpecialFall(AttackMove attackMove)
    {
        // Clear the special fall animation
        if (!string.IsNullOrEmpty(attackMove.specialFallAnimName))
        {
            anim.SetBool(attackMove.specialFallAnimName, false);
        }
        
        // Return to normal falling
        if (!isGrounded)
        {
            anim.SetBool("IsFalling", true);
        }
        
        Debug.Log("Ending special fall: " + attackMove.specialFallAnimName);
    }
    
    // Start special recovery after landing
    public void StartSpecialRecovery(AttackMove attackMove)
    {
        // Store the current attack move for reference
        currentAttackMove = attackMove;
        
        // Clear the special fall animation
        if (!string.IsNullOrEmpty(attackMove.specialFallAnimName))
        {
            anim.SetBool(attackMove.specialFallAnimName, false);
        }
        
        // Set the recovery animation
        if (!string.IsNullOrEmpty(attackMove.specialRecoveryAnimName))
        {
            anim.SetBool(attackMove.specialRecoveryAnimName, true);
        }
        
        // Attack-specific recovery behaviors
        if (attackMove.moveName == "GroundPound")
        {
            // GroundPound recovery - reduce downward velocity
            if (verticalVelocity < 0)
            {
                verticalVelocity = Mathf.Max(verticalVelocity, -20f);
                Debug.Log("Reduced vertical velocity during GPRecover to: " + verticalVelocity);
            }
        }
        else if (attackMove.moveName == "SpinKick")
        {
            // SpinKick recovery specific logic if needed
            // For example, we might want to gradually slow horizontal momentum
        }
        
        Debug.Log("Starting special recovery: " + attackMove.specialRecoveryAnimName);
    }
    
    // End special recovery
    public void EndSpecialRecovery(AttackMove attackMove)
    {
        // Clear the recovery animation
        if (!string.IsNullOrEmpty(attackMove.specialRecoveryAnimName))
        {
            anim.SetBool(attackMove.specialRecoveryAnimName, false);
        }
        
        // Return to idle
        anim.SetBool("IsIdle", true);
        
        // Clear the current attack reference
        currentAttackMove = null;
        
        Debug.Log("Special recovery complete");
    }

    public void TriggerUppercutAnimation()
    {
        anim.SetBool("Uppercut", true);
    }

    public void OnUppercutAnimationComplete()
    {
        anim.SetBool("Uppercut", false);
        Debug.Log("Uppercut animation complete - parameter reset");
    }

    private IEnumerator ResetUppercutTrigger(float delay)
    {
        yield return new WaitForSeconds(delay);
        anim.SetBool("Uppercut", false);
    }

    // Public accessors for attack system
    public bool IsGrounded() 
    {
        // Check if SlipNFall wants to override the grounded state
        if (slipNFallController != null && slipNFallController.enabled)
        {
            try
            {
                // Try to call OverrideIsGrounded method on SlipNFall
                System.Reflection.MethodInfo overrideMethod = slipNFallController.GetType().GetMethod("OverrideIsGrounded", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                if (overrideMethod != null)
                {
                    bool overrideResult = (bool)overrideMethod.Invoke(slipNFallController, null);
                    if (overrideResult)
                    {
                        // SlipNFall wants to override - return true regardless of actual grounded state
                        return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error checking for grounded override: " + e.Message);
            }
        }
        
        // Return the normal grounded state if no override
        return isGrounded;
    }
    public bool IsProning() => isProning;
    public Animator GetAnimator() => anim;
    
    // Visual debugging
    private void OnDrawGizmos()
    {
        // Draw ground check sphere
        if (groundCheckPoint != null)
        {
            Gizmos.color = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer) ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }

    public CharacterController GetCharacterController()
    {
        return charController;
    }

    public GameObject GetPlayerModel()
    {
        return playerModel;
    }

    public void ApplyHorizontalForce(Vector3 force)
    {
        // Apply the horizontal force - preserve your current vertical velocity
        moveDirection.x = force.x;
        moveDirection.z = force.z;
        
        // Make sure the player is facing the movement direction
        if (force.magnitude > 0.1f)
        {
            Quaternion newRotation = Quaternion.LookRotation(new Vector3(force.x, 0, force.z));
            playerModel.transform.rotation = Quaternion.Slerp(playerModel.transform.rotation, newRotation, rotateSpeed * Time.deltaTime);
        }
    }

    public float GetGravityScale()
    {
        return gravityScale;
    }

    public void ApplyGravityDuringPunch(float gravityForce)
    {
        // Apply gravity to vertical velocity while maintaining horizontal punch movement
        // This is called when punching off a ledge
        verticalVelocity += gravityForce;
    }
    
    public void SetFallingState(bool falling)
    {
        // Set the falling state directly
        isFalling = falling;
        
        // Update animation parameters as well
        anim.SetBool("IsFalling", falling);
        anim.SetBool("IsJumping", false);
    }

    public bool IsInSlipRecover()
    {
        // This simplified method checks if SlipNFall component exists and is in SlipRecover state
        if (slipNFallController != null)
        {
            bool inSlipRecover = slipNFallController.IsInSlipRecover();
            // For debugging - add this temporarily if needed
            // if (inSlipRecover) Debug.Log("PLAYER: SlipNFall reports we're in SlipRecover state");
            return inSlipRecover;
        }
        return false;
    }
    
    public void CheckControlLockStates()
    {
        // Force immediate rechecking of control lock states
        bool inSlipRecover = slipNFallController != null && slipNFallController.IsInSlipRecover();
        
        // Log the values to debug
        Debug.Log($"Control lock states: Splat={isSplatting}, Highland={isHighLanding}, SlipRecover={inSlipRecover}");
    }

    public void OnLandAnimationStart()
    {
        // Set the max speed to 10 when landing
        moveSpeed = 10f;
        Debug.Log("Land animation started - set max speed to 10");
    }

    public void ForceResetAttackAnimations()
    {
        // Reset common attack animations that might get stuck
        anim.SetBool("Punch", false);
        anim.SetBool("Uppercut", false);
        anim.SetBool("GPFall", false);
        anim.SetBool("GPRecover", false);
        
        Debug.Log("Forcibly reset all attack animation parameters");
    }

    public void OnSpinKickAnimationComplete()
    {
        // Reset the animation parameter when the animation is done
        anim.SetBool("SpinKick", false);
        
        // CRITICAL FIX: Notify the attack controller instead of trying to call its private method
        if (attackController != null)
        {
            // Instead of trying to access private method, just tell the attack controller
            // to handle the SpinKick animation completion
            attackController.ForceEndAttack();
        }
        
        Debug.Log("SpinKick animation complete - parameter reset");
    }

    public void OnSKFallAnimationComplete()
    {
        // Reset the animation parameter when the animation is done
        anim.SetBool("SKFall", false);
        Debug.Log("SKFall animation complete - parameter reset");
    }

    public void OnSKRecoverAnimationComplete()
    {
        // Reset the animation parameter when the animation is done
        anim.SetBool("SKRecover", false);
        Debug.Log("SKRecover animation complete - parameter reset");
    }

    private void TrackSKFallState()
    {
        // Check if we're currently in SKFall animation state (only check the actual animation state)
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool currentlyInSKFall = stateInfo.IsName("SKFall");
        
        if (currentlyInSKFall)
        {
            // Check if this is the first frame we entered SKFall
            if (skFallStateTimer == 0f && !wasInSKFallLastFrame)
            {
                // Just entered SKFall - inherit the original fall time
                skFallStateTimer = fallTimer;
                Debug.Log("Entered SKFall - inherited fall time: " + skFallStateTimer.ToString("F2") + "s");
            }
            else
            {
                // Already in SKFall, increment the timer normally
                skFallStateTimer += Time.deltaTime;
            }
            
            // Check if we've been in SKFall long enough for splat
            if (skFallStateTimer >= splatThreshold)
            {
                // At 0.8s+: Set ShouldHighLand to false and ShouldSplat to true
                if (!anim.GetBool("ShouldSplat"))
                {
                    Debug.Log("SKFall exceeded splat threshold (" + splatThreshold + "s) - setting ShouldHighLand=false, ShouldSplat=true");
                    anim.SetBool("ShouldHighLand", false);
                    anim.SetBool("ShouldSplat", true);
                    anim.SetBool("IsHighFalling", true); // Visual feedback
                }
            }
            // Check if we've been in SKFall long enough for highland (but not splat yet)
            else if (skFallStateTimer >= highLandThreshold)
            {
                // At 0.6s-0.8s: Set ShouldHighLand to true (if not already set)
                if (!anim.GetBool("ShouldHighLand"))
                {
                    Debug.Log("SKFall exceeded highland threshold (" + highLandThreshold + "s) - setting ShouldHighLand=true");
                    anim.SetBool("ShouldHighLand", true);
                }
            }
            
            // Log progress every half second
            if (Mathf.Floor(skFallStateTimer * 2) > Mathf.Floor((skFallStateTimer - Time.deltaTime) * 2))
            {
                Debug.Log("SKFall active for " + skFallStateTimer.ToString("F2") + "s (Highland@" + highLandThreshold + "s, Splat@" + splatThreshold + "s)");
            }
        }
        else
        {
            // We're not in SKFall state anymore, reset the timer
            if (skFallStateTimer > 0f)
            {
                Debug.Log("Exited SKFall after " + skFallStateTimer.ToString("F2") + " seconds total");
                skFallStateTimer = 0f;
                
                // NEW: Reset moveSpeed when exiting SKFall
                if (moveSpeed != 10f)
                {
                    moveSpeed = 10f;
                    Debug.Log("Reset moveSpeed to 10f upon exiting SKFall");
                }
            }
        }
        
        // Track if we were in SKFall this frame for next frame's comparison
        wasInSKFallLastFrame = currentlyInSKFall;
    }

    private void CheckForSKFallExit()
    {
        // Get current animation state
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool currentlyInSKFall = stateInfo.IsName("SKFall");
        
        // If we were in SKFall last frame but not anymore, reset moveSpeed
        if (wasInSKFallLastFrame && !currentlyInSKFall)
        {
            moveSpeed = 10f;
            Debug.Log("Exited SKFall - reset moveSpeed to 10f");
        }
        
        // Update the tracking variable
        wasInSKFallLastFrame = currentlyInSKFall;
    }
}