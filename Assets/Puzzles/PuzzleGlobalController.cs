using System;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class PuzzleGlobalController : NetworkBehaviour
{
    [SerializeField] private PuzzleSequenceData puzzleSequence;

    [SerializeField] private bool isRoom1Enabled = true;
    [SerializeField] private bool isRoom2Enabled = true;
    [SerializeField] private bool isRoom3Enabled = true;

    private PuzzleSubController subController1;
    private PuzzleSubController subController2;
    private PuzzleSubController subController3;

    [Networked] private int CurrentPhaseIndex { get; set; }
    [Networked] private NetworkBool IsPuzzleSolved { get; set; }

    private ChangeDetector changeDetector;
    private bool isEvaluating;
    private bool needsEvaluation;

    public UnityEvent OnPhaseCompleted;
    public UnityEvent OnPuzzleCompleted;

    private void Awake()
    {
        Debug.Log("[puzle]: Awake iniciado en PuzzleGlobalController.");
        var controllers = GetComponentsInChildren<PuzzleSubController>(true);
        if (controllers == null || controllers.Length == 0)
        {
            Debug.Log("[puzle]: No se encontraron PuzzleSubController en hijos, buscando en toda la escena.");
            controllers = FindObjectsByType<PuzzleSubController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        Debug.Log("[puzle]: Encontrados " + (controllers != null ? controllers.Length : 0) + " subcontroladores en la escena/hijos.");

        foreach (var ctrl in controllers)
        {
            string idx = ctrl.ControllerIndex?.ToLower().Trim();
            Debug.Log("[puzle]: Analizando subcontrolador con índice: '" + idx + "'");
            if (idx == "a" || idx == "1") subController1 = ctrl;
            else if (idx == "b" || idx == "2") subController2 = ctrl;
            else if (idx == "c" || idx == "3") subController3 = ctrl;
        }

        if (isRoom1Enabled)
        {
            if (subController1 == null) Debug.LogError("[puzle]: Sala 1 está habilitada pero no se encontró un PuzzleSubController con índice 'a' o '1'.");
            else
            {
                subController1.OnButtonStateChanged += HandleLocalButtonStateChanged;
                Debug.Log("[puzle]: Sala 1 suscripta a HandleLocalButtonStateChanged.");
            }
        }
        if (isRoom2Enabled)
        {
            if (subController2 == null) Debug.LogError("[puzle]: Sala 2 está habilitada pero no se encontró un PuzzleSubController con índice 'b' o '2'.");
            else
            {
                subController2.OnButtonStateChanged += HandleLocalButtonStateChanged;
                Debug.Log("[puzle]: Sala 2 suscripta a HandleLocalButtonStateChanged.");
            }
        }
        if (isRoom3Enabled)
        {
            if (subController3 == null) Debug.LogError("[puzle]: Sala 3 está habilitada pero no se encontró un PuzzleSubController con índice 'c' o '3'.");
            else
            {
                subController3.OnButtonStateChanged += HandleLocalButtonStateChanged;
                Debug.Log("[puzle]: Sala 3 suscripta a HandleLocalButtonStateChanged.");
            }
        }
    }

    private void OnDestroy()
    {
        Debug.Log("[puzle]: PuzzleGlobalController ha sido DESTRUIDO.");
        if (subController1 != null && isRoom1Enabled)
        {
            subController1.OnButtonStateChanged -= HandleLocalButtonStateChanged;
            Debug.Log("[puzle]: Sala 1 desuscripta de HandleLocalButtonStateChanged.");
        }
        if (subController2 != null && isRoom2Enabled)
        {
            subController2.OnButtonStateChanged -= HandleLocalButtonStateChanged;
            Debug.Log("[puzle]: Sala 2 desuscripta de HandleLocalButtonStateChanged.");
        }
        if (subController3 != null && isRoom3Enabled)
        {
            subController3.OnButtonStateChanged -= HandleLocalButtonStateChanged;
            Debug.Log("[puzle]: Sala 3 desuscripta de HandleLocalButtonStateChanged.");
        }
    }

    private void OnDisable()
    {
        Debug.Log("[puzle]: PuzzleGlobalController ha sido DESACTIVADO.");
    }

    public override void Spawned()
    {
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        Debug.Log("[puzle]: Spawned iniciado. HasStateAuthority=" + HasStateAuthority + ", CurrentPhaseIndex=" + CurrentPhaseIndex + ", IsPuzzleSolved=" + IsPuzzleSolved);

        if (HasStateAuthority)
        {
            CurrentPhaseIndex = 0;
            IsPuzzleSolved = false;
            Debug.Log("[puzle]: Seteado estado inicial en State Authority.");
        }

        LoadCurrentPhase();
    }

    private void LoadCurrentPhase()
    {
        Debug.Log("[puzle]: LoadCurrentPhase iniciado. CurrentPhaseIndex=" + CurrentPhaseIndex + ", HasStateAuthority=" + HasStateAuthority);
        if (puzzleSequence == null)
        {
            Debug.LogWarning("[puzle]: No se ha asignado el ScriptableObject Puzzle Sequence en el controlador global.");
            return;
        }
        if (puzzleSequence.Phases == null || puzzleSequence.Phases.Count == 0)
        {
            Debug.LogWarning("[puzle]: La secuencia del puzzle asignada no contiene fases configuradas.");
            return;
        }

        Debug.Log("[puzle]: Cantidad total de fases en secuencia: " + puzzleSequence.Phases.Count);
        if (CurrentPhaseIndex >= puzzleSequence.Phases.Count)
        {
            Debug.Log("[puzle]: CurrentPhaseIndex (" + CurrentPhaseIndex + ") supera o iguala la cantidad de fases. Completando puzzle.");
            if (HasStateAuthority)
            {
                Rpc_CompletePuzzle();
            }
            return;
        }

        var currentPhase = puzzleSequence.Phases[CurrentPhaseIndex];
        Debug.Log("[puzle]: Cargando fase " + CurrentPhaseIndex + ". Datos: " +
            "Sub1(Correct=" + currentPhase.sub1.correctButtonId + ", Inc1=" + currentPhase.sub1.incorrectButtonId1 + ", Inc2=" + currentPhase.sub1.incorrectButtonId2 + ") | " +
            "Sub2(Correct=" + currentPhase.sub2.correctButtonId + ", Inc1=" + currentPhase.sub2.incorrectButtonId1 + ", Inc2=" + currentPhase.sub2.incorrectButtonId2 + ") | " +
            "Sub3(Correct=" + currentPhase.sub3.correctButtonId + ", Inc1=" + currentPhase.sub3.incorrectButtonId1 + ", Inc2=" + currentPhase.sub3.incorrectButtonId2 + ")");

        if (isRoom1Enabled && subController1 != null)
        {
            subController1.SetPhaseData(currentPhase.sub1.correctButtonId, currentPhase.sub1.incorrectButtonId1, currentPhase.sub1.incorrectButtonId2);
            Debug.Log("[puzle]: Cargada fase " + CurrentPhaseIndex + " en subController1.");
        }
        if (isRoom2Enabled && subController2 != null)
        {
            subController2.SetPhaseData(currentPhase.sub2.correctButtonId, currentPhase.sub2.incorrectButtonId1, currentPhase.sub2.incorrectButtonId2);
            Debug.Log("[puzle]: Cargada fase " + CurrentPhaseIndex + " en subController2.");
        }
        if (isRoom3Enabled && subController3 != null)
        {
            subController3.SetPhaseData(currentPhase.sub3.correctButtonId, currentPhase.sub3.incorrectButtonId1, currentPhase.sub3.incorrectButtonId2);
            Debug.Log("[puzle]: Cargada fase " + CurrentPhaseIndex + " en subController3.");
        }
    }

    private void HandleLocalButtonStateChanged()
    {
        Debug.Log("[puzle]: HandleLocalButtonStateChanged invocado. HasStateAuthority=" + HasStateAuthority + ", IsPuzzleSolved=" + IsPuzzleSolved + ", isEvaluating=" + isEvaluating);
        if (!HasStateAuthority)
        {
            Debug.Log("[puzle]: Se ignoró HandleLocalButtonStateChanged porque este cliente no tiene State Authority.");
            return;
        }
        needsEvaluation = true;
    }

    private void EvaluateButtons()
    {
        Debug.Log("[puzle]: EvaluateButtons invocado. HasStateAuthority=" + HasStateAuthority + ", IsPuzzleSolved=" + IsPuzzleSolved + ", isEvaluating=" + isEvaluating);
        if (IsPuzzleSolved)
        {
            Debug.Log("[puzle]: Se ignoró EvaluateButtons porque el puzzle ya está resuelto.");
            return;
        }
        if (isEvaluating)
        {
            Debug.Log("[puzle]: Se ignoró EvaluateButtons porque ya se está evaluando.");
            return;
        }

        bool allRoomsReady = true;
        string notReadyRooms = "";

        if (isRoom1Enabled)
        {
            if (subController1 != null)
            {
                bool ready = subController1.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 1 Lista = " + ready + " (Botón: " + (subController1.CurrentPressedButton != null ? subController1.CurrentPressedButton.Id : "Ninguno") + "). Estado botones: " + subController1.GetButtonsStateDescription());
                if (!ready) { allRoomsReady = false; notReadyRooms += "Sala 1 "; }
            }
            else { allRoomsReady = false; notReadyRooms += "Sala 1 (Sin subcontrolador) "; }
        }
        if (isRoom2Enabled)
        {
            if (subController2 != null)
            {
                bool ready = subController2.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 2 Lista = " + ready + " (Botón: " + (subController2.CurrentPressedButton != null ? subController2.CurrentPressedButton.Id : "Ninguno") + "). Estado botones: " + subController2.GetButtonsStateDescription());
                if (!ready) { allRoomsReady = false; notReadyRooms += "Sala 2 "; }
            }
            else { allRoomsReady = false; notReadyRooms += "Sala 2 (Sin subcontrolador) "; }
        }
        if (isRoom3Enabled)
        {
            if (subController3 != null)
            {
                bool ready = subController3.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 3 Lista = " + ready + " (Botón: " + (subController3.CurrentPressedButton != null ? subController3.CurrentPressedButton.Id : "Ninguno") + "). Estado botones: " + subController3.GetButtonsStateDescription());
                if (!ready) { allRoomsReady = false; notReadyRooms += "Sala 3 "; }
            }
            else { allRoomsReady = false; notReadyRooms += "Sala 3 (Sin subcontrolador) "; }
        }

        if (!allRoomsReady)
        {
            Debug.Log("[puzle]: Evaluación cancelada. Salas no listas: " + notReadyRooms);
            return;
        }

        bool allCorrect = true;
        if (isRoom1Enabled && (subController1 == null || !subController1.IsCorrectButtonPressed)) allCorrect = false;
        if (isRoom2Enabled && (subController2 == null || !subController2.IsCorrectButtonPressed)) allCorrect = false;
        if (isRoom3Enabled && (subController3 == null || !subController3.IsCorrectButtonPressed)) allCorrect = false;

        Debug.Log("[puzle]: Evaluación - Todas las salas listas. Correctas = " + allCorrect);

        isEvaluating = true;
        try
        {
            if (allCorrect)
            {
                string r1Correct = isRoom1Enabled && subController1 != null ? subController1.CurrentPressedButton?.Id ?? "" : "";
                string r2Correct = isRoom2Enabled && subController2 != null ? subController2.CurrentPressedButton?.Id ?? "" : "";
                string r3Correct = isRoom3Enabled && subController3 != null ? subController3.CurrentPressedButton?.Id ?? "" : "";

                string r1Inc1 = isRoom1Enabled && subController1 != null ? subController1.IncorrectId1 : "";
                string r1Inc2 = isRoom1Enabled && subController1 != null ? subController1.IncorrectId2 : "";
                string r2Inc1 = isRoom2Enabled && subController2 != null ? subController2.IncorrectId1 : "";
                string r2Inc2 = isRoom2Enabled && subController2 != null ? subController2.IncorrectId2 : "";
                string r3Inc1 = isRoom3Enabled && subController3 != null ? subController3.IncorrectId1 : "";
                string r3Inc2 = isRoom3Enabled && subController3 != null ? subController3.IncorrectId2 : "";

                Debug.Log("[puzle]: Todo correcto. Emitiendo Rpc_BroadcastSuccess y cargando siguiente fase.");
                Rpc_BroadcastSuccess(r1Correct, r1Inc1, r1Inc2, r2Correct, r2Inc1, r2Inc2, r3Correct, r3Inc1, r3Inc2);

                CurrentPhaseIndex++;
                Debug.Log("[puzle]: Incrementando CurrentPhaseIndex a " + CurrentPhaseIndex);
                LoadCurrentPhase();
            }
            else
            {
                string r1Pressed = isRoom1Enabled && subController1 != null ? subController1.CurrentPressedButton?.Id ?? "" : "";
                bool r1IsCorrect = isRoom1Enabled && subController1 != null && subController1.IsCorrectButtonPressed;

                string r2Pressed = isRoom2Enabled && subController2 != null ? subController2.CurrentPressedButton?.Id ?? "" : "";
                bool r2IsCorrect = isRoom2Enabled && subController2 != null && subController2.IsCorrectButtonPressed;

                string r3Pressed = isRoom3Enabled && subController3 != null ? subController3.CurrentPressedButton?.Id ?? "" : "";
                bool r3IsCorrect = isRoom3Enabled && subController3 != null && subController3.IsCorrectButtonPressed;

                Debug.Log("[puzle]: Algún botón es incorrecto. Emitiendo Rpc_BroadcastIncorrect.");
                Rpc_BroadcastIncorrect(r1Pressed, r1IsCorrect, r2Pressed, r2IsCorrect, r3Pressed, r3IsCorrect);
            }
        }
        finally
        {
            isEvaluating = false;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_BroadcastSuccess(
        string r1CorrectId, string r1Inc1, string r1Inc2,
        string r2CorrectId, string r2Inc1, string r2Inc2,
        string r3CorrectId, string r3Inc1, string r3Inc2)
    {
        Debug.Log("[puzle]: Rpc_BroadcastSuccess recibido. Parámetros: r1Correct=" + r1CorrectId + ", r2Correct=" + r2CorrectId + ", r3Correct=" + r3CorrectId);
        if (isRoom1Enabled && subController1 != null) subController1.TriggerPhaseButtonsOnSuccess(r1CorrectId, r1Inc1, r1Inc2);
        if (isRoom2Enabled && subController2 != null) subController2.TriggerPhaseButtonsOnSuccess(r2CorrectId, r2Inc1, r2Inc2);
        if (isRoom3Enabled && subController3 != null) subController3.TriggerPhaseButtonsOnSuccess(r3CorrectId, r3Inc1, r3Inc2);

        Debug.Log("[puzle]: OnPhaseCompleted invocado para fase " + CurrentPhaseIndex);
        OnPhaseCompleted?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_BroadcastIncorrect(
        string r1PressedId, bool r1IsCorrect,
        string r2PressedId, bool r2IsCorrect,
        string r3PressedId, bool r3IsCorrect)
    {
        Debug.Log("[puzle]: Rpc_BroadcastIncorrect recibido. Parámetros: r1Pressed=" + r1PressedId + "(IsCorrect=" + r1IsCorrect + "), r2Pressed=" + r2PressedId + "(IsCorrect=" + r2IsCorrect + "), r3Pressed=" + r3PressedId + "(IsCorrect=" + r3IsCorrect + ")");
        if (isRoom1Enabled && subController1 != null) subController1.TriggerButtonResult(r1PressedId, r1IsCorrect);
        if (isRoom2Enabled && subController2 != null) subController2.TriggerButtonResult(r2PressedId, r2IsCorrect);
        if (isRoom3Enabled && subController3 != null) subController3.TriggerButtonResult(r3PressedId, r3IsCorrect);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_CompletePuzzle()
    {
        Debug.Log("[puzle]: Rpc_CompletePuzzle recibido.");
        IsPuzzleSolved = true;
        Debug.Log("[puzle]: OnPuzzleCompleted invocado.");
        OnPuzzleCompleted?.Invoke();
    }

    public override void Render()
    {
        foreach (var change in changeDetector.DetectChanges(this))
        {
            if (change == nameof(CurrentPhaseIndex) && !HasStateAuthority)
            {
                Debug.Log("[puzle]: Detectado cambio de red en CurrentPhaseIndex a " + CurrentPhaseIndex);
                if (CurrentPhaseIndex < (puzzleSequence?.Phases?.Count ?? 0))
                {
                    var phase = puzzleSequence.Phases[CurrentPhaseIndex];
                    Debug.Log("[puzle]: Actualizando fase localmente en cliente a " + CurrentPhaseIndex + " con datos: " +
                        "Sub1(" + phase.sub1.correctButtonId + ") | " +
                        "Sub2(" + phase.sub2.correctButtonId + ") | " +
                        "Sub3(" + phase.sub3.correctButtonId + ")");
                    if (isRoom1Enabled && subController1 != null) subController1.SetPhaseData(phase.sub1.correctButtonId, phase.sub1.incorrectButtonId1, phase.sub1.incorrectButtonId2);
                    if (isRoom2Enabled && subController2 != null) subController2.SetPhaseData(phase.sub2.correctButtonId, phase.sub2.incorrectButtonId1, phase.sub2.incorrectButtonId2);
                    if (isRoom3Enabled && subController3 != null) subController3.SetPhaseData(phase.sub3.correctButtonId, phase.sub3.incorrectButtonId1, phase.sub3.incorrectButtonId2);
                }
                else
                {
                    Debug.Log("[puzle]: CurrentPhaseIndex es igual o mayor a la cantidad de fases en Render.");
                }
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            LogDetailedDebugState("ACA (P Key)");
            if (Runner != null && Runner.IsRunning)
            {
                Rpc_RequestServerDebugLog(Runner.LocalPlayer);
                Rpc_RequestServerEvaluation();
            }
        }

        if (HasStateAuthority && needsEvaluation && !isEvaluating)
        {
            needsEvaluation = false;
            EvaluateButtons();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestServerDebugLog(PlayerRef fromPlayer)
    {
        LogDetailedDebugState("ACA (RPC requested by Player " + fromPlayer + ")");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestServerEvaluation()
    {
        Debug.Log("[puzle]: Rpc_RequestServerEvaluation recibido. Encolando evaluación en el servidor.");
        needsEvaluation = true;
    }

    /// <summary>
    /// Llamado por PuzzleResetTrigger cuando un jugador toca el collider de reset de una sala.
    /// Solo funciona si este cliente tiene StateAuthority.
    /// </summary>
    public void RequestResetRoom(string roomIndex)
    {
        if (!HasStateAuthority)
        {
            Debug.Log("[puzle]: RequestResetRoom ignorado, este cliente no tiene StateAuthority.");
            return;
        }
        Debug.Log("[puzle]: RequestResetRoom para sala '" + roomIndex + "'. Emitiendo Rpc_ResetRoom.");
        Rpc_ResetRoom(roomIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ResetRoom(string roomIndex)
    {
        Debug.Log("[puzle]: Rpc_ResetRoom recibido para sala '" + roomIndex + "'.");

        if (puzzleSequence == null || puzzleSequence.Phases == null || CurrentPhaseIndex >= puzzleSequence.Phases.Count)
        {
            Debug.LogWarning("[puzle]: Rpc_ResetRoom: no hay datos de fase disponibles para CurrentPhaseIndex=" + CurrentPhaseIndex);
            return;
        }

        var currentPhase = puzzleSequence.Phases[CurrentPhaseIndex];
        string idx = roomIndex?.ToLower().Trim();

        if ((idx == "a" || idx == "1") && isRoom1Enabled && subController1 != null)
            subController1.SetPhaseData(currentPhase.sub1.correctButtonId, currentPhase.sub1.incorrectButtonId1, currentPhase.sub1.incorrectButtonId2);
        else if ((idx == "b" || idx == "2") && isRoom2Enabled && subController2 != null)
            subController2.SetPhaseData(currentPhase.sub2.correctButtonId, currentPhase.sub2.incorrectButtonId1, currentPhase.sub2.incorrectButtonId2);
        else if ((idx == "c" || idx == "3") && isRoom3Enabled && subController3 != null)
            subController3.SetPhaseData(currentPhase.sub3.correctButtonId, currentPhase.sub3.incorrectButtonId1, currentPhase.sub3.incorrectButtonId2);
        else
            Debug.LogWarning("[puzle]: Rpc_ResetRoom: índice de sala desconocido o deshabilitado: '" + roomIndex + "'.");
    }

    private void LogDetailedDebugState(string label)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[puzle]: ================== {label} ==================");
        sb.AppendLine($"Time: {Time.time}s | Frame: {Time.frameCount}");
        sb.AppendLine($"HasStateAuthority: {HasStateAuthority}");
        if (Runner != null)
        {
            sb.AppendLine($"Runner: Active | IsServer: {Runner.IsServer} | IsClient: {Runner.IsClient} | LocalPlayer: {Runner.LocalPlayer}");
        }
        else
        {
            sb.AppendLine("Runner: null");
        }
        sb.AppendLine($"CurrentPhaseIndex: {CurrentPhaseIndex}");
        sb.AppendLine($"IsPuzzleSolved: {IsPuzzleSolved}");
        sb.AppendLine($"isEvaluating: {isEvaluating}");
        
        if (puzzleSequence != null)
        {
            sb.AppendLine($"PuzzleSequence Phases Count: {puzzleSequence.Phases?.Count ?? 0}");
        }
        else
        {
            sb.AppendLine("PuzzleSequence: null");
        }

        AppendSubControllerState(sb, 1, isRoom1Enabled, subController1);
        AppendSubControllerState(sb, 2, isRoom2Enabled, subController2);
        AppendSubControllerState(sb, 3, isRoom3Enabled, subController3);

        sb.AppendLine("[puzle]: ==========================================");
        Debug.Log(sb.ToString());
    }

    private void AppendSubControllerState(System.Text.StringBuilder sb, int roomNum, bool enabled, PuzzleSubController sub)
    {
        sb.AppendLine($"Room {roomNum} (Enabled: {enabled}):");
        if (sub == null)
        {
            sb.AppendLine("  SubController: null");
            return;
        }
        sb.AppendLine($"  ControllerIndex: {sub.ControllerIndex}");
        sb.AppendLine($"  Correct ID: {sub.CorrectId}");
        sb.AppendLine($"  Incorrect ID 1: {sub.IncorrectId1}");
        sb.AppendLine($"  Incorrect ID 2: {sub.IncorrectId2}");
        sb.AppendLine($"  IsPlayerOnPhaseButton: {sub.IsPlayerOnPhaseButton()}");
        sb.AppendLine($"  IsCorrectButtonPressed: {sub.IsCorrectButtonPressed}");
        sb.AppendLine($"  CurrentPressedButton: {(sub.CurrentPressedButton != null ? sub.CurrentPressedButton.Id : "None")}");
        sb.AppendLine($"  Buttons Description: {sub.GetButtonsStateDescription()}");
    }
}
