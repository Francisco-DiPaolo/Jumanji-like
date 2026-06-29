using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class GlobalPuzzleManager : NetworkBehaviour
{
    [SerializeField] List<TorchController> torches;
    [SerializeField] LeanTweenDoor tweenDoor;
    [SerializeField] float timeBetweenTorches = 2f;
    [SerializeField] float allLitDuration = 3f;
    [SerializeField] float resetPauseDuration = 1f;
    [SerializeField] float syncWindowDuration = 1.5f;

    [Header("Sonido de puerta al resolver")]
    [SerializeField] AudioSource doorAudioSource; // Sonido de puerta de prisión al abrir

    [Header("Puzzle Resuelto")]
    [SerializeField] GameObject candado; // Objeto que se apaga al resolver el puzzle

    [Networked] public NetworkBool IsBrickEnabled { get; set; }
    [Networked] public NetworkBool IsPuzzleSolved { get; set; }
    [Networked] int CurrentTorchIndex { get; set; }
    [Networked] float NextActionTime { get; set; }
    [Networked] NetworkBool AllExtinguished { get; set; }
    [Networked] float SyncWindowOpenTime { get; set; }

    [Networked, Capacity(8)]
    NetworkDictionary<PlayerRef, NetworkBool> PlayerInteracted => default;

    ChangeDetector _changeDetector;
    BrickInteractable _brick;

    BrickInteractable Brick => _brick != null ? _brick : (_brick = GetComponentInChildren<BrickInteractable>());

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (Object.HasStateAuthority)
        {
            CurrentTorchIndex = 0;
            AllExtinguished = false;
            NextActionTime = Runner.SimulationTime + timeBetweenTorches;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (IsPuzzleSolved) return;
        if (Runner.SimulationTime < NextActionTime) return;

        if (AllExtinguished)
        {
            AllExtinguished = false;
            NextActionTime = Runner.SimulationTime + timeBetweenTorches;
            return;
        }

        if (CurrentTorchIndex < torches.Count)
        {
            torches[CurrentTorchIndex].Light();
            CurrentTorchIndex++;

            if (CurrentTorchIndex >= torches.Count)
            {
                IsBrickEnabled = true;
                if (Brick != null) Brick.IsInteractable = true;
                NextActionTime = Runner.SimulationTime + allLitDuration;
            }
            else
            {
                NextActionTime = Runner.SimulationTime + timeBetweenTorches;
            }
        }
        else
        {
            ExtinguishAll();
        }

        if (IsBrickEnabled && PlayerInteracted.Count > 0)
        {
            float elapsed = Runner.SimulationTime - SyncWindowOpenTime;
            if (elapsed > syncWindowDuration)
                ResetSyncWindow();
        }
    }

    void ExtinguishAll()
    {
        foreach (var torch in torches)
            torch.Extinguish();

        CurrentTorchIndex = 0;
        IsBrickEnabled = false;
        if (Brick != null) Brick.IsInteractable = false;
        ResetSyncWindow();
        AllExtinguished = true;
        NextActionTime = Runner.SimulationTime + resetPauseDuration;
    }

    public void RegisterPlayerInteract(PlayerRef player)
    {
        Debug.Log("[PuzzleManager] RegisterPlayerInteract — player: " + player + " | HasStateAuthority: " + Object.HasStateAuthority + " | IsBrickEnabled: " + IsBrickEnabled + " | IsPuzzleSolved: " + IsPuzzleSolved);
        if (Object == null || !Object.HasStateAuthority) { Debug.LogWarning("[PuzzleManager] Blocked: no StateAuthority"); return; }
        if (!IsBrickEnabled || IsPuzzleSolved) { Debug.LogWarning("[PuzzleManager] Blocked: IsBrickEnabled=" + IsBrickEnabled + " IsPuzzleSolved=" + IsPuzzleSolved); return; }

        bool isFirstInteract = PlayerInteracted.Count == 0;
        PlayerInteracted.Set(player, true);
        Debug.Log("[PuzzleManager] PlayerInteracted count after set: " + PlayerInteracted.Count);

        if (isFirstInteract)
            SyncWindowOpenTime = Runner.SimulationTime;

        TryResolveSync();
    }

    void TryResolveSync()
    {
        var managers = FindObjectsByType<GlobalPuzzleManager>(FindObjectsSortMode.None);
        Debug.Log("[PuzzleManager] TryResolveSync — total managers in scene: " + managers.Length);

        bool allSolved = true;
        foreach (var manager in managers)
        {
            Debug.Log("[PuzzleManager] Manager: " + manager.name + " | PlayerInteracted.Count: " + manager.PlayerInteracted.Count);
            if (manager.PlayerInteracted.Count == 0)
            {
                allSolved = false;
                break;
            }
        }

        Debug.Log("[PuzzleManager] allSolved: " + allSolved);
        if (allSolved)
        {
            foreach (var manager in managers)
            {
                manager.IsPuzzleSolved = true;
            }
        }
    }

    void ResetSyncWindow()
    {
        PlayerInteracted.Clear();
        SyncWindowOpenTime = 0f;
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsPuzzleSolved) && IsPuzzleSolved)
                ResolvePuzzle();
        }
    }

    void ResolvePuzzle()
    {
        if (tweenDoor != null)
            tweenDoor.OpenDoor();

        if (doorAudioSource != null)
            doorAudioSource.Play();

        if (candado != null)
            candado.SetActive(false);
    }
}
