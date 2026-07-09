using System.Collections;
using UnityEngine;

/// <summary>
/// Agrega un efecto de cámara temblorosa procedural.
/// Colocar este script en el mismo GameObject que CameraBreathing (la Main Camera).
/// Se ejecuta en LateUpdate para sumar el offset de shake DESPUÉS de que
/// CameraBreathing ya aplicó su posición, sin interferir con él.
///
/// Llamar a TriggerShake() desde cualquier sistema para disparar el temblor.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [Tooltip("Duración total del temblor en segundos.")]
    [SerializeField] private float shakeDuration = 2.5f;

    [Tooltip("Intensidad máxima del desplazamiento de posición (unidades de mundo).")]
    [SerializeField] private float shakePositionStrength = 0.08f;

    [Tooltip("Intensidad máxima del roll de rotación (grados).")]
    [SerializeField] private float shakeRotationStrength = 2.0f;

    [Tooltip("Frecuencia del ruido Perlin (más alto = más caótico).")]
    [SerializeField] private float shakeFrequency = 30f;

    [Tooltip("La intensidad decae con esta curva a lo largo del tiempo. " +
             "Eje X: progreso normalizado (0=inicio, 1=fin). Eje Y: multiplicador de intensidad.")]
    [SerializeField] private AnimationCurve decayCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // ─── Estado interno ─────────────────────────────────────────────────
    private float   _shakeTimer   = 0f;   // tiempo transcurrido del shake actual
    private bool    _isShaking    = false;

    // Semillas Perlin separadas por eje para evitar correlación entre X/Y/Z/Roll
    private float _seedX, _seedY, _seedZ, _seedRoll;


    // ─── API pública ─────────────────────────────────────────────────────

    /// <summary>
    /// Dispara el temblor de cámara. Si ya está temblando, lo reinicia.
    /// </summary>
    public void TriggerShake()
    {
        // Semillas aleatorias para que cada temblor sea diferente
        _seedX    = Random.Range(0f, 100f);
        _seedY    = Random.Range(0f, 100f);
        _seedZ    = Random.Range(0f, 100f);
        _seedRoll = Random.Range(0f, 100f);

        _shakeTimer = 0f;
        _isShaking  = true;
    }

    /// <summary>
    /// Dispara el temblor con parámetros personalizados (duración e intensidad).
    /// </summary>
    public void TriggerShake(float duration, float posStrength, float rotStrength)
    {
        float originalDuration    = shakeDuration;
        float originalPosStrength = shakePositionStrength;
        float originalRotStrength = shakeRotationStrength;

        shakeDuration          = duration;
        shakePositionStrength  = posStrength;
        shakeRotationStrength  = rotStrength;

        TriggerShake();

        // Restaurar los valores del Inspector al siguiente frame
        StartCoroutine(RestoreAfterFrame(originalDuration, originalPosStrength, originalRotStrength));
    }

    private IEnumerator RestoreAfterFrame(float dur, float pos, float rot)
    {
        yield return null;
        shakeDuration         = dur;
        shakePositionStrength = pos;
        shakeRotationStrength = rot;
    }


    // ─── LateUpdate: se ejecuta DESPUÉS de CameraBreathing.Update() ─────

    private void LateUpdate()
    {
        if (!_isShaking) return;

        _shakeTimer += Time.deltaTime;

        if (_shakeTimer >= shakeDuration)
        {
            _isShaking = false;
            return;  // No hay que resetear transform; CameraBreathing lo sobreescribe en el próximo frame
        }

        // Progreso normalizado 0→1
        float t = _shakeTimer / shakeDuration;

        // Intensidad actual según la curva de decaimiento
        float intensity = decayCurve.Evaluate(t);

        // Offset de posición usando ruido Perlin (centrado en 0 con *2 - 1)
        float noiseTime = _shakeTimer * shakeFrequency;

        float offsetX = (Mathf.PerlinNoise(_seedX + noiseTime, 0f) * 2f - 1f) * shakePositionStrength * intensity;
        float offsetY = (Mathf.PerlinNoise(_seedY + noiseTime, 1f) * 2f - 1f) * shakePositionStrength * intensity;
        float offsetZ = (Mathf.PerlinNoise(_seedZ + noiseTime, 2f) * 2f - 1f) * shakePositionStrength * 0.3f * intensity;

        // Offset de rotación (solo roll Z, para no competir con el pitch/yaw del jugador)
        float roll = (Mathf.PerlinNoise(_seedRoll + noiseTime, 3f) * 2f - 1f) * shakeRotationStrength * intensity;

        // Sumar encima de lo que CameraBreathing ya puso
        transform.localPosition += new Vector3(offsetX, offsetY, offsetZ);
        transform.localRotation *= Quaternion.Euler(0f, 0f, roll);
    }
}
