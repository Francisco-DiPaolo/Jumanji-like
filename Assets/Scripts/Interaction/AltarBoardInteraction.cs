using UnityEngine;
using Fusion;

public class AltarBoardInteraction : NetworkBasicInteraction
{
    [Header("Altar Board Settings")]
    [Tooltip("La puerta que se abrirá al interactuar con este altar")]
    public DoubleDoorInteraction targetDoor;
    
    [Header("Feedback Visual y Sonoro")]
    [Tooltip("El AudioSource que reproducirá el sonido")]
    public AudioSource audioSource;
    [Tooltip("El sonido que se reproducirá al clickear")]
    public AudioClip interactSound;
    [Tooltip("El GameObject (mesh) que se apagará al clickear el board")]
    public GameObject visualMesh;

    [Networked]
    public NetworkBool IsActive { get; set; }
    
    [Networked]
    public NetworkBool HasBeenClicked { get; set; }

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        
        // Para jugadores que entran tarde, si ya fue clickeado, apagamos el mesh instantáneamente
        if (HasBeenClicked && visualMesh != null)
        {
            visualMesh.SetActive(false);
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(HasBeenClicked) && HasBeenClicked)
            {
                // Efecto visual y sonoro sincronizado para todos los jugadores
                if (audioSource != null && interactSound != null)
                {
                    audioSource.PlayOneShot(interactSound);
                }
                
                if (visualMesh != null)
                {
                    visualMesh.SetActive(false);
                }
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_ActivateBoard()
    {
        IsActive = true;
    }
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_ClickBoard()
    {
        if (HasBeenClicked) return;
        HasBeenClicked = true;
    }

    public override void Select()
    {
        if (!IsActive) return; // No se puede interactuar hasta que esté activo
        if (HasBeenClicked) return; // Evitar clickear más de una vez
        
        base.Select();
        
        if (targetDoor != null)
        {
            targetDoor.Rpc_RequestOpenDoor();
        }
        
        // Avisar a todos por red que el board fue clickeado
        Rpc_ClickBoard();
    }
}
