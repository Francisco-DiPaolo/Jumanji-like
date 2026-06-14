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

    void Start()
    {
        NetworkBehaviour nb = GetComponentInParent<NetworkBehaviour>();
        if (nb != null && !nb.HasInputAuthority)
        {
            enabled = false;
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        InvokeRepeating("FindObjectByRay", 0, refreshVelocity);
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
        if (currentInteractable != null) 
        {
            foreach (var item in currentInteractable) item?.Select();
        }
    }

    private void FindObjectByRay()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        bool hit = Physics.Raycast(ray, out RaycastHit raycastHit, maxDistance);
        if (!hit) { UnHover(); return; }

        if (raycastHit.collider.gameObject == currentObjectReference)
        {
            return;
        }
        else 
        {
            UnHover();
        }

        List<IInteractable> interactable = new List<IInteractable>(raycastHit.collider.GetComponents<IInteractable>());
        
        if (currentInteractable != null) foreach (var item in currentInteractable) item?.UnHover();
        
        currentObjectReference = raycastHit.collider.gameObject;

        if (interactable.Count > 0)
        {
            foreach (var item in interactable) 
            {
                item?.Hover();
            }
            currentInteractable = interactable;

            currentOutline = currentObjectReference.GetComponent<OutlineFxParent>();
            if (currentOutline == null) currentOutline = currentObjectReference.AddComponent<OutlineFxParent>();
            currentOutline.SetOutlineColor(outlineColor);

            onHover?.Invoke(currentObjectReference);
        }
        else
        {
            currentInteractable = null;
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
