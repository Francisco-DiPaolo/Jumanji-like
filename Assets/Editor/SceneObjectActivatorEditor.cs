using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneObjectActivator))]
public class SceneObjectActivatorEditor : Editor
{
    Transform sourceParent;
    bool includeDeepChildren = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── Collector Tool ──", EditorStyles.boldLabel);

        sourceParent = (Transform)EditorGUILayout.ObjectField(
            "Source Parent",
            sourceParent,
            typeof(Transform),
            true
        );

        includeDeepChildren = EditorGUILayout.Toggle("Include All Descendants", includeDeepChildren);

        EditorGUI.BeginDisabledGroup(sourceParent == null);

        if (GUILayout.Button("Collect Active Children → Add & Disable"))
            CollectAndDisable();

        EditorGUI.EndDisabledGroup();

        if (sourceParent == null)
            EditorGUILayout.HelpBox("Asigná un Transform para habilitar la herramienta.", MessageType.Info);
    }

    void CollectAndDisable()
    {
        var activator = (SceneObjectActivator)target;

        SerializedObject so = new SerializedObject(activator);
        SerializedProperty listProp = so.FindProperty("objectsToActivate");

        var collected = new List<GameObject>();

        if (includeDeepChildren)
        {
            foreach (Transform child in sourceParent.GetComponentsInChildren<Transform>(includeInactive: false))
            {
                if (child == sourceParent)
                    continue;

                if (child.gameObject.activeSelf)
                    collected.Add(child.gameObject);
            }
        }
        else
        {
            foreach (Transform child in sourceParent)
            {
                if (child.gameObject.activeSelf)
                    collected.Add(child.gameObject);
            }
        }

        if (collected.Count == 0)
        {
            EditorUtility.DisplayDialog("Sin resultados", "No se encontraron hijos activos dentro del Transform seleccionado.", "OK");
            return;
        }

        Undo.RecordObject(activator, "Collect & Disable Scene Objects");

        foreach (var go in collected)
        {
            bool alreadyInList = false;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == go)
                {
                    alreadyInList = true;
                    break;
                }
            }

            if (!alreadyInList)
            {
                listProp.arraySize++;
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = go;
            }

            Undo.RecordObject(go, "Disable collected object");
            go.SetActive(false);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(activator);

        Debug.Log($"[SceneObjectActivator] {collected.Count} objetos agregados y desactivados.");
    }
}
