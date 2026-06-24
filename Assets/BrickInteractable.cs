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

    public override void Hover()
    {
        Debug.Log("[Brick] Hover — IsInteractable: " + IsInteractable);
        base.Hover();
    }

    public override void UnHover()
    {
        Debug.Log("[Brick] UnHover");
        base.UnHover();
    }

    public override void Select()
    {
        Debug.Log("[Brick] Select called — IsInteractable: " + IsInteractable + " | isHovered: " + isHovered);
        if (!IsInteractable)
        {
            Debug.LogWarning("[Brick] Select blocked: brick is NOT interactable yet (torches not all lit)");
            return;
        }
        base.Select();
        Debug.Log("[Brick] Firing RPC_RegisterInteract");
        RPC_RegisterInteract(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RegisterInteract(PlayerRef player)
    {
        var manager = GetComponentInParent<GlobalPuzzleManager>();
        Debug.Log("[Brick] RPC received — manager found: " + (manager != null) + " | player: " + player);
        if (manager == null)
            Debug.LogError("[Brick] RPC_RegisterInteract: GlobalPuzzleManager not found in parent!");
        manager?.RegisterPlayerInteract(player);
    }
}
