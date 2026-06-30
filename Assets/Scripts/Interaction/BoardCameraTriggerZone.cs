using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BoardCameraTriggerZone : MonoBehaviour
{
    [SerializeField] private BoardCameraOverride boardCamera;

    private void OnTriggerExit(Collider other)
    {
        if (boardCamera == null) return;

        // Check if the object exiting is the player
        if (other.CompareTag("Player"))
        {
            PlayerMovement pm = other.GetComponentInParent<PlayerMovement>();
            if (pm != null && pm.HasInputAuthority && pm.CameraOverrideActive)
            {
                boardCamera.DeactivateView();
            }
        }
    }
}
