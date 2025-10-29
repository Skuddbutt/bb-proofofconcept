using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameInputManager : MonoBehaviour
{
    // Singleton pattern
    public static GameInputManager Instance { get; private set; }
    
    // Input values
    private bool jumpPressed = false;
    private bool jumpWasPressed = false;
    private bool jumpButtonPressed = false;
    private bool jumpButtonConsumed = false;
    private Vector2 moveInput;
    private Vector2 lookInput;
    
    // Prone input
    private bool proneButtonPressed = false;
    
    // Attack inputs
    private bool lightAttackButtonPressed = false;
    private bool mediumAttackButtonPressed = false;
    private bool heavyAttackButtonPressed = false;
    
    // Current input method
    private bool usingController = false;
    
    // Input
    private GameControls controls;
    
    // Events that other scripts can subscribe to
    public delegate void InputDeviceChangedEvent(bool isController);
    public event InputDeviceChangedEvent OnInputDeviceChanged;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        controls = new GameControls();
        
        // Setup basic input callbacks
        controls.Gameplay.Jump.performed += ctx => OnJumpPerformed();
        controls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;
        controls.Gameplay.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Look.canceled += ctx => lookInput = Vector2.zero;
        controls.Gameplay.Pause.performed += ctx => TogglePause();
        controls.UI.Cancel.performed += ctx => HandleUICancel();
        
        // Setup prone input
        controls.Gameplay.Prone.performed += ctx => proneButtonPressed = true;
        controls.Gameplay.Prone.canceled += ctx => proneButtonPressed = false;
        
        // Setup attack inputs
        controls.Gameplay.LightAttack.performed += ctx => lightAttackButtonPressed = true;
        controls.Gameplay.LightAttack.canceled += ctx => lightAttackButtonPressed = false;
        
        controls.Gameplay.MediumAttack.performed += ctx => mediumAttackButtonPressed = true;
        controls.Gameplay.MediumAttack.canceled += ctx => mediumAttackButtonPressed = false;
        
        controls.Gameplay.HeavyAttack.performed += ctx => heavyAttackButtonPressed = true;
        controls.Gameplay.HeavyAttack.canceled += ctx => heavyAttackButtonPressed = false;
        
        // Setup device detection
        InputSystem.onDeviceChange += OnDeviceChange;
    }
    
    private void OnEnable()
    {
        if (controls != null)
        {
            controls.Enable();
            EnableGameplayInput();
        }
    }
    
    private void OnDisable()
    {
        if (controls != null)
        {
            controls.Disable();
        }
    }
    
    private void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }
    
    private void Update()
    {
        jumpWasPressed = jumpPressed;
        jumpPressed = false;
        
        // Check for controller vs keyboard/mouse
        bool isUsingControllerNow = Gamepad.current != null && 
            (Gamepad.current.wasUpdatedThisFrame || 
            !Gamepad.current.CheckStateIsAtDefault());
            
        // Check for mouse movement
        bool isUsingMouseNow = Mouse.current != null && 
            (Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f ||
            Mouse.current.leftButton.wasReleasedThisFrame ||
            Mouse.current.rightButton.wasReleasedThisFrame);
            
        // Update controller status if changed
        if (isUsingControllerNow && !usingController)
        {
            usingController = true;
            OnInputDeviceChanged?.Invoke(true);
            
            // Hide cursor in controller mode
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else if (isUsingMouseNow && usingController)
        {
            usingController = false;
            OnInputDeviceChanged?.Invoke(false);
            
            // Show cursor in mouse mode
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        
        // TEMPORARILY COMMENT THIS OUT until controller support is implemented
        // This will prevent duplicate pause handling
        /*
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
        */
    }
    
    // Reset jump flags
    private void LateUpdate()
    {
        if (jumpButtonPressed && jumpButtonConsumed)
        {
            jumpButtonPressed = false;
            jumpButtonConsumed = false;
        }
    }
    
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad)
        {
            bool connected = change == InputDeviceChange.Added || 
                            change == InputDeviceChange.Reconnected;
            
            if (connected)
            {
                usingController = true;
                OnInputDeviceChanged?.Invoke(true);
                
                // Hide cursor in controller mode
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                usingController = false;
                OnInputDeviceChanged?.Invoke(false);
                
                // Show cursor in mouse mode
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
    
    private void OnJumpPerformed()
    {
        jumpButtonPressed = true;
    }
    
    // UPDATED: Check for respawn states before allowing pause/unpause
    private bool CanPauseOrUnpause()
    {
        // Check SceneController transition state
        if (SceneController.Instance != null && SceneController.Instance.IsTransitioning)
        {
            Debug.Log("Pause blocked: Scene transition in progress");
            return false;
        }
        
        // Check if HealthManager respawn is in progress
        if (HealthManager.instance != null && HealthManager.instance.IsRespawning)
        {
            Debug.Log("Pause blocked: HealthManager respawn in progress");
            return false;
        }
        
        // Check if GameManager respawn is in progress
        if (GameManager.instance != null && GameManager.instance.IsRespawning)
        {
            Debug.Log("Pause blocked: GameManager respawn in progress");
            return false;
        }
        
        return true;
    }
    
    private void TogglePause()
    {
        Debug.Log("=== TogglePause called ===");
        
        // Find pause menu in scene
        PauseMenu pauseMenu = FindObjectOfType<PauseMenu>();
        
        if (pauseMenu != null)
        {
            if (pauseMenu.IsPaused)
            {
                // Always allow unpausing (the PauseMenu will handle any processing checks)
                Debug.Log("Attempting to unpause game");
                pauseMenu.UnpauseGame();
                
                // Return to appropriate mode based on scene
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (sceneName == "DressUpMinigame")
                {
                    EnableUIInput();
                }
                else
                {
                    EnableGameplayInput();
                }
            }
            else
            {
                // Check if pausing is allowed
                if (CanPauseOrUnpause())
                {
                    Debug.Log("Attempting to pause game");
                    pauseMenu.PauseGame();
                    EnableUIInput();
                }
                else
                {
                    Debug.Log("Pause action blocked!");
                }
            }
        }
        else
        {
            Debug.LogWarning("GameInputManager: Could not find PauseMenu!");
        }
    }

    private void HandleUICancel()
    {
        // Find pause menu in scene
        PauseMenu pauseMenu = FindObjectOfType<PauseMenu>();
        
        if (pauseMenu != null && pauseMenu.IsPaused)
        {
            // If we're in the options panel, just return to the main pause menu
            if (pauseMenu.IsInOptionsMenu)
            {
                pauseMenu.HideOptions();
            }
            // Otherwise, unpause the game
            else
            {
                pauseMenu.UnpauseGame();
                EnableGameplayInput(); // Switch back to gameplay input mode
            }
        }
    }
    
    // Public methods for other scripts
    public void EnableGameplayInput()
    {
        if (controls != null)
        {
            controls.Gameplay.Enable();
            controls.UI.Disable();
        }
    }
    
    public void EnableUIInput()
    {
        if (controls != null)
        {
            controls.Gameplay.Disable();
            controls.UI.Enable();
        }
    }
    
    // Specific method for main menu needed by MainMenu.cs
    public void EnableMainMenuInput()
    {
        // Main menu uses UI controls
        EnableUIInput();
        
        // If using controller, try to select a button in main menu
        if (IsUsingController() && EventSystem.current != null)
        {
            // Find first interactable button in scene
            Button[] buttons = FindObjectsOfType<Button>();
            foreach (Button button in buttons)
            {
                if (button.interactable && button.gameObject.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    break;
                }
            }
        }
    }
    
    public void ResetForNewScene()
    {
        // Reset jump state
        jumpButtonPressed = false;
        jumpButtonConsumed = false;
        
        // Reset attack states
        lightAttackButtonPressed = false;
        mediumAttackButtonPressed = false;
        heavyAttackButtonPressed = false;
        
        // Determine proper mode for the current scene
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (sceneName == "DressUpMinigame")
        {
            EnableUIInput();
        }
        else if (sceneName == "BeachBombBattle" || sceneName.Contains("Battle"))
        {
            EnableGameplayInput();
        }
        else if (sceneName == "MainMenu")
        {
            EnableMainMenuInput();
        }
        else
        {
            // Default to gameplay input for unknown scenes
            EnableGameplayInput();
        }
    }
    
    // Public accessors
    public Vector2 GetMoveInput() => moveInput;
    public Vector2 GetLookInput() => lookInput;
    public bool IsUsingController() => usingController;
    public bool IsInUIMode() => controls != null && controls.UI.enabled;
    
    // Get prone button state - returns true while the button is being held
    public bool GetProneInput() => proneButtonPressed;
    
    // Get attack button states - returns true while the button is being held
    public bool GetLightAttackInput() => lightAttackButtonPressed;
    public bool GetMediumAttackInput() => mediumAttackButtonPressed;
    public bool GetHeavyAttackInput() => heavyAttackButtonPressed;
    
    public bool GetAndResetJumpButtonPressed()
    {
        bool wasPressed = jumpButtonPressed;
        jumpButtonPressed = false;
        jumpButtonConsumed = true;
        return wasPressed;
    }
    
    public bool WasJumpPressed()
    {
        bool result = jumpWasPressed;
        jumpWasPressed = false;
        return result;
    }
}