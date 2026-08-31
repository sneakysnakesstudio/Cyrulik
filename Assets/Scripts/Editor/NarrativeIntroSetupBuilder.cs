#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Narzędzie konfiguracyjne dla narracyjnego początku gry:
/// 1. Szafka nocna przy łóżku:
///    - 1. szuflada: otwiera się z animacją Hold to Open (kółeczko -> kwadrat), w środku klucz do szafy.
///    - 2. szuflada: zablokowana ("Zacięta, nie chce się otworzyć").
/// 2. Duża szafa: wymaga klucza z szuflady (Wardrobe Key).
/// 3. Dywan z plamą krwi: zbadanie [PPM] -> myśl fryzjera o niedomytej krwi.
/// 4. Śmieci w przedpokoju: zbadanie [PPM] [?] -> myśl o bałaganie przed przyjściem klienta.
/// </summary>
public static class NarrativeIntroSetupBuilder
{
    [MenuItem("Tools/Cyrulik/Setup Narrative Intro & Drawers", false, 15)]
    public static void SetupNarrativeIntro()
    {
        // 1. Znajdź lub skonfiguruj szafę (Wardrobe)
        ConfigureWardrobeKeyRequirement();

        // 2. Znajdź lub skonfiguruj stolik nocny i szuflady (Nightstand)
        ConfigureNightstandDrawers();

        // 3. Skonfiguruj plamę krwi na dywanie
        ConfigureBloodStain();

        // 4. Skonfiguruj śmieci w przedpokoju
        ConfigureHallwayTrash();

        Debug.Log("<color=#70FF70>[NarrativeIntroSetupBuilder] Pomyślnie skonfigurowano narracyjny początek gry!</color>");
    }

    private static void ConfigureWardrobeKeyRequirement()
    {
        WardrobeInteractable[] wardrobes = Object.FindObjectsByType<WardrobeInteractable>(FindObjectsInactive.Include);
        foreach (var w in wardrobes)
        {
            if (w == null) continue;

            SerializedObject so = new SerializedObject(w);
            so.FindProperty("requireKey").boolValue = true;
            so.FindProperty("requiredKeyItemId").stringValue = "wardrobe_key";
            so.FindProperty("keyMissingMessage").stringValue = "It's locked. The key must be in the nightstand drawer...";
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(w.gameObject);
        }

        DoorInteractable[] doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include);
        foreach (var d in doors)
        {
            if (d == null) continue;
            string n = d.gameObject.name.ToLowerInvariant();
            if (n.Contains("wardrobe") || n.Contains("szafa") || n.Contains("closet"))
            {
                SerializedObject so = new SerializedObject(d);
                so.FindProperty("requireKey").boolValue = true;
                so.FindProperty("requiredKeyItemId").stringValue = "wardrobe_key";
                so.FindProperty("keyMissingMessage").stringValue = "Locked. The key is in the nightstand drawer...";
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(d.gameObject);
            }
        }
    }

    private static void ConfigureNightstandDrawers()
    {
        // Szukamy stolika / szafki nocnej w scenie
        GameObject nightstand = GameObject.Find("nightstand-psx-lowpoly-pixelated") ?? GameObject.Find("Nightstand") ?? GameObject.Find("Stolik");
        if (nightstand == null) return;

        // Szukamy szuflad
        DoorInteractable[] drawers = nightstand.GetComponentsInChildren<DoorInteractable>(true);
        if (drawers.Length >= 1 && drawers[0] != null)
        {
            // 1. Szuflada - Otwierana z Hold to Open
            SerializedObject so1 = new SerializedObject(drawers[0]);
            so1.FindProperty("requireHold").boolValue = true;
            so1.FindProperty("holdDuration").floatValue = 0.45f;
            so1.FindProperty("lockedAtStart").boolValue = false;
            so1.ApplyModifiedProperties();
            EditorUtility.SetDirty(drawers[0].gameObject);
        }

        if (drawers.Length >= 2 && drawers[1] != null)
        {
            // 2. Szuflada - Zablokowana
            SerializedObject so2 = new SerializedObject(drawers[1]);
            so2.FindProperty("lockedAtStart").boolValue = true;
            so2.FindProperty("blockedMessage").stringValue = "Jammed. It won't budge.";
            so2.ApplyModifiedProperties();
            EditorUtility.SetDirty(drawers[1].gameObject);
        }
    }

    private static void ConfigureBloodStain()
    {
        GameObject stainGo = GameObject.Find("BloodStain_Carpet");
        if (stainGo == null)
        {
            GameObject carpet = GameObject.Find("carpet") ?? GameObject.Find("Carpet") ?? GameObject.Find("Dywan");
            Vector3 pos = carpet != null ? (carpet.transform.position + Vector3.up * 0.05f) : new Vector3(0f, 0.05f, 0f);

            stainGo = new GameObject("BloodStain_Carpet", typeof(BoxCollider), typeof(InspectThoughtInteractable));
            stainGo.transform.position = pos;
            var box = stainGo.GetComponent<BoxCollider>();
            box.size = new Vector3(0.8f, 0.1f, 0.8f);
            box.isTrigger = false;

            Undo.RegisterCreatedObjectUndo(stainGo, "Create BloodStain_Carpet");
        }

        var inspect = stainGo.GetComponent<InspectThoughtInteractable>();
        if (inspect != null)
        {
            SerializedObject so = new SerializedObject(inspect);
            so.FindProperty("interactionName").stringValue = "Examine bloodstain";
            so.FindProperty("thoughtText").stringValue = "An old bloodstain on the rug... I still haven't scrubbed it clean. If the client notices this...";
            so.FindProperty("triggerOnInteract").boolValue = true;
            so.FindProperty("triggerOnLookAt").boolValue = false;
            so.ApplyModifiedProperties();
        }
    }

    private static void ConfigureHallwayTrash()
    {
        GameObject trashGo = GameObject.Find("Hallway_Trash_Debris");
        if (trashGo == null)
        {
            GameObject hallway = GameObject.Find("trash-and-debris") ?? GameObject.Find("junk_props") ?? GameObject.Find("Debris");
            Vector3 pos = hallway != null ? hallway.transform.position : new Vector3(2.5f, 0.1f, -1f);

            trashGo = new GameObject("Hallway_Trash_Debris", typeof(BoxCollider), typeof(InspectThoughtInteractable));
            trashGo.transform.position = pos;
            var box = trashGo.GetComponent<BoxCollider>();
            box.size = new Vector3(1.2f, 0.6f, 1.2f);
            box.isTrigger = false;

            Undo.RegisterCreatedObjectUndo(trashGo, "Create Hallway_Trash_Debris");
        }

        var inspect = trashGo.GetComponent<InspectThoughtInteractable>();
        if (inspect != null)
        {
            SerializedObject so = new SerializedObject(inspect);
            so.FindProperty("interactionName").stringValue = "Inspect clutter";
            so.FindProperty("thoughtText").stringValue = "I need to clear this mess in the hallway. The client shouldn't see this chaos when he enters.";
            so.FindProperty("triggerOnInteract").boolValue = true;
            so.FindProperty("triggerOnLookAt").boolValue = false;
            so.ApplyModifiedProperties();
        }
    }
}
#endif
