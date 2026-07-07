using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using OutlineFx;

public class Raycast : MonoBehaviour
{
    [SerializeField] Camera mainCamera;
    [SerializeField] float refreshVelocity = 0.1f;
    [SerializeField] float maxDistance = Mathf.Infinity;
    public List<IInteractable> currentInteractable;
    public GameObject currentObjectReference;
    public Action<GameObject> onHover;
    public Action onUnHover;

    public bool SelectEnabled = true; 

    public Color outlineColor = Color.yellow;
    private OutlineFxParent currentOutline;
    private Coroutine raycastRoutine;
    private PlayerMovement playerMovement;

    void Start()
    {
        NetworkBehaviour nb = GetComponentInParent<NetworkBehaviour>();
        if (nb != null && !nb.HasInputAuthority)
        {
            enabled = false;
            return;
        }

        // Cache PlayerMovement for animation triggers
        playerMovement = GetComponentInParent<PlayerMovement>();

        if (mainCamera == null) mainCamera = Camera.main;
        
        // Iniciamos la corrutina que se encarga del raycast estabilizado
        raycastRoutine = StartCoroutine(StabilizedRaycastRoutine());
    }

    private System.Collections.IEnumerator StabilizedRaycastRoutine()
    {
        while (true)
        {
            // Esperamos el tiempo de refresco normal
            yield return new WaitForSeconds(refreshVelocity);
            
            // LA MAGIA: Esperamos a que termine todo el frame (Update, LateUpdate, y la sincronización de red de Fusion).
            // De esta forma, el rayo usa la posición EXACTA de la cámara que estás viendo en tu monitor.
            yield return new WaitForEndOfFrame();
            
            FindObjectByRay();
        }
    }

    private void Update()
    {
        // Simple default input mapping for generic usage
        if (Input.GetMouseButtonDown(0)|| Input.GetKeyDown(KeyCode.E))
        {
            select();
        }
    }

    private void select()
    {
        if (!SelectEnabled) return;
        if (currentInteractable != null && currentInteractable.Count > 0)
        {
            foreach (var item in currentInteractable) item?.Select();
            // Fire the Interact animation on the local player
            playerMovement?.TriggerInteractAnimation();
        }
    }

    private void FindObjectByRay()
    {
        // Volvemos a tu método original de mousePosition, ya que tu cámara o juego podría depender de la posición real del cursor en pantalla.
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool hitAnythingValid = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == this.transform.root) 
                continue; // Ignorar al propio jugador

            // Obtener todos los interactuables en la jerarquía del objeto golpeado
            List<IInteractable> interactables = new List<IInteractable>(hit.collider.GetComponentsInParent<IInteractable>());
            
            if (interactables.Count == 0)
            {
                if (hit.collider.isTrigger) continue; // Atravesar triggers invisibles (audios, zonas)

                // Si llegamos aquí, chocamos con una pared sólida.
                if (currentObjectReference != null)
                {
                    Debug.Log($"<color=yellow>[RAYCAST]</color> Rayo bloqueado por PARED/OBJETO: {hit.collider.gameObject.name}. Deseleccionando.");
                }
                UnHover();
                return;
            }

            GameObject mainInteractableObject = (interactables[0] as MonoBehaviour).gameObject;
            hitAnythingValid = true;

            if (mainInteractableObject == currentObjectReference)
            {
                // Seguimos mirando el mismo objeto. Todo bien, no hacemos nada.
                return;
            }

            Debug.Log($"<color=green>[RAYCAST]</color> Nuevo objeto detectado: {mainInteractableObject.name}. Seleccionando.");
            UnHover();

            currentObjectReference = mainInteractableObject;
            currentInteractable = interactables;

            foreach (var item in currentInteractable) 
            {
                item?.Hover();
            }

            if (currentObjectReference.GetComponent<DisableOutline>() == null)
            {
                currentOutline = currentObjectReference.GetComponent<OutlineFxParent>();
                if (currentOutline == null) currentOutline = currentObjectReference.AddComponent<OutlineFxParent>();
                currentOutline.SetOutlineColor(outlineColor);
            }

            onHover?.Invoke(currentObjectReference);
            return; 
        }

        if (!hitAnythingValid)
        {
            if (currentObjectReference != null)
            {
                Debug.Log($"<color=red>[RAYCAST]</color> El rayo ya no toca nada (aire). Deseleccionando.");
            }
            UnHover();
        }
    }
    
    private void UnHover()
    {
        if (currentObjectReference == null) return;
        if (currentInteractable != null) foreach (var item in currentInteractable) item?.UnHover();
        currentInteractable = null;
        currentObjectReference = null;
        
        if (currentOutline != null)
        {
            currentOutline.ClearColor();
            currentOutline = null;
        }

        onUnHover?.Invoke();
    }
}
