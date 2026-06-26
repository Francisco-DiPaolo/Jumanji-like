using System.Collections.Generic;
using Fusion;
using UnityEditor;
using UnityEngine;

public class Challenge4SetupTool : EditorWindow
{
    private static string clockRootName  = "Wheel_Complete";
    private static string soloWheelName  = "Wheel_Solo";
    private static string wheel0Name     = "Wheel_First";
    private static string wheel1Name     = "Wheel_Second";
    private static string wheel2Name     = "Wheel_Third";
    private static string gateAName      = "";
    private static string gateBName      = "";
    private static int    symbolsPerClock = 12;
    private static int    symbolsPerRing  = 6;
    private static float  cycleDuration   = 15f;

    private Vector2 _scroll;
    private string _log = "";

    [MenuItem("Tools/Challenge 4/Setup Wizard")]
    static void OpenWindow()
    {
        var window = GetWindow<Challenge4SetupTool>("Challenge 4 Setup");
        window.minSize = new Vector2(420, 560);
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(8);
        GUILayout.Label("Challenge 4 — Auto Wiring Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Esta herramienta busca los objetos por nombre en la escena activa y los cablea automáticamente.\n" +
            "Asegurate de tener la escena Game.unity abierta.", MessageType.Info);

        EditorGUILayout.Space(8);
        GUILayout.Label("Nombres de objetos en escena", EditorStyles.boldLabel);

        clockRootName = EditorGUILayout.TextField("Reloj Central",   clockRootName);
        soloWheelName = EditorGUILayout.TextField("Rueda Solo (Fase 1)", soloWheelName);
        wheel0Name    = EditorGUILayout.TextField("Rueda 0 (First)",  wheel0Name);
        wheel1Name    = EditorGUILayout.TextField("Rueda 1 (Second)", wheel1Name);
        wheel2Name    = EditorGUILayout.TextField("Rueda 2 (Third)",  wheel2Name);

        EditorGUILayout.Space(8);
        GUILayout.Label("Puertas", EditorStyles.boldLabel);
        gateAName = EditorGUILayout.TextField("Gate A (reja pequeña)", gateAName);
        gateBName = EditorGUILayout.TextField("Gate B (LA GATE)",       gateBName);

        EditorGUILayout.Space(8);
        GUILayout.Label("Parámetros del puzzle", EditorStyles.boldLabel);
        symbolsPerClock = EditorGUILayout.IntField("Símbolos totales en el reloj",   symbolsPerClock);
        symbolsPerRing  = EditorGUILayout.IntField("Símbolos por anillo de rueda",   symbolsPerRing);
        cycleDuration   = EditorGUILayout.FloatField("Duración del ciclo (segundos)", cycleDuration);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("▶  Ejecutar Setup Completo", GUILayout.Height(40)))
        {
            _log = "";
            RunSetup();
        }
        GUI.backgroundColor = Color.white;

        if (!string.IsNullOrEmpty(_log))
        {
            EditorGUILayout.Space(8);
            GUILayout.Label("Log de ejecución:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_log, GUILayout.MinHeight(160));
        }

        EditorGUILayout.EndScrollView();
    }

    void RunSetup()
    {
        Log("=== Challenge 4 Setup iniciado ===");

        var clockRoot   = FindRequired(clockRootName);
        var soloWheelGo = FindRequired(soloWheelName);
        var wheelFirst  = FindRequired(wheel0Name);
        var wheelSecond = FindRequired(wheel1Name);
        var wheelThird  = FindRequired(wheel2Name);

        if (clockRoot == null || soloWheelGo == null || wheelFirst == null || wheelSecond == null || wheelThird == null)
        {
            Log("❌ Faltan GameObjects en la escena. Setup abortado.");
            return;
        }

        Undo.SetCurrentGroupName("Challenge4 Setup");
        int undoGroup = Undo.GetCurrentGroup();

        var clockManager = SetupClockManager(clockRoot);
        var solo = SetupSoloWheel(soloWheelGo);
        var wheel0 = SetupWheel(wheelFirst, 0);
        var wheel1 = SetupWheel(wheelSecond, 1);
        var wheel2 = SetupWheel(wheelThird, 2);
        SetupChallenge4Manager(solo, clockManager, wheel0, wheel1, wheel2);

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.SetDirty(clockRoot);
        EditorUtility.SetDirty(soloWheelGo);
        EditorUtility.SetDirty(wheelFirst);
        EditorUtility.SetDirty(wheelSecond);
        EditorUtility.SetDirty(wheelThird);

        Log("=== ✅ Setup completado. Guardá la escena con Ctrl+S ===");
    }

    CentralClockManager SetupClockManager(GameObject go)
    {
        Log($"\n[Reloj] Configurando '{go.name}'...");

        EnsureComponent<NetworkObject>(go, "NetworkObject");

        var clock = EnsureComponent<CentralClockManager>(go, "CentralClockManager");
        var so = new SerializedObject(clock);

        so.FindProperty("cycleDuration").floatValue = cycleDuration;
        so.FindProperty("totalSymbols").intValue = symbolsPerClock;
        so.FindProperty("activeSymbolCount").intValue = 3;

        var defaultMat = LoadMaterial("Assets/1 Game/Environment/1_Materials/Materials/Albedo/Albedo_Dark.mat");
        var activeMat = LoadMaterial("Assets/1 Game/Environment/1_Materials/Materials/Albedo_Emissions/Red_Emission.mat");

        if (defaultMat != null) so.FindProperty("symbolDefaultMaterial").objectReferenceValue = defaultMat;
        if (activeMat != null) so.FindProperty("symbolActiveMaterial").objectReferenceValue = activeMat;

        var renderers = CollectChildRenderers(go);
        var renderersProp = so.FindProperty("clockSymbolRenderers");
        renderersProp.ClearArray();
        for (int i = 0; i < renderers.Count; i++)
        {
            renderersProp.InsertArrayElementAtIndex(i);
            renderersProp.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        so.ApplyModifiedProperties();
        Log($"  ✓ CentralClockManager: {renderers.Count} renderers asignados");

        var ambientAudio = EnsureComponent<AudioSource>(go, "AudioSource (ambiente)");
        ambientAudio.playOnAwake = false;

        return clock;
    }

    WheelRingController SetupWheel(GameObject go, int stationIndex)
    {
        Log($"\n[Rueda {stationIndex}] Configurando '{go.name}'...");

        EnsureComponent<NetworkObject>(go, "NetworkObject");

        var wheel = EnsureComponent<WheelRingController>(go, "WheelRingController");

        var audio = EnsureComponent<AudioSource>(go, "AudioSource");
        audio.playOnAwake = false;

        var trigger = go.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(go);
            trigger.isTrigger = true;
            trigger.size = new Vector3(3f, 2f, 3f);
            Log("  + BoxCollider trigger añadido");
        }
        else if (!trigger.isTrigger)
        {
            trigger.isTrigger = true;
            Log("  ~ BoxCollider convertido a trigger");
        }

        Transform firstChild = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        var ringRoots = new List<Transform>();
        if (firstChild != null)
        {
            int limit = Mathf.Min(2, firstChild.childCount);
            for (int i = 0; i < limit; i++)
            {
                ringRoots.Add(firstChild.GetChild(i));
            }
        }

        var defaultMat = LoadMaterial("Assets/1 Game/Environment/1_Materials/Materials/Albedo/Albedo_Dark.mat");
        var selectedMat = LoadMaterial("Assets/1 Game/Environment/1_Materials/Materials/Albedo_Emissions/Red_Emission.mat");

        var rotateClip = LoadAudioClip("Assets/1 Game/SFX/Sounds/Wheel/Wheel-Challenge-4-1.wav");
        var errorClip = LoadAudioClip("Assets/1 Game/SFX/Sounds/Wheel/Wheele-Challenge-4.wav");

        var so = new SerializedObject(wheel);
        if (ringRoots.Count > 0) so.FindProperty("ring0").objectReferenceValue = ringRoots[0];
        if (ringRoots.Count > 1) so.FindProperty("ring1").objectReferenceValue = ringRoots[1];

        float stepAngle = symbolsPerRing > 0 ? 360f / symbolsPerRing : 30f;
        so.FindProperty("stepAngle").floatValue = stepAngle;
        so.FindProperty("rotationDuration").floatValue = 0.25f;
        so.FindProperty("rotationEase").enumValueIndex = (int)LeanTweenType.easeOutBack;
        if (defaultMat != null) so.FindProperty("symbolDefaultMaterial").objectReferenceValue = defaultMat;
        if (selectedMat != null) so.FindProperty("symbolSelectedMaterial").objectReferenceValue = selectedMat;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        if (rotateClip != null) so.FindProperty("rotateClip").objectReferenceValue = rotateClip;
        if (errorClip != null) so.FindProperty("errorClip").objectReferenceValue = errorClip;
        so.FindProperty("shakeStrength").floatValue = 0.08f;
        so.FindProperty("shakeDuration").floatValue = 0.35f;

        for (int ringIdx = 0; ringIdx < 2; ringIdx++)
        {
            if (ringIdx >= ringRoots.Count) break;
            var ringGo = ringRoots[ringIdx].gameObject;
            var ringRenderers = CollectChildRenderers(ringGo);
            string propName = ringIdx == 0 ? "ring0SymbolRenderers" : "ring1SymbolRenderers";
            var rProp = so.FindProperty(propName);
            rProp.ClearArray();
            for (int i = 0; i < ringRenderers.Count; i++)
            {
                rProp.InsertArrayElementAtIndex(i);
                rProp.GetArrayElementAtIndex(i).objectReferenceValue = ringRenderers[i];
            }
            Log($"  ✓ Ring{ringIdx}: {ringRenderers.Count} renderers");

            var interactable = EnsureComponent<RingInteractable>(ringGo, $"RingInteractable (Ring {ringIdx})");
            var soInteractable = new SerializedObject(interactable);
            soInteractable.FindProperty("ringIndex").intValue = ringIdx;
            soInteractable.ApplyModifiedProperties();
        }

        so.ApplyModifiedProperties();

        Log($"  ✓ Rings encontrados: {ringRoots.Count}");
        return wheel;
    }

    SoloWheelController SetupSoloWheel(GameObject go)
    {
        Log($"\n[Rueda Solo] Configurando '{go.name}'...");

        EnsureComponent<NetworkObject>(go, "NetworkObject");

        var wheel = EnsureComponent<SoloWheelController>(go, "SoloWheelController");

        var audio = EnsureComponent<AudioSource>(go, "AudioSource");
        audio.playOnAwake = false;

        var trigger = go.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(go);
            trigger.isTrigger = true;
            trigger.size = new Vector3(3f, 2f, 3f);
            Log("  + BoxCollider trigger añadido");
        }
        else if (!trigger.isTrigger)
        {
            trigger.isTrigger = true;
            Log("  ~ BoxCollider convertido a trigger");
        }

        Transform firstChild = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        var ringRoots = new List<Transform>();
        if (firstChild != null)
        {
            int limit = Mathf.Min(3, firstChild.childCount);
            for (int i = 0; i < limit; i++)
            {
                ringRoots.Add(firstChild.GetChild(i));
            }
        }

        var defaultMat = LoadMaterial("Assets/1 Game/Environment/1_Materials/Materials/Albedo/Albedo_Dark.mat");
        var selectedMat = LoadMaterial("Assets/1 Game/Environment/1_Materials/Materials/Albedo_Emissions/Red_Emission.mat");

        var rotateClip = LoadAudioClip("Assets/1 Game/SFX/Sounds/Wheel/Wheel-Challenge-4-1.wav");
        var errorClip = LoadAudioClip("Assets/1 Game/SFX/Sounds/Wheel/Wheele-Challenge-4.wav");

        var so = new SerializedObject(wheel);
        if (ringRoots.Count > 0) so.FindProperty("ring0").objectReferenceValue = ringRoots[0];
        if (ringRoots.Count > 1) so.FindProperty("ring1").objectReferenceValue = ringRoots[1];
        if (ringRoots.Count > 2) so.FindProperty("ring2").objectReferenceValue = ringRoots[2];

        float stepAngle = symbolsPerRing > 0 ? 360f / symbolsPerRing : 30f;
        so.FindProperty("stepAngle").floatValue = stepAngle;
        so.FindProperty("rotationDuration").floatValue = 0.25f;
        so.FindProperty("rotationEase").enumValueIndex = (int)LeanTweenType.easeOutBack;
        if (defaultMat != null) so.FindProperty("symbolDefaultMaterial").objectReferenceValue = defaultMat;
        if (selectedMat != null) so.FindProperty("symbolSelectedMaterial").objectReferenceValue = selectedMat;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        if (rotateClip != null) so.FindProperty("rotateClip").objectReferenceValue = rotateClip;
        if (errorClip != null) so.FindProperty("errorClip").objectReferenceValue = errorClip;
        so.FindProperty("shakeStrength").floatValue = 0.08f;
        so.FindProperty("shakeDuration").floatValue = 0.35f;

        for (int ringIdx = 0; ringIdx < 3; ringIdx++)
        {
            if (ringIdx >= ringRoots.Count) break;
            var ringGo = ringRoots[ringIdx].gameObject;
            var ringRenderers = CollectChildRenderers(ringGo);
            string propName = ringIdx == 0 ? "ring0SymbolRenderers"
                           : ringIdx == 1 ? "ring1SymbolRenderers"
                           : "ring2SymbolRenderers";
            var rProp = so.FindProperty(propName);
            rProp.ClearArray();
            for (int i = 0; i < ringRenderers.Count; i++)
            {
                rProp.InsertArrayElementAtIndex(i);
                rProp.GetArrayElementAtIndex(i).objectReferenceValue = ringRenderers[i];
            }
            Log($"  ✓ Ring{ringIdx}: {ringRenderers.Count} renderers");

            var interactable = EnsureComponent<RingInteractable>(ringGo, $"RingInteractable (Ring {ringIdx})");
            var soInteractable = new SerializedObject(interactable);
            soInteractable.FindProperty("ringIndex").intValue = ringIdx;
            soInteractable.ApplyModifiedProperties();
        }

        so.ApplyModifiedProperties();
        return wheel;
    }

    void SetupChallenge4Manager(SoloWheelController solo, CentralClockManager clock, WheelRingController w0, WheelRingController w1, WheelRingController w2)
    {
        Log("\n[Manager] Creando Challenge4Manager...");

        var existing = Object.FindFirstObjectByType<Challenge4Manager>();
        GameObject managerGo;

        if (existing != null)
        {
            managerGo = existing.gameObject;
            Log("  ~ Challenge4Manager ya existe, actualizando referencias.");
        }
        else
        {
            managerGo = new GameObject("Challenge4Manager");
            Undo.RegisterCreatedObjectUndo(managerGo, "Create Challenge4Manager");
            Log("  + GameObject 'Challenge4Manager' creado.");
        }

        EnsureComponent<NetworkObject>(managerGo, "NetworkObject");
        var manager = EnsureComponent<Challenge4Manager>(managerGo, "Challenge4Manager");
        var audio = EnsureComponent<AudioSource>(managerGo, "AudioSource");
        audio.playOnAwake = false;

        var phase1Success = LoadAudioClip("Assets/1 Game/SFX/Sounds/Wheel/Wheel-Complete.wav");
        var phase2Success = LoadAudioClip("Assets/1 Game/SFX/Sounds/Gate.wav");
        var wrongClip = LoadAudioClip("Assets/1 Game/SFX/Sounds/Wheel/Wheel-Rope.wav");

        var so = new SerializedObject(manager);
        so.FindProperty("soloWheel").objectReferenceValue = solo;
        so.FindProperty("centralClock").objectReferenceValue = clock;
        so.FindProperty("wheel0").objectReferenceValue = w0;
        so.FindProperty("wheel1").objectReferenceValue = w1;
        so.FindProperty("wheel2").objectReferenceValue = w2;
        so.FindProperty("puzzleAudioSource").objectReferenceValue = audio;
        if (phase1Success != null) so.FindProperty("phase1SuccessClip").objectReferenceValue = phase1Success;
        if (phase2Success != null) so.FindProperty("phase2SuccessClip").objectReferenceValue = phase2Success;
        if (wrongClip != null) so.FindProperty("wrongCombinationClip").objectReferenceValue = wrongClip;

        if (!string.IsNullOrEmpty(gateAName))
        {
            var gateAGo = GameObject.Find(gateAName);
            if (gateAGo != null)
            {
                var door = gateAGo.GetComponent<LeanTweenDoor>() ?? Undo.AddComponent<LeanTweenDoor>(gateAGo);
                so.FindProperty("gateA").objectReferenceValue = door;
                Log($"  ✓ Gate A asignada: {gateAName}");
            }
            else Log($"  ⚠ Gate A no encontrada: '{gateAName}'");
        }

        if (!string.IsNullOrEmpty(gateBName))
        {
            var gateBGo = GameObject.Find(gateBName);
            if (gateBGo != null)
            {
                var door = gateBGo.GetComponent<LeanTweenDoor>() ?? Undo.AddComponent<LeanTweenDoor>(gateBGo);
                so.FindProperty("gateB").objectReferenceValue = door;
                Log($"  ✓ Gate B asignada: {gateBName}");
            }
            else Log($"  ⚠ Gate B no encontrada: '{gateBName}'");
        }
        else
        {
            Log("  ⚠ Gate B no especificada. Asignala manualmente en el Inspector.");
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(managerGo);
        Log("  ✓ Challenge4Manager configurado.");
    }

    List<Transform> FindRingRoots(GameObject wheelRoot)
    {
        var result = new List<Transform>();
        foreach (Transform child in wheelRoot.transform)
        {
            string n = child.name.ToLower();
            if (n.Contains("ring") || n.Contains("anillo") || n.Contains("aro"))
                result.Add(child);
        }

        if (result.Count == 0)
        {
            Log($"  ⚠ No se encontraron hijos con 'ring/anillo/aro' en '{wheelRoot.name}'. Buscando en nietos...");
            foreach (Transform child in wheelRoot.transform)
            {
                foreach (Transform grandchild in child)
                {
                    string n = grandchild.name.ToLower();
                    if (n.Contains("ring") || n.Contains("anillo") || n.Contains("aro"))
                        result.Add(grandchild);
                }
            }
        }

        if (result.Count == 0)
        {
            Log($"  ⚠ Sin rings en '{wheelRoot.name}'. Usando los 3 primeros hijos como fallback.");
            int max = Mathf.Min(3, wheelRoot.transform.childCount);
            for (int i = 0; i < max; i++)
                result.Add(wheelRoot.transform.GetChild(i));
        }

        return result;
    }

    List<Renderer> CollectChildRenderers(GameObject root)
    {
        var result = new List<Renderer>();
        var all = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in all)
        {
            if (r.gameObject != root)
                result.Add(r);
        }
        return result;
    }

    T EnsureComponent<T>(GameObject go, string label) where T : Component
    {
        var existing = go.GetComponent<T>();
        if (existing != null)
        {
            Log($"  ~ {label} ya existe.");
            return existing;
        }
        var added = Undo.AddComponent<T>(go);
        Log($"  + {label} añadido.");
        return added;
    }

    Material LoadMaterial(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) Log($"  ⚠ Material no encontrado: {path}");
        return mat;
    }

    AudioClip LoadAudioClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) Log($"  ⚠ AudioClip no encontrado: {path}");
        return clip;
    }

    GameObject FindRequired(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) Log($"  ❌ GameObject no encontrado en escena: '{name}'");
        else Log($"  ✓ Encontrado: '{name}'");
        return go;
    }

    void Log(string message)
    {
        _log += message + "\n";
        Debug.Log("[Challenge4Setup] " + message);
    }
}
