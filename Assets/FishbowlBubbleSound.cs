using System.Collections;
using UnityEngine;

/// <summary>
/// FishbowlBubbleSound — Reproduce sonido de burbujas 3D posicional para el jugador
/// que tiene la pecera (fishbowl) en la cabeza.
///
/// SETUP:
///   1. Agregar este componente al mismo GameObject donde está el AudioSource de voz
///      (típicamente el prefab del jugador remoto, dentro del nodo de cabeza o raíz).
///   2. Asignar varios AudioClip de burbujas en el array BubbleClips desde el Inspector.
///   3. Llamar a SetBubbleActive(true/false) desde el mismo lugar donde se activa
///      el modo Underwater en VoiceEffectController, o bien enganchar OnFishbowlStateChanged.
///
/// ENGANCHE AL SISTEMA EXISTENTE:
///   En el script que ya maneja HasFishbowl (detección de estado de red), donde hoy
///   asignas el AudioMixerGroup underwater al Speaker, agregá:
///
///       var bubbles = playerGO.GetComponent<FishbowlBubbleSound>();
///       if (bubbles != null) bubbles.SetBubbleActive(hasFishbowl);
///
///   No crear una detección de estado paralela; usar el mismo callback/OnChanged.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FishbowlBubbleSound : MonoBehaviour
{
    // ---- Clips de burbuja ----
    [Header("Bubble Clips")]
    [Tooltip("Lista de variantes de burbuja. Se elige una aleatoria en cada disparo.")]
    public AudioClip[] BubbleClips;

    // ---- Parámetros de volumen ----
    [Header("Volume")]
    [Tooltip("Volumen mínimo por disparo (recomendado: 0.15).")]
    [Range(0.01f, 1f)]
    public float VolumeMin = 0.15f;

    [Tooltip("Volumen máximo por disparo (recomendado: 0.30).")]
    [Range(0.01f, 1f)]
    public float VolumeMax = 0.30f;

    // ---- Parámetros de intervalo ----
    [Header("Timing")]
    [Tooltip("Intervalo mínimo entre burbujas (segundos).")]
    [Range(0.5f, 10f)]
    public float IntervalMin = 2f;

    [Tooltip("Intervalo máximo entre burbujas (segundos).")]
    [Range(0.5f, 10f)]
    public float IntervalMax = 5f;

    // ---- Internos ----
    private AudioSource _audioSource;
    private Coroutine   _bubbleCoroutine;
    private bool        _isActive;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
    }

    /// <summary>
    /// Configura el AudioSource para audio 3D posicional,
    /// igual al rango de atenuación ya usado por el chat de proximidad.
    /// NO mezcla con el canal de voz: usa el output default (sin AudioMixerGroup).
    /// </summary>
    private void ConfigureAudioSource()
    {
        // Espacialización 3D pura — respeta el mismo falloff que el chat de proximidad
        _audioSource.spatialBlend      = 1.0f;   // 100% 3D
        _audioSource.rolloffMode       = AudioRolloffMode.Logarithmic;
        _audioSource.minDistance       = 1f;
        _audioSource.maxDistance       = 20f;    // ajustar al mismo valor que el Speaker de voz
        _audioSource.dopplerLevel      = 0f;     // sin doppler para evitar pitch raro al moverse
        _audioSource.playOnAwake       = false;
        _audioSource.loop              = false;
        // outputAudioMixerGroup se deja null (default Master),
        // así NO se mezcla con el grupo VoiceUnderwater ni SFX de voz.
    }

    // ─────────────────────────────────────────────────────────────────
    //  API PÚBLICA — Llamar desde el sistema HasFishbowl existente
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activa o desactiva las burbujas. Enganchar al mismo evento/callback
    /// que ya activa el modo Underwater en VoiceEffectController.
    /// </summary>
    public void SetBubbleActive(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;

        if (active)
        {
            if (_bubbleCoroutine != null) StopCoroutine(_bubbleCoroutine);
            _bubbleCoroutine = StartCoroutine(BubbleLoop());
        }
        else
        {
            if (_bubbleCoroutine != null)
            {
                StopCoroutine(_bubbleCoroutine);
                _bubbleCoroutine = null;
            }
        }
    }

    /// <summary>
    /// Propiedad de solo lectura para saber si las burbujas están activas.
    /// </summary>
    public bool IsActive => _isActive;

    // ─────────────────────────────────────────────────────────────────
    //  Loop de burbujas
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator BubbleLoop()
    {
        // Pequeña espera inicial para no sincronizar todos los jugadores
        yield return new WaitForSeconds(Random.Range(0f, 1.5f));

        while (_isActive)
        {
            PlayBubble();

            // Intervalo aleatorio distinto en cada ciclo para evitar periodicidad
            float wait = Random.Range(IntervalMin, IntervalMax);
            yield return new WaitForSeconds(wait);
        }
    }

    private void PlayBubble()
    {
        if (BubbleClips == null || BubbleClips.Length == 0)
        {
            Debug.LogWarning("[FishbowlBubble] No hay AudioClips asignados en BubbleClips.", this);
            return;
        }

        // Clip aleatorio
        AudioClip clip = BubbleClips[Random.Range(0, BubbleClips.Length)];
        if (clip == null) return;

        // Volumen levemente aleatorio por disparo para que no suene repetitivo
        float vol = Random.Range(VolumeMin, VolumeMax);
        _audioSource.PlayOneShot(clip, vol);
    }

    private void OnDisable()
    {
        // Limpiar si el objeto se desactiva (p.ej. jugador que se va)
        SetBubbleActive(false);
    }
}
