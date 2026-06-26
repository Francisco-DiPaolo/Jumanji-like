using UnityEngine;

public class DamageSource : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            ApplyDamage();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(playerTag))
        {
            ApplyDamage();
        }
    }

    private void ApplyDamage()
    {
        if (SharedHealthSystem.Instance != null)
        {
            SharedHealthSystem.Instance.TakeDamage(damageAmount);
        }
        else
        {
            Debug.LogWarning("SharedHealthSystem instance not found!");
        }
    }
}
