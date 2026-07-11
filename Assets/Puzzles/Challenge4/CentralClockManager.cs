using Fusion;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class SymbolTrio
{
    public string Id0;
    public string Id1;
    public string Id2;
}

public class CentralClockManager : NetworkBehaviour
{
    [Header("Clock Settings")]
    [SerializeField] private float cycleDuration = 15f;
    [Tooltip("Si es false, el reloj no arranca solo; espera a que se llame a StartClock() o Rpc_RequestStartClock()")]
    [SerializeField] private bool autoStart = false;

    [Header("Symbol Combinations")]
    [SerializeField] private SymbolTrio[] symbolCombinations;

    [Header("Materials")]
    [SerializeField] private Renderer[] clockSymbolRenderers;
    [SerializeField] private Material symbolDefaultMaterial;
    [SerializeField] private Material symbolActiveMaterial;
    [Tooltip("Material que se usará para los símbolos activos durante la Fase 2 (en lugar de Symbol Active Material)")]
    [SerializeField] private Material symbolPhase2Material;

    [Header("Forbidden Combination Filters")]
    [Tooltip("Referencia a la SoloWheelController; se usa para excluir su combinación actual en Fase 1")]
    [SerializeField] private SoloWheelController soloWheelRef;
    [Tooltip("Las 3 WheelRingControllers cooperativas; se usan para excluir sus selecciones actuales en Fase 2")]
    [SerializeField] private WheelRingController[] wheelRingRefs;

    [Header("Events")]
    public UnityEvent OnCycleChanged;
    public UnityEvent OnClockStopped;

    [Networked] public float CycleTimeRemaining { get; private set; }
    [Networked] public NetworkBool IsRunning { get; set; }
    [Networked] public int CycleCount { get; set; }
    /// <summary>True a partir de la Fase 2. Controla el material de símbolos activos y el filtro de combinaciones prohibidas.</summary>
    [Networked] public NetworkBool IsPhase2 { get; set; }

    [Networked, Capacity(3)]
    private NetworkArray<NetworkString<_32>> ActiveSymbolIdsNetworked => default;

    private ChangeDetector _changeDetector;
    private int _lastCombinationIndex = -1;
    private string[] _symbolIds; // id de SymbolIdentity por cada posición del array de renderers

    public string ActiveSymbolId0 => ActiveSymbolIdsNetworked[0].ToString();
    public string ActiveSymbolId1 => ActiveSymbolIdsNetworked[1].ToString();
    public string ActiveSymbolId2 => ActiveSymbolIdsNetworked[2].ToString();

    private void Awake()
    {
        CacheSymbolIds();
    }

    private void CacheSymbolIds()
    {
        _symbolIds = new string[clockSymbolRenderers.Length];
        for (int i = 0; i < clockSymbolRenderers.Length; i++)
        {
            var identity = clockSymbolRenderers[i] != null
                ? clockSymbolRenderers[i].GetComponent<SymbolIdentity>()
                : null;

            if (identity == null)
            {
                Debug.LogError($"Falta SymbolIdentity en {clockSymbolRenderers[i]?.name}", this);
                _symbolIds[i] = null;
                continue;
            }

            _symbolIds[i] = identity.SymbolId;
        }
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            CycleTimeRemaining = cycleDuration;
            PickNewSymbols(); // Elige la combinación inicial para la Fase 1
            if (autoStart)
            {
                IsRunning = true;
            }
        }

        ApplyActiveSymbolMaterials(ActiveSymbolId0, ActiveSymbolId1, ActiveSymbolId2);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!IsRunning) return;

        CycleTimeRemaining -= Runner.DeltaTime;

        if (CycleTimeRemaining <= 0f)
        {
            CycleTimeRemaining = cycleDuration;
            PickNewSymbols();
        }
    }

    public void StartClock()
    {
        if (!HasStateAuthority) return;
        IsRunning = true;
        PickNewSymbols();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RequestStartClock()
    {
        if (IsRunning) return;
        StartClock();
    }

    public void SetActiveSymbols(string id0, string id1, string id2)
    {
        if (!HasStateAuthority) return;
        ActiveSymbolIdsNetworked.Set(0, id0);
        ActiveSymbolIdsNetworked.Set(1, id1);
        ActiveSymbolIdsNetworked.Set(2, id2);
    }

    public void StopClock()
    {
        if (!HasStateAuthority) return;
        IsRunning = false;
        Rpc_OnClockStopped();
    }

    private void PickNewSymbols()
    {
        if (symbolCombinations == null || symbolCombinations.Length == 0)
        {
            Debug.LogWarning("[CentralClock] No hay combinaciones de símbolos definidas.", this);
            return;
        }

        // Construir índices prohibidos según la fase
        var forbiddenIndices = BuildForbiddenIndices();

        // Intentar elegir un índice válido (no prohibido y no el mismo que el anterior)
        int index = -1;
        int maxAttempts = symbolCombinations.Length * 4;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int candidate = UnityEngine.Random.Range(0, symbolCombinations.Length);

            // Evitar repetir la misma combinación dos veces seguidas si hay más de una opción
            if (symbolCombinations.Length > 1 && candidate == _lastCombinationIndex)
                continue;

            if (!forbiddenIndices.Contains(candidate))
            {
                index = candidate;
                break;
            }
        }

        // Fallback: si no encontramos índice válido respetando filtros, ignoramos los prohibidos
        if (index == -1)
        {
            Debug.LogWarning("[CentralClock] No se pudo respetar todos los filtros de combinación. Eligiendo sin filtro de ruedas.", this);
            index = UnityEngine.Random.Range(0, symbolCombinations.Length);
            if (symbolCombinations.Length > 1)
            {
                int safety = 0;
                while (index == _lastCombinationIndex && safety < 20)
                {
                    index = UnityEngine.Random.Range(0, symbolCombinations.Length);
                    safety++;
                }
            }
        }

        _lastCombinationIndex = index;

        SymbolTrio trio = symbolCombinations[index];

        ActiveSymbolIdsNetworked.Set(0, trio.Id0);
        ActiveSymbolIdsNetworked.Set(1, trio.Id1);
        ActiveSymbolIdsNetworked.Set(2, trio.Id2);
        
        CycleCount++;

        Debug.Log($"[CentralClock] Nueva combinación [{index}]: '{trio.Id0}', '{trio.Id1}', '{trio.Id2}'");
    }

    /// <summary>
    /// Devuelve los índices de symbolCombinations que están prohibidos para la selección actual,
    /// según la fase del puzzle.
    /// </summary>
    private System.Collections.Generic.HashSet<int> BuildForbiddenIndices()
    {
        var forbidden = new System.Collections.Generic.HashSet<int>();

        if (!IsPhase2)
        {
            // Fase 1: excluir la combinación actualmente puesta en la SoloWheel
            if (soloWheelRef != null)
            {
                var soloSet = new System.Collections.Generic.HashSet<string>
                {
                    soloWheelRef.SelectedSymbolId0,
                    soloWheelRef.SelectedSymbolId1,
                    soloWheelRef.SelectedSymbolId2
                };
                soloSet.RemoveWhere(s => string.IsNullOrEmpty(s));

                if (soloSet.Count == 3)
                {
                    for (int i = 0; i < symbolCombinations.Length; i++)
                    {
                        var trioSet = new System.Collections.Generic.HashSet<string>
                        {
                            symbolCombinations[i].Id0,
                            symbolCombinations[i].Id1,
                            symbolCombinations[i].Id2
                        };
                        if (trioSet.SetEquals(soloSet))
                            forbidden.Add(i);
                    }
                }
            }
        }
        else
        {
            // Fase 2: excluir combinaciones cuyos 3 IDs estén cubiertos por las selecciones actuales
            // de las WheelRingControllers (2 IDs × 3 ruedas = hasta 6 IDs únicos).
            // La combinación con la que se abrió la SoloWheel SÍ puede elegirse si está en la lista.
            if (wheelRingRefs != null)
            {
                var wheelSet = new System.Collections.Generic.HashSet<string>();
                foreach (var w in wheelRingRefs)
                {
                    if (w == null) continue;
                    if (!string.IsNullOrEmpty(w.SelectedSymbolId0)) wheelSet.Add(w.SelectedSymbolId0);
                    if (!string.IsNullOrEmpty(w.SelectedSymbolId1)) wheelSet.Add(w.SelectedSymbolId1);
                }

                for (int i = 0; i < symbolCombinations.Length; i++)
                {
                    var trioSet = new System.Collections.Generic.HashSet<string>
                    {
                        symbolCombinations[i].Id0,
                        symbolCombinations[i].Id1,
                        symbolCombinations[i].Id2
                    };
                    trioSet.RemoveWhere(s => string.IsNullOrEmpty(s));

                    // Si los 3 símbolos del trio están todos cubiertos por las ruedas, prohibir
                    if (trioSet.Count > 0 && trioSet.IsSubsetOf(wheelSet))
                        forbidden.Add(i);
                }
            }
        }

        return forbidden;
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(CycleCount))
            {
                ApplyActiveSymbolMaterials(ActiveSymbolId0, ActiveSymbolId1, ActiveSymbolId2);
                OnCycleChanged?.Invoke();
            }
            else if (change == nameof(IsPhase2))
            {
                // Al cambiar de fase, refrescar materiales para que usen el material correcto
                ApplyActiveSymbolMaterials(ActiveSymbolId0, ActiveSymbolId1, ActiveSymbolId2);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_OnClockStopped()
    {
        OnClockStopped?.Invoke();
    }

    private void ApplyActiveSymbolMaterials(string id0, string id1, string id2)
    {
        if (clockSymbolRenderers == null) return;

        // En Fase 2 se usa el material de Fase 2 (si está asignado); de lo contrario el Active normal
        Material activeMat = (IsPhase2 && symbolPhase2Material != null)
            ? symbolPhase2Material
            : symbolActiveMaterial;

        for (int i = 0; i < clockSymbolRenderers.Length; i++)
        {
            if (clockSymbolRenderers[i] == null) continue;
            string id = _symbolIds[i];
            bool isActive = id == id0 || id == id1 || id == id2;
            clockSymbolRenderers[i].material = isActive ? activeMat : symbolDefaultMaterial;
        }
    }

    public bool IsSymbolActive(string symbolId)
    {
        if (string.IsNullOrEmpty(symbolId)) return false;
        return symbolId == ActiveSymbolId0 || symbolId == ActiveSymbolId1 || symbolId == ActiveSymbolId2;
    }

    public float GetNormalizedTimeRemaining()
    {
        return CycleTimeRemaining / cycleDuration;
    }
}