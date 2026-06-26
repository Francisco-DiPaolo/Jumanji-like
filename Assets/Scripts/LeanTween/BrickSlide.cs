using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BrickSlide : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float distanciaHundir = 0.1f; // Cuánto se hunde (en local Y)
    [SerializeField] private float tiempoBajar = 0.2f;      // Tiempo para bajar
    [SerializeField] private float tiempoSubir = 0.6f;      // Tiempo para volver a subir
    [SerializeField] private float delayAntesDeSubir = 0.2f; // Espera abajo antes de subir
    [SerializeField] private LeanTweenType easeBajar = LeanTweenType.easeOutQuad;
    [SerializeField] private LeanTweenType easeSubir = LeanTweenType.easeInOutQuad;

    [Header("Sonido")]
    [SerializeField] private AudioClip slideClip;

    private AudioSource audioSource;
    private float posicionYInicial;
    private bool yaInicializado = false;
    private bool isMoving = false;

    public bool IsMoving => isMoving;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        InitPosition();
    }

    private void Start()
    {
        InitPosition();
    }

    private void InitPosition()
    {
        if (yaInicializado) return;
        posicionYInicial = transform.localPosition.y;
        yaInicializado = true;
    }

    private void OnEnable()
    {
        // Asegurar que si el objeto se reactiva, mantenga su Y local correcta
        if (yaInicializado)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, posicionYInicial, transform.localPosition.z);
            isMoving = false;
        }
    }

    [ContextMenu("Probar Slide")]
    public void StartSlide()
    {
        if (isMoving) return;
        isMoving = true;

        // Sonido
        if (audioSource != null && slideClip != null)
        {
            audioSource.PlayOneShot(slideClip);
        }

        // Cancelamos cualquier animación previa en este GameObject
        LeanTween.cancel(gameObject);

        // Volvemos a inicializar por las dudas
        InitPosition();

        float targetY = posicionYInicial - distanciaHundir;

        // Animación de hundir
        LeanTween.moveLocalY(gameObject, targetY, tiempoBajar)
            .setEase(easeBajar)
            .setOnComplete(() =>
            {
                // Animación de subir con delay
                LeanTween.moveLocalY(gameObject, posicionYInicial, tiempoSubir)
                    .setEase(easeSubir)
                    .setDelay(delayAntesDeSubir)
                    .setOnComplete(() =>
                    {
                        isMoving = false;
                    });
            });
    }
}
