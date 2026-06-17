using UnityEngine;

public class LeanTweenBrickMovement : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float distanciaHundir = 0.15f; // Qué tanto baja en el mundo real
    [SerializeField] private float tiempoMovimiento = 0.2f;

    [Header("Estilo de Curva (Ease)")]
    [SerializeField] private LeanTweenType curvaAlBajar = LeanTweenType.easeOutQuad;
    [SerializeField] private LeanTweenType curvaAlSubir = LeanTweenType.easeOutQuad;

    private float posicionYInicial;
    private bool yaInicializado = false;

    private void Start()
    {
        // Guardamos la posición Y global absoluta en el mundo
        posicionYInicial = transform.position.y;
        yaInicializado = true;
    }

    private void OnEnable()
    {
        // Por si el objeto se desactiva y activa, nos aseguramos de resetear la Y correcta
        if (yaInicializado)
        {
            posicionYInicial = transform.position.y;
        }
    }

    public void HundirBrick()
    {
        LeanTween.cancel(gameObject);

        // Calculamos el destino restando la distancia directamente en la Y global
        float destinoY = posicionYInicial - distanciaHundir;

        // moveY (a secas) mueve al objeto en el espacio del mundo, es infalible
        LeanTween.moveY(gameObject, destinoY, tiempoMovimiento)
            .setEase(curvaAlBajar);
    }

    public void LevantarBrick()
    {
        LeanTween.cancel(gameObject);

        // Volvemos a la Y del mundo original
        LeanTween.moveY(gameObject, posicionYInicial, tiempoMovimiento)
            .setEase(curvaAlSubir);
    }
}