using UnityEngine;
using UnityEngine.Audio;

public class AudioZoneTrigger : MonoBehaviour
{
    [SerializeField] private AudioMixerSnapshot zoneSnapshot;
    [SerializeField] private float fadeTime = 1.5f; // Tiempo de transición en segundos

    private void OnTriggerEnter(Collider other)
    {
        // Asegúrate de que el objeto que entra sea el jugador
        if (other.CompareTag("Player"))
        {
            zoneSnapshot.TransitionTo(fadeTime);
        }
    }
}