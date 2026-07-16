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

    [Header("Tolerancia (Coyote Time)")]
    [Tooltip("Tolerancia de validación en segundos (antes y después del verde).")]
    [SerializeField] float coyoteTolerance = 0.15f;

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
    [Networked]        NetworkBool InCoyoteAfter      { get; set; }
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
            InCoyoteAfter     = false;
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
            // ── Ventana de sincronización expiró ──
            bool hasInteracted = (PlayerInteracted.Count > 0);

            if (hasInteracted)
            {
                // Si el jugador interactuó pero no se resolvió la validación conjunta, falló
                Debug.Log("[PuzzleManager] Validación incorrecta/no sincronizada. Deteniendo secuencia...");
                StopSequence();
            }
            else if (CurrentRound + 1 >= roundsToStayLit)
            {
                // Es la última ronda sin interacción → entrar en coyote time de salida
                if (!InCoyoteAfter)
                {
                    // Primer paso: apagar las antorchas para dar feedback visual de que terminó
                    foreach (var torch in torches)
                    {
                        torch.Extinguish();
                        torch.UseSuccessColor = false;
                    }

                    InCoyoteAfter = true;
                    // Esperar coyoteTolerance adicionales antes de resetear
                    NextActionTime = Runner.SimulationTime + coyoteTolerance;
                    Debug.Log($"[PuzzleManager] Verde finalizó sin interacción. Entrando en coyote time de salida ({coyoteTolerance}s)...");
                }
                else
                {
                    // El coyote time de salida expiró sin interacción → Resetear todo
                    InCoyoteAfter = false;
                    CurrentRound++;
                    Debug.Log("[PuzzleManager] Coyote time de salida expiró sin interacción. Deteniendo secuencia...");
                    StopSequence();
                }
            }
            else
            {
                // Rondas intermedias sin interacción: apagar y continuar a la siguiente ronda
                CurrentRound++;
                int colorIdx = GetColorIndexForRound(CurrentRound, roundsToStayLit);
                foreach (var torch in torches)
                    torch.SetRoundColorIndex(colorIdx);
                Debug.Log($"[PuzzleManager] Avanzando a ronda {CurrentRound + 1}/{roundsToStayLit}. Índice de color de luz: {colorIdx}.");
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
            IsStarted     = true;
            CurrentRound  = 0;
            InCoyoteAfter = false;
            if (Brick != null) Brick.IsInteractable = true;
            ResetSequence();
            return;
        }

        bool isBefore, isAfter;
        if (IsInValidationWindow(out isBefore, out isAfter))
        {
            // ✅ Verde o Coyote Time: registrar interacción y disparar sonido
            if (PlayerInteracted.ContainsKey(player))
            {
                Debug.Log($"[PuzzleManager] Player {player} ya registrado en ventana de validación.");
                return;
            }

            // Disparar sonido verde en todos los clientes solo al primer jugador que aprieta
            if (PlayerInteracted.Count == 0)
                GreenSoundTick++;

            PlayerInteracted.Set(player, true);
            Debug.Log($"[PuzzleManager] Player {player} apretó en ventana de validación (isBefore={isBefore}, isAfter={isAfter}). ({PlayerInteracted.Count} jugadores registrados)");
            
            // Cambiar color de partículas a verde
            foreach (var torch in torches)
                torch.UseSuccessColor = true;

            TryResolveSync();
        }
        else
        {
            // ❌ Fuera de ventana: apagar todo y volver a esperar
            Debug.Log($"[PuzzleManager] Player {player} apretó fuera de ventana de validación — apagando secuencia manualmente.");
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
    /// Devuelve true si el momento de presionar el botón está dentro del rango de validación
    /// permitido (incluyendo coyote time antes y después del verde).
    /// </summary>
    bool IsInValidationWindow(out bool isBefore, out bool isAfter)
    {
        isBefore = false;
        isAfter = false;

        if (!IsStarted || IsPuzzleSolved) return false;

        // Si ya estamos en el coyote time de salida:
        if (InCoyoteAfter)
        {
            isAfter = true;
            return true;
        }

        // CASO 1: En el verde (todas encendidas) - en cualquier ronda
        if (CurrentTorchIndex >= torches.Count)
        {
            if (Runner.SimulationTime < NextActionTime)
            {
                return true;
            }
        }
        // CASO 2: A punto de encender la última (antes de verde) - en cualquier ronda
        else if (CurrentTorchIndex == torches.Count - 1)
        {
            if (NextActionTime - Runner.SimulationTime <= coyoteTolerance && NextActionTime >= Runner.SimulationTime)
            {
                isBefore = true;
                return true;
            }
        }

        return false;
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
        {
            torch.Extinguish();
            torch.UseSuccessColor = false;
            torch.SetRoundColorIndex(0); // Resetear al color base al detenerse
        }

        CurrentTorchIndex = 0;
        CurrentRound      = 0;
        IsBrickEnabled    = false;
        AllExtinguished   = false;
        InCoyoteAfter     = false;
        ResetSyncWindow();
        IsStarted         = false;
        // Brick.IsInteractable NO se toca — el botón sigue activo para poder reiniciar
    }

    /// <summary>
    /// Apaga todas las antorchas con una breve pausa antes de iniciar la siguiente ronda
    /// (AllExtinguished = true dispara la pausa en FixedUpdateNetwork).
    /// </summary>
    void ExtinguishAllWithPause()
    {
        foreach (var torch in torches)
        {
            torch.Extinguish();
            torch.UseSuccessColor = false;
        }

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
        {
            torch.Extinguish();
            torch.UseSuccessColor = false;
        }

        CurrentTorchIndex = 0;
        AllExtinguished   = false;
        IsBrickEnabled    = false;
        ResetSyncWindow();
        NextActionTime = Runner.SimulationTime + torch_frequency;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers de Color por Ronda
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve el índice de color (0-3) que debe usar la luz de la antorcha en la ronda dada.
    /// <para>1 ronda  : siempre 0 (no cambia).</para>
    /// <para>2 rondas : ronda 0 → 0, ronda 1 → 3 (salto brusco al color final).</para>
    /// <para>4 rondas : igual al número de ronda (gradual: 0, 1, 2, 3).</para>
    /// <para>Cualquier otro valor: Mathf.Clamp(round, 0, 3).</para>
    /// </summary>
    int GetColorIndexForRound(int round, int totalRounds)
    {
        switch (totalRounds)
        {
            case 1:  return 0;
            case 2:  return round == 0 ? 0 : 3;
            case 4:  return Mathf.Clamp(round, 0, 3);
            default: return Mathf.Clamp(round, 0, 3);
        }
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
