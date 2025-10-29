using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthManager : MonoBehaviour
{
    public static HealthManager instance;

    static readonly float invincibilityTime = 3f;
    static readonly float flashTime = 0.1f;

    bool invincible = false;

    public int maxHeartAmount;
    public int startHearts = 1;
    public int curHealth;
    public int maxHealth;
    private int healthPerHeart = 3;

    public Image[] healthImages;
    public Sprite[] healthSprites;

    private bool isRespawning;
    public bool IsRespawning { get { return isRespawning; } } // ADD THIS LINE
    
    private Vector3 respawnPoint;
    public float respawnLength;

    public Image blackScreen;
    private bool isFadeToBlack;
    private bool isFadeFromBlack;
    public float fadeSpeed;
    public float waitForFade;

    public PlayerController thePlayer;

    public Animator anim;

    public SkinnedMeshRenderer[] meshes;
    
    private void Awake()
    {
        instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        curHealth = startHearts * healthPerHeart;
        maxHealth = maxHeartAmount * healthPerHeart;
        checkHealthAmount();
        respawnPoint = thePlayer.transform.position;
    }

    void checkHealthAmount()
    {
        for (int i = 0; i < maxHeartAmount; i++)
        {
            if (startHearts <= i) {
                healthImages [i].enabled = false;
            } else
            {
                healthImages [i].enabled = true;
            }
        }
        UpdateHearts();
    }

    // Update is called once per frame
    void UpdateHearts()
    {
        bool empty = false;
        int i = 0;

        foreach (Image image in healthImages)
        {
            if (empty)
            {
                image.sprite = healthSprites [0];
            }
            else
            {
                i++;
                if (curHealth >= i * healthPerHeart)
                {
                    image.sprite = healthSprites [healthSprites.Length - 1];
                }
                else
                {
                    int currentHeartHealth = (int)(healthPerHeart - (healthPerHeart * i - curHealth));
                    int healthPerImage = healthPerHeart / (healthSprites.Length - 1);
                    int imageIndex = currentHeartHealth / healthPerImage;
                    image.sprite = healthSprites [imageIndex];
                    empty = true;
                }
            }
        }               
    }

    public void AddHeartContainer()
    {
        startHearts++;
        startHearts = Mathf.Clamp (startHearts, 0, maxHeartAmount);

        curHealth = startHearts * healthPerHeart;
        maxHealth = maxHeartAmount * healthPerHeart;

        checkHealthAmount();
    }
    
    private void Update()
    {
        // ADD DEBUG INFO
        if (isRespawning)
        {
            Debug.Log("HealthManager: Currently respawning");
        }
    }

    public void HurtPlayer(int damage, Vector3 direction)
    {
        if (invincible == false)
        {
            curHealth -= damage;

            if (curHealth <= 0)
            {                    
                Respawn();
            }
        }
        if (!invincible && !isRespawning)
        {
            StartCoroutine(Flash());
        }

        UpdateHearts();
    }

    public void Respawn()
    {
        if (!isRespawning)
        {
            Debug.Log("HealthManager: Starting respawn process");
            StartCoroutine("RespawnCo");
        }
    }

    IEnumerator Flash()
    {
        float time = 0f;
        bool showMeshes = false;

        invincible = true;
        anim.SetBool("Hurt", true);

        while (time < invincibilityTime)
        {
            foreach (SkinnedMeshRenderer mr in meshes)
            {
                mr.enabled = showMeshes;
            }

            yield return new WaitForSeconds(flashTime);
            showMeshes = !showMeshes;

            time = time + flashTime;
        }

        foreach (SkinnedMeshRenderer mr in meshes)
        {
            mr.enabled = true;
        }

        invincible = false;
        anim.SetBool("Hurt", false);
    }

    public IEnumerator RespawnCo()
    {
        isRespawning = true; // SET AT START
        Debug.Log("HealthManager: Respawn coroutine started - isRespawning = true");

        foreach (Image image in healthImages) image.sprite = healthSprites[0];

        anim.SetBool("Dead", true);

        GameObject player = GameObject.Find("Player");
        CharacterController charController = player.GetComponent<CharacterController>();
        charController.enabled = false;

        yield return new WaitForSeconds(respawnLength);

        isFadeToBlack = true;
        anim.SetBool("Dead", false);

        yield return new WaitForSeconds(waitForFade);              

        isFadeToBlack = false;
        isFadeFromBlack = true;
        thePlayer.transform.position = respawnPoint;

        charController.enabled = true;
        
        thePlayer.transform.position = respawnPoint;
        curHealth = maxHealth;
        foreach (Image image in healthImages) image.sprite = healthSprites[3];
        
        isRespawning = false; // SET AT END
        Debug.Log("HealthManager: Respawn coroutine finished - isRespawning = false");
    }

    public void HealPlayer(int healAmount)
    {
        curHealth += healAmount;
        foreach (Image image in healthImages) 
        {
            image.sprite = healthSprites[3];
        }

        if (curHealth > maxHealth)
        {
            curHealth = maxHealth;
        }
    }

    public void SetSpawnPoint(Vector3 newPosition)
    {
        respawnPoint = newPosition;
    }
}