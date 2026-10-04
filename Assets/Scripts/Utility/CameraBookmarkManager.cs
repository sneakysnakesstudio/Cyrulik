using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Menedżer zakładek i ujęć kamery do projektowania ujęć filmowych, kadrów menu i debugowania.
/// Umożliwia błyskawiczne zapisywanie aktualnej pozycji kamery (SceneView lub Camera.main) jednym kliknięciem.
/// </summary>
[ExecuteInEditMode]
public class CameraBookmarkManager : MonoBehaviour
{
    [System.Serializable]
    public class CameraShot
    {
        public string shotName = "New_Shot";
        public Vector3 position;
        public Vector3 rotationEuler;
        [Range(10f, 120f)] public float fieldOfView = 60f;
        public Camera targetCamera;
    }

    [Header("Aktywny Slot / Ujęcie")]
    [Tooltip("Indeks ujęcia wybranego do nadpisania lub podglądu.")]
    public int selectedSlotIndex = 0;

    [Header("Lista Zapisanych Kadrów")]
    public List<CameraShot> cameraShots = new List<CameraShot>();

    public void AddNewShot(Vector3 pos, Vector3 rot, float fov, string name = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = $"Shot_{cameraShots.Count + 1:D2}";
        }

        cameraShots.Add(new CameraShot
        {
            shotName = name,
            position = pos,
            rotationEuler = rot,
            fieldOfView = fov
        });
        selectedSlotIndex = cameraShots.Count - 1;
    }

    public void OverwriteSlot(int index, Vector3 pos, Vector3 rot, float fov)
    {
        if (index < 0 || index >= cameraShots.Count) return;

        cameraShots[index].position = pos;
        cameraShots[index].rotationEuler = rot;
        cameraShots[index].fieldOfView = fov;
    }

    public void ApplyShot(int index, Camera cam = null)
    {
        if (index < 0 || index >= cameraShots.Count) return;
        var shot = cameraShots[index];

        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
#if UNITY_EDITOR
            Undo.RecordObject(cam.transform, "Apply Camera Shot");
#endif
            cam.transform.position = shot.position;
            cam.transform.eulerAngles = shot.rotationEuler;
            cam.fieldOfView = shot.fieldOfView;
        }

#if UNITY_EDITOR
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.pivot = shot.position + (Quaternion.Euler(shot.rotationEuler) * Vector3.forward * 2f);
            SceneView.lastActiveSceneView.rotation = Quaternion.Euler(shot.rotationEuler);
            SceneView.lastActiveSceneView.size = 2f;
            SceneView.lastActiveSceneView.Repaint();
        }
#endif
    }
}
