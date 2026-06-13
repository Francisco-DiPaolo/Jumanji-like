using System.Collections;
using System.Collections.Generic;
using OutlineFx;
using UnityEngine;

public class OutlineFxParent : MonoBehaviour
{
    [SerializeField]
    private Color outlineColor = Color.clear;

    [SerializeField]private List<OutlineFx.OutlineFx> childOutlines = new List<OutlineFx.OutlineFx>();

    private void Awake()
    {
        InitializeOutlines();

        ClearColor();
    }
    public void NewOutlines()
    {
        ClearChildOutline();
        InitializeOutlines();
        ClearColor();
    }
    public void InitializeOutlines()
    {
        // Get all mesh renderers in children
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();

        foreach (MeshRenderer renderer in meshRenderers)
        {
            // Add outline component if it doesn't exist
            OutlineFx.OutlineFx outline = renderer.gameObject.GetComponent<OutlineFx.OutlineFx>();
            if (outline == null)
            {
                outline = renderer.gameObject.AddComponent<OutlineFx.OutlineFx>();
            }

            // Set initial color
            outline._color = outlineColor;
            childOutlines.Add(outline);
        }
        if(meshRenderers.Length > 0) return;
        SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer renderer in skinnedMeshRenderers)
        {
            // Add outline component if it doesn't exist
            OutlineFx.OutlineFx outline = renderer.gameObject.GetComponent<OutlineFx.OutlineFx>();
            if (outline == null)
            {
                outline = renderer.gameObject.AddComponent<OutlineFx.OutlineFx>();
            }
            // Set initial color
            outline._color = outlineColor;
            childOutlines.Add(outline);
        }
    }
    public void ClearChildOutline() => childOutlines.Clear();
    public void SetOutlineColor()
    {
        SetOutlineColor(outlineColor);
    }

    public void ClearColor()
    {
        SetOutlineColor(Color.clear);
    }
    
    // Public method to change outline color for all children
    public void SetOutlineColor(Color newColor)
    {
        foreach (OutlineFx.OutlineFx outline in childOutlines)
        {
            if (outline != null)
            {
                outline._color = newColor;
            }
        }
    }


}
