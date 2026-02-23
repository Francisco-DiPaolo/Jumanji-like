using Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{   
    public Transform cameraPivot;
    public float mouseSensitivity = 5f;

    private float verticalLookRotation;
    public float speed = 5f;

    NetworkCharacterController controller;

    void Awake()
    {
        controller = GetComponent<NetworkCharacterController>();
        controller.orientRotationToMovement = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible= false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out PlayerInputData data))
            return;

        Vector3 move = (transform.forward * data.move.y + transform.right * data.move.x).normalized;

        controller.Move(move * speed * Runner.DeltaTime);


        float mouseX = data.look.x * mouseSensitivity;
        float mouseY = data.look.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -80f, 80f);

        cameraPivot.localRotation = Quaternion.Euler(verticalLookRotation, 0, 0);
    }
    public override void Spawned()
{
    if (!HasInputAuthority)
    {
        cameraPivot.gameObject.SetActive(false);
        var recorder = GetComponent<Recorder>();
    
    if (Object.HasInputAuthority)
    {
        recorder.TransmitEnabled = true;
    }
    else
    {
        recorder.TransmitEnabled = false;
    }
    }
}
}