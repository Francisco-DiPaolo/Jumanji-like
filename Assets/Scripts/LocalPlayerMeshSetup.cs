using Fusion;
using UnityEngine;

/// <summary>
/// Soluciona el problema de visibilidad de mallas en multijugador.
///
/// PROBLEMA: La camara de cada jugador tiene un culling mask que ignora la layer
/// "PlayerMesh", lo que evita que vea su propio modelo. Pero en online, tambien
/// deja de ver los modelos de los companeros porque comparten la misma layer.
///
/// SOLUCION: Al hacer spawn, si somos el jugador local (HasInputAuthority),
/// movemos SOLO nuestros propios Renderers a la layer "LocalPlayerMesh".
/// La camara en el prefab ignora "LocalPlayerMesh" (no "PlayerMesh").
/// Los companeros permanecen en "PlayerMesh" y la camara local los renderiza.
///
/// SETUP REQUERIDO (una sola vez):
///   1. En Unity > Edit > Project Settings > Tags and Layers:
///      Agregar una layer llamada exactamente "LocalPlayerMesh".
///   2. En el prefab del jugador, seleccionar la MainCamera y en su
///      culling mask: DESMARCAR "LocalPlayerMesh", dejar marcado "PlayerMesh".
///   3. Agregar este componente al prefab del jugador (mismo GameObject raiz).
///   4. Asignar en el inspector el campo "meshRoot" al hijo que contiene el
///      modelo 3D (ej: "Character_Model"). Si se deja vacio, busca automaticamente.
/// </summary>
public class LocalPlayerMeshSetup : NetworkBehaviour
{
    [Header("Referencias")]
    [Tooltip("Raiz del modelo 3D del jugador. Si se deja vacio se intenta encontrar 'Character_Model' automaticamente.")]
    [SerializeField] private Transform meshRoot;

    [Header("Configuracion de Layers")]
    [Tooltip("Layer exclusiva para el modelo del jugador LOCAL. Debe existir en Project Settings > Tags and Layers.")]
    [SerializeField] private string localPlayerMeshLayerName = "LocalPlayerMesh";

    public override void Spawned()
    {
        // Solo el jugador local necesita cambiar su layer
        if (!HasInputAuthority) return;

        // Intentar encontrar meshRoot automaticamente si no fue asignado
        if (meshRoot == null)
        {
            meshRoot = transform.Find("Character_Model");
            if (meshRoot == null)
            {
                Debug.LogWarning($"[LocalPlayerMeshSetup] No se encontro 'Character_Model' en {gameObject.name}. " +
                                 "Asigna el campo 'meshRoot' en el inspector o renombra el hijo del modelo.", this);
                return;
            }
        }

        int localLayer = LayerMask.NameToLayer(localPlayerMeshLayerName);
        if (localLayer == -1)
        {
            Debug.LogError($"[LocalPlayerMeshSetup] La layer '{localPlayerMeshLayerName}' no existe. " +
                           "Creala en Edit > Project Settings > Tags and Layers.", this);
            return;
        }

        // Cambiar la layer de todos los renderers del modelo propio
        SetLayerRecursive(meshRoot, localLayer);

        Debug.Log($"[LocalPlayerMeshSetup] Renderers del jugador local movidos a layer '{localPlayerMeshLayerName}' " +
                  $"(index {localLayer}). La camara local no los renderizara.", this);
    }

    /// <summary>
    /// Cambia recursivamente la layer de un Transform y todos sus hijos.
    /// </summary>
    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursive(child, layer);
        }
    }
}
