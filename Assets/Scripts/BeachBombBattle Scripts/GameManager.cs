using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    private Vector3 respawnPosition;
    
    // Add respawn state tracking
    private bool isRespawning = false;
    public bool IsRespawning { get { return isRespawning; } }

    public int currentCoin;
    public TextMeshProUGUI coinText;
    public int currentKey;
    public TextMeshProUGUI keyText;

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        respawnPosition = PlayerController.instance.transform.position;

        Time.timeScale = 1f;
        
        // Reset input system for this scene
        if (GameInputManager.Instance != null)
        {
            GameInputManager.Instance.ResetForNewScene();
        }
        
        Debug.Log("BeachBattleInitializer: Time scale and input system reset");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Respawn()
    {
        if (!isRespawning)
        {
            StartCoroutine(RespawnCo());
        }
    }

    public IEnumerator RespawnCo()
    {
        isRespawning = true;
        Debug.Log("Respawning...");
        PlayerController.instance.gameObject.SetActive(false);
        yield return new WaitForSeconds(2f);
        PlayerController.instance.transform.position = respawnPosition;
        PlayerController.instance.gameObject.SetActive(true);
        isRespawning = false;
    }

    public void AddCoin(int coinToAdd)
    {
        currentCoin += coinToAdd;
        coinText.text = " " + currentCoin;
    }

    public void AddKey(int keyToAdd)
    {
        currentKey += keyToAdd;
        keyText.text = " " + currentKey;
    }
}