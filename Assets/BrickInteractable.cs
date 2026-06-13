using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BrickInteractable : NetworkBasicInteraction
{

    [Networked] public NetworkBool IsInteractable { get; set; }

    ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void FixedUpdateNetwork()
    {
        if (!isHovered) return;
        if (!IsInteractable) return;
        base.FixedUpdateNetwork();
    }

    public override void Select()
    {
        base.Select();
        RPC_RegisterInteract();
        Debug.Log("Select");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RegisterInteract()
    {
        var manager = GetComponentInParent<GlobalPuzzleManager>();
        manager?.RegisterPlayerInteract(Object.InputAuthority);
    }
}
