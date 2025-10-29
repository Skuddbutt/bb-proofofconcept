using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject mainMenuPanel;
    public GameObject optionsMenuPanel;
    public Button startGameButton;
    public Button optionsButton;
    public Button quitButton;
    public Button closeOptionsButton;
    
    private Button[] mainMenuButtons;

    private void OnEnable()
    {
        if (startGameButton != null) startGameButton.onClick.AddListener(StartGame);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptionsMenu);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (closeOptionsButton != null) closeOptionsButton.onClick.AddListener(CloseOptionsMenu);
    }

    private void OnDisable()
    {
        if (startGameButton != null) startGameButton.onClick.RemoveListener(StartGame);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptionsMenu);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
        if (closeOptionsButton != null) closeOptionsButton.onClick.RemoveListener(CloseOptionsMenu);
    }

    private void Start()
    {
        mainMenuButtons = new Button[] { startGameButton, optionsButton, quitButton };
        
        optionsMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        ShowMainMenuButtons(true);
    }

    private void ShowMainMenuButtons(bool show)
    {
        foreach (Button button in mainMenuButtons)
        {
            if (button != null)
            {
                button.gameObject.SetActive(show);
                button.interactable = show; // Explicitly set interactable state
            }
        }
    }

    public void StartGame()
    {
        // Check if SceneController exists first, otherwise load scene directly
        if (SceneController.Instance != null)
        {
            Debug.Log("Starting game via SceneController");
            SceneController.Instance.StartGame();
        }
        else
        {
            Debug.LogWarning("SceneController not found, loading scene directly");
            SceneManager.LoadScene("DressUpMinigame");
        }
    }

    public void OpenOptionsMenu()
    {
        ShowMainMenuButtons(false);
        optionsMenuPanel.SetActive(true);
        if (closeOptionsButton != null)
        {
            closeOptionsButton.gameObject.SetActive(true);
            closeOptionsButton.interactable = true; // Ensure the button is interactable
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game");
        Application.Quit();
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    public void CloseOptionsMenu()
    {
        optionsMenuPanel.SetActive(false);
        if (closeOptionsButton != null)
            closeOptionsButton.gameObject.SetActive(false);
        ShowMainMenuButtons(true);
    }
}