using UnityEngine;

public class LeanTweenDoor : MonoBehaviour
{
    [Header("Configuración de Rotación")]
    [SerializeField] private float anguloApertura = -90f; // El ángulo final deseado
    [SerializeField] private float tiempoApertura = 1.5f; // Duración en segundos
    [SerializeField] private LeanTweenType tipoCurva = LeanTweenType.easeOutQuad; // Suavizado prolijo

    private bool isOpen = false;
    private float initialRotationY;

    private void Start()
    {
        // Guardamos la rotación Y inicial local del objeto
        initialRotationY = transform.localEulerAngles.y;
    }

    [ContextMenu("Toggle Door")]
    public void OpenDoor()
    {
        if (isOpen) return; // Si ya está abierta, no hace nada
        isOpen = true;

        // Calculamos el destino final sumando el ángulo a la posición inicial
        float targetY = initialRotationY + anguloApertura;

        // Esta sintaxis es universal en LeanTween y no tira error de compilación
        LeanTween.rotateY(gameObject, targetY, tiempoApertura)
            .setEase(tipoCurva);
    }
}