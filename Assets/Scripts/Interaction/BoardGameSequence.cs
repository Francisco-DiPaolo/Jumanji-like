using System.Collections;
using Fusion;
using UnityEngine;
using TMPro;

public enum BoardSequenceState
{
    Idle,
    WaitingForPlayers,
    DiceRoll,
    CameraReturn,
    TextDisplay,
    Done
}

/// <summary>
/// Maneja tanto la red como los efectos locales de la secuencia del tablero.
/// Ahora este script es un NetworkBehaviour. El GameObject donde está (el tablero)
/// DEBE tener un componente NetworkObject para que Fusion lo reconozca.
/// </summary>
public class BoardGameSequence : NetworkBehaviour
{
    [Header("Sequence Timing")]
    [SerializeField] private float initialDelay             = 1.0f;
    [SerializeField] private float diceJumpDuration         = 1.0f;
    [SerializeField] private float textDisplayDuration      = 3.0f;
    [SerializeField] private float finalDelayAfterCameraReturns = 2.0f;
    [SerializeField] private float typewriterSpeed          = 0.05f;
    [SerializeField] private float fastTypewriterSpeed      = 0.02f;

    [Header("Dice Physics")]
    public float diceJumpForce  = 3.0f;
    public float diceTorqueForce = 2.0f;

    [Header("Scene References")]
    [SerializeField] private Transform[]        dices;
    [SerializeField] private GameObject         canvasNarrator;
    [SerializeField] private TextMeshProUGUI    uiText;
    [SerializeField] private LeanTweenDoor      door;
    [SerializeField] private BoardCameraOverride cameraOverride;

    [Header("Text Message & Sound")]
    [TextArea] [SerializeField] private string  textMessage;
    [SerializeField] private AudioSource        audioSource;
    [SerializeField] private AudioClip          textAppearSound;
    [SerializeField] private AudioClip          diceRollSound;
    [Tooltip("Tiempo de espera desde que saltan hasta que se reproduce el sonido (para coincidir con la ca\u00edda)")]
    [SerializeField] private float              diceSoundDelay = 0.5f;

    [Header("Condena Message")]
    [SerializeField] private TextMeshProUGUI uiCondenaText;
    [Tooltip("Color del nombre del jugador en el mensaje de condena.")]
    [SerializeField] private Color           nameColor = new Color(1f, 0.843f, 0f); // Amarillo dorado por defecto
    [Tooltip("Mensaje de condena. Usa {nombre} como placeholder del jugador con el casco.\nEj: {nombre}: Tu condena es el océano, respirá bajo el agua.")]
    [TextArea] [SerializeField] private string condenaTemplate = "{nombre}: Tu condena es el océano, respirá bajo el agua.";

    [Header("Visual Effects & Particles")]
    [Tooltip("Objetos que se activarán al empezar el texto (ej: sistemas de partículas).")]
    [SerializeField] private GameObject[] objectsToActivate;
    [Tooltip("Objetos que se desactivarán al empezar el texto. NOTA: No desactives el objeto que tiene este script.")]
    [SerializeField] private GameObject[] objectsToDeactivate;
    [Tooltip("Mallas/Renderers del tablero que se ocultarán sin desactivar el GameObject completo.")]
    [SerializeField] private Renderer[] boardRenderersToHide;
    [Tooltip("Colliders del tablero que se desactivarán para evitar interacciones accidentales una vez oculto.")]
    [SerializeField] private Collider[] boardCollidersToDisable;

    // -----------------------------------------------------------------------
    // Estado networked
    // -----------------------------------------------------------------------
    [Networked] public BoardSequenceState State    { get; set; }
    [Networked] public int               ReadyCount { get; set; }
    /// <summary>Nombre del jugador que interactu\u00f3 primero con el tablero (el que lleva el casco).</summary>
    [Networked] public NetworkString<_32> HelmetPlayerName { get; set; }

    private BoardSequenceState _lastState = BoardSequenceState.Idle;
    private bool _hostCoroutineRunning = false;
    private bool _alreadyStartedLocal = false;
    private GameObject _lifeCanvas; // StatusBar del jugador local, encontrado en runtime

    public override void Spawned()
    {
        _lastState = State;
        
        // Si la secuencia ya terminó (ej: late join o reload), abrir la puerta al instante sin animación
        if (State == BoardSequenceState.Done && door != null)
        {
            door.OpenDoorInstant();
        }
        
        // Sincronizar el resto de efectos visuales
        if (State != BoardSequenceState.Idle)
        {
            HandleStateChanged(State);
        }
    }

    public override void Render()
    {
        if (State != _lastState)
        {
            _lastState = State;
            HandleStateChanged(State);
        }
    }

    // -----------------------------------------------------------------------
    // Punto de entrada: llamado desde el Raycast local del jugador
    // -----------------------------------------------------------------------
    public void StartSequence()
    {
        if (_alreadyStartedLocal) return;

        // Solo permitir interactuar si está Idle o Esperando (evita romper secuencias a medias)
        if (State == BoardSequenceState.Done ||
            State == BoardSequenceState.DiceRoll ||
            State == BoardSequenceState.CameraReturn ||
            State == BoardSequenceState.TextDisplay)
            return;

        _alreadyStartedLocal = true;
        
        // --- RESTAURANDO LÓGICA DEL CASCO ---
        // Si nadie tiene el casco todavía, este jugador (el primero en llegar) se lo pone
        bool anyoneHasHelmet = false;
        PlayerMovement localPlayer = null;
        
        foreach (var p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (p.HasFishBowl) anyoneHasHelmet = true;
            if (p.HasInputAuthority) localPlayer = p;
        }

        if (!anyoneHasHelmet && localPlayer != null)
        {
            localPlayer.Rpc_SetHasFishBowl(true);
            
            // Guardar el nombre del jugador en red para que todos puedan leerlo al mostrar el texto
            string nickname = SessionLauncher.LocalNickname;
            if (string.IsNullOrEmpty(nickname)) nickname = localPlayer.gameObject.name;
            Rpc_SetHelmetPlayerName(nickname);

            // Aplicar el efecto de voz directamente al jugador local
            VoiceEffectController voiceEffect = localPlayer.GetComponent<VoiceEffectController>();
            if (voiceEffect != null)
                voiceEffect.RPC_SetVoiceMode(VoiceEffectController.VoiceMode.Underwater);
        }
        // ------------------------------------

        StartCoroutine(ShowWaitingAndNotifyReady());
    }

    private IEnumerator ShowWaitingAndNotifyReady()
    {
        yield return new WaitForSeconds(initialDelay);

        // Buscar el StatusBar del jugador local en runtime (es hijo del prefab del jugador)
        if (_lifeCanvas == null)
        {
            foreach (var p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                if (!p.HasInputAuthority) continue;
                Transform statusBar = p.transform.Find("StatusBar");
                // Buscar recursivo por si está más adentro en la jerarquía
                if (statusBar == null) statusBar = FindInChildren(p.transform, "StatusBar");
                if (statusBar != null) _lifeCanvas = statusBar.gameObject;
                break;
            }
        }

        // Ocultar el canvas de vida mientras dure la secuencia
        if (_lifeCanvas != null) _lifeCanvas.SetActive(false);

        if (canvasNarrator != null)
            canvasNarrator.SetActive(true);

        if (uiText != null)
            yield return StartCoroutine(TypeTextRoutine("Esperando a que todos los integrantes esten en la mesa...", fastTypewriterSpeed));

        Rpc_PlayerReady();
    }

    // -----------------------------------------------------------------------
    // RPC para la StateMachine en el Host
    // -----------------------------------------------------------------------
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_SetHelmetPlayerName(string playerName)
    {
        HelmetPlayerName = playerName;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_PlayerReady()
    {
        if (State == BoardSequenceState.Done || _hostCoroutineRunning) return;

        if (State == BoardSequenceState.Idle)
        {
            State = BoardSequenceState.WaitingForPlayers;
            ReadyCount = 0;
        }

        if (State != BoardSequenceState.WaitingForPlayers) return;

        ReadyCount++;

        PlayerMovement[] allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        int totalPlayers = allPlayers.Length;

        if (ReadyCount >= totalPlayers && totalPlayers > 0)
        {
            _hostCoroutineRunning = true;
            StartCoroutine(HostSequenceCoroutine());
        }
    }

    private IEnumerator HostSequenceCoroutine()
    {
        yield return new WaitForSeconds(1.0f);

        State = BoardSequenceState.DiceRoll;
        yield return new WaitForSeconds(diceJumpDuration + 1.0f);

        State = BoardSequenceState.CameraReturn;
        yield return new WaitForSeconds(1.0f);

        State = BoardSequenceState.TextDisplay;
        yield return new WaitForSeconds(textDisplayDuration);

        State = BoardSequenceState.Done;

        yield return new WaitForSeconds(finalDelayAfterCameraReturns);
        _hostCoroutineRunning = false;
    }

    // -----------------------------------------------------------------------
    // Efectos visuales locales
    // -----------------------------------------------------------------------
    private void HandleStateChanged(BoardSequenceState newState)
    {
        switch (newState)
        {
            case BoardSequenceState.DiceRoll:
                StartCoroutine(DiceRollRoutine());
                break;
            case BoardSequenceState.CameraReturn:
                StartCoroutine(CameraReturnRoutine());
                break;
            case BoardSequenceState.TextDisplay:
                StartCoroutine(TextDisplayRoutine());
                break;
            case BoardSequenceState.Done:
                StartCoroutine(DoneRoutine());
                break;
        }
    }

    private IEnumerator DiceRollRoutine()
    {
        if (uiText != null)    uiText.text = "";
        if (canvasNarrator != null) canvasNarrator.SetActive(false);

        yield return new WaitForSeconds(0.1f);

        if (dices != null)
        {
            foreach (Transform dice in dices)
            {
                if (dice == null) continue;
                Rigidbody rb = dice.GetComponent<Rigidbody>();
                if (rb == null) continue;

                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                rb.AddForce(Vector3.up * diceJumpForce, ForceMode.Impulse);
                Vector3 randomTorque = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f)).normalized;
                rb.AddTorque(randomTorque * diceTorqueForce, ForceMode.Impulse);
            }
        }
        
        // Reproducir sonido de los dados cayendo
        if (audioSource != null && diceRollSound != null)
        {
            // Esperamos el tiempo necesario para que caigan (aprox 0.5s por física)
            yield return new WaitForSeconds(diceSoundDelay);
            audioSource.PlayOneShot(diceRollSound);
            
            // Si esperamos, restamos ese tiempo de la duración total para no desfasar la secuencia
            yield return new WaitForSeconds(Mathf.Max(0, diceJumpDuration + 1.0f - diceSoundDelay - 0.1f));
        }
        else
        {
            yield return new WaitForSeconds(diceJumpDuration + 1.0f - 0.1f);
        }
    }

    private IEnumerator CameraReturnRoutine()
    {
        if (cameraOverride != null) cameraOverride.DeactivateView();
        yield return null;
    }

    private IEnumerator TextDisplayRoutine()
    {
        // Activar partículas y desactivar el tablero visual (ahora soporta varios GameObjects)
        if (objectsToActivate != null)
        {
            foreach (var obj in objectsToActivate)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        if (objectsToDeactivate != null)
        {
            foreach (var obj in objectsToDeactivate)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        // Desactivar mallas visuales si se asignaron directamente
        if (boardRenderersToHide != null)
        {
            foreach (var r in boardRenderersToHide)
            {
                if (r != null) r.enabled = false;
            }
        }

        // Desactivar colliders
        if (boardCollidersToDisable != null)
        {
            foreach (var col in boardCollidersToDisable)
            {
                if (col != null) col.enabled = false;
            }
        }

        if (canvasNarrator != null) canvasNarrator.SetActive(true);

        // Ocultar el texto de condena al inicio por si estaba visible de antes
        if (uiCondenaText != null) uiCondenaText.gameObject.SetActive(false);

        // 1. Primero el typewriter del mensaje principal
        if (uiText != null) yield return StartCoroutine(TypeTextRoutine(textMessage, typewriterSpeed));

        // 2. Pequena pausa dramatica
        yield return new WaitForSeconds(0.8f);

        // 3. Luego aparece el texto de condena con el nombre del jugador
        if (uiCondenaText != null)
        {
            string playerName = HelmetPlayerName.ToString();
            if (string.IsNullOrEmpty(playerName)) playerName = "???";
            // Convertir el color del inspector a hex para el tag de TMP rich text
            string hex = ColorUtility.ToHtmlStringRGB(nameColor);
            string coloredName = $"<color=#{hex}>{playerName}</color>";
            uiCondenaText.text = condenaTemplate.Replace("{nombre}", coloredName);
            uiCondenaText.gameObject.SetActive(true);
        }
    }

    private IEnumerator DoneRoutine()
    {
        if (door != null) door.OpenDoor();
        yield return new WaitForSeconds(finalDelayAfterCameraReturns);
        if (canvasNarrator != null) canvasNarrator.SetActive(false);
        // Restaurar el StatusBar del jugador local
        if (_lifeCanvas != null) _lifeCanvas.SetActive(true);
    }

    /// <summary>Búsqueda recursiva de un Transform por nombre en la jerarquía.</summary>
    private Transform FindInChildren(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindInChildren(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private IEnumerator TypeTextRoutine(string text, float speed)
    {
        if (uiText == null) yield break;
        uiText.text = "";

        if (audioSource != null && textAppearSound != null)
        {
            audioSource.clip  = textAppearSound;
            audioSource.loop  = true;
            audioSource.Play();
        }

        foreach (char letter in text.ToCharArray())
        {
            uiText.text += letter;
            yield return new WaitForSeconds(speed);
        }

        if (audioSource != null) audioSource.Stop();
    }
}
