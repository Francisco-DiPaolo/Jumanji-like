using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class ButtonsManager : NetworkBehaviour
{
    [SerializeField]TriggerButton[] triggerButtons;
    
    [Header("Floor Settings")]
    public GameObject leftFloor;
    public GameObject rightFloor;
    public Collider leftFloorCollider;
    public Collider rightFloorCollider;
    [Tooltip("El desplazamiento que tendra el piso izquierdo al abrirse")]
    public Vector3 leftFloorOffset;
    [Tooltip("El desplazamiento que tendra el piso derecho al abrirse")]
    public Vector3 rightFloorOffset;
    public float animationDuration = 1f;
    public float timeToStayOpen = 3f;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;
    [Tooltip("Sonido que se reproduce cuando todos los botones están presionados")]
    public AudioClip allPressedSound;

    [Header("Objects To Disable")]
    public GameObject[] extraObjectsToDisable;

    [Header("Object To Enable")]
    public GameObject objectToEnable;
    [Tooltip("Tiempo en segundos para encender el objeto luego de activar")]
    public float delayToEnableObject = 1f;

    [Header("All Buttons Pressed")]
    [Tooltip("GameObject que se activa cuando TODOS los botones están presionados")]
    public GameObject allPressedObject;
    [Tooltip("Tiempo en segundos antes de ejecutar la secuencia del piso")]
    public float delayBeforeSequence = 2f;

    private Vector3 leftFloorOriginalPos;
    private Vector3 rightFloorOriginalPos;
    private bool isAnimating = false;
    private int allPressedTweenId = -1;

    public static ButtonsManager instance{
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ButtonsManager>(FindObjectsInactive.Include);
            }
            return _instance;
        }
    }
    static ButtonsManager _instance;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this) Destroy(this);
    }

    private void Start()
    {
        if (leftFloor != null) leftFloorOriginalPos = leftFloor.transform.position;
        if (rightFloor != null) rightFloorOriginalPos = rightFloor.transform.position;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void checkButton()
    {
        bool allPressed = triggerButtons.All(b => b.pressed);

        if (allPressed)
        {
            // Activar el objeto cuando todos están presionados
            if (allPressedObject != null)
                allPressedObject.SetActive(true);

            // Reproducir sonido de todos presionados
            if (audioSource != null && allPressedSound != null)
                audioSource.PlayOneShot(allPressedSound);

            // Cancelar cualquier llamada previa pendiente
            if (allPressedTweenId != -1)
            {
                LeanTween.cancel(allPressedTweenId);
                allPressedTweenId = -1;
            }

            // Esperar el delay antes de ejecutar la secuencia
            allPressedTweenId = LeanTween.delayedCall(gameObject, delayBeforeSequence, () =>
            {
                allPressedTweenId = -1;
                if (triggerButtons.All(b => b.pressed))
                {
                    if (Runner != null && Object != null && Object.IsValid)
                    {
                        Rpc_TriggerFloorSequence();
                    }
                    else
                    {
                        TriggerFloorSequenceLocal();
                    }
                }
            }).id;
        }
        else
        {
            // Algún botón se soltó: cancelar secuencia pendiente y apagar objeto
            if (allPressedTweenId != -1)
            {
                LeanTween.cancel(allPressedTweenId);
                allPressedTweenId = -1;
            }

            if (allPressedObject != null)
                allPressedObject.SetActive(false);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void Rpc_TriggerFloorSequence()
    {
        TriggerFloorSequenceLocal();
    }

    private void TriggerFloorSequenceLocal()
    {
        if (isAnimating) return;
        isAnimating = true;

        if (leftFloorCollider != null) leftFloorCollider.enabled = false;
        if (rightFloorCollider != null) rightFloorCollider.enabled = false;

        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        if (triggerButtons != null)
        {
            foreach (var btn in triggerButtons)
            {
                if (btn != null) btn.gameObject.SetActive(false);
            }
        }

        if (extraObjectsToDisable != null)
        {
            foreach (var obj in extraObjectsToDisable)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        if (leftFloor != null)
        {
            LeanTween.move(leftFloor, leftFloorOriginalPos + leftFloorOffset, animationDuration).setEase(LeanTweenType.easeInOutQuad);
        }

        if (rightFloor != null)
        {
            LeanTween.move(rightFloor, rightFloorOriginalPos + rightFloorOffset, animationDuration).setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(() => {
                    LeanTween.delayedCall(gameObject, timeToStayOpen, () => {
                        CloseFloor();
                    });
                });
        }
        else
        {
            // Fallback in case rightFloor is not assigned but we still need the delay
            LeanTween.delayedCall(gameObject, animationDuration + timeToStayOpen, () => {
                CloseFloor();
            });
        }

        if (objectToEnable != null)
        {
            LeanTween.delayedCall(gameObject, delayToEnableObject, () => {
                if (objectToEnable != null) objectToEnable.SetActive(true);
            });
        }
    }

    private void CloseFloor()
    {
        if (leftFloorCollider != null) leftFloorCollider.enabled = true;
        if (rightFloorCollider != null) rightFloorCollider.enabled = true;

        if (audioSource != null && closeSound != null)
        {
            audioSource.PlayOneShot(closeSound);
        }

        if (leftFloor != null)
        {
            LeanTween.move(leftFloor, leftFloorOriginalPos, animationDuration).setEase(LeanTweenType.easeInOutQuad);
        }

        if (rightFloor != null)
        {
            LeanTween.move(rightFloor, rightFloorOriginalPos, animationDuration).setEase(LeanTweenType.easeInOutQuad)
                .setOnComplete(() => {
                    isAnimating = false;
                    ReactivateButtons();
                });
        }
        else
        {
            LeanTween.delayedCall(gameObject, animationDuration, () => {
                isAnimating = false;
                ReactivateButtons();
            });
        }
    }

    private void ReactivateButtons()
    {
        if (triggerButtons != null)
        {
            foreach (var btn in triggerButtons)
            {
                if (btn != null) btn.gameObject.SetActive(true);
            }
        }
    }
}
