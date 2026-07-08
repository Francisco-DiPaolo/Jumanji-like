using UnityEngine;
using UnityEngine.Events;
using Fusion;

public class BoardCameraOverride : MonoBehaviour
{
    [SerializeField] private float transitionDuration = 1.0f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeInOutQuad;
    
    public UnityEvent onCameraReachedTarget;

    private PlayerMovement activePlayer;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    
    private bool isViewActive = false;

    public void ActivateView()
    {
        if (isViewActive) return;

        // Find the local player (the one with Input Authority)
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.HasInputAuthority)
            {
                activePlayer = p;
                break;
            }
        }

        if (activePlayer == null || activePlayer.CameraPivot == null) return;

        isViewActive = true;
        Transform camPivot = activePlayer.CameraPivot;

        // Save original local transform info
        originalParent = camPivot.parent;
        originalLocalPosition = camPivot.localPosition;
        originalLocalRotation = camPivot.localRotation;

        // Take control
        activePlayer.CameraOverrideActive = true;

        // Unparent but keep world position
        camPivot.SetParent(null, true);

        // Cancel any active tweens on the camera pivot
        LeanTween.cancel(camPivot.gameObject);

        // Tween to the board view (world space)
        LeanTween.move(camPivot.gameObject, transform.position, transitionDuration)
            .setEase(easeType)
            .setOnComplete(() => {
                onCameraReachedTarget?.Invoke();
            });

        // Use Quaternion.Slerp instead of LeanTween.rotate to avoid Euler wrapping issues (e.g. camera looking back for a split second)
        Quaternion startRot = camPivot.rotation;
        Quaternion targetRot = transform.rotation;
        LeanTween.value(camPivot.gameObject, 0f, 1f, transitionDuration)
            .setEase(easeType)
            .setOnUpdate((float t) => {
                camPivot.rotation = Quaternion.Slerp(startRot, targetRot, t);
            });

        // (Cursor remains locked so player doesn't lose mouse control)
    }

    public void DeactivateView()
    {
        if (!isViewActive || activePlayer == null || activePlayer.CameraPivot == null) return;

        isViewActive = false;
        Transform camPivot = activePlayer.CameraPivot;

        // Cancel active tweens
        LeanTween.cancel(camPivot.gameObject);

        // Reparent to the original parent but keep current world position
        camPivot.SetParent(originalParent, true);

        // Tween back to the saved local position and rotation
        LeanTween.moveLocal(camPivot.gameObject, originalLocalPosition, transitionDuration).setEase(easeType);
        
        // Smoothly tween rotation using LeanTween.value for Quaternions to avoid euler wrapping issues
        Quaternion startRot = camPivot.localRotation;
        Quaternion targetRot = originalLocalRotation;
        
        LeanTween.value(camPivot.gameObject, 0f, 1f, transitionDuration)
            .setEase(easeType)
            .setOnUpdate((float t) => {
                camPivot.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            })
            .setOnComplete(() => {
                // Restore control completely
                activePlayer.CameraOverrideActive = false;
                
                // Re-lock cursor
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            });
    }

    private void Update()
    {
        if (isViewActive && Input.GetKeyDown(KeyCode.Escape))
        {
            DeactivateView();
        }
    }
}
