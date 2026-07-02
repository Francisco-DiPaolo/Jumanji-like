using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class NegativeScaleColliderFixer : EditorWindow
{
    struct ColliderEntry
    {
        public GameObject go;
        public Collider collider;
        public Vector3 lossyScale;
        public Vector3 localScale;
    }

    struct ColliderSource { public bool localNegative; public bool inheritedNegative; }

    List<ColliderEntry> results = new();
    List<ColliderSource> sources = new();
    Vector2 scroll;
    bool scanned = false;
    int inactiveCount = 0;

    static readonly Color ColorWarning  = new(1f, 0.45f, 0.1f);
    static readonly Color ColorInherited = new(1f, 0.75f, 0.1f);
    static readonly Color ColorOk       = new(0.3f, 0.85f, 0.4f);

    [MenuItem("Tools/Negative Scale Collider Fixer")]
    static void Open() => GetWindow<NegativeScaleColliderFixer>("Scale Collider Fixer");

    void OnGUI()
    {
        DrawHeader();
        DrawToolbar();

        if (!scanned)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("Presioná Escanear para buscar colliders con escala negativa.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            return;
        }

        if (results.Count == 0)
        {
            GUILayout.FlexibleSpace();
            GUI.color = ColorOk;
            GUILayout.Label("✔  No se encontraron colliders con escala negativa.", EditorStyles.centeredGreyMiniLabel);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            return;
        }

        DrawResultsHeader();
        DrawResultsList();
    }

    void DrawHeader()
    {
        var bg = EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.15f) : new Color(0.85f, 0.85f, 0.87f);
        EditorGUI.DrawRect(new Rect(0, 0, position.width, 48), bg);

        GUILayout.Space(8);
        GUILayout.Label("  Negative Scale Collider Fixer", new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
        });
        GUILayout.Label("  Detecta y repara colliders cuyo transform tiene escala negativa.", EditorStyles.miniLabel);
        GUILayout.Space(6);
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("🔍  Escanear escena", EditorStyles.toolbarButton, GUILayout.Width(150)))
            Scan();

        EditorGUI.BeginDisabledGroup(results.Count == 0);
        GUI.backgroundColor = ColorWarning;
        if (GUILayout.Button($"⚙  Arreglar todos ({results.Count})", EditorStyles.toolbarButton, GUILayout.Width(190)))
            FixAll();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        if (scanned)
            GUILayout.Label($"  ({inactiveCount} inactivos incluidos)", EditorStyles.miniLabel);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    void DrawResultsHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("GameObject", EditorStyles.toolbarButton, GUILayout.MinWidth(160));
        GUILayout.Label("Collider", EditorStyles.toolbarButton, GUILayout.Width(90));
        GUILayout.Label("Origen", EditorStyles.toolbarButton, GUILayout.Width(80));
        GUILayout.Label("Escala Local", EditorStyles.toolbarButton, GUILayout.Width(145));
        GUILayout.Label("Escala Mundial", EditorStyles.toolbarButton, GUILayout.Width(145));
        GUILayout.Label("", EditorStyles.toolbarButton, GUILayout.Width(140));
        EditorGUILayout.EndHorizontal();
    }

    void DrawResultsList()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        for (int i = results.Count - 1; i >= 0; i--)
        {
            var entry  = results[i];
            var source = sources[i];
            if (entry.go == null) { results.RemoveAt(i); sources.RemoveAt(i); continue; }

            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1), new Color(0.1f, 0.1f, 0.1f, 0.3f));

            EditorGUILayout.BeginHorizontal(GUILayout.Height(24));

            GUILayout.Label(entry.go.name, GUILayout.MinWidth(160));
            GUILayout.Label(entry.collider.GetType().Name.Replace("Collider", ""), GUILayout.Width(90));

            if (source.localNegative && source.inheritedNegative)
            {
                GUI.color = ColorWarning;
                GUILayout.Label("Local+Padre", GUILayout.Width(80));
            }
            else if (source.localNegative)
            {
                GUI.color = ColorWarning;
                GUILayout.Label("Local", GUILayout.Width(80));
            }
            else
            {
                GUI.color = ColorInherited;
                GUILayout.Label("Heredada", GUILayout.Width(80));
            }

            GUI.color = HasNegative(entry.localScale) ? ColorWarning : Color.white;
            GUILayout.Label(FormatScale(entry.localScale), GUILayout.Width(145));
            GUI.color = HasNegative(entry.lossyScale) ? ColorWarning : Color.white;
            GUILayout.Label(FormatScale(entry.lossyScale), GUILayout.Width(145));
            GUI.color = Color.white;

            if (GUILayout.Button("Seleccionar", GUILayout.Width(80)))
            {
                Selection.activeGameObject = entry.go;
                SceneView.FrameLastActiveSceneView();
            }

            GUI.backgroundColor = source.inheritedNegative && !source.localNegative ? ColorInherited : ColorWarning;
            if (GUILayout.Button("Arreglar", GUILayout.Width(70)))
            {
                Fix(entry, source);
                results.RemoveAt(i);
                sources.RemoveAt(i);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Local: la escala negativa está en el propio objeto → se invierte y se rota 180°.\n" +
            "Heredada: el objeto tiene escala positiva pero un padre tiene escala negativa → se corrige el padre más cercano con escala negativa.\n" +
            "IMPORTANTE: Este scanner incluye objetos INACTIVOS, por eso ahora muestra todos.",
            MessageType.Info);
    }

    void Scan()
    {
        results.Clear();
        sources.Clear();
        scanned = true;
        inactiveCount = 0;

        var allColliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var col in allColliders)
        {
            Vector3 local = col.transform.localScale;
            Vector3 lossy = col.transform.lossyScale;

            bool localNeg    = HasNegative(local);
            bool inheritedNeg = !localNeg && HasNegative(lossy);

            if (!localNeg && !inheritedNeg) continue;

            if (!col.gameObject.activeInHierarchy)
                inactiveCount++;

            results.Add(new ColliderEntry
            {
                go         = col.gameObject,
                collider   = col,
                localScale = local,
                lossyScale = lossy
            });

            sources.Add(new ColliderSource
            {
                localNegative    = localNeg,
                inheritedNegative = inheritedNeg || (localNeg && HasNegative(lossy))
            });
        }

        Debug.Log($"[NegativeScaleFixer] Escaneado completo. {results.Count} collider(s) encontrados ({inactiveCount} en objetos inactivos).");
        Repaint();
    }

    void FixAll()
    {
        for (int i = results.Count - 1; i >= 0; i--)
        {
            if (results[i].go != null)
                Fix(results[i], sources[i]);
        }
        results.Clear();
        sources.Clear();
        Debug.Log("[NegativeScaleFixer] Todos los colliders corregidos.");
    }

    void Fix(ColliderEntry entry, ColliderSource source)
    {
        if (entry.go == null) return;

        Transform target = entry.go.transform;

        if (source.inheritedNegative && !source.localNegative)
            target = FindNegativeParent(entry.go.transform);

        if (target == null) return;

        Undo.RecordObject(target, "Fix Negative Scale");

        Vector3 ls = target.localScale;
        bool flipX = ls.x < 0;
        bool flipY = ls.y < 0;
        bool flipZ = ls.z < 0;

        target.localScale = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));

        Vector3 euler = target.localEulerAngles;
        if (flipX) euler.x += 180f;
        if (flipY) euler.y += 180f;
        if (flipZ) euler.z += 180f;
        target.localEulerAngles = euler;

        EditorUtility.SetDirty(target);
        Debug.Log($"[NegativeScaleFixer] Corregido: {target.name}  {ls} → {target.localScale}");
    }

    static Transform FindNegativeParent(Transform t)
    {
        Transform current = t.parent;
        while (current != null)
        {
            if (HasNegative(current.localScale))
                return current;
            current = current.parent;
        }
        return null;
    }

    static bool HasNegative(Vector3 v) => v.x < 0 || v.y < 0 || v.z < 0;

    static string FormatScale(Vector3 v) =>
        $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
}
