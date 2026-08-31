#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraBookmarkManager))]
public class CameraBookmarkEditor : Editor
{
    private CameraBookmarkManager _manager;

    private void OnEnable()
    {
        _manager = (CameraBookmarkManager)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Nagłówek narzędzia
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("📷 <b>SYSTEM ZAPISYWANIA POZYCJI KAMERY</b>", new GUIStyle(EditorStyles.label) { richText = true, fontSize = 13, alignment = TextAnchor.MiddleCenter });
        GUILayout.Label("Zapisuj kadry ze SceneView lub kamery gracza jednym kliknięciem.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // Główne przyciski akcji
        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.45f);
        if (GUILayout.Button("➕ DODAJ NOWY SLOT (Zapisz bieżący widok)", GUILayout.Height(34)))
        {
            AddNewShotFromCurrentView();
        }

        GUI.backgroundColor = new Color(0.35f, 0.75f, 0.95f);
        if (GUILayout.Button($"📸 NADPISZ WYBRANY SLOT [{_manager.selectedSlotIndex}] (Zapisz bieżący widok)", GUILayout.Height(28)))
        {
            OverwriteSelectedSlot();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(12);

        // Rysowanie domyślnych pól z podziałem na listę kadrów
        EditorGUILayout.LabelField("Zapisane Ujęcia / Sloty", EditorStyles.boldLabel);

        if (_manager.cameraShots.Count == 0)
        {
            EditorGUILayout.HelpBox("Brak zapisanych ujęć. Ustaw kamerę w oknie sceny i kliknij 'DODAJ NOWY SLOT'.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < _manager.cameraShots.Count; i++)
            {
                var shot = _manager.cameraShots[i];
                bool isSelected = (_manager.selectedSlotIndex == i);

                EditorGUILayout.BeginVertical(isSelected ? EditorStyles.helpBox : EditorStyles.textArea);
                EditorGUILayout.BeginHorizontal();

                // Radio-button lub wskaźnik wyboru
                if (GUILayout.Toggle(isSelected, $"Slot #{i:D2}: {shot.shotName}", "Button", GUILayout.Height(22)))
                {
                    _manager.selectedSlotIndex = i;
                }

                GUI.backgroundColor = new Color(0.95f, 0.85f, 0.35f);
                if (GUILayout.Button("👁️ Skocz", GUILayout.Width(60), GUILayout.Height(22)))
                {
                    _manager.selectedSlotIndex = i;
                    _manager.ApplyShot(i);
                }

                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button("🎥 Stwórz Kamerę", GUILayout.Width(105), GUILayout.Height(22)))
                {
                    SpawnCameraFromShot(shot, i);
                }

                GUI.backgroundColor = new Color(0.95f, 0.4f, 0.4f);
                if (GUILayout.Button("X", GUILayout.Width(24), GUILayout.Height(22)))
                {
                    Undo.RecordObject(_manager, "Delete Camera Shot");
                    _manager.cameraShots.RemoveAt(i);
                    if (_manager.selectedSlotIndex >= _manager.cameraShots.Count)
                    {
                        _manager.selectedSlotIndex = Mathf.Max(0, _manager.cameraShots.Count - 1);
                    }
                    EditorUtility.SetDirty(_manager);
                    break;
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (isSelected)
                {
                    EditorGUILayout.Space(2);
                    shot.shotName = EditorGUILayout.TextField("Nazwa ujęcia:", shot.shotName);
                    shot.position = EditorGUILayout.Vector3Field("Pozycja (XYZ):", shot.position);
                    shot.rotationEuler = EditorGUILayout.Vector3Field("Rotacja (Euler):", shot.rotationEuler);
                    shot.fieldOfView = EditorGUILayout.Slider("FOV:", shot.fieldOfView, 10f, 120f);
                    shot.targetCamera = (Camera)EditorGUILayout.ObjectField("Kamera docelowa (opcjonalnie):", shot.targetCamera, typeof(Camera), true);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void GetCurrentCameraTransform(out Vector3 pos, out Vector3 rot, out float fov)
    {
        // 1. Domyślnie bierzemy z aktywnego okna SceneView
        if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
        {
            var cam = SceneView.lastActiveSceneView.camera;
            pos = cam.transform.position;
            rot = cam.transform.eulerAngles;
            fov = cam.fieldOfView;
            return;
        }

        // 2. Jeśli brak SceneView, bierzemy z Camera.main
        if (Camera.main != null)
        {
            pos = Camera.main.transform.position;
            rot = Camera.main.transform.eulerAngles;
            fov = Camera.main.fieldOfView;
            return;
        }

        pos = Vector3.zero;
        rot = Vector3.zero;
        fov = 60f;
    }

    private void AddNewShotFromCurrentView()
    {
        Undo.RecordObject(_manager, "Add New Camera Shot");
        GetCurrentCameraTransform(out Vector3 pos, out Vector3 rot, out float fov);
        _manager.AddNewShot(pos, rot, fov);
        EditorUtility.SetDirty(_manager);
        Debug.Log($"<color=#70FF70>[CameraBookmark] Zapisano nowe ujęcie #{_manager.cameraShots.Count}: Pos={pos}, Rot={rot}, FOV={fov}</color>");
    }

    private void OverwriteSelectedSlot()
    {
        if (_manager.cameraShots.Count == 0)
        {
            AddNewShotFromCurrentView();
            return;
        }

        Undo.RecordObject(_manager, "Overwrite Camera Shot");
        GetCurrentCameraTransform(out Vector3 pos, out Vector3 rot, out float fov);
        _manager.OverwriteSlot(_manager.selectedSlotIndex, pos, rot, fov);
        EditorUtility.SetDirty(_manager);
        Debug.Log($"<color=#65C7D9>[CameraBookmark] Nadpisano slot #{_manager.selectedSlotIndex} ({_manager.cameraShots[_manager.selectedSlotIndex].shotName}) aktualnym widokiem!</color>");
    }

    private void SpawnCameraFromShot(CameraBookmarkManager.CameraShot shot, int index)
    {
        GameObject camGo = new GameObject($"Camera_{shot.shotName}_{index:D2}", typeof(Camera), typeof(AudioListener));
        camGo.transform.position = shot.position;
        camGo.transform.eulerAngles = shot.rotationEuler;

        Camera cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = shot.fieldOfView;

        // Jeśli jest obiekt-rodzic do kamer, wrzuć tam
        GameObject camParent = GameObject.Find("=== Cameras ===") ?? GameObject.Find("Cameras");
        if (camParent != null)
        {
            camGo.transform.SetParent(camParent.transform, true);
        }

        Undo.RegisterCreatedObjectUndo(camGo, "Create Camera from Shot");
        Selection.activeGameObject = camGo;
        Debug.Log($"<color=#FFD700>[CameraBookmark] Utworzono nową kamerę w scenie: {camGo.name}</color>");
    }

    [MenuItem("Tools/Cyrulik/Create Camera Bookmark Manager", false, 30)]
    public static void CreateManagerInScene()
    {
        var existing = Object.FindAnyObjectByType<CameraBookmarkManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("[CameraBookmark] Manager już istnieje w scenie.");
            return;
        }

        GameObject go = new GameObject("=== CameraBookmarkManager ===", typeof(CameraBookmarkManager));
        go.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(go, "Create CameraBookmarkManager");
        Selection.activeGameObject = go;
        Debug.Log("<color=#70FF70>[CameraBookmark] Utworzono CameraBookmarkManager w scenie!</color>");
    }
}
#endif
