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

    [Header("Events")]
    public UnityEvent OnCycleChanged;
    public UnityEvent OnClockStopped;

    [Networked] public float CycleTimeRemaining { get; private set; }
    [Networked] public NetworkBool IsRunning { get; set; }

    [Networked, Capacity(3)]
    private NetworkArray<NetworkString<_32>> ActiveSymbolIdsNetworked => default;

    private ChangeDetector _changeDetector;
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
            if (autoStart)
            {
                IsRunning = true;
                PickNewSymbols();
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

        int index = UnityEngine.Random.Range(0, symbolCombinations.Length);
        SymbolTrio trio = symbolCombinations[index];

        ActiveSymbolIdsNetworked.Set(0, trio.Id0);
        ActiveSymbolIdsNetworked.Set(1, trio.Id1);
        ActiveSymbolIdsNetworked.Set(2, trio.Id2);

        Debug.Log($"[CentralClock] Nueva combinación [{index}]: '{trio.Id0}', '{trio.Id1}', '{trio.Id2}'");
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(ActiveSymbolIdsNetworked))
            {
                ApplyActiveSymbolMaterials(ActiveSymbolId0, ActiveSymbolId1, ActiveSymbolId2);
                OnCycleChanged?.Invoke();
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

        for (int i = 0; i < clockSymbolRenderers.Length; i++)
        {
            if (clockSymbolRenderers[i] == null) continue;
            string id = _symbolIds[i];
            bool isActive = id == id0 || id == id1 || id == id2;
            clockSymbolRenderers[i].material = isActive ? symbolActiveMaterial : symbolDefaultMaterial;
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