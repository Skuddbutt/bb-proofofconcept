using UnityEngine;
using UnityEngine.Playables;
using System.Collections;

public class CutsceneManager : MonoBehaviour
{
    private static CutsceneManager instance;
    public static bool GameReady = false;

    public PlayableDirector cutsceneDirector;
    public Camera cutsceneCamera;
    public Camera gameplayCamera;

    // Add reference to ForceFrameRate script  
    public ForceFrameRate frameRateController;

    // Fade system references
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1.5f;

    public GameObject uiCanvas;

    // References to the scripts we need to disable/enable during the cutscene
    public PlayerAnimationController playerAnimationController;
    public MaterialAssignment materialAssignment;
    public BlendShapeController blendShapeController;
    public OutfitUnlockManager outfitUnlockManager;
    public BlanketBlendShapeController blanketBlendShapeController;
    public Animation blanketAnimation;

    // Reference for the gameplay camera's starting position
    public Transform gameplayCameraStartPosition;
    
    // NEW: Track if the cutscene is paused
    private bool isPaused = false;

    private void Awake()
    {
        GameReady = false;
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        GameReady = false;

        // Disable the cutscene from automatically starting
        cutsceneDirector.Stop();

        // Enable frame rate control at the start of the cutscene
        if (frameRateController != null)
        {
            frameRateController.enabled = false; // Disable frame rate control until cutscene starts
        }

        // Disable the gameplay-related scripts at the start of the cutscene
        DisableGameplayScripts();

        if (cutsceneCamera != null) cutsceneCamera.enabled = true;
        if (gameplayCamera != null) gameplayCamera.enabled = false;

        // Start the cutscene immediately upon loading
        StartCutscene();
    }
    
    // NEW: Method to handle pause state
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Debug.Log($"CutsceneManager pause state set to: {isPaused}");
        
        // If we're pausing, make sure the cutscene camera stays active
        if (paused && cutsceneCamera != null && !cutsceneCamera.enabled)
        {
            Debug.Log("Forcing cutscene camera back on during pause");
            cutsceneCamera.enabled = true;
            if (gameplayCamera != null)
                gameplayCamera.enabled = false;
        }
    }

    public void StartCutscene()
    {
        if (uiCanvas != null)
        {
            uiCanvas.SetActive(false);  // Hide UI at the start of the cutscene
        }

        // Start with black screen
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 1f;  // Start fully black
        }

        // Enable frame rate control when cutscene starts
        if (frameRateController != null)
        {
            frameRateController.enabled = true;
        }

        // Start the cutscene and fade in
        if (cutsceneDirector != null)
        {
            cutsceneDirector.time = 0;  // Ensure cutscene starts from the beginning
            cutsceneDirector.Play();
            cutsceneDirector.played += OnCutsceneStarted;
            StartCoroutine(FadeInAndWaitForCutscene());
        }
    }

    private IEnumerator FadeInAndWaitForCutscene()
    {
        // First fade in from black
        if (fadeCanvasGroup != null)
        {
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.gameObject.SetActive(false);
        }

        // MODIFIED: Now track wait time with pause awareness
        float waitTime = 0f;
        float targetDuration = (float)cutsceneDirector.duration;
        
        while (waitTime < targetDuration)
        {
            // Only increment time if not paused
            if (!isPaused)
            {
                waitTime += Time.deltaTime;
            }
            
            // Check if we should maintain the cutscene camera force
            if (isPaused && cutsceneCamera != null && !cutsceneCamera.enabled)
            {
                Debug.Log("Force maintaining cutscene camera during pause");
                cutsceneCamera.enabled = true;
                if (gameplayCamera != null)
                    gameplayCamera.enabled = false;
            }
            
            yield return null;
        }
        
        // Only call OnCutsceneFinished if not paused
        if (!isPaused)
        {
            OnCutsceneFinished();
        }
        else
        {
            // If paused, wait until unpaused to finish
            StartCoroutine(WaitForUnpauseToFinish());
        }
    }
    
    // NEW: Method to wait for unpause before finishing
    private IEnumerator WaitForUnpauseToFinish()
    {
        yield return new WaitUntil(() => !isPaused);
        OnCutsceneFinished();
    }

    private void OnCutsceneStarted(PlayableDirector director)
    {
        Debug.Log("Cutscene started");
        cutsceneDirector.played -= OnCutsceneStarted;
    }

    public void SkipCutscene()
    {
        if (cutsceneDirector != null)
        {
            if (frameRateController != null)
            {
                frameRateController.StartTransition();
            }

            StopAllCoroutines();
            cutsceneDirector.time = cutsceneDirector.duration;
            cutsceneDirector.Evaluate();
            cutsceneDirector.Stop();

            DisableGameplayScripts();

            if (playerAnimationController != null)
            {
                playerAnimationController.GetComponent<Animator>()?.Rebind();
                playerAnimationController.GetComponent<Animator>()?.Update(0f);

                if (playerAnimationController.rugAnimator != null)
                {
                    playerAnimationController.rugAnimator.Rebind();
                    playerAnimationController.rugAnimator.Update(0f);
                }
            }

            // Ensure fade panel is invisible when skipping
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.gameObject.SetActive(false);
            }

            StartCoroutine(HandleSkipTransition());
        }
    }

    private IEnumerator HandleSkipTransition()
    {
        // Switch cameras first
        if (cutsceneCamera != null)
            cutsceneCamera.enabled = false;

        if (gameplayCamera != null && gameplayCameraStartPosition != null)
        {
            gameplayCamera.transform.position = gameplayCameraStartPosition.position;
            gameplayCamera.transform.rotation = gameplayCameraStartPosition.rotation;
            gameplayCamera.enabled = true;
        }

        // Small delay to ensure everything is reset
        yield return new WaitForSeconds(0.2f);

        // Enable all scripts at once and start animations
        EnableGameplayScripts();

        GameReady = true;
        Debug.Log("Cutscene skipped - gameplay beginning");
    }

    private void OnCutsceneFinished()
    {
        Debug.Log("Cutscene finished");

        if (cutsceneCamera != null) cutsceneCamera.enabled = false;

        if (gameplayCamera != null && gameplayCameraStartPosition != null)
        {
            gameplayCamera.transform.position = gameplayCameraStartPosition.position;
            gameplayCamera.transform.rotation = gameplayCameraStartPosition.rotation;
        }

        if (gameplayCamera != null) gameplayCamera.enabled = true;

        EnableGameplayScripts(); // This will now set GameReady before animations
        StartCoroutine(DisableFrameRateController());

        Debug.Log("Cutscene complete - gameplay can begin");
    }

    private IEnumerator DisableFrameRateController()
    {
        yield return new WaitForSeconds(0.1f);
        if (frameRateController != null)
        {
            frameRateController.EndTransition();
            frameRateController.enabled = false;
        }
    }

    private void DisableGameplayScripts()
    {
        if (playerAnimationController != null)
        {
            playerAnimationController.enabled = false;
        }
        if (materialAssignment != null)
        {
            materialAssignment.enabled = false;
        }
        if (blendShapeController != null)
        {
            blendShapeController.enabled = false;
        }
        if (outfitUnlockManager != null)
        {
            outfitUnlockManager.enabled = false;
        }
        if (blanketBlendShapeController != null)
        {
            blanketBlendShapeController.enabled = false;
        }
        if (blanketAnimation != null)
        {
            blanketAnimation.enabled = false;
        }
    }

    private void EnableGameplayScripts()
    {
        // First enable all non-animation scripts
        if (materialAssignment != null)
        {
            materialAssignment.enabled = true;
        }
        if (blendShapeController != null)
        {
            blendShapeController.enabled = true;
        }
        if (outfitUnlockManager != null)
        {
            outfitUnlockManager.enabled = true;
        }
        if (blanketBlendShapeController != null)
        {
            blanketBlendShapeController.enabled = true;
        }
        if (blanketAnimation != null)
        {
            blanketAnimation.enabled = true;
        }

        // Important: Set GameReady BEFORE enabling animations
        // This ensures animations can start cleanly
        GameReady = true;

        // Start animations after a slight delay to ensure everything is ready
        StartCoroutine(StartAnimationsAfterDelay());
    }

    private IEnumerator StartAnimationsAfterDelay()
    {
        // Small delay before starting animations to ensure all systems are ready
        yield return new WaitForEndOfFrame();

        if (playerAnimationController != null)
        {
            // Reset player animator
            var playerAnimator = playerAnimationController.GetComponent<Animator>();
            if (playerAnimator != null)
            {
                playerAnimator.Rebind();
                playerAnimator.Update(0f);
            }

            // Special handling for rug animator
            if (playerAnimationController.rugAnimator != null)
            {
                // Force disable and then enable to ensure a clean state
                playerAnimationController.rugAnimator.enabled = false;
                yield return null; // Wait one frame
                playerAnimationController.rugAnimator.enabled = true;

                // Force the rug to the NewOutfitReveal state directly
                playerAnimationController.rugAnimator.Play("NewOutfitReveal", 0, 0f);
                playerAnimationController.rugAnimator.Update(0f);

                Debug.Log("Rug animation explicitly set to NewOutfitReveal");
            }

            // Enable controller and start sequence
            playerAnimationController.enabled = true;

            // Start player sequence but let the rug use our explicit animation above
            playerAnimationController.StartGameplaySequence();

            Debug.Log("Animations started after delay");
        }
    }

    private void OnDisable()
    {
        if (cutsceneDirector != null)
        {
            cutsceneDirector.played -= OnCutsceneStarted;
        }
    }
}