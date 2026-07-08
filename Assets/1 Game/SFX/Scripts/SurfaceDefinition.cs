using UnityEngine;

public class SurfaceDefinition : MonoBehaviour
{
    public SurfaceType SurfaceType;
}

public enum SurfaceType
{
    Concrete,
    Wood,
    Water
}