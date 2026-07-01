using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image healthBarFrame;
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Health Bar Colors")]
    [SerializeField] private Color normalHealthColor = Color.white;
    [SerializeField] private Color poisonHealthColor = Color.green;
    [SerializeField] private Color normalFrameColor = Color.white;
    [SerializeField] private Color poisonFrameColor = Color.green;

    [Header("Game Over Settings")]
    [SerializeField] private GameObject gameOverCanvas;
    [SerializeField] private TMPro.TextMeshProUGUI gameOverText;
    [SerializeField] private TMPro.TextMeshProUGUI continueText;
    [SerializeField] private AudioSource typewriterAudio;
    [SerializeField] private AudioClip typeSound;
    [SerializeField] [TextArea] private string gameOverMessage = "Han perdido...";
    [SerializeField] private float typewriterSpeed = 0.05f;

    [Header("Local Player Reference")]
    [Tooltip("Asigna el jugador local si lo conoces, o el script lo buscará automáticamente")]
    [SerializeField] private PlayerMovement localPlayerMovement;

    private Coroutine typewriterCoroutine;
    private bool canRevive = false;

    private void Start()
    {
        // Hide Game Over panel at start
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameOverCanvas != null) gameOverCanvas.SetActive(false);
        if (continueText != null) continueText.gameObject.SetActive(false);

        // Subscribe to Shared Health events
        if (SharedHealthSystem.Instance != null)
        {
            SubscribeToHealth();
        }
        else
        {
            Debug.LogWarning("SharedHealthSystem instance not found in Start! Will keep looking...");
        }

        // Try to find local player if not assigned
        if (localPlayerMovement == null)
        {
            FindLocalPlayerStamina();
        }
        else
        {
            SubscribeToStamina(localPlayerMovement);
        }
    }

    private bool healthSubscribed = false;

    private void Update()
    {
        // Continuously try to find the local player if we haven't found them yet
        if (localPlayerMovement == null)
        {
            FindLocalPlayerStamina();
        }

        // Continuously try to find the health system if not subscribed
        if (!healthSubscribed && SharedHealthSystem.Instance != null)
        {
            SubscribeToHealth();
        }
        // Revive input detection
        if (canRevive && Input.GetKeyDown(KeyCode.Space))
        {
            canRevive = false;
            SharedHealthSystem.Instance.Revive();
        }
    }

    private void SubscribeToHealth()
    {
        SharedHealthSystem.Instance.OnHealthChanged += UpdateHealthBar;
        SharedHealthSystem.Instance.OnPoisonStateChanged += UpdatePoisonColor;
        SharedHealthSystem.Instance.OnGameOver += ShowGameOver;
        SharedHealthSystem.Instance.OnRevived += HideGameOver;
        healthSubscribed = true;
        
        // Initial UI update just in case we subscribed late
        UpdateHealthBar(SharedHealthSystem.Instance.CurrentHealth, 100f); 
    }

    private void FindLocalPlayerStamina()
    {
        PlayerMovement[] players = FindObjectsOfType<PlayerMovement>();
        foreach (var p in players)
        {
            if (p.HasInputAuthority || p.HasStateAuthority) // local player depending on setup
            {
                localPlayerMovement = p;
                SubscribeToStamina(p);
                break;
            }
        }
    }

    private void SubscribeToStamina(PlayerMovement player)
    {
        player.OnStaminaChanged += UpdateStaminaBar;
        
        // Initialize UI with current stamina if possible
        UpdateStaminaBar(player.CurrentStamina, 100f); // default max to 100 if we can't read it easily
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = currentHealth / maxHealth;
        }
    }

    private void UpdatePoisonColor(bool isPoisoned)
    {
        if (healthBarFill != null)
        {
            healthBarFill.color = isPoisoned ? poisonHealthColor : normalHealthColor;
        }

        if (healthBarFrame != null)
        {
            healthBarFrame.color = isPoisoned ? poisonFrameColor : normalFrameColor;
        }
    }

    private void UpdateStaminaBar(float currentStamina, float maxStamina)
    {
        if (staminaBarFill != null)
        {
            staminaBarFill.fillAmount = currentStamina / maxStamina;
        }
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true); // Black screen
        
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
            if (continueText != null) continueText.gameObject.SetActive(false);
            
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = StartCoroutine(TypeTextRoutine());
        }
        else
        {
            canRevive = true;
        }
        
        // Optional: Unlock cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HideGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameOverCanvas != null) gameOverCanvas.SetActive(false);
        
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        canRevive = false;
    }

    private System.Collections.IEnumerator TypeTextRoutine()
    {
        if (gameOverText != null)
        {
            gameOverText.text = "";
            foreach (char c in gameOverMessage)
            {
                gameOverText.text += c;
                if (typewriterAudio != null && typeSound != null)
                {
                    typewriterAudio.PlayOneShot(typeSound);
                }
                yield return new WaitForSeconds(typewriterSpeed);
            }
        }

        yield return new WaitForSeconds(0.5f);

        if (continueText != null)
        {
            continueText.gameObject.SetActive(true);
            continueText.text = "pulse \"space\" para revivir";
        }
        
        canRevive = true;
    }

    private void OnDestroy()
    {
        if (healthSubscribed && SharedHealthSystem.Instance != null)
        {
            SharedHealthSystem.Instance.OnHealthChanged -= UpdateHealthBar;
            SharedHealthSystem.Instance.OnPoisonStateChanged -= UpdatePoisonColor;
            SharedHealthSystem.Instance.OnGameOver -= ShowGameOver;
            SharedHealthSystem.Instance.OnRevived -= HideGameOver;
        }

        if (localPlayerMovement != null)
        {
            localPlayerMovement.OnStaminaChanged -= UpdateStaminaBar;
        }
    }
}
