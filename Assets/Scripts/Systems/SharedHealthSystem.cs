using Fusion;
using UnityEngine;
using System;

public class SharedHealthSystem : NetworkBehaviour
{
    public static SharedHealthSystem Instance { get; private set; }

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [Networked] public float CurrentHealth { get; set; }

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip damageSound;

    // Local events for UI
    public Action<float, float> OnHealthChanged;
    public Action OnGameOver;

    private bool isGameOver = false;

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
        // Debug damage button
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            TakeDamage(10f); 
        }
    }

    // Call this method from any client or server
    public void TakeDamage(float amount)
    {
        if (isGameOver) return;
        
        if (Object != null && Object.IsValid)
        {
            RPC_TakeDamage(amount);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(float amount)
    {
        if (isGameOver) return;

        CurrentHealth -= amount;
        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            isGameOver = true;
            RPC_GameOver();
        }

        RPC_BroadcastDamage(CurrentHealth);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastDamage(float newHealth)
    {
        OnHealthChanged?.Invoke(newHealth, maxHealth);
        
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GameOver()
    {
        isGameOver = true;
        OnGameOver?.Invoke();
    }
}
