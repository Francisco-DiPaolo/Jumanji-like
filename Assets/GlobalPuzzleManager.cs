using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// GlobalPuzzleManager — Controla la secuencia de encendido de antorchas del puzzle.
/// El jugador presiona un botón para validar: si no todas las antorchas están en verde,
/// la secuencia se resetea y vuelve a empezar desde el principio.
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

    [SerializeField] float allLitDuration     = 3f;     // Tiempo que permanecen todas encendidas
    [SerializeField] float resetPauseDuration = 1f;     // Pausa breve tras apagar todas (ciclo normal)
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
    [Networked]        int         CurrentTorchIndex { get; set; }
    [Networked]        float       NextActionTime    { get; set; }
    [Networked]        NetworkBool AllExtinguished   { get; set; }
    [Networked]        float       SyncWindowOpenTime { get; set; }

    [Networked, Capacity(8)]
    NetworkDictionary<PlayerRef, NetworkBool> PlayerInteracted => default;

    // ──────────────────────────────────────────────────────────────────────────
    // Private Fields
    // ──────────────────────────────────────────────────────────────────────────

    ChangeDetector    _changeDetector;
    BrickInteractable _brick;

    BrickInteractable Brick => _brick != null ? _brick : (_brick = GetComponentInChildren<BrickInteractable>());

    // ──────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (Object.HasStateAuthority)
        {
            CurrentTorchIndex = 0;
            AllExtinguished   = false;
            NextActionTime    = Runner.SimulationTime + torch_frequency;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fixed Update (State Authority only)
    // ──────────────────────────────────────────────────────────────────────────

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (IsPuzzleSolved) return;
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
                // Todas encendidas: habilitar ladrillo interactivo
                IsBrickEnabled = true;
                if (Brick != null) Brick.IsInteractable = true;
                NextActionTime = Runner.SimulationTime + allLitDuration;
            }
            else
            {
                // Esperar torch_frequency antes de encender la siguiente
                NextActionTime = Runner.SimulationTime + torch_frequency;
            }
        }
        else
        {
            // Secuencia completa sin resolver → apagar todo y reiniciar ciclo
            ExtinguishAll();
        }

        // ── Comprobación de ventana de sincronización ──
        if (PlayerInteracted.Count > 0)
        {
            float elapsed = Runner.SimulationTime - SyncWindowOpenTime;
            if (elapsed > syncWindowDuration)
            {
                Debug.Log("[PuzzleManager] Sync window expiró. Reseteando interacción.");
                ResetSyncWindow();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Validación por botón
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Llamar cuando el jugador presiona el botón de validación.
    /// Comprueba si TODAS las antorchas están en estado Verde (apagadas).
    /// → Si están todas en verde: registra la interacción del jugador (posible resolución).
    /// → Si alguna NO está en verde: resetea la secuencia desde el principio.
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

        if (IsGreenTorchLit())
        {
            // ✅ Respuesta correcta local. Registramos que este jugador apretó a tiempo.
            Debug.Log($"[PuzzleManager] Validación CORRECTA local. Esperando a otros... (Player {player})");
            
            bool isFirstInteract = PlayerInteracted.Count == 0;
            PlayerInteracted.Set(player, true);

            if (isFirstInteract)
                SyncWindowOpenTime = Runner.SimulationTime;

            TryResolveSync();
        }
        else
        {
            // ❌ Respuesta incorrecta: resetear la secuencia de esta celda
            Debug.Log("[PuzzleManager] Validación INCORRECTA — reseteando secuencia.");
            ResetSequence();
        }
    }

    void TryResolveSync()
    {
        // Buscamos todos los managers activos en la escena
        var managers = FindObjectsByType<GlobalPuzzleManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        
        // Filtramos para asegurarnos de que solo evaluamos los que están instanciados en red correctamente
        var validManagers = new List<GlobalPuzzleManager>();
        foreach (var m in managers)
        {
            if (m.Object != null && m.Object.IsValid)
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
            Debug.Log($"[PuzzleManager] ¡TODOS LOS JUGADORES RESOLVIERON! Abriendo {validManagers.Count} puertas.");
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
    /// Devuelve true si la antorcha configurada como verde está actualmente encendida.
    /// Si hay varias, deben estar todas encendidas.
    /// </summary>
    bool IsGreenTorchLit()
    {
        bool hasGreenTorch = false;
        foreach (var torch in torches)
        {
            if (torch.IsGreenTorch)
            {
                hasGreenTorch = true;
                if (!torch.IsLit) return false; // Falta encender la antorcha verde
            }
        }
        
        // Si por algún motivo no hay antorchas verdes, ganan cuando todas estén prendidas
        if (!hasGreenTorch)
        {
            foreach (var torch in torches)
                if (!torch.IsLit) return false;
        }
        
        return true;
    }

    /// <summary>
    /// Apaga y resetea TODAS las antorchas al estado Verde y reinicia la secuencia
    /// inmediatamente, sin la pausa de resetPauseDuration del ciclo normal.
    /// </summary>
    void ResetSequence()
    {
        // Apagar todas las antorchas → estado Verde
        foreach (var torch in torches)
            torch.Extinguish();

        // Reiniciar estado de la secuencia
        CurrentTorchIndex = 0;
        AllExtinguished   = false;

        // Deshabilitar el ladrillo interactivo
        IsBrickEnabled = false;
        if (Brick != null) Brick.IsInteractable = false;

        // Limpiar ventana de sincronización
        ResetSyncWindow();

        // Reiniciar temporizador con la frecuencia configurada
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
        if (Brick != null) Brick.IsInteractable = false;
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
