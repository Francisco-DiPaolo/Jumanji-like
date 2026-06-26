using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Local Player Reference")]
    [Tooltip("Asigna el jugador local si lo conoces, o el script lo buscará automáticamente")]
    [SerializeField] private PlayerMovement localPlayerMovement;

    private void Start()
    {
        // Hide Game Over panel at start
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // Subscribe to Shared Health events
        if (SharedHealthSystem.Instance != null)
        {
            SharedHealthSystem.Instance.OnHealthChanged += UpdateHealthBar;
            SharedHealthSystem.Instance.OnGameOver += ShowGameOver;
        }
        else
        {
            Debug.LogWarning("SharedHealthSystem instance not found in Start!");
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

    private void Update()
    {
        // Continuously try to find the local player if we haven't found them yet
        if (localPlayerMovement == null)
        {
            FindLocalPlayerStamina();
        }
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

    private void UpdateStaminaBar(float currentStamina, float maxStamina)
    {
        if (staminaBarFill != null)
        {
            staminaBarFill.fillAmount = currentStamina / maxStamina;
        }
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        
        // Optional: Unlock cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (SharedHealthSystem.Instance != null)
        {
            SharedHealthSystem.Instance.OnHealthChanged -= UpdateHealthBar;
            SharedHealthSystem.Instance.OnGameOver -= ShowGameOver;
        }

        if (localPlayerMovement != null)
        {
            localPlayerMovement.OnStaminaChanged -= UpdateStaminaBar;
        }
    }
}
