using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject gameplayUICanvas;

    [Header("Pause Menu Elements")]
    public GameObject pausePanel;
    public GameObject optionsPanel;
    public Image pauseImage;
    public Button skipCutsceneButton;
    public Button optionsButton;
    public Button quitButton;  
    public Button closeOptionsButton;
    public PlayableDirector cutsceneDirector;

    // Reference to the CutsceneManager
    private CutsceneManager cutsceneManager;

    private bool isPaused = false;
    private bool isInOptionsMenu = false;
    public bool IsPaused { get { return isPaused; } }
    public bool IsInOptionsMenu { get { return isInOptionsMenu; } }
    private Button[] pauseMenuButtons;
    private bool wasUIActiveBeforePause;
    private bool wasCutscenePlaying = false;
    
    // NEW: Flag for unpausing process
    private bool isProcessingUnpause = false;

    private void Start()
    {
        pauseMenuButtons = new Button[] { skipCutsceneButton, optionsButton, quitButton };

        // Hide all pause menu elements initially
        pausePanel.SetActive(false);
        optionsPanel.SetActive(false);
        pauseImage.gameObject.SetActive(false);
        ShowPauseMenuButtons(false);

        skipCutsceneButton.onClick.AddListener(SkipCutscene);
        optionsButton.onClick.AddListener(ShowOptions);
        quitButton.onClick.AddListener(QuitGame);
        if (closeOptionsButton != null)
            closeOptionsButton.onClick.AddListener(HideOptions);
            
        // Find CutsceneManager if we have a cutsceneDirector
        if (cutsceneDirector != null)
        {
            cutsceneManager = cutsceneDirector.GetComponent<CutsceneManager>();
            if (cutsceneManager == null)
            {
                cutsceneManager = FindObjectOfType<CutsceneManager>();
            }
        }
        else
        {
            cutsceneManager = FindObjectOfType<CutsceneManager>();
        }
        
        if (cutsceneManager == null)
        {
            Debug.LogWarning("PauseMenu could not find CutsceneManager!");
        }
    }

    private void Update()
    {
        // Skip input handling if we're currently processing unpause
        if (isProcessingUnpause)
            return;
            
        // Allow pausing only if the game is in gameplay or cutscene, not in Main Menu,
        // AND not during scene transitions
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            // For now, check ONLY for the keyboard's Escape key
            // (We'll add controller support later)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("=== ESCAPE KEY PRESSED ===");
                
                // Check if a scene transition is in progress
                bool canPause = true;
                
                // Check SceneController transition state
                if (SceneController.Instance != null && SceneController.Instance.IsTransitioning)
                {
                    canPause = false;
                    Debug.Log("Pause blocked: Scene transition in progress");
                }
                
                // Enhanced debugging for respawn states
                Debug.Log($"HealthManager.instance: {(HealthManager.instance != null ? "Found" : "NULL")}");
                if (HealthManager.instance != null)
                {
                    Debug.Log($"HealthManager.instance.IsRespawning: {HealthManager.instance.IsRespawning}");
                }
                
                Debug.Log($"GameManager.instance: {(GameManager.instance != null ? "Found" : "NULL")}");
                if (GameManager.instance != null)
                {
                    Debug.Log($"GameManager.instance.IsRespawning: {GameManager.instance.IsRespawning}");
                }
                
                // Check if any respawn process is in progress
                if (HealthManager.instance != null && HealthManager.instance.IsRespawning)
                {
                    canPause = false;
                    Debug.Log("Pause blocked: HealthManager respawn in progress");
                }
                
                if (GameManager.instance != null && GameManager.instance.IsRespawning)
                {
                    canPause = false;
                    Debug.Log("Pause blocked: GameManager respawn in progress");
                }
                
                Debug.Log($"Final canPause decision: {canPause}");
                
                // Only pause/unpause if allowed
                if (canPause)
                {
                    if (isPaused)
                    {
                        Debug.Log("Attempting to unpause game");
                        UnpauseGame();
                    }
                    else
                    {
                        Debug.Log("Attempting to pause game");
                        PauseGame();
                    }
                }
                else
                {
                    Debug.Log("Pause/Unpause action blocked!");
                }
            }
        }
    }

    private void ShowPauseMenuButtons(bool show)
    {
        foreach (Button button in pauseMenuButtons)
        {
            if (button != null)
                button.gameObject.SetActive(show);
        }
    }

    public void PauseGame()
    {
        Debug.Log("PauseGame() called");
        wasCutscenePlaying = cutsceneDirector != null && cutsceneDirector.state == PlayState.Playing;
        
        // Try both methods for extra robustness
        if (wasCutscenePlaying)
        {
            // Slow down the timeline
            if (cutsceneDirector != null)
            {
                Debug.Log("Setting PlayableDirector speed to 0");
                cutsceneDirector.playableGraph.GetRootPlayable(0).SetSpeed(0);
            }
            
            // Inform CutsceneManager about pause
            if (cutsceneManager != null)
            {
                Debug.Log("Informing CutsceneManager about pause");
                cutsceneManager.SetPaused(true);
            }
        }

        Time.timeScale = 0f;

        // Store UI state before hiding it
        wasUIActiveBeforePause = gameplayUICanvas.activeSelf;
        gameplayUICanvas.SetActive(false);

        pausePanel.SetActive(true);
        pauseImage.gameObject.SetActive(true);
        ShowPauseMenuButtons(true);

        if (wasCutscenePlaying)
        {
            skipCutsceneButton.gameObject.SetActive(true);
        }
        else
        {
            skipCutsceneButton.gameObject.SetActive(false);
        }

        isPaused = true;
        Debug.Log("Game paused successfully");
    }

    public void UnpauseGame()
    {
        // Start the unpause sequence
        StartCoroutine(UnpauseSequence());
    }
    
    // NEW: Coroutine that properly sequences the unpause operation
    private IEnumerator UnpauseSequence()
    {
        // Set the flag that we're processing unpause
        isProcessingUnpause = true;
        
        // Restart the timeline first if it was playing
        if (wasCutscenePlaying)
        {
            if (cutsceneDirector != null)
            {
                Debug.Log("Restoring PlayableDirector speed to 1");
                cutsceneDirector.playableGraph.GetRootPlayable(0).SetSpeed(1);
            }
            
            // Inform CutsceneManager about unpause
            if (cutsceneManager != null)
            {
                Debug.Log("Informing CutsceneManager about unpause");
                cutsceneManager.SetPaused(false);
            }
            
            wasCutscenePlaying = false;
        }
        
        // Disable pause UI first
        pausePanel.SetActive(false);
        optionsPanel.SetActive(false);
        pauseImage.gameObject.SetActive(false);
        ShowPauseMenuButtons(false);
        if (closeOptionsButton != null)
            closeOptionsButton.gameObject.SetActive(false);
            
        // Find player and attack controller first
        PlayerController playerController = FindObjectOfType<PlayerController>();
        AttackController attackController = null;
        if (playerController != null)
        {
            attackController = playerController.GetComponent<AttackController>();
        }
        
        // Notify attack controller about upcoming unpause
        if (attackController != null)
        {
            // Add temporary immunity flag - MUST happen before timescale changes
            attackController.EnableUnpauseImmunity();
        }
            
        // Important: restore timescale
        Time.timeScale = 1f;
        
        // Restore UI to its previous state - before physics updates
        gameplayUICanvas.SetActive(wasUIActiveBeforePause);
        
        // Wait for a FIXED update to occur - this ensures physics has a chance
        // to update positions and ground checks before any game logic
        yield return new WaitForFixedUpdate();
        
        // Wait one more frame to ensure animations and other systems catch up
        yield return null;
        
        // Clear the pause flags
        isPaused = false;
        isInOptionsMenu = false;
        
        // Allow a reasonable time for physics and other systems to stabilize
        yield return new WaitForSeconds(0.05f);
        
        // Disable immunity now that systems are stable
        if (attackController != null)
        {
            attackController.DisableUnpauseImmunity();
        }
        
        // Unpause sequence complete - clear the processing flag
        isProcessingUnpause = false;
        
        Debug.Log("Unpause sequence completed");
    }

    public void ShowOptions()
    {
        isInOptionsMenu = true;
        optionsPanel.SetActive(true);
        ShowPauseMenuButtons(false);

        if (closeOptionsButton != null)
            closeOptionsButton.gameObject.SetActive(true);
    }

    public void HideOptions()
    {
        isInOptionsMenu = false;
        optionsPanel.SetActive(false);

        if (closeOptionsButton != null)
            closeOptionsButton.gameObject.SetActive(false);

        if (isPaused)
        {
            ShowPauseMenuButtons(true);
            if (!wasCutscenePlaying)
            {
                skipCutsceneButton.gameObject.SetActive(false);
            }
        }
    }

    public void SkipCutscene()
    {
       if (cutsceneManager != null)
       {
           // Make sure UI stays hidden during skip
           if (gameplayUICanvas != null)
               gameplayUICanvas.SetActive(false);
           
           UnpauseGame();
           cutsceneManager.SkipCutscene();
       }
       else if (cutsceneDirector != null)
       {
           // Fallback if no cutsceneManager reference
           CutsceneManager foundManager = cutsceneDirector.GetComponent<CutsceneManager>();
           if (foundManager != null)
           {
               if (gameplayUICanvas != null)
                   gameplayUICanvas.SetActive(false);
                   
               UnpauseGame();
               foundManager.SkipCutscene();
           }
       }
    }

    public void QuitGame()
    {
        // If we are in the pause menu, return to the main menu
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            Debug.Log("Returning to Main Menu...");
            
            // Hide all UI elements before returning to main menu
            HideAllUI();
            
            // Reset time scale
            Time.timeScale = 1f;
            
            // Unpause the cutscene manager if needed
            if (cutsceneManager != null)
            {
                cutsceneManager.SetPaused(false);
            }
            
            // Return to main menu
            SceneController.Instance.GoToMainMenu();
        }
        // If we're in the Main Menu, quit the game
        else
        {
            Debug.Log("Quitting the game...");
            Application.Quit();
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }
    
    // Existing method to hide all UI elements when returning to main menu
    private void HideAllUI()
    {
        // Hide the main gameplay UI
        if (gameplayUICanvas != null)
            gameplayUICanvas.SetActive(false);
            
        // Hide pause menu elements
        if (pausePanel != null)
            pausePanel.SetActive(false);
            
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
            
        if (pauseImage != null)
            pauseImage.gameObject.SetActive(false);
            
        // Hide all buttons
        ShowPauseMenuButtons(false);
        
        if (closeOptionsButton != null)
            closeOptionsButton.gameObject.SetActive(false);
            
        // Find and hide any other UI canvases in the scene
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in allCanvases)
        {
            // Don't hide the SceneController's fade canvas
            if (canvas.gameObject.name != "FadeCanvas" && !canvas.gameObject.name.Contains("Fade"))
            {
                canvas.gameObject.SetActive(false);
            }
        }
        
        Debug.Log("All UI elements hidden before returning to main menu");
    }

    public void ReturnToMainMenu()
    {
        // Reset time scale
        Time.timeScale = 1f;
        isInOptionsMenu = false;
        isPaused = false;
        
        // Hide all UI elements
        HideAllUI();
        
        // Unpause the cutscene manager if needed
        if (cutsceneManager != null)
        {
            cutsceneManager.SetPaused(false);
        }

        // Try to use SceneController first
        if (SceneController.Instance != null)
        {
            Debug.Log("Returning to Main Menu via SceneController");
            SceneController.Instance.GoToMainMenu();
        }
        // Fall back to FadeManager if available
        else if (FadeManager.Instance != null)
        {
            Debug.Log("Returning to Main Menu via FadeManager");
            FadeManager.Instance.FadeAndLoadScene("MainMenu");
        }
        // Last resort: direct scene loading
        else
        {
            Debug.Log("No SceneController or FadeManager found, loading MainMenu directly");
            SceneManager.LoadScene("MainMenu");
        }
    }
}