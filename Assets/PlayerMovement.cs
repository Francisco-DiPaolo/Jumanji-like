using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour, IBeforeAllTicks, IAfterAllTicks
{
    public static PlayerMovement Local { get; private set; }

    public float GetLocalYaw() => localYaw;
    [Header("Movement Settings")]
    [SerializeField] float walkSpeed = 4.0f;
    [SerializeField] float sprintSpeed = 6.0f;
    [SerializeField] float acceleration = 15.0f;
    [SerializeField] float deceleration = 20.0f;

    [Header("Jump & Gravity")]
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float jumpCooldown = 0.25f;
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
    [Tooltip("Grupo SFX del AudioMixer para asignarlo por código")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup sfxMixerGroup;

    [Networked] public float CurrentStamina { get; set; }
    public System.Action<float, float> OnStaminaChanged;

    // Rotación de cámara: LOCAL al cliente dueño, nunca networked.
    // Se lee en Update() y se aplica en LateUpdate() para máxima suavidad visual.
    float localYaw;           // Rotación horizontal del cuerpo (grados acumulados)
    float localPitch;         // Rotación vertical de la cámara (grados acumulados)
    float accumulatedMouseX;  // Delta de mouse acumulado entre FixedUpdates
    float accumulatedMouseY;  // Delta de mouse acumulado entre FixedUpdates

    [Networked] Vector3 CurrentVelocity { get; set; }
    [Networked] NetworkBool IsGrounded { get; set; }
    [Networked] TickTimer jumpCooldownTimer { get; set; }
    [Networked] NetworkBool WasJumpHeld { get; set; }
    [Networked] public NetworkBool IsReadyAtBoard { get; set; }

    // Posición y rotación sincronizadas para restaurar el CharacterController
    // en cada batch de ticks (BeforeAllTicks/AfterAllTicks) y para interpolar
    // proxies remotos en Render(). Reemplaza la función de NetworkCharacterController.
    [Networked] Vector3 NetPosition { get; set; }
    [Networked] Quaternion NetRotation { get; set; }

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
    bool lastWeavePressed;        // Si se apretó Weave en el último FixedUpdate
    GameObject fishBowlObject;    // Referencia al casco Fish_Bowl_2 en el rig
    bool lastFishBowlState;       // Estado anterior de HasFishBowl para evitar llamadas redundantes

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            CurrentStamina = maxStamina;
        }
        unityController = GetComponent<CharacterController>();

        // Asignar el grupo SFX al AudioSource por código si están configurados
        if (audioSource != null && sfxMixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = sfxMixerGroup;
        }

        if (HasInputAuthority)
        {
            Local = this;
            
            // Cargar la sensibilidad guardada en las opciones
            float savedSensitivity = PlayerPrefs.GetFloat("MouseSensitivityPref", 1.0f);
            mouseSensitivityX = savedSensitivity;
            mouseSensitivityY = savedSensitivity;
        }

        // Inicializar Yaw y Pitch desde el transform actual
        localYaw   = transform.eulerAngles.y;
        localPitch = 0f;
        accumulatedMouseX = 0f;
        accumulatedMouseY = 0f;

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

        // Inicializar el buffer de red con la posición/rotación de spawn.
        // Necesario para que BeforeAllTicks tenga valores válidos desde el primer tick.
        // Además, deshabilitar/habilitar el CC limpia su estado interno (igual que
        // hacía NetworkCharacterController.Spawned() para evitar el reset a 0,0,0).
        if (HasStateAuthority)
        {
            unityController.enabled = false;
            unityController.enabled = true;
            NetPosition = transform.position;
            NetRotation = transform.rotation;
        }
    }

    /// <summary>
    /// Llamado por Fusion ANTES de simular el batch de ticks de este frame.
    /// Restaura la posición del CharacterController desde el buffer de red para que
    /// la simulación (y la re-simulación de client-side prediction) parta
    /// del estado correcto confirmado por el servidor.
    /// </summary>
    void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount)
    {
        if (unityController == null) return;

        unityController.enabled = false;
        transform.SetPositionAndRotation(NetPosition, NetRotation);
        unityController.enabled = true;
    }

    /// <summary>
    /// Llamado por Fusion DESPUÉS de simular el batch de ticks de este frame.
    /// Guarda la posición final en el buffer de red para que el próximo BeforeAllTicks
    /// pueda restaurarla correctamente.
    /// </summary>
    void IAfterAllTicks.AfterAllTicks(bool resimulation, int tickCount)
    {
        NetPosition = transform.position;
        NetRotation = transform.rotation;
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

    /// <summary>
    /// Update se ejecuta cada frame de render.
    /// SOLO se usa para acumular el delta del mouse del cliente local.
    /// La rotación real se aplica en LateUpdate para sincronizarse con el render.
    /// </summary>
    private void Update()
    {
        if (!HasInputAuthority) return;
        if (CameraOverrideActive)  return;

        accumulatedMouseX += Input.GetAxisRaw("Mouse X") * mouseSensitivityX;
        accumulatedMouseY += Input.GetAxisRaw("Mouse Y") * mouseSensitivityY;
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out PlayerInputData data))
            return;

        // Nota: la cámara ya NO se maneja aquí.
        // FixedUpdateNetwork solo controla el movimiento físico.
        HandleMovement(data);
    }

    /// <summary>
    /// LateUpdate se ejecuta al final de cada frame de render, DESPUÉS de Update.
    /// Es el lugar correcto para aplicar rotaciones de cámara: garantiza que la
    /// física ya terminó y que no hay un frame de retraso visual.
    /// </summary>
    private void LateUpdate()
    {
        if (HasInputAuthority && !CameraOverrideActive)
        {
            // Consumir el delta acumulado desde el último frame
            localYaw   += accumulatedMouseX;
            accumulatedMouseX = 0f;

            if (invertY)
                localPitch += accumulatedMouseY;
            else
                localPitch -= accumulatedMouseY;
            accumulatedMouseY = 0f;

            localPitch = Mathf.Clamp(localPitch, bottomClamp, topClamp);

            // Aplicar rotación del cuerpo (yaw): eje Y global, sin influencia del pitch
            transform.rotation = Quaternion.Euler(0f, localYaw, 0f);
        }

        // Pivot de cámara (pitch): solo para el cliente dueño con su cámara activa
        if (HasInputAuthority && cameraPivot != null && !CameraOverrideActive)
        {
            cameraPivot.localRotation = Quaternion.Euler(localPitch, 0f, 0f);
        }

        HandleAnimator();

        if (HasInputAuthority)
        {
            OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        }

        // Sincronizar casco FishBowl
        bool currentFishBowl = HasFishBowl;
        if (fishBowlObject != null && lastFishBowlState != currentFishBowl)
        {
            fishBowlObject.SetActive(currentFishBowl);
            lastFishBowlState = currentFishBowl;
        }
    }



    private void HandleMovement(PlayerInputData data)
    {
        // Fusion llama a CopyToEngine() antes de FixedUpdateNetwork, lo que resetea
        // transform.rotation a la rotación guardada en red. Re-aplicamos el yaw del input
        // aquí para que transform.forward/.right apunten en la dirección visual correcta
        // tanto en el cliente como en el servidor durante la simulación de este tick.
        transform.rotation = Quaternion.Euler(0f, data.yaw, 0f);

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
        bool jumpPressed = jumpHeld && !WasJumpHeld;
        WasJumpHeld = jumpHeld;
        
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

        bool sprintKey = data.buttons.IsSet(InputButton.Sprint);
        bool isMoving = data.move.sqrMagnitude > 0.01f;
        bool isSprinting = false;

        if (sprintKey && isMoving && CurrentStamina > 0)
        {
            isSprinting = true;
            CurrentStamina -= staminaDrainRate * Runner.DeltaTime;
            if (CurrentStamina < 0)
            {
                CurrentStamina = 0;
            }
        }
        else
        {
            if (CurrentStamina < maxStamina)
            {
                CurrentStamina += staminaRegenRate * Runner.DeltaTime;
                if (CurrentStamina > maxStamina)
                {
                    CurrentStamina = maxStamina;
                }
            }
        }

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

            if (jumpPressed && jumpCooldownTimer.ExpiredOrNotRunning(Runner))
            {
                // Cálculo físico exacto de impulso basado en gravedad
                verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpCooldownTimer = TickTimer.CreateFromSeconds(Runner, jumpCooldown);
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

        // AfterAllTicks también guarda estos valores, pero lo hacemos aquí también
        // por si HandleMovement se llama múltiples veces en el mismo batch.
        NetPosition = transform.position;
        NetRotation = transform.rotation;
    }

    public override void Render()
    {
        // Para proxies remotos (ni Input Authority ni State Authority):
        // interpolamos suavemente posición y rotación entre snapshots de red.
        // Quaternion evita el artifact de wrap-around que tendría un float de ángulo.
        if (!HasInputAuthority && !HasStateAuthority)
        {
            var interpolator = new NetworkBehaviourBufferInterpolator(this);
            transform.position = interpolator.Vector3(nameof(NetPosition));
            transform.rotation = interpolator.Quaternion(nameof(NetRotation));
        }
    }


    private void HandleAnimator()
    {
        if (animator == null) return;

        Vector3 horizontalVelocity = new Vector3(CurrentVelocity.x, 0, CurrentVelocity.z);
        bool isMoving = horizontalVelocity.sqrMagnitude > 0.01f;
        bool isSprinting = isMoving && (CurrentVelocity.magnitude >= sprintSpeed * 0.8f);

        float targetAnimSpeed = 0f;
        if (isMoving)
        {
            targetAnimSpeed = isSprinting ? 2f : 1f;
        }

        float currentSpeed = animator.GetFloat("Speed");
        float smoothedSpeed = Mathf.Lerp(currentSpeed, targetAnimSpeed, animatorDampTime / Time.deltaTime * Runner.DeltaTime);
        
        if (smoothedSpeed < 0.01f)
        {
            smoothedSpeed = 0f;
        }
        
        animator.SetFloat("Speed", smoothedSpeed);

        float moveX = HasInputAuthority ? lastMoveInput.x : 0f;
        float moveY = HasInputAuthority ? lastMoveInput.y : 0f;
        if (!HasInputAuthority && isMoving)
        {
            Vector3 localVel = transform.InverseTransformDirection(CurrentVelocity).normalized;
            moveX = localVel.x;
            moveY = localVel.z;
        }

        animator.SetFloat("PositionX", moveX);
        animator.SetFloat("PositionY", moveY);

        animator.SetBool("isGrounded", IsGrounded);

        if (lastWeavePressed)
        {
            animator.SetTrigger("Weave");
        }
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