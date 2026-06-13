using System;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

public abstract class NetworkBasicInteraction : NetworkBehaviour, IInteractable
{
    public UnityEvent onSelect;
    public UnityEvent onUnhovered;
    public UnityEvent onHover;
    public Action onHoverAction;
    public Action onUnHoverAction;
    public bool isHovered = false;

    public virtual void Hover()
    {
        isHovered = true;
        onHover?.Invoke();
        onHoverAction?.Invoke();
    }

    public virtual void UnHover()
    {
        isHovered = false;
        onUnhovered?.Invoke();
        onUnHoverAction?.Invoke();
    }

    public virtual void Select()
    {
        onSelect?.Invoke();
    }

}
