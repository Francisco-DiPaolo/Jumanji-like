using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class PuzzleButton : MonoBehaviour
{
    private bool isPressed;
    private System.Collections.Generic.HashSet<Collider> collidersInside = new System.Collections.Generic.HashSet<Collider>();
    private AudioSource audioSource;
    private Coroutine pressRoutine;

    [Header("Puzzle Settings")]
    public string Id;
    [Tooltip("Tiempo de espera antes de activar el botón (0 para inmediato)")]
    public float pressDelay = 0f;
    public bool IsPressed => isPressed;

    public event Action<PuzzleButton, bool> OnPressedStateChanged;

    [Header("Unity Events (Inspector)")]
    public UnityEvent OnPressedStarted;   // Se ejecuta apenas lo pisan
    public UnityEvent OnCorrectPressed;   // Se ejecuta al acertar
    public UnityEvent OnIncorrectPressed; // Se ejecuta al errar
    public UnityEvent OnPhaseStarted;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[puzle]: Botón {Id} - OnTriggerEnter con: {other.gameObject.name} (tag: {other.tag})");
        if (other.GetComponentInParent<PlayerMovement>() != null)
        {
            collidersInside.Add(other);
            UpdateState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (collidersInside.Remove(other))
        {
            UpdateState();
        }
    }

    private void OnDisable()
    {
        collidersInside.Clear();
        UpdateState();
    }

    private void UpdateState()
    {
        collidersInside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        bool pressed = collidersInside.Count > 0;
        
        if (pressed)
        {
            if (pressRoutine == null && !isPressed)
            {
                if (pressDelay > 0f)
                {
                    pressRoutine = StartCoroutine(PressSequenceRoutine());
                }
                else
                {
                    ExecutePress();
                }
            }
        }
        else
        {
            if (pressRoutine != null)
            {
                StopCoroutine(pressRoutine);
                pressRoutine = null;
            }
            
            if (isPressed)
            {
                isPressed = false;
                Debug.Log("[puzle]: Botón " + Id + " LIBERADO por el jugador.");
                OnPressedStateChanged?.Invoke(this, false);
            }
        }
    }

    private void ExecutePress()
    {
        // 1. Dispara el evento de inicio (Aquí vas a colgar el sonido de deslice)
        OnPressedStarted?.Invoke();

        // 2. Confirma la presión para el sistema
        isPressed = true;
        Debug.Log($"[puzle]: Botón {Id} PRESIONADO por el jugador.");
        OnPressedStateChanged?.Invoke(this, true);
    }

    private IEnumerator PressSequenceRoutine()
    {
        // 1. Dispara el evento de inicio
        OnPressedStarted?.Invoke();

        // 2. Espera el tiempo configurado
        yield return new WaitForSeconds(pressDelay);

        // 3. Confirma la presión para el sistema
        isPressed = true;
        Debug.Log($"[puzle]: Botón {Id} PRESIONADO por el jugador tras {pressDelay}s.");
        OnPressedStateChanged?.Invoke(this, true);
        
        pressRoutine = null;
    }

    // --- FUNCIONES PÚBLICAS PARA QUE LAS ASIGNES EN LOS EVENTOS ---

    public void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void TriggerCorrectPressed()
    {
        Debug.Log("[puzle]: OnCorrectPressed en " + Id);
        OnCorrectPressed?.Invoke(); // Dispara tu evento del Inspector
    }

    public void TriggerIncorrectPressed()
    {
        Debug.Log("[puzle]: OnIncorrectPressed en " + Id);
        OnIncorrectPressed?.Invoke(); // Dispara tu evento del Inspector
    }

    public void TriggerPhaseStarted()
    {
        Debug.Log("[puzle]: OnPhaseStarted en " + Id);
        OnPhaseStarted?.Invoke();
    }
}