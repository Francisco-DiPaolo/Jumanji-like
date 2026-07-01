using UnityEngine;

/// <summary>
/// Interacción para la Boombox: alterna entre Pause y UnPause del AudioSource
/// cada vez que el jugador presiona E mientras la mira.
/// IMPORTANTE: asegurate de que el campo onSelect en el Inspector esté VACÍO,
/// o de lo contrario los eventos anteriores pueden solapar el audio.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BoomboxInteraction : BasicInteraction
{
    [Header("Boombox Settings")]
    [Tooltip("AudioSource que reproduce la canción. Se asigna automáticamente si está en el mismo GameObject.")]
    [SerializeField] private AudioSource radioAudioSource;

    private bool isPaused = false;

    private void Awake()
    {
        if (radioAudioSource == null)
            radioAudioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Toggle Pause / UnPause. NO llama a base.Select() para evitar que
    /// eventos del Inspector interfieran con la lógica de audio.
    /// </summary>
    public override void Select()
    {
        if (radioAudioSource == null) return;

        if (isPaused)
        {
            radioAudioSource.UnPause();
            isPaused = false;
        }
        else
        {
            radioAudioSource.Pause();
            isPaused = true;
        }
    }
}
