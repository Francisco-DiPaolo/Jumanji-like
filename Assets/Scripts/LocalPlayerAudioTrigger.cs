using Fusion;
using UnityEngine;

/// <summary>
/// Reproduce un sonido localmente cuando el jugador local entra en el trigger.
/// Solo se reproduce una vez (One-Shot).
/// </summary>
[RequireComponent(typeof(Collider))]
public class LocalPlayerAudioTrigger : NetworkBehaviour
{
    [Header("Configuración de Audio")]
    [Tooltip("El AudioSource que se reproducirá. Debe estar asignado en el Inspector.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Configuración de Colisión")]
    [Tooltip("El tag que identifica al jugador.")]
    [SerializeField] private string playerTag = "Player";

    // Flag booleano para asegurar que el sonido solo se reproduzca una vez
    private bool hasPlayed = false;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Verificamos si ya se reprodujo el sonido para salir rápido y ahorrar procesamiento
        if (hasPlayed) return;

        // 2. Verificamos si el objeto que colisionó tiene el tag de jugador
        if (other.CompareTag(playerTag))
        {
            // Intentamos obtener el componente NetworkObject del jugador que entró al trigger
            NetworkObject playerNetworkObject = other.GetComponent<NetworkObject>();

            // 3. Validación de red para Photon Fusion: 
            // Comprobamos si el objeto tiene un NetworkObject y si el cliente local tiene autoridad de input (HasInputAuthority).
            // Esto asegura que el sonido NO se reproduzca si un jugador remoto cruza el trigger en tu pantalla.
            if (playerNetworkObject != null && playerNetworkObject.HasInputAuthority)
            {
                ReproducirSonidoLocal();
            }
        }
    }

    private void ReproducirSonidoLocal()
    {
        if (audioSource != null)
        {
            audioSource.Play();
            hasPlayed = true; // Marcamos el flag como verdadero para que no vuelva a sonar

            // Apagamos el trigger para que no detecte más colisiones
            if (TryGetComponent<Collider>(out Collider col))
            {
                col.enabled = false;
            }
        }
        else
        {
            Debug.LogWarning("El AudioSource no está asignado en el LocalPlayerAudioTrigger.", this);
        }
    }
}
