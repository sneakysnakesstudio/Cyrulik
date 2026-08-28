#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RazorDeskSetupBuilder
{
    [MenuItem("Tools/Cyrulik/Create Razor Desk Spot", false, 4)]
    [MenuItem("GameObject/3D Object/Cyrulik - Razor Desk Spot", false, 13)]
    public static void CreateRazorDeskSpot()
    {
        // 1. Znajdź stół / biurko w scenie
        GameObject tableGo = GameObject.Find("table") ?? GameObject.Find("Table") ?? GameObject.Find("Desk");

        Vector3 spawnPos = new Vector3(-0.25f, 0.95f, -0.6f);
        if (tableGo != null)
        {
            spawnPos = tableGo.transform.position + Vector3.up * 0.85f;
        }

        // 2. Znajdź lub stwórz obiekt Razor_Desk_Spot
        GameObject spotGo = GameObject.Find("Razor_Desk_Spot");
        if (spotGo == null)
        {
            spotGo = new GameObject("Razor_Desk_Spot");
            spotGo.transform.position = spawnPos;
            if (tableGo != null)
            {
                spotGo.transform.SetParent(tableGo.transform, true);
            }
            Undo.RegisterCreatedObjectUndo(spotGo, "Create Razor Desk Spot");
        }

        // Ustaw warstwę na Interactable jeśli istnieje
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer != -1)
        {
            spotGo.layer = interactableLayer;
        }

        // 3. Dodaj BoxCollider do wykrywania interakcji celownikiem
        BoxCollider col = spotGo.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = spotGo.AddComponent<BoxCollider>();
            col.size = new Vector3(0.4f, 0.3f, 0.4f);
            col.center = new Vector3(0f, 0.1f, 0f);
        }

        // 4. Dodaj komponent RazorDeskSpot
        RazorDeskSpot deskSpot = spotGo.GetComponent<RazorDeskSpot>();
        if (deskSpot == null)
        {
            deskSpot = spotGo.AddComponent<RazorDeskSpot>();
        }

        // 5. Utwórz punkt SnapPoint dla brzytwy
        Transform snapPoint = spotGo.transform.Find("Razor_SnapPoint");
        if (snapPoint == null)
        {
            GameObject snapGo = new GameObject("Razor_SnapPoint");
            snapGo.transform.SetParent(spotGo.transform, false);
            snapGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            snapGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            snapPoint = snapGo.transform;
        }

        SerializedObject so = new SerializedObject(deskSpot);
        so.FindProperty("razorSnapPoint").objectReferenceValue = snapPoint;
        so.ApplyModifiedProperties();

        // 6. Sprawdź, czy w scenie jest brzytwa, jeśli nie, stwórz elegancką instancję brzytwy
        PickupItem existingRazor = Object.FindAnyObjectByType<PickupItem>();
        bool hasRazor = false;
        if (existingRazor != null && (existingRazor.ItemId == "razor" || existingRazor.name.ToLowerInvariant().Contains("razor")))
        {
            hasRazor = true;
        }

        if (!hasRazor)
        {
            // Załaduj model brzytwy z projektu
            GameObject razorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Razorminigame_art/razor_hand_model/source/Razor/Razor.blend")
                                  ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RazorBox.prefab");

            GameObject razorInstance;
            if (razorPrefab != null)
            {
                razorInstance = (GameObject)PrefabUtility.InstantiatePrefab(razorPrefab);
                razorInstance.name = "Razor";
                razorInstance.transform.SetParent(snapPoint, false);
                razorInstance.transform.localPosition = Vector3.zero;
                razorInstance.transform.localRotation = Quaternion.identity;
                razorInstance.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            }
            else
            {
                razorInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                razorInstance.name = "Razor";
                razorInstance.transform.SetParent(snapPoint, false);
                razorInstance.transform.localPosition = Vector3.zero;
                razorInstance.transform.localScale = new Vector3(0.18f, 0.02f, 0.04f);
            }

            if (interactableLayer != -1)
            {
                razorInstance.layer = interactableLayer;
            }

            // Collider & Rigidbody
            if (razorInstance.GetComponent<Collider>() == null)
            {
                var box = razorInstance.AddComponent<BoxCollider>();
                box.size = new Vector3(0.2f, 0.05f, 0.08f);
            }

            Rigidbody rb = razorInstance.GetComponent<Rigidbody>();
            if (rb == null) rb = razorInstance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // PickupItem
            PickupItem pickup = razorInstance.GetComponent<PickupItem>();
            if (pickup == null) pickup = razorInstance.AddComponent<PickupItem>();
            pickup.ItemId = "razor";
            pickup.InteractionName = "Pick up razor";
            pickup.InHandPosition = new Vector3(0.15f, -0.1f, 0.35f);
            pickup.InHandRotation = new Vector3(0f, -90f, 0f);
            pickup.InHandScale = new Vector3(0.08f, 0.08f, 0.08f);

            so.Update();
            so.FindProperty("initialRazorObject").objectReferenceValue = razorInstance;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(razorInstance, "Create Initial Razor");
        }

        Selection.activeGameObject = spotGo;
        EditorUtility.SetDirty(spotGo);
        Debug.Log("[RazorDeskSetupBuilder] Pomyślnie utworzono miejsce na brzytwę na biurku (Razor_Desk_Spot)!");
    }
}
#endif
