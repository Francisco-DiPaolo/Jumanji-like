using System;
using UnityEngine;
using UnityEngine.Events;

public class PuzzleGlobalController : MonoBehaviour
{
    [SerializeField] private PuzzleSequenceData puzzleSequence;

    [SerializeField] private bool isRoom1Enabled = true;
    [SerializeField] private bool isRoom2Enabled = true;
    [SerializeField] private bool isRoom3Enabled = true;

    private PuzzleSubController subController1;
    private PuzzleSubController subController2;
    private PuzzleSubController subController3;

    private int currentPhaseIndex;
    private bool isPuzzleSolved;

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
            else subController1.OnButtonStateChanged += HandleButtonStateChanged;
        }
        if (isRoom2Enabled)
        {
            if (subController2 == null) Debug.LogError("[puzle]: Sala 2 está habilitada pero no se encontró un PuzzleSubController con índice 'b' o '2'.");
            else subController2.OnButtonStateChanged += HandleButtonStateChanged;
        }
        if (isRoom3Enabled)
        {
            if (subController3 == null) Debug.LogError("[puzle]: Sala 3 está habilitada pero no se encontró un PuzzleSubController con índice 'c' o '3'.");
            else subController3.OnButtonStateChanged += HandleButtonStateChanged;
        }
    }

    private void OnDestroy()
    {
        Debug.Log("[puzle]: PuzzleGlobalController ha sido DESTRUIDO.");
        if (subController1 != null && isRoom1Enabled) subController1.OnButtonStateChanged -= HandleButtonStateChanged;
        if (subController2 != null && isRoom2Enabled) subController2.OnButtonStateChanged -= HandleButtonStateChanged;
        if (subController3 != null && isRoom3Enabled) subController3.OnButtonStateChanged -= HandleButtonStateChanged;
    }

    private void OnDisable()
    {
        Debug.Log("[puzle]: PuzzleGlobalController ha sido DESACTIVADO.");
    }

    private void Start()
    {
        StartPuzzle();
    }

    private void StartPuzzle()
    {
        currentPhaseIndex = 0;
        isPuzzleSolved = false;
        LoadCurrentPhase();
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

        if (currentPhaseIndex >= puzzleSequence.Phases.Count)
        {
            CompletePuzzle();
            return;
        }

        var currentPhase = puzzleSequence.Phases[currentPhaseIndex];
        if (isRoom1Enabled && subController1 != null) subController1.SetPhaseData(currentPhase.sub1.correctButtonId, currentPhase.sub1.incorrectButtonId1, currentPhase.sub1.incorrectButtonId2);
        if (isRoom2Enabled && subController2 != null) subController2.SetPhaseData(currentPhase.sub2.correctButtonId, currentPhase.sub2.incorrectButtonId1, currentPhase.sub2.incorrectButtonId2);
        if (isRoom3Enabled && subController3 != null) subController3.SetPhaseData(currentPhase.sub3.correctButtonId, currentPhase.sub3.incorrectButtonId1, currentPhase.sub3.incorrectButtonId2);
    }

    private void HandleButtonStateChanged()
    {
        if (isPuzzleSolved) return;

        bool allRoomsReady = true;
        string notReadyRooms = "";

        if (isRoom1Enabled)
        {
            if (subController1 != null)
            {
                bool ready = subController1.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 1 Lista = " + ready + " (Botón presionado: " + (subController1.CurrentPressedButton != null ? subController1.CurrentPressedButton.Id : "Ninguno") + ")");
                if (!ready)
                {
                    allRoomsReady = false;
                    notReadyRooms += "Sala 1 ";
                }
            }
            else
            {
                allRoomsReady = false;
                notReadyRooms += "Sala 1 (Sin subcontrolador) ";
            }
        }
        if (isRoom2Enabled)
        {
            if (subController2 != null)
            {
                bool ready = subController2.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 2 Lista = " + ready + " (Botón presionado: " + (subController2.CurrentPressedButton != null ? subController2.CurrentPressedButton.Id : "Ninguno") + ")");
                if (!ready)
                {
                    allRoomsReady = false;
                    notReadyRooms += "Sala 2 ";
                }
            }
            else
            {
                allRoomsReady = false;
                notReadyRooms += "Sala 2 (Sin subcontrolador) ";
            }
        }
        if (isRoom3Enabled)
        {
            if (subController3 != null)
            {
                bool ready = subController3.IsPlayerOnPhaseButton();
                Debug.Log("[puzle]: Check Sala 3 Lista = " + ready + " (Botón presionado: " + (subController3.CurrentPressedButton != null ? subController3.CurrentPressedButton.Id : "Ninguno") + ")");
                if (!ready)
                {
                    allRoomsReady = false;
                    notReadyRooms += "Sala 3 ";
                }
            }
            else
            {
                allRoomsReady = false;
                notReadyRooms += "Sala 3 (Sin subcontrolador) ";
            }
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
            if (isRoom1Enabled && subController1 != null) subController1.TriggerPhaseButtonsOnSuccess();
            if (isRoom2Enabled && subController2 != null) subController2.TriggerPhaseButtonsOnSuccess();
            if (isRoom3Enabled && subController3 != null) subController3.TriggerPhaseButtonsOnSuccess();
            CompletePhase();
        }
        else
        {
            if (isRoom1Enabled && subController1 != null)
            {
                if (subController1.IsCorrectButtonPressed) subController1.TriggerCurrentButtonCorrect();
                else subController1.TriggerCurrentButtonIncorrect();
            }
            if (isRoom2Enabled && subController2 != null)
            {
                if (subController2.IsCorrectButtonPressed) subController2.TriggerCurrentButtonCorrect();
                else subController2.TriggerCurrentButtonIncorrect();
            }
            if (isRoom3Enabled && subController3 != null)
            {
                if (subController3.IsCorrectButtonPressed) subController3.TriggerCurrentButtonCorrect();
                else subController3.TriggerCurrentButtonIncorrect();
            }
        }
    }

    private void CompletePhase()
    {
        Debug.Log("[puzle]: OnPhaseCompleted para fase " + currentPhaseIndex);
        OnPhaseCompleted?.Invoke();
        currentPhaseIndex++;
        LoadCurrentPhase();
    }

    private void CompletePuzzle()
    {
        isPuzzleSolved = true;
        Debug.Log("[puzle]: OnPuzzleCompleted");
        OnPuzzleCompleted?.Invoke();
    }
}
