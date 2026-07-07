using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class BrickInteractable : NetworkBasicInteraction
{

    [Networked] public NetworkBool IsInteractable { get; set; }
    [Networked] public int PressCount { get; set; }

    ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void FixedUpdateNetwork()
    {
        if (!isHovered) return;
        // El ladrillo siempre acepta hover/click — la validación la decide el PuzzleManager
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
        // Bloquear si la animación del brick está en curso
        BrickSlide slide = GetComponentInChildren<BrickSlide>();
        if (slide != null && slide.IsMoving)
        {
            Debug.LogWarning("[Brick] Select blocked: Brick is currently sliding");
            return;
        }

        // La animación se dispara siempre al hacer click
        slide?.StartSlide();

        base.Select();
        Debug.Log("[Brick] Firing RPC_ValidateAnswer");
        RPC_ValidateAnswer(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_ValidateAnswer(PlayerRef player)
    {
        // Incrementar contador para que todos los clientes reproduzcan la animación
        PressCount++;

        var manager = GetComponentInParent<GlobalPuzzleManager>();
        Debug.Log("[Brick] RPC received — manager found: " + (manager != null) + " | player: " + player);
        if (manager == null)
        {
            Debug.LogError("[Brick] RPC_ValidateAnswer: GlobalPuzzleManager not found in parent!");
            return;
        }

        // El manager decide: si todas las antorchas están en verde → correcto,
        // si no → resetea la secuencia desde el principio.
        manager.ValidateAnswer(player);
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(PressCount))
            {
                BrickSlide slide = GetComponentInChildren<BrickSlide>();
                if (slide != null)
                {
                    slide.StartSlide();
                }
            }
        }
    }
}
