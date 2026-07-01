using UnityEngine;
using System.Collections.Generic;

public class DamageSource : MonoBehaviour
{
    public enum DamageType { Instant, DamageOverTime }

    [Header("General Settings")]
    public DamageType damageType = DamageType.Instant;
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private string playerTag = "Player";

    [Header("Instant Damage Settings (Pinchos)")]
    [Tooltip("Tiempo de espera antes de volver a recibir daño (cooldown).")]
    [SerializeField] private float damageCooldown = 1.5f;
    [SerializeField] private bool applyKnockback = true;
    [SerializeField] private float knockbackForceUp = 7f;
    [SerializeField] private float knockbackForceSide = 10f;

    [Header("Damage Over Time Settings (Ácido)")]
    [Tooltip("Cada cuántos segundos se aplica el daño continuo.")]
    [SerializeField] private float tickInterval = 0.5f;

    // Track cooldowns per player collider
    private Dictionary<Collider, float> playerCooldowns = new Dictionary<Collider, float>();

    private void OnTriggerEnter(Collider other)
    {
        if (damageType == DamageType.Instant && other.CompareTag(playerTag))
        {
            TryApplyDamage(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (damageType == DamageType.DamageOverTime && other.CompareTag(playerTag))
        {
            TryApplyDamage(other);
        }
    }

    private void TryApplyDamage(Collider other)
    {
        // Cancelar si el juego ya terminó (para evitar sonidos o empujes extra)
        if (SharedHealthSystem.Instance != null && SharedHealthSystem.Instance.isGameOver) return;

        float currentTime = Time.time;
        float cooldown = (damageType == DamageType.Instant) ? damageCooldown : tickInterval;

        // Check if player is still in cooldown
        if (playerCooldowns.TryGetValue(other, out float lastTime))
        {
            if (currentTime - lastTime < cooldown) return;
        }

        // Apply damage
        playerCooldowns[other] = currentTime;

        if (SharedHealthSystem.Instance != null)
        {
            bool isPoison = (damageType == DamageType.DamageOverTime);
            SharedHealthSystem.Instance.TakeDamage(damageAmount, isPoison);
        }
        else
        {
            Debug.LogError("[DamageSource] ¡SharedHealthSystem.Instance NO ENCONTRADO!");
        }

        // Play hurt sound directly from the player
        PlayerMovement pm = other.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.Rpc_PlayHurtSound();
        }

        // Apply Knockback if it's an Instant trap (like Spikes)
        if (damageType == DamageType.Instant && applyKnockback)
        {
            if (pm != null)
            {
                // Calculate push direction away from the trap's center
                Vector3 pushDir = (other.transform.position - transform.position);
                pushDir.y = 0; // Keep it horizontal
                
                if (pushDir.sqrMagnitude < 0.01f) 
                    pushDir = Random.onUnitSphere; // random if exactly centered
                    
                pushDir.y = 0;
                pushDir.Normalize();

                Vector3 knockback = (pushDir * knockbackForceSide) + (Vector3.up * knockbackForceUp);
                pm.ApplyKnockback(knockback);
            }
        }
    }
}
