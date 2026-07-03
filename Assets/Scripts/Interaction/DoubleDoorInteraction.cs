using UnityEngine;
using Fusion;

public class DoubleDoorInteraction : NetworkBasicInteraction
{
    [Header("Door References")]
    [Tooltip("The left door child object (e.g., Door_medieval_Cut)")]
    public Transform leftDoor;
    [Tooltip("The right door child object (e.g., Door_Medieval_Cut (1))")]
    public Transform rightDoor;

    [Header("Animation Settings")]
    public float openAngle = 90f;
    [Tooltip("Tiempo que tarda en abrirse o cerrarse la puerta.")]
    public float openTime = 1.2f;
    
    [Tooltip("Curva de animación al abrir (easeOutCubic simula un empujón inicial que va frenando).")]
    public LeanTweenType openEase = LeanTweenType.easeOutCubic;
    
    [Tooltip("Curva de animación al cerrar.")]
    public LeanTweenType closeEase = LeanTweenType.easeOutCubic;

    [Networked]
    public NetworkBool IsOpenNet { get; set; }

    private bool _lastIsOpen = false;
    private bool isAnimating = false;

    public override void Spawned()
    {
        _lastIsOpen = IsOpenNet;
        
        // Si alguien se une tarde y la puerta ya está abierta, la ponemos en la posición correcta.
        if (IsOpenNet)
        {
            if (leftDoor != null) leftDoor.localRotation = Quaternion.Euler(0, openAngle, 0);
            if (rightDoor != null) rightDoor.localRotation = Quaternion.Euler(0, -openAngle, 0);
        }
    }

    public override void Render()
    {
        // Sincronizar el estado de la puerta con la variable en red
        if (IsOpenNet != _lastIsOpen)
        {
            _lastIsOpen = IsOpenNet;
            ToggleDoorVisuals(IsOpenNet);
        }
    }

    public override void Select()
    {
        base.Select();
        
        if (isAnimating) return;
        Rpc_RequestToggleDoor();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestToggleDoor()
    {
        // Solo el Host ejecuta la lógica real de cambio de estado
        if (isAnimating) return;
        IsOpenNet = !IsOpenNet;
    }

    private void ToggleDoorVisuals(bool open)
    {
        isAnimating = true;

        float targetAngleLeft = open ? openAngle : 0f;
        float targetAngleRight = open ? -openAngle : 0f;

        LeanTweenType currentEase = open ? openEase : closeEase;

        if (leftDoor != null)
        {
            LeanTween.rotateLocal(leftDoor.gameObject, new Vector3(0, targetAngleLeft, 0), openTime).setEase(currentEase);
        }

        if (rightDoor != null)
        {
            LeanTween.rotateLocal(rightDoor.gameObject, new Vector3(0, targetAngleRight, 0), openTime)
                     .setEase(currentEase)
                     .setOnComplete(() => isAnimating = false);
        }
        else
        {
            // Fallback if right door is missing
            LeanTween.delayedCall(openTime, () => isAnimating = false);
        }
    }
}
