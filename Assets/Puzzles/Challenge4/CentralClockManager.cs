using System;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class CentralClockManager : NetworkBehaviour
{
    [Header("Clock Settings")]
    [SerializeField] private float cycleDuration = 15f;
    [SerializeField] private int totalSymbols = 12;
    [SerializeField] private int activeSymbolCount = 3;

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
    public NetworkArray<int> ActiveSymbolIndices => default;

    private ChangeDetector _changeDetector;

    public event Action<NetworkArray<int>> OnActiveSymbolsChangedLocally;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            CycleTimeRemaining = cycleDuration;
            IsRunning = false;
        }
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

    public void SetActiveSymbols(int sym0, int sym1, int sym2)
    {
        if (!HasStateAuthority) return;
        ActiveSymbolIndices.Set(0, sym0);
        ActiveSymbolIndices.Set(1, sym1);
        ActiveSymbolIndices.Set(2, sym2);
        Rpc_BroadcastCycleChanged(sym0, sym1, sym2);
    }

    public void StopClock()
    {
        if (!HasStateAuthority) return;
        IsRunning = false;
        Rpc_OnClockStopped();
    }

    private void PickNewSymbols()
    {
        var picked = new System.Collections.Generic.HashSet<int>();
        while (picked.Count < activeSymbolCount)
            picked.Add(UnityEngine.Random.Range(0, totalSymbols));

        int i = 0;
        foreach (int idx in picked)
        {
            ActiveSymbolIndices.Set(i, idx);
            i++;
        }

        Rpc_BroadcastCycleChanged(ActiveSymbolIndices[0], ActiveSymbolIndices[1], ActiveSymbolIndices[2]);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_BroadcastCycleChanged(int sym0, int sym1, int sym2)
    {
        ApplyActiveSymbolMaterials(sym0, sym1, sym2);
        OnCycleChanged?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_OnClockStopped()
    {
        OnClockStopped?.Invoke();
    }

    private void ApplyActiveSymbolMaterials(int s0, int s1, int s2)
    {
        if (clockSymbolRenderers == null) return;

        for (int i = 0; i < clockSymbolRenderers.Length; i++)
        {
            if (clockSymbolRenderers[i] == null) continue;
            bool isActive = i == s0 || i == s1 || i == s2;
            clockSymbolRenderers[i].material = isActive ? symbolActiveMaterial : symbolDefaultMaterial;
        }
    }

    public bool IsSymbolActive(int symbolIndex)
    {
        for (int i = 0; i < activeSymbolCount; i++)
        {
            if (ActiveSymbolIndices[i] == symbolIndex) return true;
        }
        return false;
    }

    public float GetNormalizedTimeRemaining()
    {
        return CycleTimeRemaining / cycleDuration;
    }
}
