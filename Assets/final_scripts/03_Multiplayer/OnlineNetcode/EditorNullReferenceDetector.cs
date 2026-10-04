#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EditorNullReferenceDetector
{
    static Object[] lastSelection;

    static EditorNullReferenceDetector()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update += Monitor;
    }

    static void OnSelectionChanged()
    {
        lastSelection = Selection.objects;

        Debug.Log(
            "[EditorNullReferenceDetector] SELECCIÓN CAMBIÓ: " +
            GetSelectionInfo()
        );
    }

    static void Monitor()
    {
        if (lastSelection == null)
            return;

        Object[] current = Selection.objects;

        if (current == null || current.Length == 0)
            return;

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] == null)
            {
                Debug.LogError(
                    "[EditorNullReferenceDetector] " +
                    "LA SELECCIÓN CONTIENE UN OBJETO DESTRUIDO.\n" +
                    "Índice: " + i
                );

                PrintLastSelection();
                return;
            }
        }
    }

    static void PrintLastSelection()
    {
        if (lastSelection == null)
        {
            Debug.LogError(
                "[EditorNullReferenceDetector] No existe selección anterior."
            );

            return;
        }

        for (int i = 0; i < lastSelection.Length; i++)
        {
            Object obj = lastSelection[i];

            if (obj == null)
            {
                Debug.LogError(
                    "[EditorNullReferenceDetector] " +
                    "OBJETO ANTERIOR NULL. Índice: " + i
                );

                continue;
            }

            GameObject go = obj as GameObject;

            if (go != null)
            {
                Debug.LogError(
                    "[EditorNullReferenceDetector] OBJETO ANTERIOR:\n" +
                    "GameObject: " + GetPath(go) + "\n" +
                    "InstanceID: " + obj.GetInstanceID()
                );

                continue;
            }

            Component component = obj as Component;

            if (component != null)
            {
                Debug.LogError(
                    "[EditorNullReferenceDetector] COMPONENTE ANTERIOR:\n" +
                    "Tipo: " + component.GetType().FullName + "\n" +
                    "GameObject: " + GetPath(component.gameObject) + "\n" +
                    "InstanceID: " + obj.GetInstanceID()
                );

                continue;
            }

            Debug.LogError(
                "[EditorNullReferenceDetector] OBJETO ANTERIOR:\n" +
                "Tipo: " + obj.GetType().FullName + "\n" +
                "Nombre: " + obj.name + "\n" +
                "InstanceID: " + obj.GetInstanceID()
            );
        }
    }

    static string GetSelectionInfo()
    {
        if (Selection.objects == null || Selection.objects.Length == 0)
            return "<NINGUNA>";

        string result = "";

        foreach (Object obj in Selection.objects)
        {
            if (obj == null)
            {
                result += "\n<NULL>";
                continue;
            }

            GameObject go = obj as GameObject;

            if (go != null)
            {
                result +=
                    "\nGameObject: " +
                    GetPath(go) +
                    " | ID: " +
                    go.GetInstanceID();

                continue;
            }

            Component component = obj as Component;

            if (component != null)
            {
                result +=
                    "\nComponent: " +
                    component.GetType().Name +
                    " | GameObject: " +
                    GetPath(component.gameObject) +
                    " | ID: " +
                    component.GetInstanceID();

                continue;
            }

            result +=
                "\nObject: " +
                obj.GetType().Name +
                " | Nombre: " +
                obj.name +
                " | ID: " +
                obj.GetInstanceID();
        }

        return result;
    }

    static string GetPath(GameObject go)
    {
        if (go == null)
            return "<NULL>";

        string path = go.name;
        Transform current = go.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
#endif