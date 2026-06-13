using UnityEngine;

[RequireComponent(typeof(BasicInteraction))]
public class OutlineOnHover : MonoBehaviour
{
    BasicInteraction basicInteraction;
    OutlineFx.OutlineFx outlineFxParent;
    public Color outlineColor = Color.yellow;

    void Awake()
    {
        basicInteraction = GetComponent<BasicInteraction>();
        outlineFxParent = GetComponentInChildren<OutlineFx.OutlineFx>();
        
        if (outlineFxParent == null)
        {
            outlineFxParent = gameObject.AddComponent<OutlineFx.OutlineFx>();
        }
        
        outlineFxParent.Color = Color.clear;

        basicInteraction.onHover.AddListener(() => {
            if (outlineFxParent != null) outlineFxParent.Color = outlineColor;
        });
        basicInteraction.onUnhovered.AddListener(() => {
            if (outlineFxParent != null) outlineFxParent.Color = Color.clear;
        });
    }
}
