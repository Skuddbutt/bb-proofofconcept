using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlipNFall : MonoBehaviour
{
    // Minimal version of SlipNFall without any jump interference
    
    [Header("References")]
    private PlayerController playerController;
    private AttackController attackController;
    private CharacterController charController;
    private Animator anim;
    
    [Header("Button Mashing Detection")]
    [Tooltip("Time window to track mashing for each button (seconds)")]
    public float mashingWindow = 2f;
    
    [Header("Button-Specific Thresholds")]
    [Tooltip("Number of Prone button (L1/Shift) presses to trigger slip")]
    public int proneThreshold = 3;
    
    [Tooltip("Number of Medium Attack (Square/Left Click) presses to trigger slip")]
    public int mediumAttackThreshold = 5;
    
    [Tooltip("Number of Light Attack presses to trigger slip")]
    public int lightAttackThreshold = 5;
    
    [Tooltip("Number of Heavy Attack presses to trigger slip")]
    public int heavyAttackThreshold = 5;
    
    [Tooltip("Number of Jump (X/Space) presses to trigger slip")]
    public int jumpThreshold = 4;
    
    // Track button press counts and timers for each button
    private class ButtonTracker
    {
        public int pressCount = 0;
        public float timer = 0f;
        public bool isTracking = false;
    }
    
    private ButtonTracker proneTracker = new ButtonTracker();
    private ButtonTracker mediumAttackTracker = new ButtonTracker();
    private ButtonTracker lightAttackTracker = new ButtonTracker();
    private ButtonTracker heavyAttackTracker = new ButtonTracker();
    private ButtonTracker jumpTracker = new ButtonTracker();
    
    [Header("Animation State Tracking")]
    private bool wasInAttackState = false;
    private bool hasResetTimersForCurrentAttack = false;
    
    [Header("IdleToRun Stuck Detection")]
    private float idleToRunTimer = 0f;
    private bool wasInIdleToRun = false;
    [Tooltip("Time threshold for being stuck in IdleToRun before triggering slip")]
    public float idleToRunThreshold = 0.5f;
    
    [Header("Slip Settings")]
    [Tooltip("Duration of the slip state")]
    public float slipDuration = 1.5f;
    [Tooltip("Movement speed during the initial slip")]
    public float slipMovementSpeed = 3f;
    
    // Slip state tracking
    private bool isSlipping = false;
    private bool isInSlipRecover = false;
    private float slipTimer = 0f;
    private Vector3 slipDirection = Vector3.zero;
    private float originalMoveSpeed;
    
    // Direction where player was facing when slip began
    private Vector3 facingDirectionWhenSlipped = Vector3.zero;
    
    // Cooldown to prevent consecutive slips
    private float slipCooldownTimer = 0f;
    [Tooltip("Time before player can slip again")]
    public float slipCooldown = 5f;
    
    // Previous button states for edge detection
    private bool prevProneState = false;
    private bool prevLightAttackState = false;
    private bool prevMediumAttackState = false;
    private bool prevHeavyAttackState = false;
    private bool jumpWasPressed = false;
    
    private bool wasOriginallyGrounded;
    private bool jumpInputConsumed = false;

    private void Awake()
    {
        // Initialize references in Awake
        playerController = GetComponent<PlayerController>();
        attackController = GetComponent<AttackController>();
        
        if (playerController != null)
        {
            anim = playerController.GetAnimator();
            charController = playerController.GetCharacterController();
            originalMoveSpeed = playerController.moveSpeed;
        }
        else
        {
            Debug.LogError("SlipNFall: PlayerController not found!");
            enabled = false;
        }
        
        // Initialize to safe values
        isSlipping = false;
        isInSlipRecover = false;
    }

    private void Update()
    {
        // Reset jump input consumed flag
        jumpInputConsumed = false;
        
        // Skip if game is paused
        if (IsPaused())
            return;

        // CRITICAL: Monitor attack state changes to reset spam timers
        bool currentlyInAttackState = attackController != null && attackController.IsInAttackState();
        
        // Detect when an attack just started
        if (!wasInAttackState && currentlyInAttackState && !hasResetTimersForCurrentAttack)
        {
            // Player just launched an attack - reset all spam timers once
            ResetAllTrackers();
            hasResetTimersForCurrentAttack = true;
            Debug.Log("Attack launched - reset all button spam timers");
        }
        
        // Detect when attack ends to allow timer reset for next attack
        if (wasInAttackState && !currentlyInAttackState)
        {
            // Attack ended - allow timer reset for next attack
            hasResetTimersForCurrentAttack = false;
            Debug.Log("Attack ended - spam timer reset available for next attack");
        }
        
        // Update previous attack state for next frame
        wasInAttackState = currentlyInAttackState;

        // CRITICAL: Check for being stuck in IdleToRun animation
        CheckIdleToRunStuck();

        // Critical: Always check animation state to detect SlipRecover transitions
        if (anim != null)
        {
            // This is our animation event replacement logic
            AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
            bool currentlyInSlipRecover = currentState.IsName("SlipRecover");
            
            // If we're in SlipRecover, force parameters every frame
            if (currentlyInSlipRecover)
            {
                // Keep isInSlipRecover true
                isInSlipRecover = true;
                
                // Force parameters
                anim.SetBool("Grounded", true);
                anim.SetBool("IsFalling", false);
                
                // Force speed to zero
                if (playerController != null)
                {
                    playerController.moveSpeed = 0f;
                }
            }
            // If we just exited SlipRecover and it was fully played
            else if (isInSlipRecover && !currentlyInSlipRecover && 
                    // Check for completed animation or transitioning to different state
                    (currentState.normalizedTime >= 1.0f || !currentState.IsName("SlipRecover")))
            {
                // Now it's safe to reset isInSlipRecover
                isInSlipRecover = false;
                
                // Restore movement speed
                if (playerController != null && originalMoveSpeed > 0)
                {
                    playerController.moveSpeed = originalMoveSpeed;
                    Debug.Log($"Detected SlipRecover exit in Update - restored movement speed to {originalMoveSpeed}");
                }
            }
        }

        // Manage slip cooldown
        if (slipCooldownTimer > 0)
        {
            slipCooldownTimer -= Time.deltaTime;
        }
        
        // If currently slipping, manage the slip state
        if (isSlipping)
        {
            ManageSlipState();
            
            // Additional check for punch force interference
            CheckForPunchForceInterference();
        }
        else
        {
            // Track button presses during attacks too - we want spam detection active
            // Only skip during splats, highland, ground pound, uppercut, or cooldown
            if (!playerController.IsSplatting() && 
                !playerController.IsHighLanding() &&
                !IsInGroundPoundOrSpinKickFallState() &&
                !IsInUppercutState() &&
                slipCooldownTimer <= 0)
            {
                CheckForButtonMashing();
            }
        }
        
        // Update button trackers
        UpdateButtonTrackers();
    }

    private bool IsPaused()
    {
        PauseMenu pauseMenu = FindObjectOfType<PauseMenu>();
        return pauseMenu != null && pauseMenu.IsPaused;
    }
    
    private void UpdateButtonTrackers()
    {
        // Update all active button trackers
        UpdateTracker(proneTracker);
        UpdateTracker(mediumAttackTracker);
        UpdateTracker(lightAttackTracker);
        UpdateTracker(heavyAttackTracker);
        UpdateTracker(jumpTracker);
    }
    
    private void UpdateTracker(ButtonTracker tracker)
    {
        // If tracker is active, update its timer
        if (tracker.isTracking)
        {
            tracker.timer += Time.deltaTime;
            
            // Reset tracker if time window expired
            if (tracker.timer >= mashingWindow)
            {
                tracker.isTracking = false;
                tracker.pressCount = 0;
                tracker.timer = 0f;
            }
        }
    }

    private void CheckForButtonMashing()
    {
        // Check each button separately
        bool pronePressed = false;
        bool lightAttackPressed = false;
        bool mediumAttackPressed = false;
        bool heavyAttackPressed = false;
        bool jumpPressed = false;
        
        // Get current button states using GameInputManager if available
        if (GameInputManager.Instance != null)
        {
            // Check prone button (L1/Shift)
            pronePressed = GameInputManager.Instance.GetProneInput();
            if (pronePressed && !prevProneState)
            {
                TrackButtonPress(proneTracker, proneThreshold, "Prone");
            }
            
            // Check medium attack button (Square/Left Click)
            mediumAttackPressed = GameInputManager.Instance.GetMediumAttackInput();
            if (mediumAttackPressed && !prevMediumAttackState)
            {
                TrackButtonPress(mediumAttackTracker, mediumAttackThreshold, "Medium Attack");
            }
            
            // Check light attack button
            lightAttackPressed = GameInputManager.Instance.GetLightAttackInput();
            if (lightAttackPressed && !prevLightAttackState)
            {
                TrackButtonPress(lightAttackTracker, lightAttackThreshold, "Light Attack");
            }
            
            // Check heavy attack button
            heavyAttackPressed = GameInputManager.Instance.GetHeavyAttackInput();
            if (heavyAttackPressed && !prevHeavyAttackState)
            {
                TrackButtonPress(heavyAttackTracker, heavyAttackThreshold, "Heavy Attack");
            }
            
            // CRITICAL CHANGE: Check for jump without consuming the input
            // We'll just use a simple flag to know if we've seen a jump press this frame
            bool jumpButtonPressed = Input.GetButtonDown("Jump");
            if (jumpButtonPressed && !jumpInputConsumed)
            {
                TrackButtonPress(jumpTracker, jumpThreshold, "Jump");
                jumpInputConsumed = true;
            }
        }
        else
        {
            // Fallback to legacy input if needed
            pronePressed = Input.GetKey(KeyCode.LeftShift);
            if (pronePressed && !prevProneState)
            {
                TrackButtonPress(proneTracker, proneThreshold, "Prone");
            }
            
            mediumAttackPressed = Input.GetMouseButtonDown(0);
            if (mediumAttackPressed)
            {
                TrackButtonPress(mediumAttackTracker, mediumAttackThreshold, "Medium Attack");
            }
            
            // Use E for light attack in legacy input
            lightAttackPressed = Input.GetKeyDown(KeyCode.E);
            if (lightAttackPressed)
            {
                TrackButtonPress(lightAttackTracker, lightAttackThreshold, "Light Attack");
            }
            
            // Use Right Mouse for heavy attack in legacy input
            heavyAttackPressed = Input.GetMouseButtonDown(1);
            if (heavyAttackPressed)
            {
                TrackButtonPress(heavyAttackTracker, heavyAttackThreshold, "Heavy Attack");
            }
            
            // Check jump without consuming input
            jumpPressed = Input.GetButtonDown("Jump");
            if (jumpPressed && !jumpInputConsumed)
            {
                TrackButtonPress(jumpTracker, jumpThreshold, "Jump");
                jumpInputConsumed = true;
            }
        }
        
        // Update previous states for edge detection
        prevProneState = pronePressed;
        prevLightAttackState = lightAttackPressed;
        prevMediumAttackState = mediumAttackPressed;
        prevHeavyAttackState = heavyAttackPressed;
        jumpWasPressed = jumpPressed;
    }
    
    private void TrackButtonPress(ButtonTracker tracker, int threshold, string buttonName)
    {
        // Start tracking if this is the first press
        if (!tracker.isTracking)
        {
            tracker.isTracking = true;
            tracker.pressCount = 1;
            tracker.timer = 0f;
            Debug.Log($"Started tracking {buttonName} button mashing (1/{threshold})");
        }
        else
        {
            // Increment press count
            tracker.pressCount++;
            Debug.Log($"Button mashing progress: {buttonName} ({tracker.pressCount}/{threshold}) in {tracker.timer.ToString("F2")}s");
            
            // Check if threshold reached
            if (tracker.pressCount >= threshold)
            {
                Debug.Log($"BUTTON MASHING DETECTED on {buttonName}! ({tracker.pressCount} presses in {tracker.timer.ToString("F2")} seconds)");
                
                // Trigger slip
                TriggerSlip();
                
                // Reset tracker
                tracker.isTracking = false;
                tracker.pressCount = 0;
                tracker.timer = 0f;
            }
        }
    }

    private void TriggerSlip()
    {
        // Only allow slip if not already slipping
        if (isSlipping)
        {
            Debug.Log("Slip trigger IGNORED - already slipping");
            return;
        }

        // Force stop any active punch force
        if (attackController != null && attackController.IsPunchForceActive())
        {
            attackController.StopPunchForce();
        }

        // Log current animation state
        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
        Debug.Log($"EXECUTING SLIP - Current animation: {currentState.shortNameHash} ({AnimationName(currentState)}), normalized time: {currentState.normalizedTime:F2}");
        
        // Cancel any attack state if the player is attacking
        if (attackController != null && attackController.IsInAttackState())
        {
            Debug.Log("Canceling current attack to trigger slip");
            attackController.ForceEndAttack();
        }
        
        // Reset all animation parameters that might prevent Slip from playing
        ResetAnimationParameters();
        
        // Store the direction player is facing
        facingDirectionWhenSlipped = playerController.GetPlayerModel().transform.forward;
        
        // Calculate slip direction
        Vector3 moveDir = charController.velocity;
        moveDir.y = 0;  // Ignore vertical movement
        if (moveDir.magnitude > 0.1f)
        {
            slipDirection = Vector3.Lerp(facingDirectionWhenSlipped, moveDir.normalized, 0.3f).normalized;
        }
        else
        {
            slipDirection = facingDirectionWhenSlipped;
        }
        
        // Store original move speed before changing it
        originalMoveSpeed = playerController.moveSpeed;
        
        // Initialize slip state
        isSlipping = true;
        slipTimer = 0f;
        
        // Set PlayerController's moveSpeed to slipMovementSpeed
        playerController.moveSpeed = slipMovementSpeed;
        Debug.Log($"Setting slip movement speed cap to {slipMovementSpeed}");
        
        // Set the Slip animation trigger
        anim.SetBool("Slip", true);
        
        // Force play the slip animation directly
        anim.Play("Slip", 0, 0f);
        
        // Reset the Slip bool parameter after a short delay
        StartCoroutine(ResetSlipParameterAfterDelay(0.1f));
        
        // Reset all button trackers (redundant but safe)
        ResetAllTrackers();
        
        // Start monitoring for animation transitions
        StartCoroutine(MonitorSlipAnimationFlow());
        
        Debug.Log("SLIP TRIGGERED! Player is now slipping");
    }

    private void ResetAnimationParameters()
    {
        // Reset common animation parameters that might prevent Slip from playing
        anim.SetBool("IsJumping", false);
        anim.SetBool("IsFalling", false);
        anim.SetBool("IsHighFalling", false);
        anim.SetBool("IsHighJumping", false);
        anim.SetBool("Punch", false);
        anim.SetBool("Uppercut", false);
        anim.SetBool("GPFall", false);
        anim.SetBool("GPRecover", false);
        
        // Also reset SpinKick animations
        anim.SetBool("SpinKick", false);
        anim.SetBool("SKFall", false);
        anim.SetBool("SKRecover", false);
        
        Debug.Log("Reset all animation parameters to ensure Slip can play");
    }
    
    private string AnimationName(AnimatorStateInfo stateInfo)
    {
        // This function tries to identify common animation states
        if (stateInfo.IsName("Idle")) return "Idle";
        if (stateInfo.IsName("Run")) return "Run";
        if (stateInfo.IsName("Jump")) return "Jump";
        if (stateInfo.IsName("Fall")) return "Fall";
        if (stateInfo.IsName("Slip")) return "Slip";
        if (stateInfo.IsName("SlipRecover")) return "SlipRecover";
        if (stateInfo.IsName("Land")) return "Land";
        if (stateInfo.IsName("Punch")) return "Punch";
        if (stateInfo.IsName("Uppercut")) return "Uppercut";
        if (stateInfo.IsName("Splat")) return "Splat";
        if (stateInfo.IsName("HighLand")) return "HighLand";
        if (stateInfo.IsName("ProneIdle")) return "ProneIdle";
        if (stateInfo.IsName("Crawl")) return "Crawl";
        if (stateInfo.IsName("ProneDown")) return "ProneDown";
        if (stateInfo.IsName("ProneUp")) return "ProneUp";
        if (stateInfo.IsName("GPFall")) return "GPFall";
        if (stateInfo.IsName("GPRecover")) return "GPRecover";
        if (stateInfo.IsName("SpinKick")) return "SpinKick";
        if (stateInfo.IsName("SKFall")) return "SKFall";
        if (stateInfo.IsName("SKRecover")) return "SKRecover";
        
        // If no match found, return the hash
        return "Unknown State " + stateInfo.shortNameHash;
    }
    
    private IEnumerator ResetSlipParameterAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        anim.SetBool("Slip", false);
        Debug.Log("Slip parameter reset to false after delay to allow transitions");
    }
    
    private void ResetAllTrackers()
    {
        // Count how many trackers were actually active
        int activeTrackers = 0;
        if (proneTracker.isTracking) activeTrackers++;
        if (mediumAttackTracker.isTracking) activeTrackers++;
        if (lightAttackTracker.isTracking) activeTrackers++;
        if (heavyAttackTracker.isTracking) activeTrackers++;
        if (jumpTracker.isTracking) activeTrackers++;
        
        if (activeTrackers > 0)
        {
            Debug.Log($"Resetting {activeTrackers} active button spam trackers");
        }
        
        // Reset all button trackers
        proneTracker.isTracking = false;
        proneTracker.pressCount = 0;
        proneTracker.timer = 0f;
        
        mediumAttackTracker.isTracking = false;
        mediumAttackTracker.pressCount = 0;
        mediumAttackTracker.timer = 0f;
        
        lightAttackTracker.isTracking = false;
        lightAttackTracker.pressCount = 0;
        lightAttackTracker.timer = 0f;
        
        heavyAttackTracker.isTracking = false;
        heavyAttackTracker.pressCount = 0;
        heavyAttackTracker.timer = 0f;
        
        jumpTracker.isTracking = false;
        jumpTracker.pressCount = 0;
        jumpTracker.timer = 0f;
    }

    private void ManageSlipState()
    {
        // Update slip timer
        slipTimer += Time.deltaTime;
        
        // Get current animation state
        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
        bool inSlipAnimation = currentState.IsName("Slip");
        bool inSlipRecoverAnimation = currentState.IsName("SlipRecover");
        
        // CRITICAL FIX: Only apply movement if we're actually in Slip or SlipRecover animations
        if (!inSlipAnimation && !inSlipRecoverAnimation)
        {
            // We're not in slip animations anymore - don't apply ANY movement
            Debug.Log("ManageSlipState: Not in slip animations - skipping movement application");
            return;
        }
        
        // Force player model to face the slip direction
        if (inSlipAnimation && slipDirection.magnitude > 0.01f)
        {
            GameObject playerModel = playerController.GetPlayerModel();
            Quaternion targetRotation = Quaternion.LookRotation(slipDirection);
            playerModel.transform.rotation = targetRotation;
        }
        
        // Apply movement based on the current animation state
        if (inSlipAnimation) 
        {
            Vector3 moveDirection = Vector3.zero;
            
            // Apply constant forward movement in the slip direction
            moveDirection = slipDirection * slipMovementSpeed * Time.deltaTime;
            
            // Apply VERY MINIMAL downward force to maintain stability
            moveDirection.y = -0.1f * Time.deltaTime; // REDUCED from -0.2f
            
            // Apply the movement directly to the character controller
            charController.Move(moveDirection);
            
            // Clear PlayerController's moveDirection to prevent input-based movement
            ClearPlayerControllerMovement();
            
            // Check if we need to transition to SlipRecover
            if (slipTimer >= slipDuration && !isInSlipRecover)
            {
                isInSlipRecover = true;
                Debug.Log($"Slip duration ({slipDuration}s) reached - transitioning to SlipRecover");
                
                if (playerController != null)
                {
                    playerController.CheckControlLockStates();
                }
            }
        }
        else if (inSlipRecoverAnimation)
        {
            if (!isInSlipRecover)
            {
                isInSlipRecover = true;
                Debug.Log("In SlipRecover animation but flag wasn't set - fixed");
                
                if (playerController != null)
                {
                    playerController.CheckControlLockStates();
                }
            }
            
            // CRITICAL FIX: Apply NO movement during SlipRecover - let PlayerController handle gravity
            // The PlayerController will handle gravity naturally, we don't need to force anything
            //Debug.Log("SlipRecover: Letting PlayerController handle all movement including gravity");
            
            // Clear any movement from PlayerController to ensure no horizontal movement
            ClearPlayerControllerMovement();
        }
    }

    private void ClearPlayerControllerMovement()
    {
        if (playerController != null)
        {
            try
            {
                var field = playerController.GetType().GetField("moveDirection", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                if (field != null)
                {
                    // Force X and Z components to 0, preserve Y (gravity) ONLY during Slip
                    Vector3 currentMoveDir = (Vector3)field.GetValue(playerController);
                    
                    // During SlipRecover, preserve ALL PlayerController movement (including gravity)
                    AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
                    if (currentState.IsName("SlipRecover"))
                    {
                        // During SlipRecover, only clear horizontal movement, preserve vertical
                        Vector3 newMoveDir = new Vector3(0, currentMoveDir.y, 0);
                        field.SetValue(playerController, newMoveDir);
                    }
                    else if (currentState.IsName("Slip"))
                    {
                        // During Slip, clear horizontal movement, preserve vertical
                        Vector3 newMoveDir = new Vector3(0, currentMoveDir.y, 0);
                        field.SetValue(playerController, newMoveDir);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error clearing PlayerController moveDirection: " + e.Message);
            }
        }
    }

    public bool ShouldBlockRotation()
    {
        return isSlipping;
    }

    private IEnumerator MonitorSlipAnimationFlow()
    {
        // Wait until the Slip animation has started
        yield return new WaitForSeconds(0.1f);
        
        // REMOVED: bool inSlipRecover = false; <-- This variable was never used
        bool slipRecoverComplete = false;
        bool wasInSlipRecover = false; // To track transitions
        
        while (isSlipping && !slipRecoverComplete)
        {
            AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
            bool currentlyInSlipRecover = currentState.IsName("SlipRecover");
            
            // DETECT ENTERING SlipRecover
            if (!wasInSlipRecover && currentlyInSlipRecover)
            {
                // We just entered SlipRecover animation
                isInSlipRecover = true; // Set the class variable directly
                
                // FORCE movement speed to zero when entering SlipRecover
                if (playerController != null)
                {
                    // Store original move speed for later restoration
                    if (originalMoveSpeed <= 0)
                    {
                        originalMoveSpeed = playerController.moveSpeed;
                    }
                    
                    // Absolutely force moveSpeed to zero for SlipRecover
                    playerController.moveSpeed = 0f;
                    
                    // Force PlayerController to update its control lock state
                    playerController.CheckControlLockStates();
                    
                    Debug.Log("Entered SlipRecover - force set moveSpeed to 0 and locked controls");
                }
                
                // CRITICAL: Force animation parameters to correct values
                if (anim != null)
                {
                    // Explicitly set Grounded to TRUE
                    anim.SetBool("Grounded", true);
                    
                    // Explicitly set IsFalling to FALSE
                    anim.SetBool("IsFalling", false);
                    
                    Debug.Log("Force set animation parameters: Grounded=TRUE, IsFalling=FALSE");
                }
            }
            
            // Continue forcing moveSpeed to zero and animation parameters every frame during SlipRecover
            if (currentlyInSlipRecover)
            {
                if (playerController != null)
                {
                    playerController.moveSpeed = 0f;
                }
                
                // Reinforce animation parameters every frame during SlipRecover
                if (anim != null)
                {
                    // Keep Grounded TRUE
                    if (!anim.GetBool("Grounded"))
                    {
                        anim.SetBool("Grounded", true);
                    }
                    
                    // Keep IsFalling FALSE
                    if (anim.GetBool("IsFalling"))
                    {
                        anim.SetBool("IsFalling", false);
                    }
                }
                
                // DETECT COMPLETION of SlipRecover
                if (currentState.normalizedTime >= 1.0f)
                {
                    // Animation has fully completed
                    slipRecoverComplete = true;
                    isInSlipRecover = false;
                    
                    // Restore original speed when recovery fully completes
                    if (playerController != null && originalMoveSpeed > 0)
                    {
                        playerController.moveSpeed = originalMoveSpeed;
                        Debug.Log($"SlipRecover animation FULLY completed - controls restored, speed reset to {originalMoveSpeed}");
                    }
                }
            }
            
            // DETECT EXITING SlipRecover to another state
            if (wasInSlipRecover && !currentlyInSlipRecover)
            {
                // We just exited SlipRecover to another animation
                slipRecoverComplete = true;
                isInSlipRecover = false;
                
                // Restore original speed when transitioning to another state
                if (playerController != null && originalMoveSpeed > 0)
                {
                    playerController.moveSpeed = originalMoveSpeed;
                    Debug.Log($"Transitioned from SlipRecover to {AnimationName(currentState)} - controls restored");
                }
            }
            
            // Check if we've fallen off a ledge during slip (but only during actual slip, not after recovery)
            if (!currentlyInSlipRecover && !playerController.IsGrounded() && slipTimer > 0.5f && isSlipping && !slipRecoverComplete)
            {
                playerController.moveSpeed = 8f;
                Debug.Log("Slipped off ledge - transitioning to fall state");
            }
            
            // Update wasInSlipRecover for next frame
            wasInSlipRecover = currentlyInSlipRecover;
            
            yield return null;
        }
        
        EndSlip();
    
        // Add this extra check to ensure speed is reset properly
        yield return new WaitForSeconds(0.5f);
        
        // If we're not slipping and not in slip recover, double-check speed
        if (!isSlipping && !isInSlipRecover && playerController != null)
        {
            if (playerController.moveSpeed < originalMoveSpeed)
            {
                playerController.moveSpeed = originalMoveSpeed;
                Debug.Log("Post-slip check - Restored speed to: " + originalMoveSpeed);
            }
        }
    }

    private void EndSlip()
    {
        isSlipping = false;
        isInSlipRecover = false;
        
        // Ensure movement speed is always restored to original value
        if (playerController != null)
        {
            // Add a safeguard in case originalMoveSpeed wasn't set properly
            if (originalMoveSpeed <= 0)
            {
                Debug.LogWarning("originalMoveSpeed was invalid: " + originalMoveSpeed + ", using default of 10");
                originalMoveSpeed = 10f;
            }
            
            playerController.moveSpeed = originalMoveSpeed;
            Debug.Log("EndSlip - Restored original speed to: " + originalMoveSpeed);
        }
        
        anim.SetBool("Slip", false);
        slipCooldownTimer = slipCooldown;
        
        // CRITICAL: Stop applying any movement from SlipNFall
        Debug.Log("EndSlip - SlipNFall will no longer apply any movement");
        
        StartCoroutine(ResetSlipParameterAfterAnimation());
    }
    
    private IEnumerator ResetSlipParameterAfterAnimation()
    {
        yield return new WaitForSeconds(0.1f);
        
        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
        
        if (currentState.IsName("Land"))
        {
            while (true)
            {
                currentState = anim.GetCurrentAnimatorStateInfo(0);
                if (currentState.IsName("Land") && currentState.normalizedTime >= 0.9f)
                {
                    break;
                }
                yield return null;
            }
        }
        
        anim.SetBool("Slip", false);
        Debug.Log("Animation sequence complete - Slip parameter reset");
    }
    
    public void OnSlipRecoverAnimationStarted()
    {
        isInSlipRecover = true;
        Debug.Log("SlipRecover animation started via event");
        
        // Force movement speed to zero when SlipRecover starts
        if (playerController != null)
        {
            // Store the current speed if we haven't already
            if (originalMoveSpeed <= 0)
            {
                originalMoveSpeed = playerController.moveSpeed;
            }
            
            // Force speed to zero to block movement
            playerController.moveSpeed = 0f;
            Debug.Log("Force set moveSpeed to 0 in SlipRecover animation event");
            
            // Notify PlayerController of state change
            playerController.CheckControlLockStates();
        }
    }

    public void OnSlipRecoverAnimationEnded()
    {
        isInSlipRecover = false;
        Debug.Log("SlipRecover animation ended via event");
        
        // Restore original move speed with additional safeguards
        if (playerController != null)
        {
            if (originalMoveSpeed <= 0)
            {
                Debug.LogWarning("originalMoveSpeed was invalid: " + originalMoveSpeed + ", using default of 10");
                originalMoveSpeed = 10f;
            }
            
            playerController.moveSpeed = originalMoveSpeed;
            Debug.Log("SlipRecover ended - Restored original moveSpeed: " + originalMoveSpeed + " (was: " + playerController.moveSpeed + ")");
            
            // Notify PlayerController of state change
            playerController.CheckControlLockStates();
        }
        
        // End slip state if still active
        if (isSlipping)
        {
            EndSlip();
        }
    }

    public void OnLandAnimationEnded()
    {
        Debug.Log("Land animation ended - event received");
        
        if (anim.GetBool("Slip"))
        {
            anim.SetBool("Slip", false);
        }
        
        // IMPORTANT ADDITION: Ensure max speed is reset when landing
        if (playerController != null)
        {
            // Always reset to default speed after landing
            playerController.moveSpeed = 10f;
            Debug.Log("Land animation ended - forced max speed to 10");
        }
    }

    public void EnsureMoveSpeedReset()
    {
        if (playerController != null)
        {
            // Always reset to default speed if we're not currently slipping
            if (!isSlipping && !isInSlipRecover)
            {
                // Make sure original speed is valid
                if (originalMoveSpeed <= 0)
                {
                    originalMoveSpeed = 10f;
                }
                
                // Restore original speed
                playerController.moveSpeed = originalMoveSpeed;
                Debug.Log("Forced move speed reset to: " + originalMoveSpeed);
            }
        }
    }
    
    public void DebugTriggerSlip()
    {
        if (!isSlipping && slipCooldownTimer <= 0)
        {
            TriggerSlip();
        }
    }
    
    // Public API
    public bool IsSlipping()
    {
        return isSlipping;
    }
    
    public bool IsInSlipRecover()
    {
        // Since we can't use animation events, we need to make this method more robust
        // by checking the current animation state directly
        
        if (anim != null)
        {
            AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
            bool currentlyInSlipRecover = currentState.IsName("SlipRecover");
            
            // If our flag doesn't match the actual animation state, fix it
            if (currentlyInSlipRecover && !isInSlipRecover)
            {
                // Animation is in SlipRecover but our flag says we're not
                isInSlipRecover = true;
                Debug.LogWarning("Fixed isInSlipRecover flag - animation is in SlipRecover but flag was false");
                
                // Make sure movement speed is zero
                if (playerController != null)
                {
                    // Store original move speed for later restoration
                    if (originalMoveSpeed <= 0)
                    {
                        originalMoveSpeed = playerController.moveSpeed;
                    }
                    
                    playerController.moveSpeed = 0f;
                }
            }
            else if (!currentlyInSlipRecover && isInSlipRecover && currentState.normalizedTime >= 1.0f)
            {
                // Animation is no longer in SlipRecover (and fully completed) but our flag says we still are
                isInSlipRecover = false;
                Debug.LogWarning("Fixed isInSlipRecover flag - animation is not in SlipRecover but flag was true");
                
                // Restore movement speed
                if (playerController != null && originalMoveSpeed > 0)
                {
                    playerController.moveSpeed = originalMoveSpeed;
                }
            }
            
            // Force movement speed to zero and animation parameters while in SlipRecover
            if (isInSlipRecover && playerController != null)
            {
                // Block all movement by setting speed to zero
                playerController.moveSpeed = 0f;
                
                // Force animation parameters to be correct
                anim.SetBool("Grounded", true);
                anim.SetBool("IsFalling", false);
            }
        }
        
        return isInSlipRecover;
    }

    public void ForceResetSpeed()
    {
        if (playerController != null && originalMoveSpeed > 0)
        {
            playerController.moveSpeed = originalMoveSpeed;
            Debug.Log("Force reset speed to original");
        }
        
        isSlipping = false;
        isInSlipRecover = false;
    }

    private void CheckForPunchForceInterference()
    {
        if (!isSlipping) return;
        
        if (attackController == null) return;
        
        if (attackController.IsPunchForceActive())
        {
            Debug.Log("Detected active punch force during slip - stopping");
            attackController.StopPunchForce();
        }
        
        if (charController.velocity.magnitude > slipMovementSpeed * 1.5f)
        {
            Debug.LogWarning("Detected unusually fast movement during slip");
            attackController.StopPunchForce();
            playerController.moveSpeed = slipMovementSpeed;
        }
    }

    private void CheckIdleToRunStuck()
    {
        if (anim == null || isSlipping || slipCooldownTimer > 0) return;
        
        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
        bool currentlyInIdleToRun = currentState.IsName("IdleToRun");
        
        if (currentlyInIdleToRun)
        {
            // If we just entered IdleToRun, start the timer
            if (!wasInIdleToRun)
            {
                idleToRunTimer = 0f;
                Debug.Log("Entered IdleToRun state - starting stuck detection timer");
            }
            
            // Increment the timer
            idleToRunTimer += Time.deltaTime;
            
            // Check if we've been stuck too long
            if (idleToRunTimer >= idleToRunThreshold)
            {
                Debug.Log($"STUCK IN IDLETORUN for {idleToRunTimer:F2}s - triggering slip!");
                TriggerSlip();
                idleToRunTimer = 0f; // Reset timer after triggering
            }
        }
        else
        {
            // Not in IdleToRun anymore, reset timer
            if (wasInIdleToRun)
            {
                Debug.Log($"Exited IdleToRun after {idleToRunTimer:F2}s - normal transition");
                idleToRunTimer = 0f;
            }
        }
        
        // Update previous state
        wasInIdleToRun = currentlyInIdleToRun;
    }

    // Helper method to check if player is in ground pound OR spinkick state
    private bool IsInGroundPoundOrSpinKickFallState()
    {
        if (anim == null) return false;
        
        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
        return currentState.IsName("GPFall") || 
            currentState.IsName("GPRecover") ||
            currentState.IsName("SKFall");
    }

    // Helper method to check if player is in uppercut state
    private bool IsInUppercutState()
    {
        if (anim == null) return false;
        
        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
        return currentState.IsName("Uppercut");
    }

    // Add this public method for external testing/debugging
    public void DebugLogTrackerStates()
    {
        Debug.Log($"Tracker States - Prone: {proneTracker.pressCount}/{proneThreshold}, " +
                 $"Medium: {mediumAttackTracker.pressCount}/{mediumAttackThreshold}, " +
                 $"Light: {lightAttackTracker.pressCount}/{lightAttackThreshold}, " +
                 $"Heavy: {heavyAttackTracker.pressCount}/{heavyAttackThreshold}, " +
                 $"Jump: {jumpTracker.pressCount}/{jumpThreshold}");
    }
}