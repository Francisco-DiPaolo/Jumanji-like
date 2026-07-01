using UnityEngine;

public class DamageSource : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            Debug.Log($"[DamageSource] OnTriggerEnter activado por {other.name}");
            ApplyDamage();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(playerTag))
        {
            Debug.Log($"[DamageSource] OnCollisionEnter activado por {collision.collider.name}");
            ApplyDamage();
        }
    }

    private void ApplyDamage()
    {
        if (SharedHealthSystem.Instance != null)
        {
            Debug.Log($"[DamageSource] Aplicando {damageAmount} de daño al SharedHealthSystem.");
            SharedHealthSystem.Instance.TakeDamage(damageAmount);
        }
        else
        {
            Debug.LogError("[DamageSource] ¡SharedHealthSystem.Instance NO ENCONTRADO en la escena!");
        }
    }
}
