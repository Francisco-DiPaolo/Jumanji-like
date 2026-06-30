using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] float walkSpeed = 4.0f;
    [SerializeField] float sprintSpeed = 6.0f;
    [SerializeField] float acceleration = 15.0f;
    [SerializeField] float deceleration = 20.0f;

    [Header("Jump & Gravity")]
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float gravity = -15.0f;
    [SerializeField] float groundSnapForce = 10.0f; // Evita salir volando en rampas
    [SerializeField, Range(0f, 1f)] float airControl = 0.3f; // Control de dirección en el aire
    [SerializeField] LayerMask groundMask = 1; // Default layer. ¡Asignar en inspector!
    [SerializeField] float groundCheckRadius = 0.3f;

    [Header("Camera Settings")]
    [SerializeField] Transform cameraPivot;
    public float mouseSensitivityX = 1.0f;
    public float mouseSensitivityY = 1.0f;
    [SerializeField] bool invertY = false;
    [SerializeField] float topClamp = 85.0f;
    [SerializeField] float bottomClamp = -85.0f;
    
    [HideInInspector] public bool CameraOverrideActive = false;
    public Transform CameraPivot => cameraPivot;

    [Header("Stamina Settings")]
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float staminaDrainRate = 20f;
    [SerializeField] float staminaRegenRate = 15f;

    [Networked] public float CurrentStamina { get; set; }
    public System.Action<float, float> OnStaminaChanged; // actual, max

    [Networked] float VerticalLook { get; set; }
    [Networked] Vector3 CurrentVelocity { get; set; }
    [Networked] NetworkBool IsGrounded { get; set; }

    CharacterController unityController;

    public override void Spawned()
    {
        CurrentStamina = maxStamina;
        unityController = GetComponent<CharacterController>();

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

        HandleStamina(data);
        HandleCamera(data);
        HandleMovement(data);
    }

    private void HandleStamina(PlayerInputData data)
    {
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
    }

    private void HandleCamera(PlayerInputData data)
    {
        if (CameraOverrideActive) return;
        
        float mouseX = data.look.x * mouseSensitivityX;
        float mouseY = data.look.y * mouseSensitivityY;

        transform.Rotate(Vector3.up * mouseX);

        if (invertY)
        {
            VerticalLook += mouseY; 
        }
        else
        {
            VerticalLook -= mouseY; 
        }

        VerticalLook = Mathf.Clamp(VerticalLook, bottomClamp, topClamp);
    }

    private void HandleMovement(PlayerInputData data)
    {
        // 1. Ground Check más preciso usando CheckSphere en la base
        Vector3 spherePos = transform.position + unityController.center + Vector3.down * (unityController.height / 2f - groundCheckRadius + 0.05f);
        IsGrounded = Physics.CheckSphere(spherePos, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        // 2. Sprint & Speed logic
        bool isSprinting = data.buttons.IsSet(InputButton.Sprint) && CurrentStamina > 0;
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 moveDirection = (transform.forward * data.move.y + transform.right * data.move.x).normalized;
        Vector3 targetVelocity = moveDirection * targetSpeed;

        // 3. Smooth Acceleration / Deceleration (Inercia lineal fluida)
        Vector3 horizontalVel = new Vector3(CurrentVelocity.x, 0, CurrentVelocity.z);
        float speedRate = (targetVelocity.sqrMagnitude > 0) ? acceleration : deceleration;
        
        if (!IsGrounded)
        {
            speedRate *= airControl; // Reducir maniobrabilidad en el aire
        }

        horizontalVel = Vector3.Lerp(horizontalVel, targetVelocity, speedRate * Runner.DeltaTime);

        // 4. Gravedad, Salto y Ground Snap
        float verticalVel = CurrentVelocity.y;

        if (IsGrounded)
        {
            if (verticalVel < 0.0f)
            {
                // Fuerza hacia abajo constante para no salir volando en las rampas/escaleras
                verticalVel = -groundSnapForce; 
            }

            if (data.buttons.IsSet(InputButton.Jump))
            {
                // Cálculo físico exacto de impulso basado en gravedad
                verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            // Gravedad en el aire
            verticalVel += gravity * Runner.DeltaTime;
        }

        // 5. Aplicar Movimiento
        Vector3 finalVelocity = new Vector3(horizontalVel.x, verticalVel, horizontalVel.z);
        
        unityController.Move(finalVelocity * Runner.DeltaTime);

        // Guardar la velocidad teórica para que los muros actúen como mantequilla (0 Fricción real)
        CurrentVelocity = finalVelocity;
        
        // Corrección si chocamos con el techo
        if ((unityController.collisionFlags & CollisionFlags.Above) != 0 && CurrentVelocity.y > 0)
        {
            CurrentVelocity = new Vector3(CurrentVelocity.x, 0, CurrentVelocity.z);
        }
    }

    public override void Render()
    {
        if (CameraOverrideActive) return;
        
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(VerticalLook, 0, 0);
    }
}