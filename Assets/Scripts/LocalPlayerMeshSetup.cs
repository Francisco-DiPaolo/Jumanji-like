using Fusion;
using UnityEngine;

/// <summary>
/// Soluciona el problema de visibilidad de mallas en multijugador FPS.
///
/// ARQUITECTURA DE LA SOLUCIÓN:
/// ─────────────────────────────────────────────────────────────────────
///  Prefab del jugador (todos los clientes instancian el mismo prefab)
///  │
///  ├── [Layer: Default]          ← Raíz del jugador
///  ├── CameraPivot/MainCamera    ← cullingMask ignora "LocalPlayerMesh"
///  └── Character_Model           ← Aquí viven los SkinnedMeshRenderers
///       ├── [Layer: PlayerMesh]  ← Estado inicial en el prefab
///       └── ...hijos...
///
///  Al hacer Spawned():
///   • Si HasInputAuthority  → movemos Character_Model a "LocalPlayerMesh"
///     → La cámara LOCAL no lo renderiza (cullingMask lo excluye).
///   • Si !HasInputAuthority → dejamos el modelo en "PlayerMesh"
///     → La cámara LOCAL SÍ lo renderiza (PlayerMesh está en cullingMask).
///
/// SETUP REQUERIDO EN UNITY (una sola vez):
/// ─────────────────────────────────────────────────────────────────────
///  1. Edit → Project Settings → Tags and Layers:
///     - Agregar layer: "PlayerMesh"       (p.ej. User Layer 6)
///     - Agregar layer: "LocalPlayerMesh"  (p.ej. User Layer 7)
///
///  2. En el prefab Player.prefab:
///     a) Seleccionar el hijo "Character_Model" → Inspector → Layer: "PlayerMesh"
///        (aplica a todos los hijos cuando Unity pregunte).
///     b) Seleccionar la MainCamera → Inspector → Culling Mask:
///        ✓ Marcar TODO lo que quieras renderizar (Default, PlayerMesh, etc.)
///        ✗ DESMARCAR "LocalPlayerMesh"
///
///  3. Este componente debe estar en el mismo GameObject raíz del Player.prefab.
///     El campo "meshRoot" puede dejarse vacío (se autodetecta "Character_Model").
///
/// USO DEL TOOL DE EDITOR:
///     Tools → Player → Configure Camera Culling Mask
///     (configura todo automáticamente en el prefab)
/// </summary>
public class LocalPlayerMeshSetup : NetworkBehaviour
{
    [Header("Referencias")]
    [Tooltip("Raíz del modelo 3D. Si se deja vacío, busca 'Character_Model' automáticamente.")]
    [SerializeField] private Transform meshRoot;

    [Header("Configuración de Layers")]
    [Tooltip("Layer en la que viven los modelos de TODOS los jugadores en el prefab. " +
             "La cámara local SÍ la renderiza.")]
    [SerializeField] private string remoteMeshLayerName = "PlayerMesh";

    [Tooltip("Layer EXCLUSIVA para el modelo del jugador LOCAL. " +
             "La cámara local la IGNORA en su Culling Mask.")]
    [SerializeField] private string localMeshLayerName = "LocalPlayerMesh";

    // -------------------------------------------------------------------------
    // Fusion Callback
    // -------------------------------------------------------------------------

    public override void Spawned()
    {
        ResolveMeshRoot();

        if (meshRoot == null)
        {
            Debug.LogError(
                $"[LocalPlayerMeshSetup] ({gameObject.name}) No se encontró la raíz del modelo. " +
                "Asigna el campo 'meshRoot' en el Inspector o asegúrate de que el hijo se llame 'Character_Model'.",
                this);
            return;
        }

        if (HasInputAuthority)
        {
            // Somos el jugador LOCAL: ocultar nuestro propio modelo de nuestra cámara.
            ApplyLayer(localMeshLayerName, isLocal: true);
        }
        else
        {
            // Somos un proxy REMOTO: asegurarnos de que esté en la layer visible.
            // Esto cubre el caso en que el servidor o un late-joiner spawnee el prefab
            // y el layer haya sido cambiado de forma incorrecta.
            ApplyLayer(remoteMeshLayerName, isLocal: false);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    private void ResolveMeshRoot()
    {
        if (meshRoot != null) return;

        meshRoot = transform.Find("Character_Model");

        if (meshRoot == null)
        {
            // Búsqueda recursiva como fallback
            meshRoot = FindInHierarchy(transform, "Character_Model");
        }
    }

    private void ApplyLayer(string layerName, bool isLocal)
    {
        int layerIndex = LayerMask.NameToLayer(layerName);

        if (layerIndex == -1)
        {
            Debug.LogError(
                $"[LocalPlayerMeshSetup] La layer '{layerName}' NO existe en el proyecto. " +
                "Ve a Edit → Project Settings → Tags and Layers y agrégala.",
                this);
            return;
        }

        SetLayerRecursive(meshRoot, layerIndex);

        string role = isLocal ? "LOCAL (propio)" : "REMOTO (proxy)";
        Debug.Log(
            $"[LocalPlayerMeshSetup] [{gameObject.name}] Jugador {role} → " +
            $"modelo movido a layer '{layerName}' (index {layerIndex}).",
            this);
    }

    /// <summary>
    /// Cambia la layer de forma recursiva en toda la jerarquía del modelo.
    /// </summary>
    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursive(child, layer);
    }

    /// <summary>
    /// Búsqueda recursiva de un Transform por nombre exacto.
    /// </summary>
    private static Transform FindInHierarchy(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindInHierarchy(child, targetName);
            if (found != null) return found;
        }
        return null;
    }
}
