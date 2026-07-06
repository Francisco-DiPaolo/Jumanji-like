#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de editor para configurar automáticamente el Culling Mask
/// de la cámara FPS en el prefab del jugador.
///
/// Acceso: Tools → Player → Configure Camera Culling Mask
/// </summary>
public static class PlayerCameraSetupTool
{
    private const string PLAYER_PREFAB_PATH = "Assets/1 Game/Player/Prefab/Player.prefab";
    private const string REMOTE_MESH_LAYER  = "PlayerMesh";
    private const string LOCAL_MESH_LAYER   = "LocalPlayerMesh";
    private const string CAMERA_NAME        = "PlayerCamera"; // Nombre de la cámara dentro del prefab

    // ─────────────────────────────────────────────────────────────────────────
    // Menú principal
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Player/1 - Crear layers (PlayerMesh y LocalPlayerMesh)")]
    public static void CreateLayers()
    {
        bool remoteDone = EnsureLayerExists(REMOTE_MESH_LAYER);
        bool localDone  = EnsureLayerExists(LOCAL_MESH_LAYER);

        AssetDatabase.SaveAssets();

        if (remoteDone && localDone)
            Debug.Log($"[PlayerCameraSetup] ✓ Layers '{REMOTE_MESH_LAYER}' y '{LOCAL_MESH_LAYER}' creadas o ya existían.");
        else
            Debug.LogError("[PlayerCameraSetup] No se pudieron crear las layers. " +
                           "Agrégalas manualmente en Edit → Project Settings → Tags and Layers.");
    }

    [MenuItem("Tools/Player/2 - Configurar Culling Mask de la cámara")]
    public static void ConfigureCameraCullingMask()
    {
        int remoteLayer = LayerMask.NameToLayer(REMOTE_MESH_LAYER);
        int localLayer  = LayerMask.NameToLayer(LOCAL_MESH_LAYER);

        if (remoteLayer == -1 || localLayer == -1)
        {
            EditorUtility.DisplayDialog(
                "Layers faltantes",
                $"Primero ejecuta:\nTools → Player → 1 - Crear layers\n\n" +
                $"Layer '{REMOTE_MESH_LAYER}': {(remoteLayer == -1 ? "FALTA" : "OK")}\n" +
                $"Layer '{LOCAL_MESH_LAYER}': {(localLayer == -1 ? "FALTA" : "OK")}",
                "OK");
            return;
        }

        // Abrir prefab en modo edición
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (prefabAsset == null)
        {
            Debug.LogError($"[PlayerCameraSetup] No se encontró el prefab en: {PLAYER_PREFAB_PATH}");
            return;
        }

        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(PLAYER_PREFAB_PATH))
        {
            var prefabRoot = editingScope.prefabContentsRoot;

            // Buscar cámara por nombre o por tag/componente
            Camera fpsCam = FindCameraInPrefab(prefabRoot);

            if (fpsCam == null)
            {
                Debug.LogError(
                    $"[PlayerCameraSetup] No se encontró ninguna Camera dentro del prefab '{PLAYER_PREFAB_PATH}'. " +
                    "Asegúrate de que el GameObject de la cámara FPS esté dentro del prefab.");
                return;
            }

            // Calcular el nuevo Culling Mask:
            // Incluir TODOS los layers excepto LocalPlayerMesh
            int newMask = fpsCam.cullingMask;

            // Asegurarse de incluir PlayerMesh (remote players visibles)
            newMask |= (1 << remoteLayer);

            // Asegurarse de excluir LocalPlayerMesh (propio modelo invisible)
            newMask &= ~(1 << localLayer);

            fpsCam.cullingMask = newMask;

            Debug.Log(
                $"[PlayerCameraSetup] ✓ Culling Mask de '{fpsCam.gameObject.name}' configurado correctamente.\n" +
                $"  ✓ Incluye '{REMOTE_MESH_LAYER}' (jugadores remotos visibles)\n" +
                $"  ✗ Excluye '{LOCAL_MESH_LAYER}' (propio modelo oculto)\n" +
                $"  Culling Mask value: {newMask}");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "¡Configuración completada!",
            $"Culling Mask de la cámara configurado.\n\n" +
            $"✓ Renderiza '{REMOTE_MESH_LAYER}' (otros jugadores)\n" +
            $"✗ Ignora '{LOCAL_MESH_LAYER}' (tu propio modelo)\n\n" +
            $"Recuerda agregar 'LocalPlayerMeshSetup' al prefab del jugador.",
            "OK");
    }

    [MenuItem("Tools/Player/3 - Diagnóstico de configuración")]
    public static void DiagnoseSetup()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== DIAGNÓSTICO: LocalPlayerMesh Setup ===\n");

        // 1. Verificar layers
        int remoteLayer = LayerMask.NameToLayer(REMOTE_MESH_LAYER);
        int localLayer  = LayerMask.NameToLayer(LOCAL_MESH_LAYER);

        sb.AppendLine("[ LAYERS ]");
        sb.AppendLine($"  '{REMOTE_MESH_LAYER}': {(remoteLayer >= 0 ? $"✓ index {remoteLayer}" : "✗ NO EXISTE")}");
        sb.AppendLine($"  '{LOCAL_MESH_LAYER}':  {(localLayer >= 0 ? $"✓ index {localLayer}" : "✗ NO EXISTE")}");

        // 2. Verificar prefab
        sb.AppendLine("\n[ PREFAB ]");
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (prefabAsset == null)
        {
            sb.AppendLine($"  ✗ Prefab NO encontrado en: {PLAYER_PREFAB_PATH}");
        }
        else
        {
            sb.AppendLine($"  ✓ Prefab encontrado: {PLAYER_PREFAB_PATH}");

            // Verificar cámara
            Camera[] cams = prefabAsset.GetComponentsInChildren<Camera>(true);
            if (cams.Length == 0)
            {
                sb.AppendLine("  ✗ No se encontró ninguna Camera en el prefab");
            }
            else
            {
                foreach (var cam in cams)
                {
                    bool includesRemote = remoteLayer >= 0 && (cam.cullingMask & (1 << remoteLayer)) != 0;
                    bool excludesLocal  = localLayer  >= 0 && (cam.cullingMask & (1 << localLayer)) == 0;

                    sb.AppendLine($"\n  Camera: '{cam.gameObject.name}'");
                    sb.AppendLine($"    Culling Mask: {cam.cullingMask}");
                    sb.AppendLine($"    Incluye '{REMOTE_MESH_LAYER}': {(remoteLayer >= 0 ? (includesRemote ? "✓" : "✗ PROBLEMA") : "Layer no existe")}");
                    sb.AppendLine($"    Excluye '{LOCAL_MESH_LAYER}':  {(localLayer >= 0 ? (excludesLocal ? "✓" : "✗ PROBLEMA - cámara ve su propio modelo") : "Layer no existe")}");
                }
            }

            // Verificar LocalPlayerMeshSetup
            var setup = prefabAsset.GetComponent<LocalPlayerMeshSetup>();
            sb.AppendLine($"\n  LocalPlayerMeshSetup: {(setup != null ? "✓ Presente" : "✗ Falta agregar al GameObject raíz")}");

            // Verificar Character_Model
            Transform charModel = prefabAsset.transform.Find("Character_Model");
            sb.AppendLine($"  'Character_Model' hijo: {(charModel != null ? $"✓ Encontrado (layer actual: {LayerMask.LayerToName(charModel.gameObject.layer)})" : "✗ No encontrado")}");
        }

        sb.AppendLine("\n===========================================");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Diagnóstico", sb.ToString(), "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Camera FindCameraInPrefab(GameObject root)
    {
        // 1. Buscar por nombre exacto
        var byName = root.transform.Find(CAMERA_NAME);
        if (byName != null)
        {
            var cam = byName.GetComponent<Camera>();
            if (cam != null) return cam;
        }

        // 2. Buscar por nombre en profundidad
        Camera[] allCams = root.GetComponentsInChildren<Camera>(true);
        foreach (var cam in allCams)
        {
            if (cam.gameObject.name.ToLower().Contains("player") ||
                cam.gameObject.name.ToLower().Contains("fps") ||
                cam.gameObject.name.ToLower().Contains("main"))
                return cam;
        }

        // 3. Retornar la primera que encuentre
        return allCams.Length > 0 ? allCams[0] : null;
    }

    /// <summary>
    /// Crea una layer con el nombre indicado si no existe.
    /// Retorna true si quedó disponible (ya existía o se creó).
    /// </summary>
    private static bool EnsureLayerExists(string layerName)
    {
        if (LayerMask.NameToLayer(layerName) != -1)
            return true; // ya existe

        // Acceder al objeto serializado de TagManager
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));

        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
            return false;

        // Las layers de usuario van del index 6 al 31
        for (int i = 6; i < 32; i++)
        {
            SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
            if (layerProp.stringValue == "")
            {
                layerProp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[PlayerCameraSetup] Layer '{layerName}' creada en index {i}.");
                return true;
            }
        }

        Debug.LogError($"[PlayerCameraSetup] No hay slots de layer disponibles (máximo 32). " +
                       "Elimina una layer sin usar en Edit → Project Settings → Tags and Layers.");
        return false;
    }
}
#endif
