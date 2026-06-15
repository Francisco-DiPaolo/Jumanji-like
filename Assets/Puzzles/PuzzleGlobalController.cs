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

    public UnityEvent OnPhaseCompleted;
    public UnityEvent OnPuzzleCompleted;

    private void Awake()
    {
        var controllers = GetComponentsInChildren<PuzzleSubController>(true);
        if (controllers == null || controllers.Length == 0)
        {
            controllers = FindObjectsByType<PuzzleSubController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        foreach (var ctrl in controllers)
        {
            string idx = ctrl.ControllerIndex?.ToLower().Trim();
            if (idx == "a" || idx == "1") subController1 = ctrl;
            else if (idx == "b" || idx == "2") subController2 = ctrl;
            else if (idx == "c" || idx == "3") subController3 = ctrl;
        }

        if (isRoom1Enabled)
        {
            if (subController1 == null) Debug.LogError("[puzle]: Sala 1 está habilitada pero no se encontró un PuzzleSubController con índice 'a' o '1'.");
            else subController1.OnButtonStateChanged += HandleLocalButtonStateChanged;
        }
        if (isRoom2Enabled)
        {
            if (subController2 == null) Debug.LogError("[puzle]: Sala 2 está habilitada pero no se encontró un PuzzleSubController con índice 'b' o '2'.");
            else subController2.OnButtonStateChanged += HandleLocalButtonStateChanged;
        }
        if (isRoom3Enabled)
        {
            if (subController3 == null) Debug.LogError("[puzle]: Sala 3 está habilitada pero no se encontró un PuzzleSubController con índice 'c' o '3'.");
            else subController3.OnButtonStateChanged += HandleLocalButtonStateChanged;
        }
    }

    private void OnDestroy()
    {
        Debug.Log("[puzle]: PuzzleGlobalController ha sido DESTRUIDO.");
        if (subController1 != null && isRoom1Enabled) subController1.OnButtonStateChanged -= HandleLocalButtonStateChanged;
        if (subController2 != null && isRoom2Enabled) subController2.OnButtonStateChanged -= HandleLocalButtonStateChanged;
        if (subController3 != null && isRoom3Enabled) subController3.OnButtonStateChanged -= HandleLocalButtonStateChanged;
    }

    private void OnDisable()
    {
        Debug.Log("[puzle]: PuzzleGlobalController ha sido DESACTIVADO.");
    }

    public override void Spawned()
    {
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            CurrentPhaseIndex = 0;
            IsPuzzleSolved = false;
            LoadCurrentPhase();
        }
    }

    private void LoadCurrentPhase()
    {
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

        if (CurrentPhaseIndex >= puzzleSequence.Phases.Count)
        {
            Rpc_CompletePuzzle();
            return;
        }

        var currentPhase = puzzleSequence.Phases[CurrentPhaseIndex];
        if (isRoom1Enabled && subController1 != null) subController1.SetPhaseData(currentPhase.sub1.correctButtonId, currentPhase.sub1.incorrectButtonId1, currentPhase.sub1.incorrectButtonId2);
        if (isRoom2Enabled && subController2 != null) subController2.SetPhaseData(currentPhase.sub2.correctButtonId, currentPhase.sub2.incorrectButtonId1, currentPhase.sub2.incorrectButtonId2);
        if (isRoom3Enabled && subController3 != null) subController3.SetPhaseData(currentPhase.sub3.correctButtonId, currentPhase.sub3.incorrectButtonId1, currentPhase.sub3.incorrectButtonId2);
    }

    private void HandleLocalButtonStateChanged()
    {
        if (!HasStateAuthority) return;
        if (IsPuzzleSolved) return;

        bool allRoomsReady = true;
        string notReadyRooms = "";

        if (isRoom1Enabled)
        {
            if (subController1 != null)
            {
                bool ready = subController1.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 1 Lista = " + ready + " (Botón: " + (subController1.CurrentPressedButton != null ? subController1.CurrentPressedButton.Id : "Ninguno") + ")");
                if (!ready) { allRoomsReady = false; notReadyRooms += "Sala 1 "; }
            }
            else { allRoomsReady = false; notReadyRooms += "Sala 1 (Sin subcontrolador) "; }
        }
        if (isRoom2Enabled)
        {
            if (subController2 != null)
            {
                bool ready = subController2.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 2 Lista = " + ready + " (Botón: " + (subController2.CurrentPressedButton != null ? subController2.CurrentPressedButton.Id : "Ninguno") + ")");
                if (!ready) { allRoomsReady = false; notReadyRooms += "Sala 2 "; }
            }
            else { allRoomsReady = false; notReadyRooms += "Sala 2 (Sin subcontrolador) "; }
        }
        if (isRoom3Enabled)
        {
            if (subController3 != null)
            {
                bool ready = subController3.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 3 Lista = " + ready + " (Botón: " + (subController3.CurrentPressedButton != null ? subController3.CurrentPressedButton.Id : "Ninguno") + ")");
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

            Rpc_BroadcastSuccess(r1Correct, r1Inc1, r1Inc2, r2Correct, r2Inc1, r2Inc2, r3Correct, r3Inc1, r3Inc2);

            CurrentPhaseIndex++;
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

            Rpc_BroadcastIncorrect(r1Pressed, r1IsCorrect, r2Pressed, r2IsCorrect, r3Pressed, r3IsCorrect);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_BroadcastSuccess(
        string r1CorrectId, string r1Inc1, string r1Inc2,
        string r2CorrectId, string r2Inc1, string r2Inc2,
        string r3CorrectId, string r3Inc1, string r3Inc2)
    {
        Debug.Log("[puzle]: Rpc_BroadcastSuccess recibido.");
        if (isRoom1Enabled && subController1 != null) subController1.TriggerPhaseButtonsOnSuccess(r1CorrectId, r1Inc1, r1Inc2);
        if (isRoom2Enabled && subController2 != null) subController2.TriggerPhaseButtonsOnSuccess(r2CorrectId, r2Inc1, r2Inc2);
        if (isRoom3Enabled && subController3 != null) subController3.TriggerPhaseButtonsOnSuccess(r3CorrectId, r3Inc1, r3Inc2);

        Debug.Log("[puzle]: OnPhaseCompleted para fase " + (CurrentPhaseIndex - 1));
        OnPhaseCompleted?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_BroadcastIncorrect(
        string r1PressedId, bool r1IsCorrect,
        string r2PressedId, bool r2IsCorrect,
        string r3PressedId, bool r3IsCorrect)
    {
        Debug.Log("[puzle]: Rpc_BroadcastIncorrect recibido.");
        if (isRoom1Enabled && subController1 != null) subController1.TriggerButtonResult(r1PressedId, r1IsCorrect);
        if (isRoom2Enabled && subController2 != null) subController2.TriggerButtonResult(r2PressedId, r2IsCorrect);
        if (isRoom3Enabled && subController3 != null) subController3.TriggerButtonResult(r3PressedId, r3IsCorrect);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_CompletePuzzle()
    {
        IsPuzzleSolved = true;
        Debug.Log("[puzle]: OnPuzzleCompleted");
        OnPuzzleCompleted?.Invoke();
    }

    public override void Render()
    {
        foreach (var change in changeDetector.DetectChanges(this))
        {
            if (change == nameof(CurrentPhaseIndex) && !HasStateAuthority)
            {
                Debug.Log("[puzle]: Fase de red cambió a " + CurrentPhaseIndex);
                if (CurrentPhaseIndex < (puzzleSequence?.Phases?.Count ?? 0))
                {
                    var phase = puzzleSequence.Phases[CurrentPhaseIndex];
                    if (isRoom1Enabled && subController1 != null) subController1.SetPhaseData(phase.sub1.correctButtonId, phase.sub1.incorrectButtonId1, phase.sub1.incorrectButtonId2);
                    if (isRoom2Enabled && subController2 != null) subController2.SetPhaseData(phase.sub2.correctButtonId, phase.sub2.incorrectButtonId1, phase.sub2.incorrectButtonId2);
                    if (isRoom3Enabled && subController3 != null) subController3.SetPhaseData(phase.sub3.correctButtonId, phase.sub3.incorrectButtonId1, phase.sub3.incorrectButtonId2);
                }
            }
        }
    }
}
