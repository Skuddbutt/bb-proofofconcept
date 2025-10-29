using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This class represents a single attack move that can be equipped/slotted
[System.Serializable]
public class AttackMove
{
    public string moveName;
    public AttackType attackType;
    public AnimationClip attackAnimation;
    
    // Attack properties
    public float launchForce = 0f;       // Upward force (for uppercut-like moves)
    public float downwardForce = 0f;     // Downward force (for ground pound-like moves)
    public float horizontalForce = 0f;   // Horizontal force (for dash-like moves)
    public float damageAmount = 10f;
    
    // Attack timing properties
    public float attackDuration = 0.5f;      // How long the attack animation plays
    public float launchForceDelay = 0.2f;    // When to apply launch force during animation
    
    // Pre-requisites for this attack
    public bool requiresGrounded = false;
    public bool requiresAirborne = false;
    public bool requiresProne = false;
    
    // Follow-up settings
    public bool canAirAttackAfter = false;   // Can this move be followed by an air attack?
    public List<string> allowedFollowupAttacks = new List<string>();
    public bool hasSpecialFall = false;      // Does this move have a special falling animation?
    public bool hasSpecialLanding = false;   // Does this move have a special landing animation?
    
    // Animation names
    public string specialFallAnimName = "";
    public string specialLandAnimName = "";
    public string specialRecoveryAnimName = "";
    
    // Special timing properties
    public float specialFallDuration = 0.5f;     // Duration for special fall
    public float specialRecoveryDuration = 0.5f; // Duration for special recovery

    [Header("Recovery Settings")]
    [Tooltip("Movement speed multiplier during recovery (1.0 = normal speed, 0 = no movement)")]
    [Range(0f, 1f)]
    public float recoveryMovementMultiplier = 0.5f;

    [Tooltip("Movement speed multiplier during attack (1.0 = normal speed, 0 = no movement)")]
    [Range(0f, 1f)]
    public float attackMovementMultiplier = 1.0f;
}

// Enum for attack types
public enum AttackType
{
    Light,
    Medium,
    Heavy
}

public class AttackController : MonoBehaviour
{
    private bool unpauseImmunity = false;
    // Reference to player controller
    private PlayerController playerController;
    
    // Regular attack move slots (one for each attack type)
    [Header("Regular Attack Move Slots")]
    public AttackMove lightAttackSlot;
    public AttackMove mediumAttackSlot;
    public AttackMove heavyAttackSlot;
    
    // Ground attack move slots (when crouching/prone)
    [Header("Prone Attack Move Slots")]
    public AttackMove lightProneAttackSlot;
    public AttackMove mediumProneAttackSlot;
    public AttackMove heavyProneAttackSlot;
    
    // Air attack move slots
    [Header("Air Attack Move Slots")]
    public AttackMove lightAirAttackSlot;
    public AttackMove mediumAirAttackSlot;
    public AttackMove heavyAirAttackSlot;
    
    // Current active attack
    private AttackMove currentAttack;
    private bool isAttacking = false;
    private bool isInSpecialFall = false;
    private bool isInSpecialRecovery = false;
    private bool hasUsedGroundPoundThisJump = false;
    
    // Timers for animation state monitoring (since we can't use animation events)
    private float attackAnimationTimer = 0f;
    private float specialFallTimer = 0f;
    private float specialRecoveryTimer = 0f;
    
    // Attack cooldown
    private float attackCooldownTimer = 0f;
    public float attackCooldown = 0.5f;
    private bool isPunchingOffLedge = false;
    private bool hasPunchedOffLedge = false;
    private bool punchedOffLedgeRecently = false;
    private float punchOffLedgeTimer = 0f;
    
    // Previous attack input states
    private bool lightAttackPrevious = false;
    private bool mediumAttackPrevious = false;
    private bool heavyAttackPrevious = false;
    
    void Start()
    {
        playerController = GetComponent<PlayerController>();
        
        // Set up default attack moves if not assigned
        SetupDefaultAttackMoves();
    }
    
    void Update()
    {
        if (IsPaused())
        {
            return;
        }
        
        // Get animator reference once at the beginning
        Animator anim = null;
        if (playerController != null) 
        {
            anim = playerController.GetAnimator();
        }
        
        // SAFETY CHECK FOR STUCK SPINKICK ANIMATION:
        if (anim != null)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            
            // If SpinKick bool is true but we're not in SpinKick animation state anymore
            if (anim.GetBool("SpinKick") && !stateInfo.IsName("SpinKick"))
            {
                anim.SetBool("SpinKick", false);
            }
        }
        
        // Update cooldown timer
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }
        
        // CRITICAL FIX: Use isAttacking flag to block ALL attacks during ANY attack
        // SpinKick specifically sets isAttacking to true and keeps it true through the entire
        // attack sequence (including SKFall) until we reach SKRecover
        
        if (isAttacking)
        {
            // SPECIAL CASE: Check for valid follow-up attacks before blocking
            bool lightAttackJustPressed = GameInputManager.Instance != null ? 
                (GameInputManager.Instance.GetLightAttackInput() && !lightAttackPrevious) : false;
            
            // Allow SpinKick follow-up during Punch
            if (lightAttackJustPressed && currentAttack != null && currentAttack.moveName == "Punch" && 
                lightAttackSlot != null && lightAttackSlot.moveName == "SpinKick")
            {
                if (CanExecuteFollowupAttack(lightAttackSlot))
                {
                    // Execute the follow-up SpinKick
                    ExecuteFollowupAttack(lightAttackSlot);
                    UpdatePreviousInputStates(); // Update input states before returning
                    return;
                }
                else
                {
                    // Follow-up not ready yet, but don't spam log
                    UpdatePreviousInputStates();
                    MonitorAttackState();
                    return;
                }
            }
            
            // We're in an active attack - check for and log any OTHER attack attempts
            if (GameInputManager.Instance != null)
            {
                // Check if any attack button was just pressed this frame
                bool anyOtherAttackJustPressed = 
                    (GameInputManager.Instance.GetMediumAttackInput() && !mediumAttackPrevious) ||
                    (GameInputManager.Instance.GetHeavyAttackInput() && !heavyAttackPrevious) ||
                    // Only block light attack if it's not a valid follow-up
                    (lightAttackJustPressed && !(currentAttack != null && currentAttack.moveName == "Punch" && 
                    lightAttackSlot != null && lightAttackSlot.moveName == "SpinKick"));
                
                if (anyOtherAttackJustPressed)
                {
                    Debug.Log("Attack input BLOCKED during active attack: " + 
                            (currentAttack != null ? currentAttack.moveName : "unknown"));
                }
            }
            
            // Continue to monitor the current attack state
            MonitorAttackState();
        }
        else if (isInSpecialFall)
        {
            // We're in a special fall state - also block all attacks
            if (GameInputManager.Instance != null)
            {
                // Check if any attack button was just pressed this frame
                bool anyAttackJustPressed = 
                    (GameInputManager.Instance.GetLightAttackInput() && !lightAttackPrevious) ||
                    (GameInputManager.Instance.GetMediumAttackInput() && !mediumAttackPrevious) ||
                    (GameInputManager.Instance.GetHeavyAttackInput() && !heavyAttackPrevious);
                
                if (anyAttackJustPressed)
                {
                    Debug.Log("Attack input BLOCKED during special fall: " + 
                            (currentAttack != null ? currentAttack.specialFallAnimName : "unknown"));
                }
            }
            
            // Continue to monitor the special fall state
            MonitorSpecialFallState();
        }
        else if (isInSpecialRecovery)
        {
            // We're in a special recovery state - also block all attacks
            if (GameInputManager.Instance != null)
            {
                // Check if any attack button was just pressed this frame
                bool anyAttackJustPressed = 
                    (GameInputManager.Instance.GetLightAttackInput() && !lightAttackPrevious) ||
                    (GameInputManager.Instance.GetMediumAttackInput() && !mediumAttackPrevious) ||
                    (GameInputManager.Instance.GetHeavyAttackInput() && !heavyAttackPrevious);
                
                if (anyAttackJustPressed)
                {
                    Debug.Log("Attack input BLOCKED during special recovery: " + 
                            (currentAttack != null ? currentAttack.specialRecoveryAnimName : "unknown"));
                }
            }
            
            // Continue to monitor the special recovery state
            MonitorSpecialRecoveryState();
        }
        else
        {
            // We're not in any attack state, so normal input processing can happen
            
            // SPECIAL CHECK FOR SPINKICK ANIMATION:
            // Even if isAttacking is false, we need to check if the SpinKick animation or bool is active
            // This catches cases where the flag might have gotten out of sync with the animation
            bool spinKickActive = false;
            if (anim != null)
            {
                spinKickActive = anim.GetBool("SpinKick") || anim.GetCurrentAnimatorStateInfo(0).IsName("SpinKick");
                
                // Also check for SKFall state as a backup
                bool skFallActive = anim.GetBool("SKFall") || anim.GetCurrentAnimatorStateInfo(0).IsName("SKFall");
                
                // If either is active but we're not in isAttacking, something is wrong - FORCE RESET
                if ((spinKickActive || skFallActive) && !isAttacking)
                {
                    Debug.LogWarning("Animation state (SpinKick/SKFall) active but isAttacking is false - FORCE RESETTING parameters");
                    
                    // FORCE RESET the stuck parameters
                    if (anim.GetBool("SpinKick"))
                    {
                        anim.SetBool("SpinKick", false);
                        Debug.Log("FORCE RESET: SpinKick parameter set to false");
                    }
                    
                    if (anim.GetBool("SKFall"))
                    {
                        anim.SetBool("SKFall", false);
                        Debug.Log("FORCE RESET: SKFall parameter set to false");
                    }
                    
                    if (anim.GetBool("SKRecover"))
                    {
                        anim.SetBool("SKRecover", false);
                        Debug.Log("FORCE RESET: SKRecover parameter set to false");
                    }
                    
                    // Also reset any lingering attack state
                    isInSpecialFall = false;
                    isInSpecialRecovery = false;
                    currentAttack = null;
                    
                    Debug.Log("FORCE RESET: Cleared all attack states - attacks should now be available");
                    
                    // Check for attack inputs but block them THIS FRAME (since we just reset)
                    if (GameInputManager.Instance != null)
                    {
                        bool anyAttackJustPressed = 
                            (GameInputManager.Instance.GetLightAttackInput() && !lightAttackPrevious) ||
                            (GameInputManager.Instance.GetMediumAttackInput() && !mediumAttackPrevious) ||
                            (GameInputManager.Instance.GetHeavyAttackInput() && !heavyAttackPrevious);
                        
                        if (anyAttackJustPressed)
                        {
                            Debug.Log("Attack input detected during parameter reset - will be available next frame");
                        }
                    }
                    
                    // Update previous input states and exit early THIS FRAME
                    UpdatePreviousInputStates();
                    return;
                }
            }
            
            // Only if we're really sure we're not in any attack state, process attack inputs
            
            // Always check for GroundPound when in the air, regardless of attack state
            bool mediumAttackJustPressed = GameInputManager.Instance != null ? 
                (GameInputManager.Instance.GetMediumAttackInput() && !mediumAttackPrevious) : false;

            // Only allow GroundPound if we're airborne AND haven't used it already this jump
            if (!playerController.IsGrounded() && mediumAttackJustPressed && 
                mediumAirAttackSlot != null && !hasUsedGroundPoundThisJump)
            {
                // Execute GroundPound immediately, overriding any cooldown
                attackCooldownTimer = 0f;
                ExecuteAttack(mediumAirAttackSlot);
                
                // Mark that we've used GroundPound for this jump
                hasUsedGroundPoundThisJump = true;
            }
            // Check for attack inputs - with special handling for air attacks after Uppercut
            else if ((!isAttacking && !isInSpecialFall && !isInSpecialRecovery && attackCooldownTimer <= 0) ||
                    (isAttacking && currentAttack != null && currentAttack.moveName == "Uppercut" && !playerController.IsGrounded()))
            {
                // Allow attack inputs during Uppercut if we're airborne
                if (isAttacking && currentAttack != null && currentAttack.moveName == "Uppercut" && !playerController.IsGrounded())
                {
                    // Only check for air attacks when in this state
                    CheckAirAttackInputs();
                }
                else
                {
                    // Normal attack input checking
                    CheckAttackInputs();
                }
            }
        }
        
        // Update previous input states
        UpdatePreviousInputStates();
    }

    // New method just for air attacks
    private void CheckAirAttackInputs()
    {
        // Don't allow attacks during slip recover
        if (playerController.IsInSlipRecover())
        {
            //Debug.Log("Air attack blocked during SlipRecover animation");
            return;
        }

        // CRITICAL FIX: Don't allow attacks during SpinKick or SKFall
        if (IsInSpinKickOrFall())
        {
            Debug.Log("Air attack blocked during SpinKick/SKFall");
            return;
        }

        // Get animator state info
        Animator anim = playerController.GetAnimator();
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool inUppercutState = stateInfo.IsName("Uppercut");
        
        // Also check if we're in a transition FROM Uppercut
        AnimatorTransitionInfo transInfo = anim.GetAnimatorTransitionInfo(0);
        bool isTransitioningFromUppercut = transInfo.userNameHash != 0 && 
                                        anim.GetCurrentAnimatorStateInfo(0).IsName("Uppercut");
        
        // Only allow medium air attacks (GroundPound) as a follow-up to Uppercut
        bool mediumAttackPressed = GameInputManager.Instance != null ? GameInputManager.Instance.GetMediumAttackInput() : false;
        bool mediumAttackJustPressed = mediumAttackPressed && !mediumAttackPrevious;
        
        // Allow GroundPound if we're in Uppercut state OR transitioning from it
        if (mediumAttackJustPressed && !playerController.IsGrounded() && 
            mediumAirAttackSlot != null && 
            (inUppercutState || isTransitioningFromUppercut))
        {
            // End the current Uppercut attack
            isAttacking = false;
            playerController.EndAttackAnimation(currentAttack);
            
            // Force-cancel any ongoing transitions
            if (isTransitioningFromUppercut)
            {
                // Jump directly to GroundPound
                anim.Play("GPFall", 0, 0f);
            }
            
            // Start the GroundPound attack
            Debug.Log("Quick transition from Uppercut to GroundPound");
            ExecuteAttack(mediumAirAttackSlot);
        }
    }
    
    private void SetupDefaultAttackMoves()
    {
        // If any attack move slots are empty, create default ones
        // Example: Default Light Regular Attack (Spinkick)
        if (lightAttackSlot == null || string.IsNullOrEmpty(lightAttackSlot.moveName))
        {
            lightAttackSlot = new AttackMove
            {
                moveName = "SpinKick",
                attackType = AttackType.Light,
                requiresGrounded = false,        
                requiresAirborne = false,        
                requiresProne = false,           
                launchForce = 0f,                
                downwardForce = 0f,              
                horizontalForce = 300f,          
                damageAmount = 8f,               
                attackDuration = 0.6f,          
                launchForceDelay = 0.0f,         
                canAirAttackAfter = false,       
                hasSpecialFall = true,           // Set to true to enable special fall
                hasSpecialLanding = true,        // Set to true to enable special recovery
                specialFallAnimName = "SKFall",  // Name of the fall animation parameter
                specialRecoveryAnimName = "SKRecover", // Name of the recovery animation parameter
                specialFallDuration = 1.0f,      // Duration of fall state (if not interrupted)
                specialRecoveryDuration = 0f,  // Duration of recovery animation
                attackMovementMultiplier = 1f, 
                recoveryMovementMultiplier = 1f,
                allowedFollowupAttacks = new List<string>()
            };
        }

        // Example: Default Medium Regular Attack (Punch)
        if (mediumAttackSlot == null || string.IsNullOrEmpty(mediumAttackSlot.moveName))
        {
            mediumAttackSlot = new AttackMove
            {
                moveName = "Punch",
                attackType = AttackType.Medium,     // Medium attack type (Square/Left click)
                requiresGrounded = true,            // Only usable when grounded
                requiresAirborne = false,           // Not usable in air
                requiresProne = false,              // Not usable in prone
                launchForce = 0f,                   // No upward force
                downwardForce = 0f,                 // No downward force
                horizontalForce = 500f,              // Horizontal force of 10
                damageAmount = 10f,                 // Base damage amount
                attackDuration = 1.46f,              // Duration of punch animation (adjust as needed)
                launchForceDelay = 0.0f,            // No delay for force application
                canAirAttackAfter = false,          // Cannot air attack after
                hasSpecialFall = false,             // No special fall
                hasSpecialLanding = false,          // No special landing
                attackMovementMultiplier = 0.0f,    // No movement during punch (locked controls)
                recoveryMovementMultiplier = 0.3f,  // Reduced movement during recovery phase
                allowedFollowupAttacks = new List<string> {"SpinKick"}
            };
        }

        // Example: Default Medium Prone Attack (Uppercut)
        if (mediumProneAttackSlot == null || string.IsNullOrEmpty(mediumProneAttackSlot.moveName))
        {
            mediumProneAttackSlot = new AttackMove
            {
                moveName = "Uppercut",
                attackType = AttackType.Medium,
                requiresGrounded = true,
                requiresProne = true,
                launchForce = 20f,
                attackDuration = 0.5f,
                launchForceDelay = 0.05f,
                canAirAttackAfter = true,
                allowedFollowupAttacks = new List<string> { "GroundPound", "SpinKick" },
                attackMovementMultiplier = 0.5f // Add this line - 50% movement
            };
        }
        
        // Example: Default Medium Air Attack (Ground Pound)
        if (mediumAirAttackSlot == null || string.IsNullOrEmpty(mediumAirAttackSlot.moveName))
        {
            mediumAirAttackSlot = new AttackMove
            {
                moveName = "GroundPound",
                attackType = AttackType.Medium,
                requiresAirborne = true,
                downwardForce = 40f,
                attackDuration = 0.2f,
                hasSpecialFall = true,
                hasSpecialLanding = true,
                specialFallAnimName = "GPFall",
                specialFallDuration = 2.0f, // This can be long since it's interrupted by landing
                specialLandAnimName = "",
                specialRecoveryAnimName = "GPRecover",
                specialRecoveryDuration = 0.6f,
                recoveryMovementMultiplier = 0.3f // 30% movement speed during recovery
            };
        }
    }

    private void CheckAttackInputs()
    {
        // FIRST CHECK: Don't allow attacks during control locks, slip recover, OR slipping state
        SlipNFall slipController = GetComponent<SlipNFall>();
        bool isSlipping = slipController != null && slipController.IsSlipping();
        
        if (playerController.IsInControlLock() || playerController.IsInSlipRecover() || isSlipping)
        {
            // Log when an attack is blocked specifically due to slip
            if (isSlipping)
            {
                //Debug.Log("Attack blocked during slip state");
            }
            return; // Exit immediately - no attacks allowed
        }

        // CRITICAL FIX: Check for SpinKick or SKFall states and block ALL attacks
        // This check runs before any other attack input processing
        if (IsInSpinKickOrFall())
        {
            // SIMPLIFIED: We don't need to check each button separately
            // Just log that attacks are blocked during SpinKick/SKFall
            Debug.Log("Attack input BLOCKED during SpinKick/SKFall state");
            return; // Exit immediately - no attacks allowed during SpinKick or SKFall
        }

        // SECOND CHECK: Don't allow any attacks if we've punched off a ledge
        // until we're grounded AND not in a landing state
        if (hasPunchedOffLedge)
        {
            // Only clear this flag when we're safely grounded and not landing
            if (playerController.IsGrounded() && !playerController.IsHighLanding() && !playerController.IsSplatting())
            {
                hasPunchedOffLedge = false;
                Debug.Log("Player has landed after punching off ledge - attacks now allowed");
            }
            else
            {
                // Still in air or landing after punching off ledge - block all attacks
                return;
            }
        }

        bool isGrounded = playerController.IsGrounded();
        bool isAirborne = !isGrounded;
        bool isProne = playerController.IsProning();
        
        // Get the animator reference before we need it for the condition checks
        Animator anim = playerController.GetAnimator();
        
        // Check for light attack input
        bool lightAttackPressed = GameInputManager.Instance != null ? GameInputManager.Instance.GetLightAttackInput() : false;
        bool lightAttackJustPressed = lightAttackPressed && !lightAttackPrevious;
        
        // Check for medium attack input
        bool mediumAttackPressed = GameInputManager.Instance != null ? GameInputManager.Instance.GetMediumAttackInput() : false;
        bool mediumAttackJustPressed = mediumAttackPressed && !mediumAttackPrevious;
        
        // Check for heavy attack input
        bool heavyAttackPressed = GameInputManager.Instance != null ? GameInputManager.Instance.GetHeavyAttackInput() : false;
        bool heavyAttackJustPressed = heavyAttackPressed && !heavyAttackPrevious;
        
        // Handle light attack
        if (lightAttackJustPressed)
        {
            // NEW: Check if this is a valid follow-up attack
            if (isAttacking && lightAttackSlot != null && lightAttackSlot.moveName == "SpinKick")
            {
                if (CanExecuteFollowupAttack(lightAttackSlot))
                {
                    // Execute the follow-up SpinKick
                    ExecuteFollowupAttack(lightAttackSlot);
                    return; // Exit early since we handled the follow-up
                }
                else
                {
                    // Follow-up not allowed yet, but don't log spam - just return
                    return;
                }
            }
            
            // EXISTING CODE: Regular attack checks when not in attack state
            // First check: Don't allow spinkick in invalid states
            bool isInInvalidState = playerController.IsProning() || 
                                    playerController.IsHighLanding() || 
                                    playerController.IsSplatting() || 
                                    anim.GetBool("IsHighJumping") ||
                                    anim.GetBool("IsHighFalling") ||
                                    anim.GetBool("ShouldProne");  // Add ShouldProne check here
            
            // Also check if we're in any prone-related animation
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            bool inProneRelatedAnim = stateInfo.IsName("ProneDown") || 
                                    stateInfo.IsName("ProneIdle") || 
                                    stateInfo.IsName("ProneTo") || 
                                    stateInfo.IsName("Crawl") || 
                                    stateInfo.IsName("ProneUp");
            
            // Add prone animations to invalid states
            isInInvalidState = isInInvalidState || inProneRelatedAnim;

            // If in an invalid state, don't execute attack
            if (isInInvalidState)
            {
                Debug.Log("SpinKick blocked - player is in an invalid state (prone or prone-related animation)");
                return;
            }
            
            // Now check for valid states and execute the attack if appropriate
            if (isGrounded && isProne && lightProneAttackSlot != null && !string.IsNullOrEmpty(lightProneAttackSlot.moveName))
            {
                // Check if we can execute from current animation state
                if (CanExecuteAttackFromCurrentState(lightProneAttackSlot))
                {
                    ExecuteAttack(lightProneAttackSlot);
                }
                else
                {
                    Debug.Log("Cannot execute " + lightProneAttackSlot.moveName + " from current animation state");
                }
            }
            else if ((isGrounded || isAirborne) && !isProne && lightAttackSlot != null && !string.IsNullOrEmpty(lightAttackSlot.moveName))
            {
                // Allow SpinKick in both grounded and airborne states as long as not in prone
                ExecuteAttack(lightAttackSlot);
                Debug.Log("Executing SpinKick attack - grounded: " + isGrounded + ", airborne: " + isAirborne);
            }
            else
            {
                // No valid attack for current state
                Debug.Log("Light attack attempted but no valid attack found for current state. Grounded: " + 
                        isGrounded + ", Prone: " + isProne + ", Airborne: " + isAirborne);
            }
        }
        
        // Handle medium attack
        if (mediumAttackJustPressed)
        {
            // Use the already declared anim variable - no need to get it again
            bool shouldProne = anim.GetBool("ShouldProne");
            
            if (isGrounded && shouldProne && mediumProneAttackSlot != null)
            {
                // Special handling for Uppercut
                if (mediumProneAttackSlot.moveName == "Uppercut")
                {
                    if (CanExecuteAttackFromCurrentState(mediumProneAttackSlot))
                    {
                        Debug.Log("Setting Uppercut parameter to TRUE");
                        
                        // Set the parameter but don't directly play the animation
                        anim.SetBool("Uppercut", true);
                        
                        // Add a small trigger delay to ensure state machine processes it
                        StartCoroutine(DelayedUppercutTrigger(anim));
                        
                        // Start monitoring the Uppercut animation
                        StartCoroutine(MonitorUppercutAnimation());
                        
                        // Execute the attack
                        ExecuteAttack(mediumProneAttackSlot);
                    }
                    else
                    {
                        Debug.Log("Cannot execute Uppercut from current animation state");
                    }
                }
                else if (CanExecuteAttackFromCurrentState(mediumProneAttackSlot))
                {
                    ExecuteAttack(mediumProneAttackSlot);
                }
            }
            else if (isGrounded && !shouldProne && mediumAttackSlot != null)
            {
                // Special handling for Punch attack
                if (mediumAttackSlot.moveName == "Punch")
                {
                    // Execute Punch attack when grounded and not prone
                    Debug.Log("Executing Punch attack");
                    ExecuteAttack(mediumAttackSlot);
                }
                // Check if any other attack is implemented for this slot
                else if (!string.IsNullOrEmpty(mediumAttackSlot.moveName))
                {
                    ExecuteAttack(mediumAttackSlot);
                }
                else
                {
                    Debug.Log("Medium attack not implemented yet");
                }
            }
            else if (isAirborne && mediumAirAttackSlot != null)
            {
                // ADDITIONAL CHECK: Block GroundPound after punching off ledge
                if (punchedOffLedgeRecently && mediumAirAttackSlot.moveName == "GroundPound")
                {
                    Debug.Log("GroundPound BLOCKED in CheckAttackInputs after punching off ledge");
                    return;
                }
                
                // Check if this attack is implemented before executing
                if (!string.IsNullOrEmpty(mediumAirAttackSlot.moveName))
                {
                    ExecuteAttack(mediumAirAttackSlot);
                }
                else
                {
                    Debug.Log("Medium air attack not implemented yet");
                }
            }
            else
            {
                // No valid attack for current state
                Debug.Log("Medium attack attempted but no valid attack found for current state. Grounded: " + 
                        isGrounded + ", Prone: " + isProne + ", Airborne: " + isAirborne);
            }
        }
        
        // Handle heavy attack
        if (heavyAttackJustPressed)
        {
            if (isGrounded && isProne && heavyProneAttackSlot != null)
            {
                // Check if we can execute from current animation state
                if (CanExecuteAttackFromCurrentState(heavyProneAttackSlot))
                {
                    ExecuteAttack(heavyProneAttackSlot);
                }
                else
                {
                    Debug.Log("Cannot execute " + heavyProneAttackSlot.moveName + " from current animation state");
                }
            }
            else if (isGrounded && !isProne && heavyAttackSlot != null)
            {
                ExecuteAttack(heavyAttackSlot);
            }
            else if (isAirborne && heavyAirAttackSlot != null)
            {
                ExecuteAttack(heavyAirAttackSlot);
            }
        }
    }

    private IEnumerator DelayedUppercutTrigger(Animator anim)
    {
        // Wait for a tiny fraction of a second
        yield return new WaitForSeconds(0.02f);
        
        // Check if animation hasn't started yet
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("Uppercut"))
        {
            // Force it to start if needed
            anim.Play("Uppercut", 0, 0f);
            Debug.Log("Forced Uppercut animation to play after delay");
        }
    }

    private IEnumerator MonitorUppercutAnimation()
    {
        Animator anim = playerController.GetAnimator();
        bool inUppercutState = false;
        bool uppercutStarted = false;
        
        // Wait until we detect we're in the Uppercut state
        float maxWaitTime = 0.5f; // Safety timeout
        float waitTimer = 0f;
        
        while (!uppercutStarted && waitTimer < maxWaitTime)
        {
            waitTimer += Time.deltaTime;
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            
            if (stateInfo.IsName("Uppercut"))
            {
                uppercutStarted = true;
                inUppercutState = true;
                Debug.Log("Uppercut animation started");
            }
            
            yield return null;
        }
        
        // If we never entered the Uppercut state, reset the parameter and exit
        if (!uppercutStarted)
        {
            Debug.Log("Uppercut animation never started - resetting parameter");
            anim.SetBool("Uppercut", false);
            yield break;
        }
        
        // Now monitor until the Uppercut animation completes
        // Use the attack duration as a reference for when to consider it "complete"
        float animationDuration = 0f;
        if (currentAttack != null && currentAttack.moveName == "Uppercut")
        {
            animationDuration = currentAttack.attackDuration;
        }
        else
        {
            animationDuration = 0.5f; // Default duration if not available
        }
        
        float animationTimer = 0f;
        
        while (inUppercutState)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            animationTimer += Time.deltaTime;
            
            // If we're still in Uppercut state
            if (stateInfo.IsName("Uppercut"))
            {
                // Check if the animation is nearly complete by time or normalizedTime
                if (stateInfo.normalizedTime >= 0.95f || animationTimer >= animationDuration * 0.95f)
                {
                    Debug.Log("Uppercut animation nearly complete - preparing to reset parameter");
                    inUppercutState = false;
                }
            }
            else
            {
                // We've transitioned to another state
                Debug.Log("Transitioned out of Uppercut to state: " + stateInfo.shortNameHash);
                inUppercutState = false;
            }
            
            yield return null;
        }
        
        // Animation complete or we've transitioned out, wait a small buffer time to ensure smooth transition
        yield return new WaitForSeconds(0.1f);
        
        // Now it's safe to reset the parameter
        Debug.Log("Resetting Uppercut parameter to FALSE");
        anim.SetBool("Uppercut", false);
    }

    private IEnumerator ResetAnimParameter(string paramName, float delay)
    {
        yield return new WaitForSeconds(delay);
        playerController.GetAnimator().SetBool(paramName, false);
    }
    
    private void UpdatePreviousInputStates()
    {
        // Update previous attack button states for edge detection
        lightAttackPrevious = GameInputManager.Instance != null ? GameInputManager.Instance.GetLightAttackInput() : false;
        mediumAttackPrevious = GameInputManager.Instance != null ? GameInputManager.Instance.GetMediumAttackInput() : false;
        heavyAttackPrevious = GameInputManager.Instance != null ? GameInputManager.Instance.GetHeavyAttackInput() : false;
    }
    
    public void ExecuteAttack(AttackMove attackMove)
    {
        // Store the current attack
        currentAttack = attackMove;
        isAttacking = true;  // IMPORTANT: This must remain true throughout the entire attack sequence
        
        // Reset the attack animation timer
        attackAnimationTimer = 0f;
        
        // Set parameters for SpinKick
        if (attackMove.moveName == "SpinKick" && playerController != null && playerController.GetAnimator() != null)
        {
            Animator anim = playerController.GetAnimator();
            // Make sure SKFall and SKRecover are OFF before starting SpinKick
            anim.SetBool("SKFall", false);
            anim.SetBool("SKRecover", false);
            // EXPLICITLY set SpinKick to true
            anim.SetBool("SpinKick", true);
            
            Debug.Log("ExecuteAttack: Setting SpinKick bool to TRUE, ensuring SKFall/SKRecover are FALSE");
        }
        
        // Tell player controller to execute the attack
        playerController.ExecuteAttackMove(attackMove);
        
        // Start the appropriate movement method based on attack type
        if (attackMove.moveName == "Punch")
        {
            activePunchForceCoroutine = StartCoroutine(ApplyPunchForceCoroutine(attackMove));
            Debug.Log("Starting punch force coroutine");
        }
        else if (attackMove.launchForce > 0)
        {
            StartCoroutine(ApplyAttackLaunchForce(attackMove));
        }
        else if (attackMove.moveName == "SpinKick")
        {
            Debug.Log("Starting SpinKick force coroutine");
        }
    }

    private IEnumerator ResetAttackParameter(string paramName, float delay)
    {
        yield return new WaitForSeconds(delay);
        playerController.GetAnimator().SetBool(paramName, false);
    }

    public void ResetAttackCooldown()
    {
        attackCooldownTimer = 0f;
    }

    private bool CanExecuteSpinKick()
    {
        // Check for invalid states
        bool isInSlipState = GetComponent<SlipNFall>()?.IsSlipping() ?? false;
        bool isInSlipRecover = GetComponent<SlipNFall>()?.IsInSlipRecover() ?? false;
        
        // Get animator for checking high falling/jumping states and prone states
        Animator anim = playerController.GetAnimator();
        if (anim == null) return false;
        
        bool isHighJumping = anim.GetBool("IsHighJumping");
        bool isHighFalling = anim.GetBool("IsHighFalling");
        bool isProning = playerController.IsProning();
        bool shouldProne = anim.GetBool("ShouldProne");
        
        // Check for prone-related animations
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool inProneAnimation = stateInfo.IsName("ProneDown") || 
                            stateInfo.IsName("ProneIdle") || 
                            stateInfo.IsName("ProneTo") || 
                            stateInfo.IsName("Crawl") || 
                            stateInfo.IsName("ProneUp");
        
        bool isInControlLock = playerController.IsInControlLock();
        
        // Check if we're already in an attack state using the public method
        bool inAttackState = IsInAttackState();
        
        // Also check if we're in SpinKick or SKFall states
        bool inSpinKickState = stateInfo.IsName("SpinKick") || anim.GetBool("SpinKick");
        bool inSKFallState = stateInfo.IsName("SKFall") || anim.GetBool("SKFall");
        bool inSpinKickOrFall = inSpinKickState || inSKFallState;
        
        // Block spin kick in these states - added shouldProne and inProneAnimation checks
        return !isInSlipState && 
            !isInSlipRecover && 
            !isHighJumping && 
            !isHighFalling && 
            !isProning && 
            !shouldProne &&      // Block when prone button is held
            !inProneAnimation && // Block in any prone animation
            !isInControlLock && 
            !inAttackState &&
            !inSpinKickOrFall;   // Block when already in SpinKick or SKFall
    }

    // Add a new public method to handle SpinKick animation completion
    public void HandleSpinKickAnimationComplete()
    {
        // Check if we're in attack state and the current attack is SpinKick
        if (isAttacking && currentAttack != null && currentAttack.moveName == "SpinKick")
        {
            // Transition to SKFall if airborne, otherwise end the attack
            if (!playerController.IsGrounded())
            {
                // Start special fall
                isInSpecialFall = true;
                specialFallTimer = 0f;
                playerController.StartSpecialFall(currentAttack);
                
                // Now we're in SKFall instead of SpinKick, but still in attack state
                isAttacking = false;
                
                Debug.Log("SpinKick animation complete - transitioning to SKFall");
            }
            else
            {
                // End the attack state entirely
                isAttacking = false;
                
                // Reset the animation parameter
                Animator anim = playerController.GetAnimator();
                if (anim != null)
                {
                    anim.SetBool("SpinKick", false);
                }
                
                // Set cooldown
                attackCooldownTimer = attackCooldown;
                
                Debug.Log("SpinKick animation complete - attack ended");
            }
        }
    }

    private bool CanExecuteAttackFromCurrentState(AttackMove attackMove)
    {
        // For SpinKick, use our custom logic
        if (attackMove.moveName == "SpinKick")
        {
            return CanExecuteSpinKick();
        }
        
        // For Uppercut, only check if ShouldProne is true
        if (attackMove.moveName == "Uppercut")
        {
            Animator anim = playerController.GetAnimator();
            bool shouldProne = anim.GetBool("ShouldProne");
            
            // Allow Uppercut whenever ShouldProne is true
            return shouldProne && playerController.IsGrounded();
        }
        
        // For GroundPound, always allow in air
        if (attackMove.moveName == "GroundPound")
        {
            return !playerController.IsGrounded() && !hasUsedGroundPoundThisJump;
        }
        
        // For other attacks, always allow
        return true;
    }

    private Coroutine activePunchForceCoroutine = null;

    private IEnumerator ApplyPunchForceCoroutine(AttackMove attackMove)
    {
        Debug.Log("Starting punch movement with earlier major movement (frame 1936)");
        
        // Wait briefly for animation to start
        yield return new WaitForSeconds(0.01f);
        
        // Check if we're still in punch state
        if (isAttacking && currentAttack != null && currentAttack.moveName == "Punch")
        {
            // Get reference to CharacterController
            CharacterController controller = playerController.GetCharacterController();
            
            // Capture the original forward direction at punch start
            Vector3 punchDirection = playerController.GetPlayerModel().transform.forward;
            
            // Animation parameters
            float animationDuration = 1.458f; // 35 frames at 24fps
            float startTime = Time.time;
            float endTime = startTime + animationDuration;
            
            // Starting position - will track total movement
            Vector3 startPosition = controller.transform.position;
            
            // Constant downward force to apply during punch
            float punchDownwardForce = 2.0f; // Adjust this value as needed
            
            // Create a dictionary of target positions at exact frames based on Maya curve
            // APPLY 50% INCREASE TO ALL POSITION VALUES
            Dictionary<int, float> framePositions = new Dictionary<int, float>
            {
                { 1934, 0f },    // Starting position
                { 1935, 0f },    // Still in anticipation
                { 1936, 15f },   // First major movement (increased from 10f)
                { 1937, 30f },   // Climbing quickly (increased from 20f)
                { 1938, 42f },   // Still climbing (increased from 28f)
                { 1939, 48f },   // Approaching peak (increased from 32f)
                { 1942, 57f },   // Near peak (increased from 38f)
                { 1944, 60.75f }, // First peak point (increased from 40.5f)
                { 1948, 64.5f }, // Second peak point (increased from 43f)
                { 1952, 66f },   // Third peak point - maximum distance (increased from 44f)
                { 1958, 66f },   // Maintain max distance (increased from 44f)
                { 1962, 66f },   // Maintain max distance (increased from 44f)
                { 1966, 66f },   // Maintain max distance (increased from 44f)
                { 1968, 66f }    // End position - stays at max distance (increased from 44f)
            };
            
            // Define a scale multiplier to convert Maya units to Unity world units
            float scaleMultiplier = 0.1f; // Adjust this to control the overall distance
            
            // Run at full frame rate for smooth movement
            while (Time.time < endTime && isAttacking && currentAttack != null && currentAttack.moveName == "Punch")
            {
                // CRITICAL ADDITION: Check if player is slipping
                SlipNFall slipController = playerController.GetComponent<SlipNFall>();
                if (slipController != null && slipController.IsSlipping())
                {
                    Debug.Log("Punch force canceled due to slip state");
                    break; // Exit the punch force coroutine immediately
                }
                
                // Calculate exact frame based on elapsed time
                float normalizedTime = (Time.time - startTime) / animationDuration;
                float currentFrame = 1934 + (normalizedTime * 34); // 34 frames total (1934-1968)
                
                // Find last and next defined frames
                int lastDefinedFrame = 1934;
                float lastDefinedPosition = 0f;
                int nextDefinedFrame = 1968;
                float nextDefinedPosition = 66f; // Updated end position (increased from 44f)
                
                // Find surrounding defined frames
                foreach (var framePair in framePositions)
                {
                    int frame = framePair.Key;
                    float position = framePair.Value;
                    
                    if (frame <= currentFrame && frame > lastDefinedFrame)
                    {
                        lastDefinedFrame = frame;
                        lastDefinedPosition = position;
                    }
                    
                    if (frame > currentFrame && frame < nextDefinedFrame)
                    {
                        nextDefinedFrame = frame;
                        nextDefinedPosition = position;
                    }
                }
                
                // Get exact target position through interpolation
                float frameFraction = (currentFrame - lastDefinedFrame) / (nextDefinedFrame - lastDefinedFrame);
                float targetPosition = lastDefinedPosition + (frameFraction * (nextDefinedPosition - lastDefinedPosition));
                
                // Calculate the target world position
                Vector3 targetWorldPos = startPosition + (punchDirection * targetPosition * scaleMultiplier);
                
                // Calculate movement needed to reach that position this frame
                Vector3 currentPosition = controller.transform.position;
                Vector3 movement = targetWorldPos - currentPosition;
                
                // CRITICAL: Add downward force to keep grounded
                movement.y -= punchDownwardForce * Time.deltaTime;
                
                // Apply the movement
                controller.Move(movement);
                
                // Wait for next frame
                yield return null;
            }
            
            Debug.Log("Punch movement complete or canceled");
        }
    }

    public bool IsPunchForceActive()
    {
        // Return true if there's an active punch force coroutine and we're in the Punch state
        return activePunchForceCoroutine != null && 
            isAttacking && 
            currentAttack != null && 
            currentAttack.moveName == "Punch";
    }

    public void StopPunchForce()
    {
        // Stop the punch force coroutine if it's active
        if (activePunchForceCoroutine != null)
        {
            StopCoroutine(activePunchForceCoroutine);
            activePunchForceCoroutine = null;
            Debug.Log("Punch force coroutine stopped externally");
        }
        
        // Also check if we're still in a punch state and exit it
        if (isAttacking && currentAttack != null && currentAttack.moveName == "Punch")
        {
            // End the attack animation but don't reset movement
            Animator anim = playerController.GetAnimator();
            anim.SetBool("Punch", false);
            
            // Update attack state
            isAttacking = false;
            Debug.Log("Punch state ended due to slip");
        }
    }

    private bool CanExecuteUppercut()
    {
        // Get references
        bool isGrounded = playerController.IsGrounded();
        bool isProne = playerController.IsProning();
        Animator anim = playerController.GetAnimator();
        
        // Must be grounded and prone
        if (!isGrounded || !isProne)
            return false;
        
        // Check current animation state
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        
        // Can only uppercut from these states
        bool inValidState = stateInfo.IsName("ProneIdle") || 
                            stateInfo.IsName("Crawl") || 
                            (stateInfo.IsName("ProneDown") && stateInfo.normalizedTime >= 0.5f);
        
        return inValidState;
    }

    private IEnumerator ApplyAttackLaunchForce(AttackMove attackMove)
    {
        // For Uppercut, we need special handling to match the animation curve
        if (attackMove.moveName == "Uppercut")
        {
            // Wait a small amount to ensure animation has started
            yield return new WaitForSeconds(0.015f);
            
            // First, suspend gravity to prevent competing forces
            playerController.SuspendGravity();
            
            // Total duration of our force application
            float totalDuration = 0.5f; // Adjust to match your animation length
            float elapsedTime = 0f;
            
            // Define key points in our force curve that match your Maya animation
            // These forces will be applied in small increments
            float[] forceValues = {
            24.0f,  // Initial strong upward force (quadrupled from 6.0)
            22.0f,  // Still strong but slightly less (quadrupled from 5.5)
            20.0f,  // Continuing strong ascent (quadrupled from 5.0)
            16.0f,  // Starting to reduce (quadrupled from 4.0)
            12.0f,  // Moderate force as we approach peak (quadrupled from 3.0)
            8.0f,   // Lower force near peak (quadrupled from 2.0)
            4.0f,   // Small force at near-peak (quadrupled from 1.0)
            2.0f,   // Very small force at peak (quadrupled from 0.5)
            0.0f    // No force at the top (stays at 0)
            };
            
            int forceIndex = 0;
            float timePerForce = totalDuration / forceValues.Length;
            
            // Apply forces incrementally over time to match the curve
            while (elapsedTime < totalDuration && forceIndex < forceValues.Length)
            {
                // Check if we're still in the attack state
                if (!isAttacking || currentAttack != attackMove)
                {
                    break; // Exit if the attack was interrupted
                }
                
                // Get current force value
                float currentForce = forceValues[forceIndex];
                
                // Apply the current force
                playerController.ApplyAttackLaunchForce(currentForce);
                
                // Wait for next force application time
                yield return new WaitForSeconds(timePerForce);
                
                // Update time and index
                elapsedTime += timePerForce;
                forceIndex++;
            }
            
            // After all forces are applied, let physics take over naturally
            Debug.Log("Uppercut force sequence complete");
        }
        else
        {
            // For other attacks, use standard timing
            yield return new WaitForSeconds(attackMove.launchForceDelay);
            
            // Only apply force if we're still in the attack state
            if (isAttacking && currentAttack == attackMove)
            {
                playerController.SuspendGravity();
                playerController.ApplyAttackLaunchForce(attackMove.launchForce);
            }
        }
    }
    
    private void MonitorAttackState()
    {
        if (IsPaused())
        {
            return;
        }
        // Increment attack animation timer
        attackAnimationTimer += Time.deltaTime;
        
        // NEW: Check for unpause immunity - skip fall detection if it's active
        if (!unpauseImmunity)
        {
            // IMPORTANT: Check if player has started falling during punch - SET FLAG IMMEDIATELY
            // This is the critical code that needs to be skipped right after unpausing
            if (currentAttack != null && currentAttack.moveName == "Punch" && !playerController.IsGrounded())
            {
                // Set the flag IMMEDIATELY when we detect a punch while not grounded
                // Don't wait for the timer
                punchedOffLedgeRecently = true;
                //Debug.Log("IMMEDIATE FLAG SET: Punch detected while airborne - GroundPound blocked");
                
                // Continue with normal detection and transition logic
                if (!isPunchingOffLedge)
                {
                    isPunchingOffLedge = true;
                    punchOffLedgeTimer = 0f;
                    Debug.Log("Started punching off ledge");
                }
                else
                {
                    // Increment the timer
                    punchOffLedgeTimer += Time.deltaTime;
                    
                    // Check if we've been off the ground long enough
                    if (punchOffLedgeTimer > 0.15f) // Adjust this value as needed
                    {
                        // We're definitely off a ledge now, transition to falling
                        Debug.Log("Confirmed punching off ledge - transitioning to falling");
                        
                        // Set the appropriate animation parameters
                        Animator animController = playerController.GetAnimator();
                        animController.SetBool("Punch", false);    // End punch animation
                        animController.SetBool("IsFalling", true); // Start falling animation
                        
                        // Tell PlayerController we're falling
                        playerController.SetFallingState(true);
                        
                        // End the attack state
                        isAttacking = false;
                        
                        // Reset timers
                        isPunchingOffLedge = false;
                        punchOffLedgeTimer = 0f;
                        
                        // Set cooldown
                        attackCooldownTimer = attackCooldown;
                        
                        return;
                    }
                }
            }
            else if (isPunchingOffLedge && playerController.IsGrounded())
            {
                // We were punching off a ledge but now we're grounded again
                isPunchingOffLedge = false;
                punchOffLedgeTimer = 0f;
                Debug.Log("No longer punching off ledge - returned to ground");
            }
        }
        
        // Normal attack animation completion check - always performs this check
        // regardless of immunity status
        Animator anim = playerController.GetAnimator();
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        
        bool animationCompleteByTime = attackAnimationTimer >= currentAttack.attackDuration;
        bool animationCompleteByState = stateInfo.IsName(currentAttack.moveName) && stateInfo.normalizedTime >= 0.9f;
        
        // Check if the attack animation is complete
        if (animationCompleteByTime || animationCompleteByState)
        {
            // Attack animation complete
            OnAttackAnimationComplete();
        }
    }
    
    private void OnAttackAnimationComplete(bool resetMovement = true)
    {
        // CRITICAL: Special handling for SpinKick
        if (currentAttack != null && currentAttack.moveName == "SpinKick")
        {
            Animator anim = playerController.GetAnimator();
            
            // Set SpinKick parameter to false
            anim.SetBool("SpinKick", false);
            
            // Check if we should transition to fall or recovery
            if (!playerController.IsGrounded())
            {
                // Transition to special fall (still in attack state)
                isInSpecialFall = true;
                specialFallTimer = 0f;
                anim.SetBool("SKFall", true);
                
                // CRITICAL FIX: If we were jumping, clear the jumping state since we're now in SKFall
                if (anim.GetBool("IsJumping"))
                {
                    anim.SetBool("IsJumping", false);
                    Debug.Log("Cleared IsJumping animation - now in SKFall");
                }
                
                //Debug.Log("SpinKick animation complete - transitioning to SKFall (still in attack state)");
            }
            else
            {
                // Transition to recovery (no longer in attack state)
                isAttacking = false;
                isInSpecialRecovery = true;
                specialRecoveryTimer = 0f;
                
                // Clear any lingering jump state
                if (anim.GetBool("IsJumping"))
                {
                    anim.SetBool("IsJumping", false);
                    //Debug.Log("Cleared IsJumping animation - transitioning to SKRecover");
                }
                
                //Debug.Log("SpinKick animation complete - transitioning to SKRecover (attack state ended)");
            }
            return;
        }
        
        // For other attacks, continue with existing logic
        isAttacking = false;
        
        // Check if this attack has a special fall
        if (currentAttack.hasSpecialFall && !playerController.IsGrounded())
        {
            // Start special fall
            isInSpecialFall = true;
            specialFallTimer = 0f;
            playerController.StartSpecialFall(currentAttack);
            
            //Debug.Log("Attack complete - transitioning to special fall: " + currentAttack.specialFallAnimName);
        }
        else
        {
            // Reset attack animation but optionally preserve movement
            if (resetMovement)
            {
                playerController.EndAttackAnimation(currentAttack);
            }
            else
            {
                // Just reset animation without resetting movement
                Animator anim = playerController.GetAnimator();
                anim.SetBool(currentAttack.moveName, false);
            }
            
            // Set cooldown
            attackCooldownTimer = attackCooldown;
            
            //Debug.Log("Attack " + currentAttack.moveName + " complete" + (resetMovement ? "" : " (preserving momentum)"));
        }
    }
    
    private void MonitorSpecialFallState()
    {
        if (IsPaused())
        {
            return;
        }
        
        // Increment special fall timer
        specialFallTimer += Time.deltaTime;
        
        // Special handling for SKFall
        if (currentAttack != null && currentAttack.moveName == "SpinKick")
        {
            // Check if we've hit the ground
            if (playerController.IsGrounded())
            {
                // Check if highland or splat bools are set for transition handling
                Animator anim = playerController.GetAnimator();
                bool shouldHighLand = anim.GetBool("ShouldHighLand");
                bool shouldSplat = anim.GetBool("ShouldSplat");
                
                if (shouldSplat)
                {
                    Debug.Log("SKFall landed with ShouldSplat=true - letting animator handle Splat transition");
                    // Clean up attack state and let animator transition to Splat
                    isInSpecialFall = false;
                    isAttacking = false;
                    specialFallTimer = 0f;
                    attackCooldownTimer = attackCooldown;
                    return;
                }
                else if (shouldHighLand)
                {
                    //Debug.Log("SKFall landed with ShouldHighLand=true - letting animator handle HighLand transition");
                    // Clean up attack state and let animator transition to HighLand
                    isInSpecialFall = false;
                    isAttacking = false;
                    specialFallTimer = 0f;
                    attackCooldownTimer = attackCooldown;
                    return;
                }
                else
                {
                    // Normal SKFall -> SKRecover transition (short fall)
                    //Debug.Log("SKFall normal landing - transitioning to SKRecover");
                    
                    // Set SKFall to false
                    anim.SetBool("SKFall", false);
                    
                    // Clear any jumping state
                    anim.SetBool("IsJumping", false);
                    
                    // End attack state and start recovery
                    isAttacking = false;
                    isInSpecialFall = false;
                    isInSpecialRecovery = true;
                    specialRecoveryTimer = 0f;
                    anim.SetBool("SKRecover", true);
                    
                    Debug.Log("Normal transition from SKFall to SKRecover");
                    return;
                }
            }
        }
        else if (currentAttack != null && currentAttack.moveName == "GroundPound")
        {
            // Existing GroundPound logic remains unchanged
            playerController.TrackGroundPoundFallTime(Time.deltaTime);
            playerController.UpdateGroundPoundForce(Time.deltaTime);
            
            if (specialFallTimer >= playerController.GetSplatThreshold())
            {
                playerController.PrepareGroundPoundForSplat();
                Debug.Log("GroundPound fall time exceeds splat threshold - preparing for splat landing");
            }
            
            // Check if we've hit the ground
            if (playerController.IsGrounded())
            {
                if (playerController.ShouldGroundPoundSplat())
                {
                    isInSpecialFall = false;
                    playerController.TransitionToSplat(currentAttack);
                    Debug.Log("GroundPound transitioned to splat due to extreme height!");
                }
                else
                {
                    isInSpecialFall = false;
                    isInSpecialRecovery = true;
                    specialRecoveryTimer = 0f;
                    playerController.StartSpecialRecovery(currentAttack);
                    Debug.Log("Special fall landed - transitioning to recovery");
                }
            }
        }
        
        // If we've been in special fall for too long (safeguard)
        if (specialFallTimer > 5.0f && !playerController.IsGrounded())
        {
            // Force end the special fall
            isInSpecialFall = false;
            playerController.EndSpecialFall(currentAttack);
            attackCooldownTimer = attackCooldown;
            
            Debug.Log("Special fall timed out - returning to normal fall");
        }
    }

    public void NotifySpecialFallEnded()
    {
        isInSpecialFall = false;
        isInSpecialRecovery = false;  // Make sure this is also reset
        
        // Reset timers
        specialFallTimer = 0f;
        specialRecoveryTimer = 0f;
        
        Debug.Log("AttackController notified that special fall ended");
    }
    
    private void MonitorSpecialRecoveryState()
    {
        if (IsPaused())
        {
            return;
        }
        // Increment special recovery timer
        specialRecoveryTimer += Time.deltaTime;
        
        // Check if the recovery is complete
        if (specialRecoveryTimer >= currentAttack.specialRecoveryDuration)
        {
            // Recovery complete
            isInSpecialRecovery = false;
            playerController.EndSpecialRecovery(currentAttack);
            
            // Set cooldown
            attackCooldownTimer = attackCooldown;
        }
    }
    
    // Public methods to check attack state
    public bool IsInAttackState()
    {
        // Get the animator
        Animator anim = playerController?.GetAnimator();
        if (anim != null)
        {
            // Check for SpinKick animation or parameter directly
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            bool inSpinKickAnim = stateInfo.IsName("SpinKick");
            bool spinKickParamActive = anim.GetBool("SpinKick");
            
            if (inSpinKickAnim || spinKickParamActive)
            {
                return true;
            }
            
            // Check for SKFall animation or parameter
            bool inSKFallAnim = stateInfo.IsName("SKFall");
            bool skFallParamActive = anim.GetBool("SKFall");
            
            if (inSKFallAnim || skFallParamActive)
            {
                return true;
            }
            
            // Also check for GPFall animation or parameter
            bool inGPFallAnim = stateInfo.IsName("GPFall");
            bool gpFallParamActive = anim.GetBool("GPFall");
            
            if (inGPFallAnim || gpFallParamActive)
            {
                return true;
            }
        }
        
        // If we're in recovery for SpinKick, don't consider it an attack state
        if (isInSpecialRecovery && currentAttack != null && currentAttack.moveName == "SpinKick")
        {
            return false;
        }
        
        // Otherwise, use the standard logic
        return isAttacking || isInSpecialFall || isInSpecialRecovery;
    }

    public bool IsInAnySpecialFall()
    {
        // First check the explicit IsInSpecialFall flag
        if (isInSpecialFall)
        {
            return true;
        }
        
        // Also check SpinKick/SKFall states
        if (IsInSpinKickOrFall())
        {
            return true;
        }
        
        // Check for any other special fall animations
        Animator anim = playerController?.GetAnimator();
        if (anim != null)
        {
            // Check for GroundPound fall state or parameter
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            bool inGPFallAnim = stateInfo.IsName("GPFall");
            bool gpFallParamActive = anim.GetBool("GPFall");
            
            if (inGPFallAnim || gpFallParamActive)
            {
                return true;
            }
        }
        
        return false;
    }

    public bool IsInSpinKickOrFall()
    {
        Animator anim = playerController?.GetAnimator();
        if (anim == null) return false;
        
        // Only check for SKFall here, not SpinKick animation
        bool skFallActive = anim.GetBool("SKFall");
        
        // Check actual animation state for SKFall
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool inSKFallAnim = stateInfo.IsName("SKFall");
        
        return skFallActive || inSKFallAnim;
    }

    public bool IsInSpinKickAnimation()
    {
        Animator anim = playerController?.GetAnimator();
        if (anim == null) return false;
        
        // Check animation parameter
        bool spinKickActive = anim.GetBool("SpinKick");
        
        // Check actual animation state
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        bool inSpinKickAnim = stateInfo.IsName("SpinKick");
        
        return spinKickActive || inSpinKickAnim;
    }
    
    public bool IsInSpecialFall()
    {
        return isInSpecialFall;
    }
    
    public bool IsInSpecialRecovery()
    {
        return isInSpecialRecovery;
    }

    public void NotifyPlayerLanded()
    {
        // Clear the punchedOffLedgeRecently flag when landing
        if (punchedOffLedgeRecently)
        {
            Debug.Log("Player landed - clearing punchedOffLedgeRecently flag");
            punchedOffLedgeRecently = false;
        }
        // Reset the GroundPound flag when landing
        hasUsedGroundPoundThisJump = false;
        Debug.Log("Player landed - GroundPound available again");
    }
    
    // Methods for swapping attacks at runtime
    public void SwapLightAttack(AttackMove newAttack)
    {
        if (newAttack.attackType != AttackType.Light)
        {
            Debug.LogWarning("Attack type mismatch - expected Light attack");
            return;
        }
        
        lightAttackSlot = newAttack;
        Debug.Log("Swapped Light attack to: " + newAttack.moveName);
    }
    
    public void SwapMediumAttack(AttackMove newAttack)
    {
        if (newAttack.attackType != AttackType.Medium)
        {
            Debug.LogWarning("Attack type mismatch - expected Medium attack");
            return;
        }
        
        mediumAttackSlot = newAttack;
        Debug.Log("Swapped Medium attack to: " + newAttack.moveName);
    }
    
    public void SwapHeavyAttack(AttackMove newAttack)
    {
        if (newAttack.attackType != AttackType.Heavy)
        {
            Debug.LogWarning("Attack type mismatch - expected Heavy attack");
            return;
        }
        
        heavyAttackSlot = newAttack;
        Debug.Log("Swapped Heavy attack to: " + newAttack.moveName);
    }
    
    public void SwapProneAttack(AttackType attackType, AttackMove newAttack)
    {
        if (newAttack.attackType != attackType)
        {
            Debug.LogWarning("Attack type mismatch");
            return;
        }
        
        switch (attackType)
        {
            case AttackType.Light:
                lightProneAttackSlot = newAttack;
                Debug.Log("Swapped Light Prone attack to: " + newAttack.moveName);
                break;
            case AttackType.Medium:
                mediumProneAttackSlot = newAttack;
                Debug.Log("Swapped Medium Prone attack to: " + newAttack.moveName);
                break;
            case AttackType.Heavy:
                heavyProneAttackSlot = newAttack;
                Debug.Log("Swapped Heavy Prone attack to: " + newAttack.moveName);
                break;
        }
    }
    
    public void SwapAirAttack(AttackType attackType, AttackMove newAttack)
    {
        if (newAttack.attackType != attackType)
        {
            Debug.LogWarning("Attack type mismatch");
            return;
        }
        
        switch (attackType)
        {
            case AttackType.Light:
                lightAirAttackSlot = newAttack;
                Debug.Log("Swapped Light Air attack to: " + newAttack.moveName);
                break;
            case AttackType.Medium:
                mediumAirAttackSlot = newAttack;
                Debug.Log("Swapped Medium Air attack to: " + newAttack.moveName);
                break;
            case AttackType.Heavy:
                heavyAirAttackSlot = newAttack;
                Debug.Log("Swapped Heavy Air attack to: " + newAttack.moveName);
                break;
        }
    }
    
    // Methods for loading multiple attacks at once (loadout system)
    [System.Serializable]
    public class AttackLoadout
    {
        public string loadoutName = "Default Loadout";
        
        // Ground attacks
        public AttackMove lightAttack;
        public AttackMove mediumAttack;
        public AttackMove heavyAttack;
        
        // Prone attacks
        public AttackMove lightProneAttack;
        public AttackMove mediumProneAttack;
        public AttackMove heavyProneAttack;
        
        // Air attacks
        public AttackMove lightAirAttack;
        public AttackMove mediumAirAttack;
        public AttackMove heavyAirAttack;
    }
    
    public AttackLoadout[] availableLoadouts;
    private int currentLoadoutIndex = 0;
    
    public void ApplyLoadout(int index)
    {
        if (index < 0 || index >= availableLoadouts.Length)
        {
            Debug.LogWarning("Invalid loadout index: " + index);
            return;
        }
        
        AttackLoadout loadout = availableLoadouts[index];
        
        // Apply all attacks from the loadout
        if (loadout.lightAttack != null) lightAttackSlot = loadout.lightAttack;
        if (loadout.mediumAttack != null) mediumAttackSlot = loadout.mediumAttack;
        if (loadout.heavyAttack != null) heavyAttackSlot = loadout.heavyAttack;
        
        if (loadout.lightProneAttack != null) lightProneAttackSlot = loadout.lightProneAttack;
        if (loadout.mediumProneAttack != null) mediumProneAttackSlot = loadout.mediumProneAttack;
        if (loadout.heavyProneAttack != null) heavyProneAttackSlot = loadout.heavyProneAttack;
        
        if (loadout.lightAirAttack != null) lightAirAttackSlot = loadout.lightAirAttack;
        if (loadout.mediumAirAttack != null) mediumAirAttackSlot = loadout.mediumAirAttack;
        if (loadout.heavyAirAttack != null) heavyAirAttackSlot = loadout.heavyAirAttack;
        
        currentLoadoutIndex = index;
        Debug.Log("Applied attack loadout: " + loadout.loadoutName);
    }
    
    public int GetCurrentLoadoutIndex()
    {
        return currentLoadoutIndex;
    }

    public void EnableUnpauseImmunity()
    {
        unpauseImmunity = true;
        Debug.Log("Attack controller: Unpause immunity enabled");
    }

    public void DisableUnpauseImmunity()
    {
        unpauseImmunity = false;
        Debug.Log("Attack controller: Unpause immunity disabled");
    }

    private bool IsPaused()
    {
        PauseMenu pauseMenu = FindObjectOfType<PauseMenu>();
        return pauseMenu != null && pauseMenu.IsPaused;
    }

    public void ForceEndAttack()
    {
        // This method forcibly ends any current attack
        isAttacking = false;
        isInSpecialFall = false;
        isInSpecialRecovery = false;
        
        // Reset timer
        attackAnimationTimer = 0f;
        
        // Reset ALL animation parameters
        if (playerController != null && playerController.GetAnimator() != null)
        {
            Animator anim = playerController.GetAnimator();
            
            // EXPLICITLY reset SpinKick parameters
            anim.SetBool("SpinKick", false);
            anim.SetBool("SKFall", false);
            anim.SetBool("SKRecover", false);
            
            // Reset other attack parameters
            anim.SetBool("Punch", false);
            anim.SetBool("Uppercut", false);
            anim.SetBool("GPFall", false);
            anim.SetBool("GPRecover", false);
            
            Debug.Log("ForceEndAttack: Reset ALL attack animation parameters");
        }
        
        // If there's a current attack reference, end it properly
        if (currentAttack != null)
        {
            // Call EndAttackAnimation on PlayerController if available
            playerController?.EndAttackAnimation(currentAttack);
            
            // Reset current attack reference
            currentAttack = null;
        }
        
        Debug.Log("Attack forcibly canceled by SlipNFall");
    }

    private bool CanExecuteFollowupAttack(AttackMove newAttack)
    {
        // Only allow follow-ups if we're currently in an attack
        if (!isAttacking || currentAttack == null)
            return false;
        
        // Check for Punch -> SpinKick follow-up
        if (currentAttack.moveName == "Punch" && newAttack.moveName == "SpinKick")
        {
            Animator anim = playerController.GetAnimator();
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            
            // Must be in Punch state and at least 80% complete
            if (stateInfo.IsName("Punch") && stateInfo.normalizedTime >= 0.5f)
            {
                Debug.Log("Punch -> SpinKick follow-up allowed at " + (stateInfo.normalizedTime * 100f).ToString("F1") + "% completion");
                return true;
            }
            else if (stateInfo.IsName("Punch"))
            {
                Debug.Log("Punch -> SpinKick follow-up too early - only " + (stateInfo.normalizedTime * 100f).ToString("F1") + "% complete (need 80%+)");
                return false;
            }
        }
        
        return false;
    }

    private void ExecuteFollowupAttack(AttackMove newAttack)
    {
        Debug.Log("Executing follow-up attack: " + currentAttack.moveName + " -> " + newAttack.moveName);
        
        // Special handling for Punch -> SpinKick
        if (currentAttack.moveName == "Punch" && newAttack.moveName == "SpinKick")
        {
            // Stop the punch force coroutine if it's running
            if (activePunchForceCoroutine != null)
            {
                StopCoroutine(activePunchForceCoroutine);
                activePunchForceCoroutine = null;
                Debug.Log("Stopped punch force coroutine for follow-up SpinKick");
            }
            
            // Clear the punch animation
            Animator anim = playerController.GetAnimator();
            anim.SetBool("Punch", false);
            
            // End the punch attack properly
            playerController.EndAttackAnimation(currentAttack);
            
            // Reset attack state briefly
            isAttacking = false;
            attackAnimationTimer = 0f;
            
            // Now execute the new attack
            ExecuteAttack(newAttack);
        }
    }
}