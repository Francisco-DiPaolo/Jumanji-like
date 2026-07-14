using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// GlobalPuzzleManager — Controla la secuencia de encendido de antorchas del puzzle.
///
/// Comportamiento por celda:
///  - Al apretar el botón por primera vez (o tras detenerse): arranca la secuencia.
///  - Si el jugador aprieta mientras las antorchas están encendidas pero NO en verde:
///      se apaga todo y el puzzle vuelve a esperar (IsStarted = false).
///  - Al llegar al verde (todas encendidas): ventana de sincronización abierta.
///      Si todos los jugadores validan → puzzle resuelto.
///      Al expirar la ventana → se completa una ronda.
///  - Tras completar todas las rondas configuradas: se apaga y espera nuevo botón.
///  - Entre rondas: breve pausa (resetPauseDuration) y nueva secuencia automática.
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
    [SerializeField] float resetPauseDuration = 1f;     // Pausa breve entre rondas
    [Tooltip("Tiempo que los jugadores tienen para apretar el botón una vez que todas las antorchas están encendidas.")]
    [SerializeField] float syncWindowDuration = 1.5f;   // Ventana de sincronización multijugador

    [Header("Rondas")]
    [Tooltip("Cuántas veces debe llegar al verde (completar la secuencia entera) antes de apagarse y esperar el botón.")]
    [SerializeField] int roundsToStayLit = 1;

    [Header("Sonido al llegar al verde")]
    [Tooltip("AudioSource que se reproduce en todos los clientes cuando un jugador aprieta el botón con todas las antorchas encendidas.")]
    [SerializeField] AudioSource greenCellAudioSource;

    [Header("Sonido de puerta al resolver")]
    [SerializeField] AudioSource doorAudioSource;       // Sonido de puerta de prisión al abrir

    [Header("Puzzle Resuelto")]
    [SerializeField] GameObject candado;                // Objeto que se apaga al resolver el puzzle

    // ──────────────────────────────────────────────────────────────────────────
    // Networked State
    // ──────────────────────────────────────────────────────────────────────────

    [Networked] public NetworkBool IsBrickEnabled    { get; set; }
    [Networked] public NetworkBool IsPuzzleSolved    { get; set; }
    [Networked] public NetworkBool IsStarted         { get; set; }
    [Networked]        int         CurrentTorchIndex  { get; set; }
    [Networked]        float       NextActionTime     { get; set; }
    [Networked]        NetworkBool AllExtinguished    { get; set; }
    [Networked]        float       SyncWindowOpenTime { get; set; }
    [Networked]        int         CurrentRound       { get; set; }
    /// <summary>
    /// Contador que se incrementa cada vez que debe reproducirse el sonido verde.
    /// El cambio se detecta en Render() en todos los clientes.
    /// </summary>
    [Networked]        int         GreenSoundTick     { get; set; }

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
            CurrentRound      = 0;
            GreenSoundTick    = 0;
            AllExtinguished   = true;
            IsStarted         = false;
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
        // Solo verificamos el input en la primera instancia para no enviar múltiples RPCs
        if (_activeManagers.Count > 0 && _activeManagers[0] == this)
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (Object.HasStateAuthority)
                    ForceSolveAll();
                else
                    RPC_ForceSolvePuzzle();
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

        // ── Pausa breve entre rondas ──
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
                bool isLastRound = (CurrentRound + 1) >= roundsToStayLit;

                if (isLastRound)
                {
                    // Última ronda → abrir ventana de validación (IsBrickEnabled = true)
                    IsBrickEnabled     = true;
                    SyncWindowOpenTime = Runner.SimulationTime;
                    NextActionTime     = Runner.SimulationTime + syncWindowDuration;
                    Debug.Log("[PuzzleManager] ¡Todas las antorchas encendidas! (Última ronda) Ventana de sincronización abierta.");
                }
                else
                {
                    // Ronda intermedia → el verde se mantiene visible syncWindowDuration, pero sin abrir validación
                    SyncWindowOpenTime = Runner.SimulationTime;
                    NextActionTime     = Runner.SimulationTime + syncWindowDuration;
                    Debug.Log($"[PuzzleManager] Verde intermedio (ronda {CurrentRound + 1}/{roundsToStayLit}). Duración: {syncWindowDuration}s.");
                }
            }
            else
            {
                NextActionTime = Runner.SimulationTime + torch_frequency;
            }
        }
        else
        {
            // ── Ventana de sincronización expiró: completar la ronda ──
            CurrentRound++;
            Debug.Log($"[PuzzleManager] Ronda {CurrentRound}/{roundsToStayLit} completada.");

            if (CurrentRound >= roundsToStayLit)
            {
                // Todas las rondas completadas → apagar todo y esperar botón
                Debug.Log("[PuzzleManager] Todas las rondas completadas. Apagando y esperando botón...");
                StopSequence();
            }
            else
            {
                // Quedan más rondas → apagar inmediatamente y continuar con el mismo ritmo
                Debug.Log($"[PuzzleManager] Iniciando ronda {CurrentRound + 1}/{roundsToStayLit}...");
                ResetSequence();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Validación por botón
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Llamar cuando el jugador presiona el botón de validación.
    ///  • Si el puzzle NO está iniciado  → arranca la secuencia.
    ///  • Si está en verde (todas lit)    → registra interacción + dispara sonido verde.
    ///                                      Si todos los jugadores validan → puzzle resuelto.
    ///  • Si hay antorchas encendidas pero no todas → apaga todo (StopSequence).
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

        // ── Arrancar la secuencia (no iniciado o tras detenerse) ──
        if (!IsStarted)
        {
            Debug.Log("[PuzzleManager] Arrancando el puzzle (botón apretado).");
            IsStarted    = true;
            CurrentRound = 0;
            if (Brick != null) Brick.IsInteractable = true;
            ResetSequence();
            return;
        }

        if (AreAllTorchesLit())
        {
            // ✅ Verde: registrar interacción y disparar sonido
            if (PlayerInteracted.ContainsKey(player))
            {
                Debug.Log($"[PuzzleManager] Player {player} ya registrado en verde.");
                return;
            }

            // Disparar sonido verde en todos los clientes solo al primer jugador que aprieta
            if (PlayerInteracted.Count == 0)
                GreenSoundTick++;

            PlayerInteracted.Set(player, true);
            Debug.Log($"[PuzzleManager] Player {player} apretó en verde. ({PlayerInteracted.Count} jugadores registrados)");
            TryResolveSync();
        }
        else
        {
            // ❌ Mitad de secuencia: apagar todo y volver a esperar
            Debug.Log($"[PuzzleManager] Player {player} apagó la secuencia manualmente.");
            StopSequence();
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

        int activePlayerCount = 0;
        if (Runner != null)
        {
            foreach (var p in Runner.ActivePlayers)
                activePlayerCount++;
        }

        int managersWithInteractions = 0;
        foreach (var manager in validManagers)
        {
            if (manager.PlayerInteracted.Count > 0)
                managersWithInteractions++;
        }

        bool allSolved = (activePlayerCount > 0) && (managersWithInteractions >= activePlayerCount);

        if (allSolved && validManagers.Count > 0)
        {
            Debug.Log($"[PuzzleManager] ¡TODOS LOS JUGADORES RESOLVIERON! Abriendo {validManagers.Count} puertas.");
            foreach (var manager in validManagers)
                manager.IsPuzzleSolved = true;
        }
    }

    void ResetSyncWindow()
    {
        PlayerInteracted.Clear();
        SyncWindowOpenTime = 0f;
    }

    /// <summary>
    /// Devuelve true si TODAS las antorchas del puzzle están actualmente encendidas (estado verde).
    /// </summary>
    bool AreAllTorchesLit()
    {
        foreach (var torch in torches)
            if (!torch.IsLit) return false;
        return true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apaga todas las antorchas y detiene la secuencia completamente (IsStarted = false).
    /// El jugador deberá volver a apretar el botón para reiniciar.
    /// El ladrillo permanece interactivo para permitir el reinicio.
    /// </summary>
    void StopSequence()
    {
        foreach (var torch in torches)
            torch.Extinguish();

        CurrentTorchIndex = 0;
        CurrentRound      = 0;
        IsBrickEnabled    = false;
        AllExtinguished   = false; // Sin pausa automática; IsStarted=false detiene el bucle
        ResetSyncWindow();
        IsStarted = false;
        // Brick.IsInteractable NO se toca — el botón sigue activo para poder reiniciar
    }

    /// <summary>
    /// Apaga todas las antorchas con una breve pausa antes de iniciar la siguiente ronda
    /// (AllExtinguished = true dispara la pausa en FixedUpdateNetwork).
    /// </summary>
    void ExtinguishAllWithPause()
    {
        foreach (var torch in torches)
            torch.Extinguish();

        CurrentTorchIndex = 0;
        IsBrickEnabled    = false;
        ResetSyncWindow();
        AllExtinguished = true;
        NextActionTime  = Runner.SimulationTime + resetPauseDuration;
    }

    /// <summary>
    /// Apaga todas las antorchas e inicia la secuencia desde cero inmediatamente
    /// (sin pausa de AllExtinguished). Usado al arrancar o reiniciar manualmente.
    /// </summary>
    void ResetSequence()
    {
        foreach (var torch in torches)
            torch.Extinguish();

        CurrentTorchIndex = 0;
        AllExtinguished   = false;
        IsBrickEnabled    = false;
        ResetSyncWindow();
        NextActionTime = Runner.SimulationTime + torch_frequency;
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

            // GreenSoundTick se incrementa en el servidor cuando el primer jugador
            // aprieta en verde → todos los clientes reproducen el audio verde
            if (change == nameof(GreenSoundTick))
            {
                if (greenCellAudioSource != null)
                    greenCellAudioSource.Play();
            }
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
