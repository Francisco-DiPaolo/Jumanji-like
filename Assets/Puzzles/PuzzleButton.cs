using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class PuzzleButton : MonoBehaviour
{
    private bool isPressed;
    private AudioSource audioSource;
    private Coroutine pressRoutine;
    private static readonly Collider[] hitBuffer = new Collider[8];

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

    private void FixedUpdate()
    {
        UpdateState();
    }

    private void OnDisable()
    {
        if (pressRoutine != null)
        {
            StopCoroutine(pressRoutine);
            pressRoutine = null;
        }
        
        if (isPressed)
        {
            isPressed = false;
            Debug.Log("[puzle]: Botón " + Id + " LIBERADO (OnDisable).");
            OnPressedStateChanged?.Invoke(this, false);
        }
    }

    private void UpdateState()
    {
        bool pressed = CheckIfPlayerIsOnButton();
        
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

    private bool CheckIfPlayerIsOnButton()
    {
        Collider myCollider = GetComponent<Collider>();
        if (myCollider == null) return false;

        if (myCollider is BoxCollider box)
        {
            Vector3 center = transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, hitBuffer, transform.rotation);
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (hit != null)
                {
                    var player = hit.GetComponentInParent<PlayerMovement>();
                    if (player != null)
                    {
                        Debug.Log($"[puzle]: Botón {Id} detectó a {player.gameObject.name} (Collider: {hit.gameObject.name}) en pos {player.transform.position}");
                        return true;
                    }
                }
            }
        }
        else if (myCollider is SphereCollider sphere)
        {
            Vector3 center = transform.TransformPoint(sphere.center);
            float radius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            int count = Physics.OverlapSphereNonAlloc(center, radius, hitBuffer);
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (hit != null)
                {
                    var player = hit.GetComponentInParent<PlayerMovement>();
                    if (player != null)
                    {
                        Debug.Log($"[puzle]: Botón {Id} detectó a {player.gameObject.name} (Collider: {hit.gameObject.name}) en pos {player.transform.position}");
                        return true;
                    }
                }
            }
        }
        else
        {
            var players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player != null && myCollider.bounds.Contains(player.transform.position))
                {
                    Debug.Log($"[puzle]: Botón {Id} (Bounds) detectó a {player.gameObject.name} en pos {player.transform.position}");
                    return true;
                }
            }
        }
        return false;
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