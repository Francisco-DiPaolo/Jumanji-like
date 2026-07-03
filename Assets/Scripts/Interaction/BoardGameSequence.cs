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

    // -----------------------------------------------------------------------
    // Estado networked
    // -----------------------------------------------------------------------
    [Networked] public BoardSequenceState State    { get; set; }
    [Networked] public int               ReadyCount { get; set; }

    private BoardSequenceState _lastState = BoardSequenceState.Idle;
    private bool _hostCoroutineRunning = false;
    private bool _alreadyStartedLocal = false; // Evita doble-interacción local

    public override void Spawned()
    {
        _lastState = State;
        
        // Si entramos tarde y la secuencia ya había avanzado, sincronizamos visualmente
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
        StartCoroutine(ShowWaitingAndNotifyReady());
    }

    private IEnumerator ShowWaitingAndNotifyReady()
    {
        yield return new WaitForSeconds(initialDelay);

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
    }

    private IEnumerator CameraReturnRoutine()
    {
        if (cameraOverride != null) cameraOverride.DeactivateView();
        yield return null;
    }

    private IEnumerator TextDisplayRoutine()
    {
        if (canvasNarrator != null) canvasNarrator.SetActive(true);
        if (uiText != null) yield return StartCoroutine(TypeTextRoutine(textMessage, typewriterSpeed));
    }

    private IEnumerator DoneRoutine()
    {
        if (door != null) door.OpenDoor();
        yield return new WaitForSeconds(finalDelayAfterCameraReturns);
        if (canvasNarrator != null) canvasNarrator.SetActive(false);
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
