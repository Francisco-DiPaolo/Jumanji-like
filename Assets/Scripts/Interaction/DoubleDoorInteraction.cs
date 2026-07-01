using UnityEngine;

public class DoubleDoorInteraction : BasicInteraction
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

    private bool isOpen = false;
    private bool isAnimating = false;

    public override void Select()
    {
        base.Select();
        
        if (isAnimating) return;
        ToggleDoor();
    }

    private void ToggleDoor()
    {
        isAnimating = true;
        isOpen = !isOpen;

        // One door opens in positive angle, the other in negative angle. 
        // Depending on the exact pivot orientation, we might need to adjust this.
        float targetAngleLeft = isOpen ? openAngle : 0f;
        float targetAngleRight = isOpen ? -openAngle : 0f;

        LeanTweenType currentEase = isOpen ? openEase : closeEase;

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
