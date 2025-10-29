using UnityEngine;
using System.Collections;

public class BlanketBlendShapeController : MonoBehaviour
{
    // Core component references
    public SkinnedMeshRenderer blanketRenderer;
    
    // Configuration settings for the blendshape animation
    public string blendShapeName = "YourBlendShapeName"; // The name of the blendshape to animate
    public float targetWeight = 100f;     // The maximum value our blendshape should reach
    public float transitionDuration = 1f; // How long it takes to reach the target value
    public float holdTime = 1f;           // How long to maintain the maximum value

    // Internal tracking variables
    private int blendShapeIndex;          // Stores the index of our blendshape
    private float originalWeight;          // Keeps track of the starting weight value
    private bool isInitialized = false;    // Tracks whether we've completed initialization

    private void Awake()
    {
        // We don't trigger anything here, since we want to delay initialization
        // until the cutscene starts (through StartGame in MainMenu).
    }

    // This method will be triggered when the cutscene starts
    public void InitializeAfterCutscene()
    {
        // Start our initialization sequence once the cutscene begins
        StartCoroutine(InitializeAfterCutsceneCoroutine());
    }

    private IEnumerator InitializeAfterCutsceneCoroutine()
    {
        Debug.Log("BlanketBlendShapeController: Waiting for cutscene to complete...");
        yield return new WaitUntil(() => CutsceneManager.GameReady); // Wait until the game is ready

        Debug.Log("BlanketBlendShapeController: Cutscene complete, initializing blanket blendshapes...");

        // Perform our initialization checks now that the cutscene is complete
        if (blanketRenderer == null)
        {
            Debug.LogWarning("Blanket Renderer is not assigned!");
            yield break;
        }
        
        // Get and validate the blendshape index
        blendShapeIndex = blanketRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);
        if (blendShapeIndex < 0)
        {
            Debug.LogWarning("Blendshape not found on the blanket.");
            yield break;
        }

        // Store the initial weight value
        originalWeight = blanketRenderer.GetBlendShapeWeight(blendShapeIndex);
        
        // Mark initialization as complete
        isInitialized = true;
        Debug.Log("BlanketBlendShapeController initialization complete.");
    }

    // This method is called externally to trigger the blendshape animation
    public void PlayBlendShapeAnimation()
    {
        // Only proceed if we're properly initialized
        if (isInitialized && blendShapeIndex >= 0)
        {
            StartCoroutine(AnimateBlendShape());
        }
        else
        {
            Debug.LogWarning("Cannot play animation - initialization incomplete or invalid blendshape.");
        }
    }

    private IEnumerator AnimateBlendShape()
    {
        // First, animate from original weight to target weight
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float newWeight = Mathf.Lerp(originalWeight, targetWeight, elapsed / transitionDuration);
            blanketRenderer.SetBlendShapeWeight(blendShapeIndex, newWeight);
            yield return null;
        }
        // Ensure we reach exactly the target weight
        blanketRenderer.SetBlendShapeWeight(blendShapeIndex, targetWeight);
        
        // Hold at the target weight for the specified duration
        yield return new WaitForSeconds(holdTime);
        
        // Then animate back to the original weight
        elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float newWeight = Mathf.Lerp(targetWeight, originalWeight, elapsed / transitionDuration);
            blanketRenderer.SetBlendShapeWeight(blendShapeIndex, newWeight);
            yield return null;
        }
        // Ensure we return exactly to the original weight
        blanketRenderer.SetBlendShapeWeight(blendShapeIndex, originalWeight);
    }
}
