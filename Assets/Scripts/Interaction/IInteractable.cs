using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IInteractable
{
    public abstract void Hover();
    public abstract void UnHover();
    public abstract void Select();
}
