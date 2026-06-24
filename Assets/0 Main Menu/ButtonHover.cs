using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Escala")]
    [SerializeField] private float escalaHover = 1.15f;
    [SerializeField] private float velocidad = 8f;

    [Header("Sonidos")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    private AudioSource audioSource;
    private Vector3 escalaOriginal;
    private Vector3 escalaObjetivo;

    private void Start()
    {
        escalaOriginal = transform.localScale;
        escalaObjetivo = escalaOriginal;

        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            escalaObjetivo,
            velocidad * Time.deltaTime
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        escalaObjetivo = escalaOriginal * escalaHover;

        if (hoverSound != null)
            audioSource.PlayOneShot(hoverSound);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        escalaObjetivo = escalaOriginal;
    }

    public void OnClick()
    {
        if (clickSound != null)
            audioSource.PlayOneShot(clickSound);
    }
}