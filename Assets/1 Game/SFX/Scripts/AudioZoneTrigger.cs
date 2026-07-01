using UnityEngine;
using UnityEngine.Audio;

public class AudioZoneTrigger : MonoBehaviour
{
    [SerializeField] private AudioMixerSnapshot zoneSnapshot;
    [SerializeField] private float fadeTime = 1.5f; // Tiempo de transición en segundos

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"AudioZoneTrigger: Algo entró al trigger -> {other.gameObject.name} con Tag: {other.tag}");

        // Asegúrate de que el objeto que entra sea el jugador
        if (other.CompareTag("Player"))
        {
            if (zoneSnapshot != null)
            {
                Debug.Log($"AudioZoneTrigger: ¡Es el Player! Cambiando a snapshot {zoneSnapshot.name}");
                zoneSnapshot.TransitionTo(fadeTime);
            }
            else
            {
                Debug.LogWarning("AudioZoneTrigger: El Player entró, pero 'zoneSnapshot' no está asignado en el Inspector.");
            }
        }
    }
}