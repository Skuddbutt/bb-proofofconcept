using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    // Scene names
    public const string MAIN_MENU_SCENE = "MainMenu";
    public const string DRESS_UP_SCENE = "DressUpMinigame";
    public const string BEACH_BATTLE_SCENE = "BeachBombBattle";

    // Fade references
    public Canvas fadeCanvas;
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1.5f;
    
    // Transition flag
    private bool isTransitioning = false;
    public bool IsTransitioning { get { return isTransitioning; } }
    
    // Outfit persistence properties
    private string currentOutfit = "PJs";
    private HashSet<int> activeAccessories = new HashSet<int>();
    private float waistBlendShapeValue = 0f;
    private float torsoBlendShapeValue = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Ensure fade canvas is properly configured
            if (fadeCanvas != null)
            {
                DontDestroyOnLoad(fadeCanvas.gameObject);

                if (fadeCanvasGroup == null)
                    fadeCanvasGroup = fadeCanvas.GetComponent<CanvasGroup>();

                // Make sure the canvas is at the highest sorting order
                fadeCanvas.sortingOrder = 999;

                // Start fully faded in (black screen) when game begins
                fadeCanvasGroup.alpha = 1f;
                fadeCanvasGroup.blocksRaycasts = true;

                Debug.Log("SceneController: Fade canvas initialized - starting with black screen");

                // Subscribe to scene loaded event
                SceneManager.sceneLoaded += OnSceneLoaded;

                // Start with a fade-in
                StartCoroutine(FadeIn());
            }
            else
            {
                Debug.LogError("SceneController: Fade canvas not assigned!");
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Scene loaded: {scene.name}, Current outfit: {currentOutfit}, Accessories: {activeAccessories.Count}");
        
        // Start fade-in when any scene is loaded
        StartCoroutine(FadeIn());
    }

    // Outfit persistence methods
    public void SaveOutfitState(string outfit, HashSet<int> accessories, float waistValue, float torsoValue)
    {
        // Only save if values are valid
        if (!string.IsNullOrEmpty(outfit))
        {
            currentOutfit = outfit;
        }
        
        if (accessories != null)
        {
            activeAccessories = new HashSet<int>(accessories);
        }
        
        waistBlendShapeValue = waistValue;
        torsoBlendShapeValue = torsoValue;
        
        Debug.Log($"Saved outfit state: {outfit}, Accessories: {accessories.Count}, Waist: {waistValue}, Torso: {torsoValue}");
    }

    public string GetCurrentOutfit()
    {
        return currentOutfit;
    }

    public HashSet<int> GetActiveAccessories()
    {
        return new HashSet<int>(activeAccessories);
    }

    public float GetWaistBlendShapeValue()
    {
        return waistBlendShapeValue;
    }

    public float GetTorsoBlendShapeValue()
    {
        return torsoBlendShapeValue;
    }

    public void StartGame()
    {
        Debug.Log("SceneController: Starting game with fade transition");
        FadeAndLoadScene(DRESS_UP_SCENE);
    }

    public void GoToMainMenu()
    {
        FadeAndLoadScene(MAIN_MENU_SCENE);
    }

    public void GoToBeachBattle()
    {
        // Before transitioning, ensure outfit data is persisted if we're in the dress-up scene
        if (SceneManager.GetActiveScene().name == DRESS_UP_SCENE)
        {
            MaterialAssignment materialAssignment = FindObjectOfType<MaterialAssignment>();
            BlendShapeController blendShapeController = FindObjectOfType<BlendShapeController>();
            
            if (materialAssignment != null)
            {
                // Force apply changes to ensure current state is saved
                materialAssignment.ApplyChanges();
                
                // Get the current outfit and accessories
                string outfitName = materialAssignment.CurrentOutfit;
                HashSet<int> accessories = materialAssignment.ActiveAccessories;
                
                // Get blendshape values if available
                float waistValue = 0f;
                float torsoValue = 0f;
                
                if (blendShapeController != null)
                {
                    waistValue = blendShapeController.GetCurrentWaistBlendShapeValue();
                    torsoValue = blendShapeController.GetCurrentTorsoBlendShapeValue();
                }
                
                // Save the outfit state
                SaveOutfitState(outfitName, accessories, waistValue, torsoValue);
                
                Debug.Log($"SceneController: Saved outfit before transition - Outfit: {outfitName}, Accessories: {accessories.Count}");
            }
            else
            {
                Debug.LogWarning("MaterialAssignment not found before scene transition!");
            }
        }
        
        FadeAndLoadScene(BEACH_BATTLE_SCENE);
    }

    public void FadeAndLoadScene(string sceneName)
    {
        StartCoroutine(FadeOutAndLoadScene(sceneName));
    }

    private IEnumerator FadeOutAndLoadScene(string sceneName)
    {
        Debug.Log($"Starting fade out to load scene: {sceneName}");
        isTransitioning = true; // Set transition flag to true

        // Make sure the fade canvas is active and visible
        if (fadeCanvas != null && fadeCanvasGroup != null)
        {
            fadeCanvas.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;

            // Fade to black
            float elapsedTime = 0;
            float startAlpha = fadeCanvasGroup.alpha;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsedTime / fadeDuration);
                yield return null;
            }

            // Ensure we're at full black
            fadeCanvasGroup.alpha = 1f;
            Debug.Log($"Fade out complete, loading scene: {sceneName}");

            // Load the new scene
            SceneManager.LoadScene(sceneName);

            // Note: We don't set isTransitioning to false here
            // It will be set to false at the end of FadeIn
        }
        else
        {
            Debug.LogError("Fade canvas or canvas group not assigned!");
            SceneManager.LoadScene(sceneName);
            isTransitioning = false; // Reset flag in case of direct scene load
        }
    }

    private IEnumerator FadeIn()
    {
        Debug.Log("Starting fade in from black");
        isTransitioning = true; // Ensure transition flag is true during fade in as well

        if (fadeCanvas != null && fadeCanvasGroup != null)
        {
            fadeCanvas.gameObject.SetActive(true);

            // Small delay to ensure scene is fully loaded
            yield return new WaitForSeconds(0.1f);

            // Ensure we start from black
            fadeCanvasGroup.alpha = 1f;

            // Fade from black to clear
            float elapsedTime = 0;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
                yield return null;
            }

            // Ensure we're fully transparent
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            Debug.Log("Fade in complete - screen is now clear and interactive");
            
            // Scene transition is now complete
            isTransitioning = false;
        }
        else
        {
            isTransitioning = false; // Reset flag even if no fade canvas
        }
    }
}