using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class SceneSetupTool : EditorWindow
{
    [MenuItem("Tools/Setup Torch Puzzle")]
    static void SetupTorchPuzzle()
    {
        var root = new GameObject("TorchPuzzleRoot");
        Undo.RegisterCreatedObjectUndo(root, "Create TorchPuzzleRoot");
        root.transform.position = Vector3.zero;

        var torchList = new List<TorchController>();
        torchList.Add(CreateTorch(root.transform, "Torch_1", 0, false));
        torchList.Add(CreateTorch(root.transform, "Torch_2", 1, false));
        torchList.Add(CreateTorch(root.transform, "Torch_3_Green", 2, true));

        var door = CreateDoor(root.transform);
        var brick = CreateBrick(root.transform);
        CreateManager(root, torchList, door, brick);

        Selection.activeGameObject = root;
        Debug.Log("[TorchPuzzle] Setup completo. Revisa TorchPuzzleRoot en la jerarquía.");
    }

    static TorchController CreateTorch(Transform parent, string name, int index, bool isGreen)
    {
        return TorchPrefabCreator.InstantiateTorch(parent, name, index, isGreen);
    }


    static GameObject CreateDoor(Transform parent)
    {
        var door = new GameObject("Door");
        Undo.RegisterCreatedObjectUndo(door, "Create Door");
        door.transform.SetParent(parent, false);
        door.transform.position = new Vector3(3f, 0f, 4f);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(cube, "Create Door Mesh");
        cube.name = "DoorMesh";
        cube.transform.SetParent(door.transform, false);
        cube.transform.localScale = new Vector3(2f, 3f, 0.2f);

        door.AddComponent<Animator>();

        return door;
    }

    static BrickInteractable CreateBrick(Transform parent)
    {
        var go = new GameObject("Brick");
        Undo.RegisterCreatedObjectUndo(go, "Create Brick");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(3f, 0f, 2f);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(cube, "Create Brick Mesh");
        cube.name = "BrickMesh";
        cube.transform.SetParent(go.transform, false);
        cube.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        DestroyImmediate(cube.GetComponent<BoxCollider>());

        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2f, 2f, 2f);

        go.AddComponent<BrickInteractableView>();
        var netObj = go.AddComponent<Fusion.NetworkObject>();
        var brick = go.AddComponent<BrickInteractable>();

        var hint = CreateInteractHint(go.transform);

        var so = new SerializedObject(brick);
        so.FindProperty("interactHint").objectReferenceValue = hint;
        so.ApplyModifiedProperties();

        return brick;
    }

    static GameObject CreateInteractHint(Transform parent)
    {
        var canvasGo = new GameObject("InteractHint");
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create InteractHint");
        canvasGo.transform.SetParent(parent, false);
        canvasGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200f, 60f);
        rect.localScale = Vector3.one * 0.005f;

        var textGo = new GameObject("HintText");
        Undo.RegisterCreatedObjectUndo(textGo, "Create HintText");
        textGo.transform.SetParent(canvasGo.transform, false);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "[E] Activar";
        text.fontSize = 36;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        canvasGo.SetActive(false);
        return canvasGo;
    }

    static void CreateManager(GameObject root, List<TorchController> torches, GameObject door, BrickInteractable brick)
    {
        var netObj = root.AddComponent<Fusion.NetworkObject>();
        var audioSource = root.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        var manager = root.AddComponent<GlobalPuzzleManager>();

        var so = new SerializedObject(manager);

        var torchListProp = so.FindProperty("torches");
        torchListProp.ClearArray();
        for (int i = 0; i < torches.Count; i++)
        {
            torchListProp.InsertArrayElementAtIndex(i);
            torchListProp.GetArrayElementAtIndex(i).objectReferenceValue = torches[i];
        }

        so.FindProperty("doorAnimator").objectReferenceValue = door.GetComponent<Animator>();
        so.FindProperty("doorAudioSource").objectReferenceValue = audioSource;
        so.FindProperty("syncWindowDuration").floatValue = 0.5f;
        so.ApplyModifiedProperties();
    }

    [MenuItem("Tools/Setup Jumanji Scene")]
    static void SetupScene()
    {
        Vector3 spawnPos = new Vector3(-13.79f, -13.34f, 45.45f); // Defaults from teammate's scene
        float spawnRot = -344.922f;

        // Try to find the local player in scene to extract position and then remove it
        var localPlayer = GameObject.Find("Player");
        if (localPlayer == null) localPlayer = GameObject.Find("PlayerCapsule");
        
        if (localPlayer != null)
        {
            spawnPos = localPlayer.transform.position;
            spawnRot = localPlayer.transform.eulerAngles.y;
            Undo.DestroyObjectImmediate(localPlayer);
            Debug.Log($"Removed local player and saved spawn point: {spawnPos}");
        }

        RemoveOldFusionLauncher();
        CreateSessionLauncher();
        var lobbyCamera = CreateLobbyCamera();
        CreateLoginCanvas(lobbyCamera);
        CreatePlayerSpawner(spawnPos, spawnRot);

        Debug.Log("Scene setup complete. Save the scene with Ctrl+S.");
    }

    static void RemoveOldFusionLauncher()
    {
        var old = GameObject.Find("FusionLauncher");
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
            Debug.Log("Removed old FusionLauncher GameObject.");
        }
    }

    static void CreateSessionLauncher()
    {
        if (Object.FindFirstObjectByType<SessionLauncher>() != null)
        {
            Debug.Log("SessionLauncher already exists in scene.");
            return;
        }

        var go = new GameObject("SessionLauncher");
        Undo.RegisterCreatedObjectUndo(go, "Create SessionLauncher");
        var launcher = go.AddComponent<SessionLauncher>();

        string[] guids = AssetDatabase.FindAssets("t:Prefab NetworkRunner");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("NetworkRunner"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var so = new SerializedObject(launcher);
                var prop = so.FindProperty("runnerPrefab");
                if (prop != null)
                {
                    prop.objectReferenceValue = prefab;
                    so.ApplyModifiedProperties();
                    Debug.Log($"Assigned NetworkRunner prefab from: {path}");
                }
                break;
            }
        }
    }

    static Camera CreateLobbyCamera()
    {
        var existing = GameObject.Find("LobbyCamera");
        if (existing != null)
            return existing.GetComponent<Camera>();

        var go = new GameObject("LobbyCamera");
        Undo.RegisterCreatedObjectUndo(go, "Create LobbyCamera");
        go.transform.position = new Vector3(0, 3, -10);
        go.transform.rotation = Quaternion.Euler(15, 0, 0);
        go.tag = "MainCamera";

        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.depth = 0;

        go.AddComponent<AudioListener>();

        Debug.Log("LobbyCamera created.");
        return cam;
    }

    static void CreateLoginCanvas(Camera lobbyCamera)
    {
        if (Object.FindFirstObjectByType<LoginUI>() != null)
        {
            Debug.Log("LoginUI already exists in scene.");
            return;
        }

        var canvasGo = new GameObject("LoginCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create LoginCanvas");

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = CreatePanel(canvasGo.transform);
        var nicknameInput = CreateInputField(panelGo.transform);
        var joinButton = CreateButton(panelGo.transform, "Join Game");
        var statusText = CreateStatusText(panelGo.transform);

        var loginUI = canvasGo.AddComponent<LoginUI>();
        var so = new SerializedObject(loginUI);
        so.FindProperty("loginPanel").objectReferenceValue = panelGo;
        so.FindProperty("nicknameInput").objectReferenceValue = nicknameInput;
        so.FindProperty("joinButton").objectReferenceValue = joinButton;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("lobbyCamera").objectReferenceValue = lobbyCamera;
        so.ApplyModifiedProperties();

        var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            var esGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        Debug.Log("Login Canvas created with LobbyCamera reference.");
    }

    static void CreatePlayerSpawner(Vector3 pos, float rot)
    {
        // Delete existing to ensure fresh state
        var oldManagers = GameObject.Find("MultiplayerManagers");
        if (oldManagers != null) Undo.DestroyObjectImmediate(oldManagers);

        var mgr = new GameObject("MultiplayerManagers");
        Undo.RegisterCreatedObjectUndo(mgr, "Create MultiplayerManagers");
        
        var spawner = mgr.AddComponent<PlayerSpawner>();
        var so = new SerializedObject(spawner);
        so.FindProperty("spawnPosition").vector3Value = pos;
        so.FindProperty("spawnRotationY").floatValue = rot;

        // Find the networked player prefab
        string[] guids = AssetDatabase.FindAssets("Player t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("Assets/1 Game/Player/Prefab/Player.prefab"))
            {
                var prefabProp = so.FindProperty("playerPrefab");
                var guidProp = prefabProp.FindPropertyRelative("RawGuidValue") ?? prefabProp.FindPropertyRelative("m_AssetGuid");
                
                if (guidProp != null)
                {
                    guidProp.stringValue = guid;
                    Debug.Log($"Successfully assigned new player prefab: {path}");
                }
                break;
            }
        }

        so.ApplyModifiedProperties();
        Debug.Log("PlayerSpawner setup complete.");
    }

    static GameObject CreatePanel(Transform parent)
    {
        var go = new GameObject("LoginPanel");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420, 320);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 30, 30);
        layout.spacing = 20;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(go.transform, false);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "Enter Nickname";
        title.fontSize = 32;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0, 50);

        return go;
    }

    static TMP_InputField CreateInputField(Transform parent)
    {
        var go = new GameObject("NicknameInput");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 50);

        var textAreaGo = new GameObject("Text Area");
        textAreaGo.transform.SetParent(go.transform, false);
        var textAreaRect = textAreaGo.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);

        var placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(textAreaGo.transform, false);
        var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Your name...";
        placeholder.fontSize = 20;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        var phRect = placeholderGo.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(textAreaGo.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 20;
        text.color = Color.white;
        var txtRect = textGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        var inputField = go.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.characterLimit = 20;

        return inputField;
    }

    static Button CreateButton(Transform parent, string label)
    {
        var go = new GameObject("JoinButton");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 0.9f, 1f);

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 50);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 22;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        var txtRect = textGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        return go.AddComponent<Button>();
    }

    static TextMeshProUGUI CreateStatusText(Transform parent)
    {
        var go = new GameObject("StatusText");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = 18;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 30);
        return text;
    }
}
