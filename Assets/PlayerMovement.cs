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

    [Header("Animation Settings")]
    [SerializeField] float animatorDampTime = 0.1f; // Suavidad del Lerp de Speed

    [Header("Stamina Settings")]
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float staminaDrainRate = 20f;
    [SerializeField] float staminaRegenRate = 15f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hurtSound;
    [Tooltip("Volumen específico del sonido de daño (0 a 1).")]
    [SerializeField, Range(0f, 1f)] private float hurtSoundVolume = 1.0f;
    [Tooltip("Tiempo mínimo en segundos entre sonidos de daño.")]
    [SerializeField] private float hurtSoundCooldown = 1.5f;

    public float CurrentStamina { get; set; }
    public System.Action<float, float> OnStaminaChanged; // actual, max

    [Networked] float VerticalLook { get; set; }
    [Networked] Vector3 CurrentVelocity { get; set; }
    [Networked] NetworkBool IsGrounded { get; set; }
    [Networked] public NetworkBool IsReadyAtBoard { get; set; }

    /// <summary>True cuando este jugador es el primero que interactuó con el tablero y debe llevar el casco Fish_Bowl_2.</summary>
    [Networked] public NetworkBool HasFishBowl { get; set; }

    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetReadyAtBoard(NetworkBool isReady)
    {
        IsReadyAtBoard = isReady;
    }

    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetHasFishBowl(NetworkBool hasHelmet)
    {
        HasFishBowl = hasHelmet;
    }

    CharacterController unityController;
    Animator animator;
    Vector2 lastMoveInput;        // Input de dirección para Render()
    bool lastJumpPressed;         // Si se apretó Jump en el último FixedUpdate (rising edge únicamente)
    bool wasJumpHeld;             // Estado anterior del botón Jump para detectar rising edge
    bool lastWeavePressed;        // Si se apretó Weave en el último FixedUpdate
    bool localIsGrounded;         // Copia local de IsGrounded para el Animator (sin lag de red)
    GameObject fishBowlObject;    // Referencia al casco Fish_Bowl_2 en el rig
    bool lastFishBowlState;       // Estado anterior de HasFishBowl para evitar llamadas redundantes

    public override void Spawned()
    {
        CurrentStamina = maxStamina;
        unityController = GetComponent<CharacterController>();

        // Busca el Animator en el hijo llamado Character_Model
        Transform model = transform.Find("Character_Model");
        if (model != null)
            animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Inicializar el Animator en estado Idle/Grounded para evitar
        // que el primer frame de física lo ponga en el estado de salto
        if (animator != null)
        {
            animator.SetBool("isGrounded", true);
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("PositionX", 0f);
            animator.SetFloat("PositionY", 0f);
            // Limpiar triggers que pudieran haber quedado sucios del frame anterior
            animator.ResetTrigger("Weave");
            animator.ResetTrigger("Interact");
        }

        // Buscar el casco Fish_Bowl_2 en toda la jerarquía del prefab (está dentro del rig)
        Transform fishBowlTransform = FindInHierarchy(transform, "Fish_Bowl_2");
        if (fishBowlTransform != null)
        {
            fishBowlObject = fishBowlTransform.gameObject;
            fishBowlObject.SetActive(false); // Asegurar que inicie desactivado
        }

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

    /// <summary>Búsqueda recursiva de un Transform por nombre en toda la jerarquía.</summary>
    private Transform FindInHierarchy(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindInHierarchy(child, name);
            if (found != null) return found;
        }
        return null;
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
        // Al no ser [Networked], evitamos que la resimulación modifique la estamina múltiples veces
        if (!Runner.IsForward) return;

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
        // Freeze movement if game is over or camera is overridden
        bool freezeMovement = CameraOverrideActive || (SharedHealthSystem.Instance != null && SharedHealthSystem.Instance.isGameOver);

        if (freezeMovement)
        {
            data.move = Vector2.zero;
            data.buttons = default;
            
            // Si está congelado, asegurarse de que no haya input residual para el animador
            lastMoveInput = Vector2.zero;
        }
        else
        {
            // Guardar input de dirección para Render() / Animator
            lastMoveInput = data.move;
        }
        
        // Rising edge del Jump: solo true en el frame que se APRIETA, no mientras se sostiene
        bool jumpHeld = data.buttons.IsSet(InputButton.Jump);
        lastJumpPressed = jumpHeld && !wasJumpHeld;
        wasJumpHeld = jumpHeld;
        
        lastWeavePressed = data.buttons.IsSet(InputButton.Weave);

        // 1. Ground Check más preciso usando OverlapSphere que ignora al propio jugador
        Vector3 spherePos = transform.position + unityController.center + Vector3.down * (unityController.height / 2f - groundCheckRadius + 0.05f);
        
        Collider[] hitColliders = new Collider[5];
        int numHits = Physics.OverlapSphereNonAlloc(spherePos, groundCheckRadius, hitColliders, groundMask, QueryTriggerInteraction.Ignore);
        
        IsGrounded = false;
        for (int i = 0; i < numHits; i++)
        {
            if (hitColliders[i].gameObject != gameObject) // Ignorar el propio CharacterController
            {
                IsGrounded = true;
                break;
            }
        }
        
        localIsGrounded = IsGrounded; // Copia local sin lag de red para el Animator

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
        if (!CameraOverrideActive && cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(VerticalLook, 0, 0);

        HandleAnimator();

        // Activar/desactivar el casco Fish_Bowl_2 según el estado networked (solo cambia cuando hay diferencia)
        bool currentFishBowl = HasFishBowl;
        if (fishBowlObject != null && lastFishBowlState != currentFishBowl)
        {
            fishBowlObject.SetActive(currentFishBowl);
            lastFishBowlState = currentFishBowl;

            // Si este jugador se puso el casco y es el jugador local, forzar voz Underwater
            if (currentFishBowl && HasInputAuthority)
            {
                VoiceEffectController voiceEffect = GetComponent<VoiceEffectController>();
                if (voiceEffect != null)
                {
                    voiceEffect.RPC_SetVoiceMode(VoiceEffectController.VoiceMode.Underwater);
                }
            }
        }
    }

    private void HandleAnimator()
    {
        if (animator == null) return;

        bool isMoving = lastMoveInput.sqrMagnitude > 0.01f;
        bool isSprinting = isMoving && (CurrentVelocity.magnitude >= sprintSpeed * 0.8f);

        // Speed: 0 = Idle, 1 = Walk, 2 = Run
        float targetAnimSpeed = 0f;
        if (isMoving) targetAnimSpeed = isSprinting ? 2f : 1f;

        float currentSpeed = animator.GetFloat("Speed");
        float smoothedSpeed = Mathf.Lerp(currentSpeed, targetAnimSpeed, animatorDampTime / Time.deltaTime * Runner.DeltaTime);
        
        // Filtro Deadzone para limpiar valores de notación científica extremadamente chicos
        if (smoothedSpeed < 0.01f)
        {
            smoothedSpeed = 0f;
        }
        
        animator.SetFloat("Speed", smoothedSpeed);

        // Dirección para los Blend Trees 2D anidados
        animator.SetFloat("PositionX", lastMoveInput.x);
        animator.SetFloat("PositionY", lastMoveInput.y);

        // IsGrounded (bool local, sin lag de red) — actualizar siempre
        animator.SetBool("isGrounded", localIsGrounded);

        // Weave Trigger — G key
        if (lastWeavePressed)
            animator.SetTrigger("Weave");
    }

    /// <summary>
    /// Llamar desde Raycast.cs cuando el jugador local interactúa con un objeto.
    /// </summary>
    public void TriggerInteractAnimation()
    {
        if (animator != null)
            animator.SetTrigger("Interact");
    }

    /// <summary>
    /// Llamar para empujar al jugador (ej: trampas de pinchos).
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        // Cancelar velocidad vertical negativa para que el empuje hacia arriba funcione bien
        if (CurrentVelocity.y < 0)
        {
            CurrentVelocity = new Vector3(CurrentVelocity.x, 0, CurrentVelocity.z);
        }
        
        CurrentVelocity += force;
    }

    private float lastHurtSoundTime = -999f;

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void Rpc_PlayHurtSound()
    {
        // Limitar la frecuencia del sonido basado en la configuración del inspector
        if (Time.time - lastHurtSoundTime < hurtSoundCooldown) return;
        lastHurtSoundTime = Time.time;

        if (audioSource != null && hurtSound != null)
        {
            audioSource.PlayOneShot(hurtSound, hurtSoundVolume);
        }
    }
}