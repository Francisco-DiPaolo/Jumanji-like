using UnityEngine;

/// <summary>
/// AltarTimer — Sistema de feedback sonoro de urgencia.
/// Se conecta al CentralClockManager para leer el tiempo restante del ciclo
/// y reproduce sonidos de alerta de forma intercalada y dinámica a medida que
/// el tiempo se agota.
/// 
/// FLUJO:
///   - Silencio          → timeRemaining > alertThreshold
///   - Alerta intercalada → timeRemaining <= alertThreshold (Clip A, Clip B, Clip A...)
///   - Combinación cambia → timeRemaining llega a 0 → suena combinationChangeClip
/// </summary>
public class AltarTimer : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  REFERENCIAS
    // ─────────────────────────────────────────────
    [Header("Referencias")]
    [Tooltip("El CentralClockManager del que se leerá el tiempo restante.")]
    [SerializeField] private CentralClockManager centralClock;

    [SerializeField] private AudioSource audioSource;


    // ─────────────────────────────────────────────
    //  CLIPS
    // ─────────────────────────────────────────────
    [Header("Audio Clips")]
    [Tooltip("Primer sonido de la secuencia alternada.")]
    [SerializeField] private AudioClip clipA;

    [Tooltip("Segundo sonido de la secuencia alternada.")]
    [SerializeField] private AudioClip clipB;

    [Tooltip("Sonido que suena cuando el tiempo llega a 0 y la combinación cambia.")]
    [SerializeField] private AudioClip combinationChangeClip;


    // ─────────────────────────────────────────────
    //  CONFIGURACIÓN
    // ─────────────────────────────────────────────
    [Header("Configuración de Alerta")]
    [Tooltip("Tiempo restante (en segundos) a partir del cual empieza la alerta sonora.")]
    [SerializeField] private float alertThreshold = 8f;

    [Tooltip(
        "Mapea el tiempo restante normalizado (X: 1=umbral, 0=agotado) " +
        "contra el intervalo entre sonidos en segundos (Y). " +
        "Ejemplo: X=1 → Y=1.0s (normal), X=0 → Y=0.4s (rápido).")]
    [SerializeField] private AnimationCurve urgencyCurve = new AnimationCurve(
        new Keyframe(0f, 0.4f),   // cerca de 0 → más rápido (pero sin ser metralleta)
        new Keyframe(1f, 1.0f)    // cerca del umbral → tic tac normal (1 segundo)
    );


    // ─────────────────────────────────────────────
    //  ESTADO INTERNO
    // ─────────────────────────────────────────────

    /// <summary>Tiempo hasta el próximo sonido de alerta.</summary>
    private float _alertTimer = 0f;

    /// <summary>Controla qué clip se reproduce a continuación (A o B).</summary>
    private bool _playClipA = true;

    /// <summary>Indica si ya estamos en la fase de alerta activa.</summary>
    private bool _isAlerting = false;

    /// <summary>Guarda el tiempo del último frame para detectar el reset (llegada a 0).</summary>
    private float _previousTimeRemaining = float.MaxValue;


    // ─────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────
    private void Update()
    {
        // Validación de dependencias mínimas y asegurarse de que el objeto de red esté inicializado (evita el error de 'Spawned')
        if (centralClock == null || audioSource == null || centralClock.Object == null) return;

        // Si el reloj está detenido (puzzle resuelto), desactivar el sistema
        if (!centralClock.IsRunning)
        {
            StopAlertSequence();
            return;
        }

        float timeRemaining = centralClock.CycleTimeRemaining;

        // ── Detectar llegada a 0 (reset del ciclo) ──────────────────────────
        // El CycleTimeRemaining salta de un valor bajo de vuelta a cycleDuration
        // cuando el ciclo se reinicia. Lo detectamos comparando con el frame anterior.
        if (_previousTimeRemaining < alertThreshold && timeRemaining > alertThreshold)
        {
            OnCombinationChanged();
        }

        _previousTimeRemaining = timeRemaining;

        // ── Fase de silencio ────────────────────────────────────────────────
        if (timeRemaining > alertThreshold)
        {
            _isAlerting = false;
            _alertTimer = 0f;   // Reiniciar para que el primer sonido suene inmediatamente al entrar en alerta
            _playClipA  = true;
            return;
        }

        // ── Fase de alerta ──────────────────────────────────────────────────

        // Calcular valor normalizado: 1 = estamos justo en el umbral, 0 = tiempo agotado
        float normalized = Mathf.Clamp01(timeRemaining / alertThreshold);

        // Obtener el intervalo actual de la curva de urgencia
        float currentInterval = urgencyCurve.Evaluate(normalized);

        _alertTimer -= Time.deltaTime;

        if (_alertTimer <= 0f)
        {
            PlayNextAlertSound();

            // Programar el siguiente sonido con el intervalo dinámico actual
            _alertTimer = Mathf.Max(currentInterval, 0.05f); // mínimo de 50ms de seguridad
        }

        _isAlerting = true;
    }


    // ─────────────────────────────────────────────
    //  MÉTODOS PRIVADOS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Reproduce el clip correspondiente (A o B) y alterna para la próxima vez.
    /// </summary>
    private void PlayNextAlertSound()
    {
        AudioClip toPlay = _playClipA ? clipA : clipB;
        
        // Si el segundo clip (B) está vacío, repetimos el primero para que no haya un "bache" de silencio (tempo irregular)
        if (toPlay == null) toPlay = clipA;

        if (toPlay != null)
            audioSource.PlayOneShot(toPlay);

        _playClipA = !_playClipA;
    }

    /// <summary>
    /// Se llama cuando el ciclo llega a 0 y la combinación cambia.
    /// Interrumpe la secuencia y reproduce el sonido de cambio de combinación.
    /// </summary>
    private void OnCombinationChanged()
    {
        StopAlertSequence();

        if (combinationChangeClip != null)
            audioSource.PlayOneShot(combinationChangeClip);

        Debug.Log("[AltarTimer] Combinación cambiada — sonido de cambio reproducido.");
    }

    /// <summary>
    /// Resetea el estado de alerta, dejando el sistema en fase de silencio.
    /// </summary>
    private void StopAlertSequence()
    {
        _isAlerting  = false;
        _alertTimer  = 0f;
        _playClipA   = true;
    }


    // ─────────────────────────────────────────────
    //  GIZMOS (ayuda visual en el editor)
    // ─────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (centralClock == null) return;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.3f,
            $"Alerta activa: {_isAlerting}\nPróximo sonido en: {_alertTimer:F2}s");
    }
#endif
}
