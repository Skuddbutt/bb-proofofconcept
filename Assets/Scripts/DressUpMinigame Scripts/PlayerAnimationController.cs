using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    public int Level = 1;  // Default Level is 1 (DressUpMinigame)
    private float idleTimer = 0f;
    private float timeUntilStretch = 10f;
    private float stretchChance = 0.2f;

    // References to other systems (outfit, blendshape, etc.)
    public MaterialAssignment materialAssignment;
    public BlendShapeController blendShapeController;
    public Button changeButton;
    public Button confirmButton;  // Confirm button reference

    // Reference to the UI canvas (or entire UI GameObject) to be toggled
    public PauseMenu pauseMenu;
    public GameObject uiCanvas;

    // NEW: Reference to the Dressscreen's Animator (already set up)
    public Animator dressscreenAnimator;
    // NEW: Reference to the Rug's Animator.
    public Animator rugAnimator;
    // NEW: Reference to the Blanket's Animator.
    public Animator blanketAnimator;

    // Flags for managing sequences.
    private bool outfitWornUpdated = false;
    private bool isChanging = false;
    private bool isConfirming = false;

    public string nextSceneName = "BeachBombBattle"; // Set your next scene name

    private void Start()
    {
        animator = GetComponent<Animator>();

        // Do not trigger Start logic immediately, move it to StartGameplaySequence
        // Hide the UI immediately at startup.
        if (uiCanvas != null)
            uiCanvas.SetActive(false);

        // Set up button listeners.
        changeButton.onClick.AddListener(TriggerChange);
        confirmButton.onClick.AddListener(TriggerConfirmAnimation);
    }

    public void StartGameplaySequenceSync()
    {
        // First, prepare player animation
        animator.Play("NewOutfitReveal1", 0, 0f);  // Layer 0, time 0
        animator.Update(0f);
        animator.speed = 1;
        
        // Only initialize rug if it hasn't already been initialized
        if (rugAnimator != null && !rugAnimator.GetCurrentAnimatorStateInfo(0).IsName("NewOutfitReveal"))
        {
            rugAnimator.Play("NewOutfitReveal", 0, 0f);
            rugAnimator.Update(0f);
            rugAnimator.speed = 1;
        }
        
        // Start the coroutines for completion handling
        StartCoroutine(WaitForNewOutfitRevealToComplete());
        if (rugAnimator != null)
        {
            StartCoroutine(WaitForRugRevealToComplete());
        }
    }

    public void StartGameplaySequence()
    {
        StartGameplaySequenceSync();
    }

    private void Update()
    {
        if (!CutsceneManager.GameReady)
        {
            // Ensure UI is hidden during cutscenes
            if (uiCanvas != null && uiCanvas.activeSelf)
            {
                uiCanvas.SetActive(false);
            }
            return;
        }
        // Always handle UI visibility first
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        
        if (isChanging || isConfirming)
        {
            if (uiCanvas.activeSelf)
            {
                uiCanvas.SetActive(false);
                Debug.Log("UI hidden during change/confirm sequence.");
            }
            if (confirmButton.gameObject.activeSelf)
            {
                confirmButton.gameObject.SetActive(false);
            }
        }
        else
        {
            // Show UI when idle or stretching
            if (currentState.IsName("IdleBlink") ||
                currentState.IsName("IdleNoBlink") ||
                currentState.IsName("Stretching"))
            {
                if (!pauseMenu.IsPaused && !uiCanvas.activeSelf)
                {
                    uiCanvas.SetActive(true);
                    Debug.Log("UI shown because idle or stretching animation is playing.");
                }
            }

            // Confirm Button Visibility
            if (!isConfirming && (currentState.IsName("IdleBlink") ||
                currentState.IsName("IdleNoBlink") ||
                currentState.IsName("Stretching")))
            {
                if (!confirmButton.gameObject.activeSelf)
                {
                    confirmButton.gameObject.SetActive(true);
                    Debug.Log("Confirm button shown because idle or stretching animation is playing.");
                }
            }
            else
            {
                if (confirmButton.gameObject.activeSelf)
                {
                    confirmButton.gameObject.SetActive(false);
                    Debug.Log("Confirm button hidden because not in idle or confirm sequence.");
                }
            }
        }

        // Now check for states that should prevent further updates
        if (currentState.IsName("Invalid") ||
            currentState.IsName("Confirm"))
            return;

        // Change button interactability
        if (changeButton != null)
        {
            changeButton.interactable = materialAssignment.HasPendingChanges();
        }

        // Handle idle behavior
        if (animator.GetBool("IsIdle"))
        {
            animator.SetBool("ShouldBlink", Random.value < 0.5f);
            idleTimer += Time.deltaTime;
            if (idleTimer >= timeUntilStretch)
            {
                animator.SetBool("ShouldStretch", Random.value < stretchChance);
                idleTimer = 0f;
            }
        }
        else
        {
            idleTimer = 0f;
            animator.SetBool("ShouldBlink", false);
            animator.SetBool("ShouldStretch", false);
        }

        // Force IsIdle true if an idle animation is playing
        if (currentState.IsName("IdleBlink") || 
            currentState.IsName("IdleNoBlink") || 
            currentState.IsName("Stretching"))
        {
            if (!animator.GetBool("IsIdle"))
            {
                animator.SetBool("IsIdle", true);
                Debug.Log("IsIdle forced true because idle animation is playing.");
            }
            outfitWornUpdated = false;
        }

        // When NewOutfitReveal2 plays, update the OutfitWorn parameter
        if (currentState.IsName("NewOutfitReveal2") && !outfitWornUpdated)
        {
            UpdateOutfitWornParameter();
            outfitWornUpdated = true;
        }
    }

    private void PlayNewOutfitReveal()
    {
        animator.Play("NewOutfitReveal1");
    }

    private IEnumerator WaitForNewOutfitRevealToComplete()
    {
        yield return new WaitUntil(() =>
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName("NewOutfitReveal1") && stateInfo.normalizedTime >= 1f;
        });
        
        SetIdle(true);
        if (uiCanvas != null)
        {
            uiCanvas.SetActive(true);
            Debug.Log("NewOutfitReveal1 completed; idle animations enabled and UI shown.");
        }
    }

    private IEnumerator WaitForRugRevealToComplete()
    {
        yield return new WaitUntil(() => rugAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
        rugAnimator.SetBool("RevealComplete", true);
        Debug.Log("Rug NewOutfitReveal completed; RevealComplete set to true.");
    }

    public void SetIdle(bool isIdle)
    {
        animator.SetBool("IsIdle", isIdle);
    }

    public void TriggerChange()
    {
        if (!changeButton.interactable)
        {
            Debug.LogWarning("Change button is disabled. Cannot change outfit.");
            return;
        }
        isChanging = true;

        if (dressscreenAnimator != null)
        {
            dressscreenAnimator.SetTrigger("Shake");
            Debug.Log("Dressscreen Shake Triggered.");
        }

        if (blanketAnimator != null)
        {
            blanketAnimator.SetTrigger("PlayBlendAnim");
            Debug.Log("Blanket blendshape animation triggered.");
        }

        if (rugAnimator != null)
        {
            rugAnimator.Play("RunToChange1");
            StartCoroutine(WaitForRugRunToChangeComplete());
        }

        SetIdle(false);
        animator.Play("RunToChange");
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        Debug.Log("Change triggered; waiting for NewOutfitReveal2 Animation Event.");
    }

    private IEnumerator WaitForRugRunToChangeComplete()
    {
        yield return new WaitUntil(() =>
        {
            AnimatorStateInfo rugState = rugAnimator.GetCurrentAnimatorStateInfo(0);
            return rugState.IsName("RunToChange1") && rugState.normalizedTime >= 1f;
        });
        rugAnimator.Play("Idle");
        Debug.Log("Rug RunToChange1 complete; switched to Idle.");
    }

    public void OnNewOutfitReveal2Started()
    {
        materialAssignment.ApplyChanges();
        blendShapeController.ApplyStagedBlendShape();
        changeButton.interactable = false;
        Debug.Log("OnNewOutfitReveal2Started: Applied outfit/accessory and blendshape changes.");
        StartCoroutine(WaitForPostRevealDelay());
    }

    private IEnumerator WaitForPostRevealDelay()
    {
        yield return new WaitForSeconds(3.5f);
        isChanging = false;
        uiCanvas.SetActive(true);
        Debug.Log("Change sequence complete; UI shown 3.5 seconds after changes were applied.");
    }

    public void TriggerConfirmAnimation()
    {
        if (isConfirming) return;
        isConfirming = true;
        if (uiCanvas.activeSelf)
        {
            uiCanvas.SetActive(false);
        }
        if (confirmButton.gameObject.activeSelf)
        {
            confirmButton.gameObject.SetActive(false);
        }
        SetIdle(false);
        animator.SetBool("ShouldBlink", false);
        animator.SetBool("ShouldStretch", false);
        animator.Play("Confirm");
        Debug.Log("Confirm animation triggered!");
        Level = 2; // Set Level to 2, indicating transition should happen
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        StartCoroutine(WaitForLevelChange());
    }

    private IEnumerator WaitForLevelChange()
    {
        Debug.Log("Waiting 5 seconds before loading the next scene...");
        yield return new WaitForSeconds(4f); // x-second delay
    
        if (Level == 2) // Only transition if Level is still 2
        {
            Debug.Log("Level changed to 2, starting scene transition...");
            LoadNextScene();
        }
    }

    public void LoadNextScene()
    {
        // Use SceneController if available, otherwise direct scene loading
        if (SceneController.Instance != null)
        {
            Debug.Log("Loading next scene via SceneController...");
            SceneController.Instance.GoToBeachBattle();
        }
        else
        {
            Debug.Log("SceneController not found, loading scene directly...");
            SceneManager.LoadScene(nextSceneName);
        }
    }
    
    public void TriggerInvalidAnimation()
    {
        SetIdle(false);
        animator.SetBool("ShouldBlink", false);
        animator.SetBool("ShouldStretch", false);
        animator.Play("Invalid");
        Debug.Log("Invalid animation triggered!");
        StartCoroutine(WaitForInvalidToFinish());
    }

    private IEnumerator WaitForInvalidToFinish()
    {
        yield return new WaitUntil(() =>
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName("Invalid") && stateInfo.normalizedTime >= 1f;
        });
        ReturnToIdleAfterInvalid();
    }

    private void ReturnToIdleAfterInvalid()
    {
        SetIdle(true);
        if (Random.value < 0.5f)
            animator.Play("IdleBlink");
        else
            animator.Play("IdleNoBlink");
        Debug.Log("Returned to idle after Invalid animation.");
    }

    private void UpdateOutfitWornParameter()
    {
        int outfitValue = 0;
        string current = materialAssignment.CurrentOutfit;
        if (current == "PJs")
            outfitValue = 0;
        else if (current == "Magnasaur")
            outfitValue = 1;
        else if (current == "Zunleth")
            outfitValue = 2;
        else if (current == "Work")
            outfitValue = 3;
        else if (current == "Karate")
            outfitValue = 4;
        animator.SetInteger("OutfitWorn", outfitValue);
        Debug.Log("OutfitWorn parameter set to: " + outfitValue);
    }
}