using UnityEngine;

/// <summary>
/// Agrega una animación procedural de respiración y head-bob a la cámara del jugador.
/// Colocar este script directamente en el GameObject de la Main Camera (hijo del cameraPivot).
/// No usa red (solo es visual/local) y no interfiere con la rotación de PlayerMovement.
/// </summary>
public class CameraBreathing : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  Referencia al jugador
    // ──────────────────────────────────────────────
    [Header("Player Reference")]
    [Tooltip("Arrastrá aquí el componente PlayerMovement del prefab del jugador.")]
    [SerializeField] private PlayerMovement playerMovement;

    // ──────────────────────────────────────────────
    //  Respiración en Idle
    // ──────────────────────────────────────────────
    [Header("Idle Breathing")]
    [Tooltip("Ciclos por segundo de la respiración en reposo.")]
    [SerializeField] private float idleBreathSpeed   = 0.35f;
    [Tooltip("Desplazamiento vertical máximo de la respiración (unidades de mundo).")]
    [SerializeField] private float idleBreathAmountY  = 0.004f;
    [Tooltip("Leve tilt lateral durante la respiración.")]
    [SerializeField] private float idleBreathRollAmount = 0.08f;
    [Tooltip("Pequeño movimiento lateral de la respiración.")]
    [SerializeField] private float idleBreathAmountX  = 0.0015f;

    // ──────────────────────────────────────────────
    //  Head Bob al caminar/correr
    // ──────────────────────────────────────────────
    [Header("Head Bob (Walking / Running)")]
    [Tooltip("Ciclos por segundo del bob mientras camina.")]
    [SerializeField] private float walkBobSpeed   = 1.8f;
    [Tooltip("Ciclos por segundo del bob mientras corre.")]
    [SerializeField] private float runBobSpeed    = 2.8f;
    [Tooltip("Desplazamiento vertical del bob al caminar.")]
    [SerializeField] private float walkBobAmountY = 0.012f;
    [Tooltip("Desplazamiento vertical del bob al correr.")]
    [SerializeField] private float runBobAmountY  = 0.022f;
    [Tooltip("Desplazamiento lateral del bob al caminar.")]
    [SerializeField] private float walkBobAmountX = 0.006f;
    [Tooltip("Desplazamiento lateral del bob al correr.")]
    [SerializeField] private float runBobAmountX  = 0.012f;
    [Tooltip("Leve tilt (Z-roll) durante el head bob.")]
    [SerializeField] private float bobRollAmount  = 0.25f;

    // ──────────────────────────────────────────────
    //  Transiciones suaves
    // ──────────────────────────────────────────────
    [Header("Smoothing")]
    [Tooltip("Velocidad de transición entre idle y movimiento.")]
    [SerializeField] private float transitionSpeed = 3.0f;
    [Tooltip("Velocidad de interpolación final de posición/rotación de la cámara.")]
    [SerializeField] private float smoothSpeed     = 12.0f;

    // ──────────────────────────────────────────────
    //  Estado interno
    // ──────────────────────────────────────────────
    private float   _bobTimer         = 0f;   // acumula tiempo para el seno del bob
    private float   _breathTimer      = 0f;   // acumula tiempo para la respiración idle
    private float   _movementBlend    = 0f;   // 0 = idle, 1 = caminando, 2 = corriendo
    private Vector3 _targetLocalPos   = Vector3.zero;
    private Vector3 _currentLocalPos  = Vector3.zero;
    private Vector3 _targetLocalEuler = Vector3.zero;
    private Vector3 _currentLocalEuler= Vector3.zero;

    // Posición original de la cámara (offset base respecto al pivot)
    private Vector3 _originLocalPos;
    private Quaternion _originLocalRot;

    // Seguimiento del flanco de subida del override
    private bool _wasOverrideActive = false;

    private void Start()
    {
        _originLocalPos   = transform.localPosition;
        _originLocalRot   = transform.localRotation;
        _currentLocalPos  = _originLocalPos;
        _currentLocalEuler= Vector3.zero;
    }

    private void Update()
    {
        bool overrideActive = playerMovement != null && playerMovement.CameraOverrideActive;

        // ── Si el override del tablero está activo ──────────────────
        // No tocamos nada para no pelear contra LeanTween, y evitamos forzar posiciones
        // que puedan causar un salto si el objeto fue desparentado.
        if (overrideActive)
        {
            _wasOverrideActive = true;
            return; 
        }

        // Flanco de bajada: el override acaba de desactivarse
        if (_wasOverrideActive)
        {
            _wasOverrideActive = false;
            // Para evitar un salto fuerte, reseteamos el punto de inicio de la interpolación al actual
            _currentLocalPos = transform.localPosition;
            
            // Extraer solo el roll (Z) actual relativo al origen
            Quaternion relativeRot = Quaternion.Inverse(_originLocalRot) * transform.localRotation;
            _currentLocalEuler = new Vector3(0, 0, relativeRot.eulerAngles.z);
            if (_currentLocalEuler.z > 180f) _currentLocalEuler.z -= 360f;
        }

        // Solo aplica en el jugador local
        if (playerMovement != null && !playerMovement.HasInputAuthority)
        {
            // Reseteamos para que otros jugadores no tengan bob visual (tienen su cámara desactivada de todas formas)
            return;
        }

        // ── Determinar blending de movimiento ──────
        float speedBlendTarget = GetSpeedBlend();
        _movementBlend = Mathf.Lerp(_movementBlend, speedBlendTarget, Time.deltaTime * transitionSpeed);

        float idleWeight  = Mathf.Clamp01(1f - _movementBlend);
        float walkWeight  = Mathf.Clamp01(_movementBlend <= 1f ? _movementBlend : 2f - _movementBlend);
        float runWeight   = Mathf.Clamp01(_movementBlend - 1f);

        // ── Respiración idle ───────────────────────
        _breathTimer += Time.deltaTime * idleBreathSpeed * Mathf.PI * 2f;
        float breathY    = Mathf.Sin(_breathTimer)                * idleBreathAmountY  * idleWeight;
        float breathX    = Mathf.Sin(_breathTimer * 0.6f)         * idleBreathAmountX  * idleWeight;
        float breathRoll = Mathf.Sin(_breathTimer * 0.5f)         * idleBreathRollAmount * idleWeight;

        // ── Head Bob (caminar + correr) ────────────
        float activeBobSpeed  = Mathf.Lerp(walkBobSpeed,   runBobSpeed,   runWeight);
        float activeBobAmountY= Mathf.Lerp(walkBobAmountY, runBobAmountY, runWeight);
        float activeBobAmountX= Mathf.Lerp(walkBobAmountX, runBobAmountX, runWeight);

        float movingWeight = walkWeight + runWeight;
        if (movingWeight > 0.001f)
            _bobTimer += Time.deltaTime * activeBobSpeed * Mathf.PI * 2f;

        float bobY    = -Mathf.Abs(Mathf.Sin(_bobTimer))      * activeBobAmountY * movingWeight; // siempre baja
        float bobX    =  Mathf.Sin(_bobTimer * 0.5f)           * activeBobAmountX * movingWeight;
        float bobRoll =  Mathf.Sin(_bobTimer * 0.5f)           * bobRollAmount    * movingWeight;

        // ── Combinar y asignar ─────────────────────
        _targetLocalPos   = _originLocalPos + new Vector3(breathX + bobX, breathY + bobY, 0f);
        _targetLocalEuler = new Vector3(0f, 0f, breathRoll + bobRoll);

        // Interpolación suave final
        _currentLocalPos   = Vector3.Lerp(_currentLocalPos,   _targetLocalPos,   Time.deltaTime * smoothSpeed);
        _currentLocalEuler = Vector3.Lerp(_currentLocalEuler, _targetLocalEuler, Time.deltaTime * smoothSpeed);

        transform.localPosition = _currentLocalPos;
        // Solo modificamos el roll (Z) relativo a su rotación original
        transform.localRotation = _originLocalRot * Quaternion.Euler(0f, 0f, _currentLocalEuler.z);
    }

    // ──────────────────────────────────────────────
    //  Helper: velocidad → blend 0 (idle), 1 (walk), 2 (run)
    // ──────────────────────────────────────────────
    private float GetSpeedBlend()
    {
        if (playerMovement == null) return 0f;

        // Velocidad horizontal (ignoramos Y)
        Vector3 vel = playerMovement.transform.GetComponent<CharacterController>()?.velocity ?? Vector3.zero;
        float hSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;

        // Umbrales: <0.5 = idle, ~2.5 = walk, ~5+ = run
        if (hSpeed < 0.5f)  return 0f;
        if (hSpeed < 3.5f)  return Mathf.InverseLerp(0.5f, 3.5f, hSpeed);       // 0→1 (idle→walk)
        return 1f + Mathf.InverseLerp(3.5f, 6.0f, hSpeed);                       // 1→2 (walk→run)
    }
}
