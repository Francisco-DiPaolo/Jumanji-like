using Fusion;
using UnityEngine;
using System;

public class SharedHealthSystem : NetworkBehaviour
{
    public static SharedHealthSystem Instance { get; private set; }

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [Networked] public float CurrentHealth { get; set; }
    
    [Header("Debug")]
    [SerializeField] [Tooltip("Valor de debug para ver la vida en el inspector")]
    private float debugCurrentHealth;

    // Removed global audio settings, now handled by PlayerMovement locally

    [Header("Poison State")]
    [Networked] public NetworkBool IsPoisoned { get; set; }
    private float poisonResetTimer = 0f;
    private const float POISON_TIMEOUT = 1.0f; // Seconds without acid damage to clear poison

    // Local events for UI
    public Action<float, float> OnHealthChanged;
    public Action OnGameOver;
    public Action OnRevived;
    public Action<bool> OnPoisonStateChanged;

    public bool isGameOver = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            CurrentHealth = maxHealth;
        }
        
        // Ensure UI updates initially for all clients
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Update()
    {
        // Update debug value so it can be seen in the Inspector
        debugCurrentHealth = CurrentHealth;

        if (IsPoisoned)
        {
            poisonResetTimer -= Time.deltaTime;
            if (poisonResetTimer <= 0)
            {
                if (Object != null && Object.IsValid && HasStateAuthority)
                {
                    IsPoisoned = false;
                    RPC_BroadcastPoisonState(false);
                }
                else if (Object == null || !Object.IsValid)
                {
                    IsPoisoned = false;
                    OnPoisonStateChanged?.Invoke(false);
                }
            }
        }

        // Debug damage button
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            TakeDamage(10f); 
        }
    }

    // Call this method from any client or server
    public void TakeDamage(float amount, bool isPoison = false)
    {
        if (isGameOver) return;
        
        if (isPoison)
        {
            poisonResetTimer = POISON_TIMEOUT;
        }

        if (Object != null && Object.IsValid)
        {
            Debug.Log($"[SharedHealthSystem] TakeDamage RPC called for {amount} damage.");
            RPC_TakeDamage(amount, isPoison);
        }
        else
        {
            Debug.LogWarning($"[SharedHealthSystem] Red no detectada (offline). Aplicando {amount} de daño localmente.");
            ApplyDamageLocal(amount, isPoison);
        }
    }

    private void ApplyDamageLocal(float amount, bool isPoison)
    {
        CurrentHealth -= amount;
        
        if (isPoison && !IsPoisoned)
        {
            IsPoisoned = true;
            OnPoisonStateChanged?.Invoke(true);
        }

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            isGameOver = true;
            OnGameOver?.Invoke();
        }
        
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(float amount, NetworkBool isPoison)
    {
        Debug.Log($"[SharedHealthSystem] Ejecutando RPC_TakeDamage (Host) por {amount}. Vida antes: {CurrentHealth}");
        if (isGameOver) return;

        CurrentHealth -= amount;
        
        if (isPoison && !IsPoisoned)
        {
            IsPoisoned = true;
            RPC_BroadcastPoisonState(true);
        }

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            isGameOver = true;
            RPC_GameOver();
        }

        Debug.Log($"[SharedHealthSystem] Vida despues: {CurrentHealth}. Llamando a RPC_BroadcastDamage...");
        RPC_BroadcastDamage(CurrentHealth);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastDamage(float newHealth)
    {
        Debug.Log($"[SharedHealthSystem] Ejecutando RPC_BroadcastDamage (Todos) con nueva vida: {newHealth}");
        OnHealthChanged?.Invoke(newHealth, maxHealth);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastPoisonState(NetworkBool state)
    {
        OnPoisonStateChanged?.Invoke(state);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GameOver()
    {
        isGameOver = true;
        OnGameOver?.Invoke();
    }

    public void Revive()
    {
        if (Object != null && Object.IsValid)
        {
            RPC_Revive();
        }
        else
        {
            ApplyReviveLocal();
        }
    }

    private void ApplyReviveLocal()
    {
        CurrentHealth = maxHealth;
        isGameOver = false;
        IsPoisoned = false;
        OnPoisonStateChanged?.Invoke(false);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnRevived?.Invoke();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Revive()
    {
        CurrentHealth = maxHealth;
        isGameOver = false;
        IsPoisoned = false;
        RPC_BroadcastRevive();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastRevive()
    {
        isGameOver = false;
        OnPoisonStateChanged?.Invoke(false);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnRevived?.Invoke();
    }
}
