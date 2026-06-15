using System;
using UnityEngine;
using UnityEngine.Events;

public class PuzzleSubController : MonoBehaviour
{
    [SerializeField] private string controllerIndex;

    private PuzzleButton[] buttons;
    private string correctButtonId;
    private string incorrectButtonId1;
    private string incorrectButtonId2;

    public string ControllerIndex => controllerIndex;

    public UnityEvent OnCorrectButtonPressed;
    public event Action OnButtonStateChanged;

    public bool IsCorrectButtonPressed
    {
        get
        {
            var pressedBtn = CurrentPressedButton;
            if (pressedBtn == null) return false;
            return CompareIds(pressedBtn.Id, correctButtonId);
        }
    }

    public bool IsPlayerOnPhaseButton()
    {
        return CurrentPressedButton != null;
    }

    public PuzzleButton CurrentPressedButton
    {
        get
        {
            foreach (var button in buttons)
            {
                if (button.IsPressed && (
                    CompareIds(button.Id, correctButtonId) ||
                    CompareIds(button.Id, incorrectButtonId1) ||
                    CompareIds(button.Id, incorrectButtonId2)))
                {
                    return button;
                }
            }
            return null;
        }
    }

    private void Awake()
    {
        buttons = GetComponentsInChildren<PuzzleButton>(true);
        foreach (var button in buttons)
        {
            button.OnPressedStateChanged += HandleButtonStateChanged;
            if (!button.Id.StartsWith(controllerIndex, StringComparison.OrdinalIgnoreCase))
            {
                button.Id = controllerIndex + button.Id;
            }
        }
    }

    private void OnDestroy()
    {
        if (buttons != null)
        {
            foreach (var button in buttons)
            {
                if (button != null)
                {
                    button.OnPressedStateChanged -= HandleButtonStateChanged;
                }
            }
        }
    }

    public void SetPhaseData(string correct, string incorrect1, string incorrect2)
    {
        correctButtonId = correct.StartsWith(controllerIndex, StringComparison.OrdinalIgnoreCase) ? correct : controllerIndex + correct;
        incorrectButtonId1 = incorrect1.StartsWith(controllerIndex, StringComparison.OrdinalIgnoreCase) ? incorrect1 : controllerIndex + incorrect1;
        incorrectButtonId2 = incorrect2.StartsWith(controllerIndex, StringComparison.OrdinalIgnoreCase) ? incorrect2 : controllerIndex + incorrect2;

        foreach (var button in buttons)
        {
            if (CompareIds(button.Id, correctButtonId) || 
                CompareIds(button.Id, incorrectButtonId1) || 
                CompareIds(button.Id, incorrectButtonId2))
            {
                button.TriggerPhaseStarted();
            }
        }
    }

    public void TriggerCurrentButtonCorrect()
    {
        var pressedBtn = CurrentPressedButton;
        if (pressedBtn != null)
        {
            pressedBtn.TriggerCorrectPressed();
            Debug.Log("[puzle]: OnCorrectButtonPressed en subcontrolador de sala " + controllerIndex);
            OnCorrectButtonPressed?.Invoke();
        }
    }

    public void TriggerPhaseButtonsOnSuccess()
    {
        var pressedBtn = CurrentPressedButton;
        if (pressedBtn != null)
        {
            pressedBtn.TriggerCorrectPressed();
            Debug.Log("[puzle]: OnCorrectButtonPressed en subcontrolador de sala " + controllerIndex);
            OnCorrectButtonPressed?.Invoke();
        }

        foreach (var button in buttons)
        {
            if (CompareIds(button.Id, incorrectButtonId1) || CompareIds(button.Id, incorrectButtonId2))
            {
                button.TriggerIncorrectPressed();
            }
        }
    }

    public void TriggerCurrentButtonIncorrect()
    {
        var pressedBtn = CurrentPressedButton;
        if (pressedBtn != null)
        {
            pressedBtn.TriggerIncorrectPressed();
        }
    }

    private void HandleButtonStateChanged(PuzzleButton button, bool isPressed)
    {
        Debug.Log("[puzle]: Subcontrolador detectó cambio en " + button.Id + " (Presionado: " + isPressed + ")");
        try
        {
            if (OnButtonStateChanged == null)
            {
                Debug.Log("[puzle]: OnButtonStateChanged no tiene suscriptores en subcontrolador de sala " + controllerIndex);
            }
            else
            {
                Debug.Log("[puzle]: Invocando OnButtonStateChanged (" + OnButtonStateChanged.GetInvocationList().Length + " suscriptores) en subcontrolador de sala " + controllerIndex);
                OnButtonStateChanged.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[puzle]: Excepción al invocar OnButtonStateChanged en subcontrolador de sala " + controllerIndex + ": " + ex);
        }
    }

    private bool CompareIds(string id1, string id2)
    {
        if (id1 == null || id2 == null) return false;
        return id1.Trim().Equals(id2.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
