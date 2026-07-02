using UnityEngine;
using UnityEngine.Events;
using Fusion;

/// <summary>
/// Controla una puerta de rejas (gate) mediante una rueda/winch interactuable.
///
/// ══════════════════════════════════════════════════════════════
///  SETUP EN UNITY (pasos obligatorios):
/// ══════════════════════════════════════════════════════════════
///  1. Agregar ESTE script al GameObject raíz del Winch (Winch_Wooden).
///     IMPORTANTE: Debe tener un componente NetworkObject para sincronizar en red.
///
///  2. Agregar un componente BasicInteraction al GameObject que tenga
///     el COLLIDER del winch (puede ser un hijo del FBX).
///
///  3. En ese BasicInteraction, en el evento "On Select":
///       → arrastrar el Winch_Wooden → función WinchGateController.OnPressed()
///
///  4. Asignar la referencia "Gate" (la puerta que sube) desde el Inspector.
///
///  5. Ajustar Lowest Y / Highest Y con ayuda de los Gizmos en la Scene View.
///
/// ══════════════════════════════════════════════════════════════
///  ¿Por qué no hereda de BasicInteraction ni implementa IInteractable?
/// ══════════════════════════════════════════════════════════════
///  El Raycast busca IInteractable con GetComponents() en el GameObject
///  exacto del collider. En FBX importados, el collider está en un hijo
///  mientras que este script debe estar en el padre. Separar los dos
///  componentes es la única forma de integrarse correctamente.
/// </summary>
public class WinchGateController : NetworkBehaviour
{
    public enum RotationAxis { X, Y, Z }

    // ─────────────────────────────────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Referencias")]
    [Tooltip("Transform de la puerta que debe subir y bajar.")]
    [SerializeField] private Transform gate;

    [Tooltip("Transform del subobjeto 'Winch_Rotation' que girará en su eje Z local." +
             " Si se deja vacío, se busca automáticamente por nombre.")]
    [SerializeField] private Transform winchRotation;

    [Header("Límites de la Puerta (World Y)")]
    [Tooltip("Posición Y mínima de la puerta (abajo / estado inicial).")]
    [SerializeField] private float lowestY = 0f;

    [Tooltip("Posición Y máxima de la puerta (arriba / completamente abierta).")]
    [SerializeField] private float highestY = 3f;

    [Header("Velocidad")]
    [Tooltip("Tiempo en segundos para recorrer la distancia TOTAL al SUBIR (lowestY → highestY)." +
             " El tiempo real es proporcional a la distancia restante.")]
    [SerializeField] private float riseTime = 4f;

    [Tooltip("Tiempo en segundos para recorrer la distancia TOTAL al BAJAR (highestY → lowestY)." +
             " Puede ser mayor que riseTime para que la bajada sea más lenta.")]
    [SerializeField] private float fallTime = 7f;

    [Tooltip("Segundos de espera en el tope antes de empezar a bajar (al soltar la rueda). 0 = baja inmediatamente.")]
    [SerializeField] private float delayBeforeFall = 0f;

    [Header("Rotación de la Rueda")]
    [Tooltip("Si es verdadero, la rueda gira desde su centro geométrico (Mesh bounds). Si es falso, gira desde su Pivot original.")]
    [SerializeField] private bool rotateFromCenter = true;

    [Tooltip("El eje local sobre el cual rotará la rueda.")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;

    [Tooltip("Velocidad de rotación del Winch_Rotation en grados/segundo sobre su eje local.")]
    [SerializeField] private float wheelRotationSpeed = 180f;

    [Header("Rotación de la Rueda – Tope")]
    [Tooltip("Grados máximos que puede girar la rueda antes de hacer tope. 0 = sin límite.")]
    [SerializeField] private float winchMaxRotationDegrees = 720f;

    [Header("Audio – Rueda")]
    [Tooltip("AudioSource del winch. Suena al empezar a interactuar (one-shot).")]
    [SerializeField] private AudioSource winchAudioSource;

    [Tooltip("Clip que suena al presionar la rueda.")]
    [SerializeField] private AudioClip winchPressClip;

    [Tooltip("Clip que suena cuando la rueda llega a su límite de rotación (tope).")]
    [SerializeField] private AudioClip winchStopClip;

    [Header("Audio – Puerta")]
    [Tooltip("AudioSource de la puerta. Loop mientras la puerta se mueve.")]
    [SerializeField] private AudioSource gateAudioSource;

    [Tooltip("Clip en loop que suena mientras la puerta sube o baja.")]
    [SerializeField] private AudioClip gateMovingClip;

    [Tooltip("Clip one-shot que suena cuando la puerta llega al límite superior.")]
    [SerializeField] private AudioClip gateArrivedClip;

    [Header("Eventos")]
    [Tooltip("Se dispara cuando el jugador comienza a mantener presionada la rueda." +
             " Conectar desde BasicInteraction.onSelect → WinchGateController.OnPressed()")]
    public UnityEvent onPressed;

    [Tooltip("Se dispara cuando el jugador suelta la rueda.")]
    public UnityEvent onUnPressed;

    // ─────────────────────────────────────────────────────────────────────────
    // ESTADO INTERNO
    // ─────────────────────────────────────────────────────────────────────────

    private bool  isBeingHeld         = false;
    private bool  isLocalHolder       = false; // Solo el cliente que presionó puede soltar
    private int   activeTweenId       = -1;
    private bool  winchAtLimit        = false;   // La rueda ya llegó a su tope de rotación
    private float rotationAccumulated = 0f;      // Grados girados desde el último press
    private bool  fallPending         = false;   // Hay un descenso con delay pendiente

    // Offset en espacio local desde el pivot hasta el centro geométrico del mesh.
    // Se calcula una vez en Start() para rotar desde el centro, no desde el pivot.
    private Vector3 winchLocalCenterOffset = Vector3.zero;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (winchRotation == null)
        {
            Transform found = FindDeepChild(transform, "Winch_Rotation");
            if (found != null)
                winchRotation = found;
            else
                Debug.LogWarning("[WinchGateController] Subobjeto 'Winch_Rotation' no encontrado." +
                                 " Asignalo manualmente en el Inspector.", this);
        }

        if (gate == null)
            Debug.LogWarning("[WinchGateController] El campo 'Gate' está vacío." +
                             " Asignalo desde el Inspector.", this);
    }

    private void Start()
    {
        if (gate != null)
        {
            Vector3 pos = gate.position;
            pos.y = lowestY;
            gate.position = pos;
        }

        if (gateAudioSource != null && gateMovingClip != null)
        {
            gateAudioSource.clip        = gateMovingClip;
            gateAudioSource.loop        = true;
            gateAudioSource.playOnAwake = false;
        }

        // Calcula el offset entre el pivot y el centro geométrico del mesh.
        // Así RotateAround() girará desde el centro visual, no desde el pivot del FBX.
        if (winchRotation != null)
        {
            Renderer rend = winchRotation.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                // bounds.center está en world space → lo convertimos a local
                winchLocalCenterOffset = winchRotation.InverseTransformPoint(rend.bounds.center);
            }
        }
    }

    private void Update()
    {
        // ── Rotación de la rueda ──────────────────────────────────────────────
        if (isBeingHeld && winchRotation != null && !winchAtLimit)
        {
            float degreesThisFrame = wheelRotationSpeed * Time.deltaTime;

            // Comprueba si este frame sobrepasaría el tope de rotación
            bool hasLimit = winchMaxRotationDegrees > 0f;
            if (hasLimit && rotationAccumulated + degreesThisFrame >= winchMaxRotationDegrees)
            {
                // Rota exactamente hasta el límite y detiene
                degreesThisFrame = winchMaxRotationDegrees - rotationAccumulated;
                winchAtLimit = true;

                if (winchAudioSource != null && winchStopClip != null)
                    winchAudioSource.PlayOneShot(winchStopClip);
            }

            if (degreesThisFrame > 0f)
            {
                Vector3 rotationPoint = rotateFromCenter 
                    ? winchRotation.TransformPoint(winchLocalCenterOffset) 
                    : winchRotation.position;

                Vector3 axis = winchRotation.up;
                if (rotationAxis == RotationAxis.X) axis = winchRotation.right;
                else if (rotationAxis == RotationAxis.Z) axis = winchRotation.forward;

                winchRotation.RotateAround(rotationPoint, axis, degreesThisFrame);
                rotationAccumulated += degreesThisFrame;
            }
        }

        // ── Detección de release ──────────────────────────────────────────────
        // Solo el cliente que inició la interacción puede soltar la rueda.
        if (!isLocalHolder) return;

        if (Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.E))
        {
            OnUnPressed();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MÉTODOS PÚBLICOS
    // Asignar en BasicInteraction.onSelect → WinchGateController.OnPressed()
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Llamar al PRESIONAR la rueda.
    /// Conectar desde: BasicInteraction (del collider) → onSelect → este método.
    /// </summary>
    public void OnPressed()
    {
        if (gate == null || isBeingHeld) return;
        
        isLocalHolder = true;

        if (Object != null && Object.IsValid)
        {
            Rpc_OnPressed();
        }
        else
        {
            DoOnPressed();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void Rpc_OnPressed()
    {
        DoOnPressed();
    }

    private void DoOnPressed()
    {
        if (gate == null || isBeingHeld) return;

        // Cancela cualquier descenso con delay pendiente
        if (fallPending)
        {
            CancelInvoke(nameof(StartFall));
            fallPending = false;
        }

        isBeingHeld = true;
        winchAtLimit = false;
        rotationAccumulated = 0f;
        onPressed?.Invoke();

        float currentY = gate.position.y;

        // Si ya llegó al tope superior, la rueda no gira más ni hay tween.
        // El audio NO arranca aquí (la puerta no se mueve todavía); arrancará
        // cuando el jugador suelte y la puerta empiece a bajar.
        if (Mathf.Approximately(currentY, highestY))
        {
            winchAtLimit = true;
            return;
        }

        CancelCurrentTween();

        float time = ProportionalTime(currentY, highestY, riseTime);
        if (time <= 0f) return;

        activeTweenId = LeanTween.moveY(gate.gameObject, highestY, time)
            .setEase(LeanTweenType.linear)
            .setOnComplete(OnReachedTop)
            .id;

        StartGateSound();
        PlayWinchSound();
    }

    /// <summary>
    /// Llamar al SOLTAR la rueda.
    /// Se llama automáticamente al detectar GetMouseButtonUp/GetKeyUp,
    /// pero también puede ser invocado manualmente desde otros sistemas.
    /// </summary>
    public void OnUnPressed()
    {
        if (gate == null || !isBeingHeld) return;
        
        isLocalHolder = false;

        if (Object != null && Object.IsValid)
        {
            Rpc_OnUnPressed();
        }
        else
        {
            DoOnUnPressed();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void Rpc_OnUnPressed()
    {
        DoOnUnPressed();
    }

    private void DoOnUnPressed()
    {
        if (gate == null || !isBeingHeld) return;

        isBeingHeld  = false;
        winchAtLimit = false;
        onUnPressed?.Invoke();

        CancelCurrentTween();

        float currentY = gate.position.y;

        if (Mathf.Approximately(currentY, lowestY))
        {
            StopGateSound();
            return;
        }

        // El audio arranca de inmediato (antes del delay de caída)
        StartGateSound();

        // Espera el delay configurado antes de iniciar la bajada
        if (delayBeforeFall > 0f)
        {
            fallPending = true;
            Invoke(nameof(StartFall), delayBeforeFall);
        }
        else
        {
            StartFall();
        }
    }

    /// <summary>Inicia el descenso de la puerta. Llamado directamente o tras el delay.</summary>
    private void StartFall()
    {
        fallPending = false;

        if (gate == null) return;

        float currentY = gate.position.y;
        float time = ProportionalTime(currentY, lowestY, fallTime);
        if (time <= 0f) { StopGateSound(); return; }

        activeTweenId = LeanTween.moveY(gate.gameObject, lowestY, time)
            .setEase(LeanTweenType.linear)
            .setOnComplete(OnReachedBottom)
            .id;

        // El audio ya debería estar sonando desde OnUnPressed; lo iniciamos
        // aquí sólo por si StartFall() se llama desde otro contexto.
        StartGateSound();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CALLBACKS INTERNOS DE LEANTWEEN
    // ─────────────────────────────────────────────────────────────────────────

    private void OnReachedTop()
    {
        activeTweenId = -1;
        StopGateSound();

        // Sonido de llegada al tope de la puerta
        if (gateAudioSource != null && gateArrivedClip != null)
            gateAudioSource.PlayOneShot(gateArrivedClip);

        // La puerta se queda arriba. Si el jugador sigue apretando,
        // no pasa nada: al soltar, OnUnPressed() iniciará la bajada normalmente.
        // No hace falta tocar isBeingHeld aquí.
        winchAtLimit = true; // La rueda no tiene sentido seguir girando
    }

    private void OnReachedBottom()
    {
        activeTweenId = -1;
        StopGateSound();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calcula el tiempo de viaje manteniendo velocidad constante,
    /// proporcional a la fracción de distancia que queda por recorrer.
    /// </summary>
    private float ProportionalTime(float fromY, float toY, float baseTravelTime)
    {
        float total = Mathf.Abs(highestY - lowestY);
        if (total <= 0f) return 0f;
        return baseTravelTime * (Mathf.Abs(toY - fromY) / total);
    }

    private void CancelCurrentTween()
    {
        if (activeTweenId != -1 && LeanTween.isTweening(activeTweenId))
            LeanTween.cancel(activeTweenId);
        activeTweenId = -1;
    }


    private void PlayWinchSound()
    {
        if (winchAudioSource != null && winchPressClip != null)
            winchAudioSource.PlayOneShot(winchPressClip);
    }

    private void StartGateSound()
    {
        if (gateAudioSource == null || gateMovingClip == null) return;

        // Forzamos siempre el clip y el loop, y arrancamos la reproducción.
        // No depende de isPlaying para evitar que el one-shot del winch
        // (u otro clip previo) bloquee el arranque del loop de la puerta.
        gateAudioSource.clip = gateMovingClip;
        gateAudioSource.loop = true;
        if (!gateAudioSource.isPlaying)
            gateAudioSource.Play();
    }

    private void StopGateSound()
    {
        if (gateAudioSource != null)
            gateAudioSource.Stop();
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindDeepChild(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GIZMOS
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (gate == null) return;
        Vector3 gp = gate.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(gp.x, lowestY, gp.z),  new Vector3(1f, 0.05f, 1f));

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(gp.x, highestY, gp.z), new Vector3(1f, 0.05f, 1f));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(gp.x, lowestY, gp.z), new Vector3(gp.x, highestY, gp.z));

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(gp, new Vector3(0.9f, 0.1f, 0.9f));
    }
}
