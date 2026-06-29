using UnityEngine;
using UnityEngine.Audio;

public class AudioMixerStarter : MonoBehaviour
{
    [SerializeField] private AudioMixerSnapshot startSnapshot;

    void Start()
    {
        // Fuerza al Audio Mixer a ponerse en el estado correcto desde el inicio
        if (startSnapshot != null)
        {
            startSnapshot.TransitionTo(0f);
        }
    }
}