using UnityEngine;
using UnityEditor;

public class TorchPrefabCreator
{
    public const string PrefabFolder = "Assets/1 Game/Environment/Torches";

    const string VfxNormalPath = "Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_TorchLight.prefab";
    const string VfxGreenPath  = "Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_TorchLight_Green.prefab";
    const string FireChildName = "FireVFX";

    [MenuItem("Tools/Create Torch Prefab")]
    public static void CreateTorchPrefabs()
    {
        EnsureFolder();

        SaveTorchPrefab("Torch",       false);
        SaveTorchPrefab("Torch_Green", true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TorchPuzzle] Prefabs creados en: {PrefabFolder}");
    }

    public static TorchController InstantiateTorch(Transform parent, string goName, int index, bool isGreen)
    {
        var prefabName = isGreen ? "Torch_Green" : "Torch";
        var prefabPath = $"{PrefabFolder}/{prefabName}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogWarning($"[TorchPuzzle] Prefab no encontrado: '{prefabPath}'. Ejecuta Tools/Create Torch Prefab primero.");
            return FallbackTorch(parent, goName, index, isGreen);
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create " + goName);
        go.name = goName;
        go.transform.position = new Vector3(index * 2f, 0f, 0f);

        return go.GetComponent<TorchController>();
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/1 Game/Environment"))
            AssetDatabase.CreateFolder("Assets/1 Game", "Environment");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/1 Game/Environment", "Torches");
    }

    static void SaveTorchPrefab(string prefabName, bool isGreen)
    {
        var root = new GameObject(prefabName);

        var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handle.name = "Handle";
        handle.transform.SetParent(root.transform, false);
        handle.transform.localPosition = Vector3.zero;
        handle.transform.localScale    = new Vector3(0.12f, 0.7f, 0.12f);
        Object.DestroyImmediate(handle.GetComponent<BoxCollider>());

        var vfxPath   = isGreen ? VfxGreenPath : VfxNormalPath;
        var vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
        GameObject vfx;
        if (vfxPrefab != null)
        {
            vfx = (GameObject)PrefabUtility.InstantiatePrefab(vfxPrefab);
            vfx.transform.SetParent(root.transform, false);
        }
        else
        {
            vfx = new GameObject();
            vfx.transform.SetParent(root.transform, false);
            Debug.LogWarning($"[TorchPuzzle] VFX no encontrado: {vfxPath}");
        }
        vfx.name = FireChildName;
        vfx.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        vfx.SetActive(false);

        root.AddComponent<TorchView>();
        var ctrl = root.AddComponent<TorchController>();
        root.AddComponent<Fusion.NetworkObject>();

        var so = new SerializedObject(ctrl);
        so.FindProperty("isGreenTorch").boolValue = isGreen;
        so.ApplyModifiedProperties();

        var prefabPath = $"{PrefabFolder}/{prefabName}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            AssetDatabase.DeleteAsset(prefabPath);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    static TorchController FallbackTorch(Transform parent, string goName, int index, bool isGreen)
    {
        var go = new GameObject(goName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + goName);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(index * 2f, 0f, 0f);

        go.AddComponent<TorchView>();
        var ctrl = go.AddComponent<TorchController>();
        go.AddComponent<Fusion.NetworkObject>();

        var so = new SerializedObject(ctrl);
        so.FindProperty("isGreenTorch").boolValue = isGreen;
        so.ApplyModifiedProperties();

        return ctrl;
    }
}
