using UnityEngine;

public class LeanTweenDoor : MonoBehaviour
{
    [Header("Configuración de Rotación")]
    [SerializeField] private float anguloApertura = -90f; // El ángulo final deseado
    [SerializeField] private float tiempoApertura = 1.5f; // Duración en segundos
    [SerializeField] private LeanTweenType tipoCurva = LeanTweenType.easeOutQuad; // Suavizado prolijo

    private bool isOpen = false;
    private Vector3 initialLocalAngles;
    
    [Header("Configuración de Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;

    private void Start()
    {
        // Guardamos los ángulos locales iniciales del objeto
        initialLocalAngles = transform.localEulerAngles;
    }

    [ContextMenu("Toggle Door")]
    public void OpenDoor()
    {
        if (isOpen) return; // Si ya está abierta, no hace nada
        isOpen = true;

        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        // Usamos LeanTween.value para realizar una rotación local robusta e independiente de wrapping
        LeanTween.value(gameObject, 0f, anguloApertura, tiempoApertura)
            .setEase(tipoCurva)
            .setOnUpdate((float val) =>
            {
                transform.localEulerAngles = new Vector3(initialLocalAngles.x, initialLocalAngles.y + val, initialLocalAngles.z);
            });
    }

    public void OpenDoorInstant()
    {
        isOpen = true;
        transform.localEulerAngles = new Vector3(initialLocalAngles.x, initialLocalAngles.y + anguloApertura, initialLocalAngles.z);
    }
}