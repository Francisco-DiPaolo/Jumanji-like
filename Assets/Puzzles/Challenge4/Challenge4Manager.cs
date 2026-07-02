using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class Challenge4Manager : NetworkBehaviour
{
    [Header("Fase 1 — Rueda Individual")]
    [SerializeField] private SoloWheelController soloWheel;

    [Header("Fase 2 — Reloj + 3 Ruedas Cooperativas")]
    [SerializeField] private CentralClockManager centralClock;
    [SerializeField] private WheelRingController wheel0;
    [SerializeField] private WheelRingController wheel1;
    [SerializeField] private WheelRingController wheel2;

    [Header("Puertas")]
    [SerializeField] private LeanTweenDoor gateA;
    [SerializeField] private LeanTweenDoor gateB;

    [Header("Audio")]
    [SerializeField] private AudioSource puzzleAudioSource;
    [SerializeField] private AudioClip phase1SuccessClip;
    [SerializeField] private AudioClip phase2SuccessClip;
    [SerializeField] private AudioClip wrongCombinationClip;

    [Header("Events")]
    public UnityEvent OnPhase1Completed;
    public UnityEvent OnPuzzleFullyCompleted;
    public UnityEvent OnWrongCombinationAttempt;

    [Networked] private NetworkBool IsPhase1Done { get; set; }
    [Networked] private NetworkBool IsPuzzleSolved { get; set; }

    private ChangeDetector _changeDetector;
    private float _validationCooldown;
    private float _wrongFeedbackCooldown;
    private bool _needsCoopEvaluation;

    private const float ValidationCooldownSeconds = 2f;
    private const float WrongFeedbackCooldownSeconds = 1.5f;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            IsPhase1Done = false;
            IsPuzzleSolved = false;
        }

        soloWheel?.OnSelectionChanged.AddListener(EvaluateSoloWheel);

        wheel0?.OnSelectionChanged.AddListener(MarkNeedsCoopEvaluation);
        wheel1?.OnSelectionChanged.AddListener(MarkNeedsCoopEvaluation);
        wheel2?.OnSelectionChanged.AddListener(MarkNeedsCoopEvaluation);
        centralClock?.OnCycleChanged.AddListener(MarkNeedsCoopEvaluation);
    }

    private void OnDestroy()
    {
        soloWheel?.OnSelectionChanged.RemoveListener(EvaluateSoloWheel);

        wheel0?.OnSelectionChanged.RemoveListener(MarkNeedsCoopEvaluation);
        wheel1?.OnSelectionChanged.RemoveListener(MarkNeedsCoopEvaluation);
        wheel2?.OnSelectionChanged.RemoveListener(MarkNeedsCoopEvaluation);
        centralClock?.OnCycleChanged.RemoveListener(MarkNeedsCoopEvaluation);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (IsPuzzleSolved) return;

        if (_validationCooldown > 0f) _validationCooldown -= Runner.DeltaTime;
        if (_wrongFeedbackCooldown > 0f) _wrongFeedbackCooldown -= Runner.DeltaTime;

        if (!IsPhase1Done) return;

        if (!_needsCoopEvaluation) return;
        _needsCoopEvaluation = false;
        EvaluateCoopWheels();
    }

    private void EvaluateSoloWheel()
    {
        if (!HasStateAuthority) return;
        if (IsPhase1Done) return;
        if (_validationCooldown > 0f) return;

        if (soloWheel == null) return;

        var selected = new System.Collections.Generic.HashSet<string>
        {
            soloWheel.SelectedSymbolId0,
            soloWheel.SelectedSymbolId1,
            soloWheel.SelectedSymbolId2
        };

        Debug.Log($"[Challenge4] Chequeando rueda solo (sin orden) — " +
                  $"Seleccionados: [{soloWheel.SelectedSymbolId0}, {soloWheel.SelectedSymbolId1}, {soloWheel.SelectedSymbolId2}] | " +
                  $"Targets: [{centralClock.ActiveSymbolId0}, {centralClock.ActiveSymbolId1}, {centralClock.ActiveSymbolId2}]");

        bool correct = selected.Contains(centralClock.ActiveSymbolId0)
                    && selected.Contains(centralClock.ActiveSymbolId1)
                    && selected.Contains(centralClock.ActiveSymbolId2);

        if (correct)
        {
            _validationCooldown = ValidationCooldownSeconds;
            soloWheel.MarkResolved();
            Rpc_CompletePhase1();
        }
        else if (_wrongFeedbackCooldown <= 0f)
        {
            _wrongFeedbackCooldown = WrongFeedbackCooldownSeconds;
            Rpc_TriggerSoloWrongFeedback();
        }
    }

    private void MarkNeedsCoopEvaluation()
    {
        _needsCoopEvaluation = true;
    }

    private void EvaluateCoopWheels()
    {
        if (!HasStateAuthority) return;
        if (IsPuzzleSolved) return;
        if (_validationCooldown > 0f) return;

        bool allMatch = CheckAllWheelsMatch();

        if (allMatch)
        {
            _validationCooldown = ValidationCooldownSeconds;
            _wrongFeedbackCooldown = 0f;
            IsPuzzleSolved = true;
            centralClock.StopClock();
            Rpc_CompletePhase2();
        }
        else if (_wrongFeedbackCooldown <= 0f)
        {
            bool anyPartialMatch = CheckAnyWheelMatches();
            if (!anyPartialMatch)
            {
                _wrongFeedbackCooldown = WrongFeedbackCooldownSeconds;
                Rpc_TriggerCoopWrongFeedback();
            }
        }
    }

   private bool CheckAllWheelsMatch()
{
    if (centralClock == null || wheel0 == null || wheel1 == null || wheel2 == null) return false;

    bool w0ok = wheel0.TryGetMatchingSymbolId(centralClock, out string id0);
    bool w1ok = wheel1.TryGetMatchingSymbolId(centralClock, out string id1);
    bool w2ok = wheel2.TryGetMatchingSymbolId(centralClock, out string id2);

    var resolvedIds = new System.Collections.Generic.HashSet<string> { id0, id1, id2 };
    var clockIds = new System.Collections.Generic.HashSet<string>
    {
        centralClock.ActiveSymbolId0,
        centralClock.ActiveSymbolId1,
        centralClock.ActiveSymbolId2
    };

    Debug.Log($"[Challenge4] Chequeando ruedas coop (sin orden) — " +
              $"Wheel0: '{id0}' ({(w0ok ? "OK" : "FAIL")}) | " +
              $"Wheel1: '{id1}' ({(w1ok ? "OK" : "FAIL")}) | " +
              $"Wheel2: '{id2}' ({(w2ok ? "OK" : "FAIL")}) | " +
              $"Clock targets: [{centralClock.ActiveSymbolId0}, {centralClock.ActiveSymbolId1}, {centralClock.ActiveSymbolId2}]");

    return w0ok && w1ok && w2ok && resolvedIds.SetEquals(clockIds);
}

private bool CheckAnyWheelMatches()
{
    if (centralClock == null || wheel0 == null || wheel1 == null || wheel2 == null) return false;

    bool w0ok = wheel0.TryGetMatchingSymbolId(centralClock, out _);
    bool w1ok = wheel1.TryGetMatchingSymbolId(centralClock, out _);
    bool w2ok = wheel2.TryGetMatchingSymbolId(centralClock, out _);

    return w0ok || w1ok || w2ok;
}

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_CompletePhase1()
    {
        Debug.Log("[Challenge4] Victoria Solo — Fase 1 completada.");
        IsPhase1Done = true;

        gateA?.OpenDoor();
        soloWheel?.TriggerSuccessFeedback();

        if (puzzleAudioSource != null && phase1SuccessClip != null)
            puzzleAudioSource.PlayOneShot(phase1SuccessClip);

        if (HasStateAuthority && centralClock != null && soloWheel != null)
        {
            centralClock.SetActiveSymbols(
                soloWheel.SelectedSymbolId0,
                soloWheel.SelectedSymbolId1,
                soloWheel.SelectedSymbolId2
            );
        }

        OnPhase1Completed?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_CompletePhase2()
    {
        Debug.Log("[Challenge4] Victoria Final — Puzzle completamente resuelto.");
        gateB?.OpenDoor();

        wheel0?.TriggerSuccessFeedback();
        wheel1?.TriggerSuccessFeedback();
        wheel2?.TriggerSuccessFeedback();

        if (puzzleAudioSource != null && phase2SuccessClip != null)
            puzzleAudioSource.PlayOneShot(phase2SuccessClip);

        OnPuzzleFullyCompleted?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_TriggerSoloWrongFeedback()
    {
        soloWheel?.TriggerErrorFeedback();

        if (puzzleAudioSource != null && wrongCombinationClip != null)
            puzzleAudioSource.PlayOneShot(wrongCombinationClip);

        OnWrongCombinationAttempt?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_TriggerCoopWrongFeedback()
    {
        wheel0?.TriggerErrorFeedback();
        wheel1?.TriggerErrorFeedback();
        wheel2?.TriggerErrorFeedback();

        if (puzzleAudioSource != null && wrongCombinationClip != null)
            puzzleAudioSource.PlayOneShot(wrongCombinationClip);

        OnWrongCombinationAttempt?.Invoke();
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsPuzzleSolved) && IsPuzzleSolved)
            {
                wheel0?.TriggerSuccessFeedback();
                wheel1?.TriggerSuccessFeedback();
                wheel2?.TriggerSuccessFeedback();
            }
        }
    }
}