using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// GlobalPuzzleManager — Controla la secuencia de encendido de antorchas del puzzle.
/// Cuando TODAS las antorchas están encendidas (verdes), todos los jugadores deben
/// apretar el botón simultáneamente dentro de la ventana de sincronización para resolver el puzzle.
/// Si algún jugador aprieta cuando no todas están encendidas, la secuencia se resetea.
/// </summary>
public class GlobalPuzzleManager : NetworkBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ──────────────────────────────────────────────────────────────────────────

    [SerializeField] List<TorchController> torches;
    [SerializeField] LeanTweenDoor tweenDoor;

    [Header("Temporización de la secuencia")]
    [Tooltip("Tiempo en segundos que tarda en encenderse la siguiente antorcha de la secuencia.")]
    [SerializeField] float torch_frequency = 2f;        // Frecuencia de encendido entre antorchas
    [SerializeField] float resetPauseDuration = 1f;     // Pausa breve tras apagar todas (ciclo normal)
    [Tooltip("Tiempo que los jugadores tienen para apretar el botón una vez que todas las antorchas están encendidas.")]
    [SerializeField] float syncWindowDuration = 1.5f;   // Ventana de sincronización multijugador

    [Header("Sonido de puerta al resolver")]
    [SerializeField] AudioSource doorAudioSource;       // Sonido de puerta de prisión al abrir

    [Header("Puzzle Resuelto")]
    [SerializeField] GameObject candado;                // Objeto que se apaga al resolver el puzzle

    // ──────────────────────────────────────────────────────────────────────────
    // Networked State
    // ──────────────────────────────────────────────────────────────────────────

    [Networked] public NetworkBool IsBrickEnabled   { get; set; }
    [Networked] public NetworkBool IsPuzzleSolved   { get; set; }
    [Networked] public NetworkBool IsStarted        { get; set; }
    [Networked]        int         CurrentTorchIndex { get; set; }
    [Networked]        float       NextActionTime    { get; set; }
    [Networked]        NetworkBool AllExtinguished   { get; set; }
    [Networked]        float       SyncWindowOpenTime { get; set; }

    [Networked, Capacity(8)]
    NetworkDictionary<PlayerRef, NetworkBool> PlayerInteracted => default;

    // ──────────────────────────────────────────────────────────────────────────
    // Private Fields
    // ──────────────────────────────────────────────────────────────────────────

    static List<GlobalPuzzleManager> _activeManagers = new List<GlobalPuzzleManager>();

    ChangeDetector    _changeDetector;
    BrickInteractable _brick;

    BrickInteractable Brick => _brick != null ? _brick : (_brick = GetComponentInChildren<BrickInteractable>());

    // ──────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (!_activeManagers.Contains(this))
            _activeManagers.Add(this);

        if (Object.HasStateAuthority)
        {
            CurrentTorchIndex = 0;
            AllExtinguished   = true;
            IsStarted         = false;
            // No seteamos NextActionTime hasta que el jugador apriete el botón
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_activeManagers.Contains(this))
            _activeManagers.Remove(this);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Input / Debug (F9 para resolver)
    // ──────────────────────────────────────────────────────────────────────────

    void Update()
    {
        // Solo verificamos el input en la primera instancia para no enviar múltiples RPCs a la vez
        if (_activeManagers.Count > 0 && _activeManagers[0] == this)
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (Object.HasStateAuthority)
                {
                    ForceSolveAll();
                }
                else
                {
                    RPC_ForceSolvePuzzle();
                }
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ForceSolvePuzzle()
    {
        ForceSolveAll();
    }

    void ForceSolveAll()
    {
        Debug.Log("[PuzzleManager] Forzando resolución del puzzle (F9)...");
        foreach (var m in _activeManagers)
        {
            if (m != null && m.Object != null && m.Object.IsValid)
                m.IsPuzzleSolved = true;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fixed Update (State Authority only)
    // ──────────────────────────────────────────────────────────────────────────

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // ── Puzzle resuelto: asegurar que el ladrillo quede deshabilitado ──
        if (IsPuzzleSolved)
        {
            if (IsBrickEnabled) { IsBrickEnabled = false; if (Brick != null) Brick.IsInteractable = false; }
            return;
        }

        if (!IsStarted) return;
        if (Runner.SimulationTime < NextActionTime) return;

        // ── Pausa breve tras apagarse todas (ciclo normal) ──
        if (AllExtinguished)
        {
            AllExtinguished = false;
            NextActionTime  = Runner.SimulationTime + torch_frequency;
            return;
        }

        // ── Encender la siguiente antorcha de la secuencia ──
        if (CurrentTorchIndex < torches.Count)
        {
            torches[CurrentTorchIndex].Light();
            CurrentTorchIndex++;

            if (CurrentTorchIndex >= torches.Count)
            {
                // Todas encendidas: marcar estado e iniciar ventana de sincronización
                IsBrickEnabled     = true;
                SyncWindowOpenTime = Runner.SimulationTime;
                NextActionTime     = Runner.SimulationTime + syncWindowDuration;
                Debug.Log("[PuzzleManager] ¡Todas las antorchas encendidas! Ventana de sincronización abierta.");
            }
            else
            {
                // Esperar torch_frequency antes de encender la siguiente
                NextActionTime = Runner.SimulationTime + torch_frequency;
            }
        }
        else
        {
            // Ventana de sincronización expiró sin que todos presionaran → apagar y reiniciar
            Debug.Log("[PuzzleManager] Ventana de sincronización expiró. Reseteando...");
            ExtinguishAll();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Validación por botón
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Llamar cuando el jugador presiona el botón de validación.
    /// → Si TODAS las antorchas están encendidas: registra la interacción del jugador.
    ///    Cuando todos los jugadores hayan interactuado → puzzle resuelto.
    /// → Si alguna antorcha NO está encendida: resetea SOLO la secuencia de este jugador.
    /// El botón permanece activo en todo momento mientras el puzzle está activo.
    /// Solo el StateAuthority ejecuta la lógica; los clientes envían RPC al host.
    /// </summary>
    public void ValidateAnswer(PlayerRef player)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            Debug.LogWarning("[PuzzleManager] ValidateAnswer: sin StateAuthority.");
            return;
        }

        if (IsPuzzleSolved) return;

        if (!IsStarted)
        {
            // Arrancar el puzzle por primera vez y habilitar el botón permanentemente
            Debug.Log("[PuzzleManager] Arrancando el puzzle (primer click).");
            IsStarted = true;
            if (Brick != null) Brick.IsInteractable = true;
            ResetSequence();
            return;
        }

        if (AreAllTorchesLit())
        {
            // ✅ Todas las antorchas encendidas: registrar interacción del jugador
            if (PlayerInteracted.ContainsKey(player))
            {
                Debug.Log($"[PuzzleManager] Player {player} ya registrado.");
                return;
            }

            PlayerInteracted.Set(player, true);
            Debug.Log($"[PuzzleManager] Player {player} apretó a tiempo. ({PlayerInteracted.Count} jugadores registrados)");

            TryResolveSync();
        }
        else
        {
            // ❌ No todas las antorchas encendidas: resetear la secuencia
            Debug.Log($"[PuzzleManager] Player {player} apretó con antorchas apagadas — reseteando secuencia.");
            ResetSequence();
        }
    }

    void TryResolveSync()
    {
        var validManagers = new List<GlobalPuzzleManager>();
        foreach (var m in _activeManagers)
        {
            if (m != null && m.Object != null && m.Object.IsValid)
                validManagers.Add(m);
        }

        bool allSolved = true;
        foreach (var manager in validManagers)
        {
            if (manager.PlayerInteracted.Count == 0)
            {
                allSolved = false;
                break;
            }
        }

        if (allSolved && validManagers.Count > 0)
        {
            Debug.Log($"[PuzzleManager] ¡TODOS LOS JUGADORES RESOLVIERON (Botón)! Abriendo {validManagers.Count} puertas.");
            foreach (var manager in validManagers)
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

    /// <summary>
    /// Devuelve true si TODAS las antorchas del puzzle están actualmente encendidas.
    /// Esta es la condición para que el jugador pueda validar correctamente.
    /// </summary>
    bool AreAllTorchesLit()
    {
        foreach (var torch in torches)
            if (!torch.IsLit) return false;
        return true;
    }

    /// <summary>
    /// Apaga y resetea TODAS las antorchas al estado Verde y reinicia la secuencia
    /// inmediatamente, sin la pausa de resetPauseDuration del ciclo normal.
    /// </summary>
    void ResetSequence()
    {
        // Apagar todas las antorchas
        foreach (var torch in torches)
            torch.Extinguish();

        // Reiniciar estado de la secuencia
        CurrentTorchIndex = 0;
        AllExtinguished   = false;
        IsBrickEnabled    = false;

        // Limpiar ventana de sincronización
        ResetSyncWindow();

        // Reiniciar temporizador con la frecuencia configurada
        // (Brick.IsInteractable NO se toca — el botón sigue activo)
        NextActionTime = Runner.SimulationTime + torch_frequency;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apaga todas las antorchas al terminar el ciclo normal (sin que el jugador valide).
    /// Incluye la pausa de reset estándar (resetPauseDuration) antes de reiniciar.
    /// </summary>
    void ExtinguishAll()
    {
        foreach (var torch in torches)
            torch.Extinguish();

        CurrentTorchIndex = 0;
        IsBrickEnabled    = false;
        // Brick.IsInteractable NO se toca — el botón sigue activo para que el jugador pueda
        // intentar apretar (y sea penalizado con reset si lo hace con antorchas apagadas)
        ResetSyncWindow();
        AllExtinguished = true;
        NextActionTime  = Runner.SimulationTime + resetPauseDuration;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Render (Change Detection)
    // ──────────────────────────────────────────────────────────────────────────

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
