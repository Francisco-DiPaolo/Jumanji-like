using UnityEngine;

public class TriggerButton : MonoBehaviour
{
    public bool pressed;
    [SerializeField] private int playersInside = 0;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // O usar TryGetComponent
        {
            playersInside++;
            UpdateState();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playersInside--;
            // Un pequeño fail-safe para que no baje de cero por errores de física
            if (playersInside < 0) playersInside = 0;
            
            UpdateState();
        }
    }

    private void UpdateState()
    {
        bool wasPressed = pressed;
        pressed = playersInside > 0;

        // Solo avisar al manager si el estado cambió de 'libre' a 'presionado'
        if (pressed && !wasPressed)
        {
            ButtonsManager.instance.checkButton();
        }
    }
}