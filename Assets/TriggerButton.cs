using UnityEngine;

public class TriggerButton : MonoBehaviour
{
    public bool pressed;
    [SerializeField] private int playersInside = 0;

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out PlayerMovement p))
        {
            playersInside++;
            UpdateState();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out PlayerMovement p))
        {
            playersInside--;
            // Fail-safe para que no baje de cero por errores de física
            if (playersInside < 0) playersInside = 0;

            UpdateState();
        }
    }

    private void UpdateState()
    {
        bool wasPressed = pressed;
        pressed = playersInside > 0;

        // Avisar al manager siempre que el estado cambie, no solo al presionar
        if (pressed != wasPressed)
        {
            ButtonsManager.instance.checkButton();
        }
    }
}