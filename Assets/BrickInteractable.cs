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
        // Bloquear si la animación del brick está en curso (independiente de todo)
        BrickSlide slide = GetComponentInChildren<BrickSlide>();
        if (slide != null && slide.IsMoving)
        {
            Debug.LogWarning("[Brick] Select blocked: Brick is currently sliding");
            return;
        }

        // La animación SIEMPRE se dispara al hacer click, sin importar si el puzzle está activo
        slide?.StartSlide();

        Debug.Log("[Brick] Select called — IsInteractable: " + IsInteractable);

        // Solo registrar en el puzzle si el brick está habilitado (todas las antorchas prendidas)
        if (!IsInteractable)
        {
            Debug.LogWarning("[Brick] Click registrado pero brick no habilitado (antorchas no completas)");
            return;
        }

        base.Select();
        Debug.Log("[Brick] Firing RPC_RegisterInteract");
        RPC_RegisterInteract(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RegisterInteract(PlayerRef player)
    {
        // Siempre incrementamos el contador para que TODOS los clientes
        // reproduzcan la animación (correcto o incorrecto)
        PressCount++;

        var manager = GetComponentInParent<GlobalPuzzleManager>();
        Debug.Log("[Brick] RPC received — manager found: " + (manager != null) + " | player: " + player);
        if (manager == null)
            Debug.LogError("[Brick] RPC_RegisterInteract: GlobalPuzzleManager not found in parent!");
        manager?.RegisterPlayerInteract(player);
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
