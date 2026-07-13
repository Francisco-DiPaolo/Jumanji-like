using System.Collections.Generic;
using UnityEngine;

public class TriggerButton : MonoBehaviour
{
    public bool pressed;
    [SerializeField] private List<Collider> collidersInside = new List<Collider>();

    [Header("Pressed Visuals")]
    [Tooltip("GameObject que se activa mientras el botón está presionado")]
    [SerializeField] private GameObject pressedObject;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Sonido que se reproduce cuando el botón es presionado")]
    [SerializeField] private AudioClip pressSound;

    void OnTriggerEnter(Collider other)
    {
        PlayerMovement p = other.GetComponentInParent<PlayerMovement>();
        if (p != null)
        {
            if (!collidersInside.Contains(other))
            {
                collidersInside.Add(other);
                UpdateState();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (collidersInside.Contains(other))
        {
            collidersInside.Remove(other);
            UpdateState();
        }
    }

    private void Update()
    {
        if (collidersInside.Count > 0)
        {
            Collider buttonCollider = GetComponent<Collider>();
            if (buttonCollider != null)
            {
                int countBefore = collidersInside.Count;

                // Limpiar colisionadores nulos, deshabilitados, inactivos o que ya no intersectan físicamente
                collidersInside.RemoveAll(c => 
                    c == null || 
                    !c.enabled || 
                    !c.gameObject.activeInHierarchy || 
                    !buttonCollider.bounds.Intersects(c.bounds)
                );

                if (collidersInside.Count != countBefore)
                {
                    UpdateState();
                }
            }
        }
    }

    private void OnDisable()
    {
        collidersInside.Clear();
        UpdateState();
    }

    private void UpdateState()
    {
        collidersInside.RemoveAll(c => c == null);

        bool wasPressed = pressed;
        pressed = collidersInside.Count > 0;

        if (pressed != wasPressed)
        {
            Debug.Log($"[TriggerButton] {gameObject.name} pressed state changed to: {pressed} (Colliders inside: {collidersInside.Count})");

            // Activar/desactivar el objeto visual según el estado
            if (pressedObject != null)
                pressedObject.SetActive(pressed);

            // Reproducir sonido al presionar
            if (pressed && audioSource != null && pressSound != null)
                audioSource.PlayOneShot(pressSound);
        }

        // Avisar al manager cuando el estado cambia (tanto al presionar como al soltar)
        if (pressed != wasPressed)
        {
            ButtonsManager.instance.checkButton();
        }
    }
}