#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// координати клітинки під курсором
// вимкнути - Tools/Cell Coordinates
[InitializeOnLoad]
public static class CellCoordsOverlay
{
    private const string PrefKey = "CellCoordsOverlay.Enabled";
    private const string MenuPath = "Tools/Cell Coordinates";

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static CellCoordsOverlay()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static void OnSceneGUI(SceneView view)
    {
        if (!Enabled) return;

        Event e = Event.current;
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            view.Repaint();

        Vector3 world = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        int cx = Mathf.FloorToInt(world.x);
        int cy = Mathf.FloorToInt(world.y);

        Handles.color = new Color(0.36f, 0.88f, 0.9f, 0.9f);   // #5CE1E6
        Vector3 a = new Vector3(cx, cy, 0f);
        Handles.DrawAAPolyLine(3f, a, a + Vector3.right, a + new Vector3(1f, 1f, 0f), a + Vector3.up, a);

        Handles.BeginGUI();
        var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        style.normal.textColor = Color.white;
        string text = $"({cx}; {cy})";
        Vector2 size = style.CalcSize(new GUIContent(text));
        var rect = new Rect(e.mousePosition.x + 16f, e.mousePosition.y + 8f, size.x + 10f, size.y + 4f);
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.23f, 0.3f, 0.9f));   // #1F3B4D
        GUI.Label(new Rect(rect.x + 5f, rect.y + 2f, size.x, size.y), text, style);
        Handles.EndGUI();
    }
}
#endif
