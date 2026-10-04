#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RazorMinigame))]
public class RazorMinigameEditor : Editor
{
    private bool _showWaypointHandles = true;
    private float _previewSliderT = 0f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RazorMinigame minigame = (RazorMinigame)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🛠️ RAZOR PATH WAYPOINT TOOLS (5 POINTS)", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _showWaypointHandles = EditorGUILayout.ToggleLeft("Show Interactive Handles in Scene View", _showWaypointHandles);

        EditorGUILayout.Space(5);
        if (GUILayout.Button("📐 Distribute 5 Waypoints Evenly Along Path", GUILayout.Height(28)))
        {
            minigame.EditorDistributeWaypointsEvenly();
            EditorUtility.SetDirty(minigame);
        }

        if (GUILayout.Button("📍 Preview Razor at Start Position (P1)", GUILayout.Height(26)))
        {
            minigame.EditorPreviewStart();
        }

        if (GUILayout.Button("🎯 Capture Scene Razor Position as Start (P1)", GUILayout.Height(26)))
        {
            minigame.EditorCaptureStart();
            EditorUtility.SetDirty(minigame);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🔍 Preview Razor Path Progress (Slider T: 0..1):", EditorStyles.miniBoldLabel);
        EditorGUI.BeginChangeCheck();
        _previewSliderT = EditorGUILayout.Slider("Progress (t)", _previewSliderT, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            RectTransform razorRect = minigame.transform.Find("RazorImage")?.GetComponent<RectTransform>();
            if (razorRect != null)
            {
                Undo.RecordObject(razorRect, "Preview Razor Path Progress");
                razorRect.anchoredPosition = minigame.EvaluatePathPosition(_previewSliderT);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void OnSceneGUI()
    {
        if (!_showWaypointHandles) return;

        RazorMinigame minigame = (RazorMinigame)target;
        Vector2[] pts = minigame.GetEffectiveWaypoints();
        if (pts == null || pts.Length < 2) return;

        RectTransform parentRect = minigame.GetComponent<RectTransform>();
        if (parentRect == null) return;

        // 1. Draw path line
        Handles.matrix = parentRect.localToWorldMatrix;
        Handles.color = new Color(0f, 0.85f, 1f, 0.85f);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Handles.DrawDottedLine(pts[i], pts[i + 1], 4f);
        }

        // 2. Draw handles and labels (100% English)
        string[] nodeNames = new string[] { "P1: Start", "P2: Lower Strop", "P3: Good Zone", "P4: Upper Strop", "P5: Top Anchor" };

        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 p = pts[i];
            Color nodeColor = (i == 0) ? Color.green : (i == pts.Length - 1 ? Color.red : (i == 2 ? Color.yellow : Color.cyan));
            Handles.color = nodeColor;

            // Draw point disk
            Handles.DrawSolidDisc(p, Vector3.forward, 8f);

            // Interactive 2D FreeMoveHandle
            EditorGUI.BeginChangeCheck();
            Vector2 movedPos = Handles.FreeMoveHandle(p, 14f, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(minigame, "Move Waypoint " + (i + 1));
                if (minigame.WaypointTransforms != null && i < minigame.WaypointTransforms.Length && minigame.WaypointTransforms[i] != null)
                {
                    Undo.RecordObject(minigame.WaypointTransforms[i], "Move Waypoint Transform " + (i + 1));
                    minigame.WaypointTransforms[i].anchoredPosition = movedPos;
                }
                else if (minigame.Waypoints != null && i < minigame.Waypoints.Length)
                {
                    minigame.Waypoints[i] = movedPos;
                }
                EditorUtility.SetDirty(minigame);
            }

            // Waypoint Label
            string labelText = (i < nodeNames.Length) ? nodeNames[i] : $"P{i + 1}";
            labelText += $"\n({p.x:F1}, {p.y:F1})";

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = nodeColor;
            labelStyle.fontSize = 11;
            labelStyle.alignment = TextAnchor.UpperLeft;

            Handles.Label(p + new Vector2(16f, 16f), labelText, labelStyle);
        }
    }
}
#endif
