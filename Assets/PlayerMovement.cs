using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] float walkSpeed = 4.0f;
    [SerializeField] float sprintSpeed = 6.0f;
    [SerializeField] float speedChangeRate = 10.0f;

    [Header("Jump & Gravity")]
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float gravity = -15.0f;

    [Header("Camera Settings")]
    [SerializeField] Transform cameraPivot;
    [SerializeField] float rotationSpeed = 1.0f;
    [SerializeField] float topClamp = 90.0f;
    [SerializeField] float bottomClamp = -90.0f;

    [Header("Stamina Settings")]
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float staminaDrainRate = 20f;
    [SerializeField] float staminaRegenRate = 15f;

    [Networked] public float CurrentStamina { get; set; }
    public System.Action<float, float> OnStaminaChanged; // actual, max

    [Networked] float VerticalLook { get; set; }

    NetworkCharacterController controller;

    public override void Spawned()
    {
        CurrentStamina = maxStamina;
        controller = GetComponent<NetworkCharacterController>();
        controller.orientRotationToMovement = false;
        
        // Match the impulse calculation from FirstPersonController.cs
        controller.jumpImpulse = Mathf.Sqrt(jumpHeight * -2f * gravity);
        controller.gravity = gravity;

        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

        // Sprint & Stamina logic
        bool isSprinting = data.buttons.IsSet(InputButton.Sprint);
        
        if (isSprinting && data.move.sqrMagnitude > 0.01f && CurrentStamina > 0)
        {
            CurrentStamina -= staminaDrainRate * Runner.DeltaTime;
            if (CurrentStamina < 0) CurrentStamina = 0;
        }
        else
        {
            if (CurrentStamina < maxStamina)
            {
                CurrentStamina += staminaRegenRate * Runner.DeltaTime;
                if (CurrentStamina > maxStamina) CurrentStamina = maxStamina;
            }
        }

        if (HasInputAuthority)
        {
            OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        bool canRun = isSprinting && CurrentStamina > 0;
        controller.maxSpeed = canRun ? sprintSpeed : walkSpeed;
        controller.acceleration = speedChangeRate;

        Vector3 moveDirection = (transform.forward * data.move.y + transform.right * data.move.x).normalized;

        if (data.buttons.IsSet(InputButton.Jump))
            controller.Jump();

        controller.Move(moveDirection * Runner.DeltaTime);

        // Rotation logic
        float mouseX = data.look.x * rotationSpeed;
        float mouseY = data.look.y * rotationSpeed;

        transform.Rotate(Vector3.up * mouseX);

        VerticalLook += mouseY; // Note: Reverting to += if the teammate used that, or keeping standard FPS -=
        VerticalLook = Mathf.Clamp(VerticalLook, bottomClamp, topClamp);
    }

    public override void Render()
    {
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(VerticalLook, 0, 0);
    }
}