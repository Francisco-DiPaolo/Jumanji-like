using System;
using UnityEngine;
using UnityEngine.Events;

public class PuzzleButton : MonoBehaviour
{
    private bool isPressed;
    private int playersInside;

    public string Id;
    public bool IsPressed => isPressed;

    public event Action<PuzzleButton, bool> OnPressedStateChanged;

    public UnityEvent OnCorrectPressed;
    public UnityEvent OnIncorrectPressed;
    public UnityEvent OnPhaseStarted;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out _))
        {
            playersInside++;
            UpdateState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out _))
        {
            playersInside--;
            if (playersInside < 0)
            {
                playersInside = 0;
            }
            UpdateState();
        }
    }

    private void OnDisable()
    {
        playersInside = 0;
        UpdateState();
    }

    private void UpdateState()
    {
        bool pressed = playersInside > 0;
        if (isPressed == pressed) return;
        isPressed = pressed;
        Debug.Log("[puzle]: Botón " + Id + (isPressed ? " PRESIONADO" : " LIBERADO") + " por el jugador.");
        OnPressedStateChanged?.Invoke(this, isPressed);
    }

    public void TriggerCorrectPressed()
    {
        Debug.Log("[puzle]: OnCorrectPressed en " + Id);
        OnCorrectPressed?.Invoke();
    }

    public void TriggerIncorrectPressed()
    {
        Debug.Log("[puzle]: OnIncorrectPressed en " + Id);
        OnIncorrectPressed?.Invoke();
    }

    public void TriggerPhaseStarted()
    {
        Debug.Log("[puzle]: OnPhaseStarted en " + Id);
        OnPhaseStarted?.Invoke();
    }
}
