using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] Transform cameraPivot;
    [SerializeField] float speed = 6f;
    [SerializeField] float acceleration = 12f;
    [SerializeField] float jumpForce = 8f;
    [SerializeField] float mouseSensitivity = 5f;

    [Networked] float VerticalLook { get; set; }

    NetworkCharacterController controller;

    public override void Spawned()
    {
        controller = GetComponent<NetworkCharacterController>();
        controller.orientRotationToMovement = false;
        controller.maxSpeed = speed;
        controller.acceleration = acceleration;
        controller.jumpImpulse = jumpForce;

        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var lobbyCamera = GameObject.Find("LobbyCamera");
            if (lobbyCamera != null)
                lobbyCamera.SetActive(false);
        }
        else
        {
            if (cameraPivot != null)
                cameraPivot.gameObject.SetActive(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out PlayerInputData data))
            return;

        Vector3 moveDirection = (transform.forward * data.move.y + transform.right * data.move.x).normalized;

        if (data.buttons.IsSet(InputButton.Jump))
            controller.Jump();

        controller.Move(moveDirection * Runner.DeltaTime);

        float mouseX = data.look.x * mouseSensitivity;
        float mouseY = data.look.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        VerticalLook -= mouseY;
        VerticalLook = Mathf.Clamp(VerticalLook, -80f, 80f);
    }

    public override void Render()
    {
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(VerticalLook, 0, 0);
    }
}