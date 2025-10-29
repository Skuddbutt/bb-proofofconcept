using UnityEngine;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }
    
    public CanvasGroup fadeCanvasGroup;

    private void Awake()
    {
        // Set up singleton instance
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Make sure we have a CanvasGroup
            if (fadeCanvasGroup == null)
            {
                fadeCanvasGroup = GetComponent<CanvasGroup>();
                if (fadeCanvasGroup == null)
                {
                    fadeCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            
            // Check if we need to register with SceneController
            if (SceneController.Instance != null && SceneController.Instance.fadeCanvasGroup == null)
            {
                Debug.Log("FadeManager: Registering canvas group with SceneController");
                SceneController.Instance.fadeCanvasGroup = fadeCanvasGroup;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // If we're not managed by SceneController, start transparent and unblocking
        if (SceneController.Instance == null || SceneController.Instance.fadeCanvasGroup != fadeCanvasGroup)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }
    
    // Method to handle fade transitions between scenes
    public void FadeAndLoadScene(string sceneName)
    {
        if (SceneController.Instance != null)
        {
            // Delegate to SceneController if available
            SceneController.Instance.FadeAndLoadScene(sceneName);
        }
        else
        {
            // Otherwise handle it ourselves
            StartCoroutine(FadeOutAndLoadScene(sceneName));
        }
    }
    
    private System.Collections.IEnumerator FadeOutAndLoadScene(string sceneName)
    {
        Debug.Log($"FadeManager: Starting fade out to load scene: {sceneName}");
        
        float fadeDuration = 1.5f; // Default duration if not otherwise specified
        
        // Make sure the fade canvas is active and blocking raycasts
        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.blocksRaycasts = true;
        
        // Fade to black
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        fadeCanvasGroup.alpha = 1f;
        
        // Load the new scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        
        // Small delay to ensure scene is loaded
        yield return new WaitForSeconds(0.1f);
        
        // Fade in from black in the new scene
        StartCoroutine(FadeIn(fadeDuration));
    }
    
    private System.Collections.IEnumerator FadeIn(float fadeDuration)
    {
        Debug.Log("FadeManager: Starting fade in");
        
        // Ensure alpha is 1 (fully black) before starting fade
        fadeCanvasGroup.alpha = 1f;
        
        // Fade from black to clear
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure we end at fully transparent
        fadeCanvasGroup.alpha = 0f;
        
        // Stop blocking raycasts once fade is complete
        fadeCanvasGroup.blocksRaycasts = false;
    }
}