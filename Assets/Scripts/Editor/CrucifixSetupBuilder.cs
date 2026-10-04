#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Kreator i konfigurator krzyża (Crucifix z napisem LORD HAVE MERCY) oraz anielskich efektów kinowych.
/// Dodaje menu: Tools -> Cyrulik -> Setup Crucifix & Cinema Effects.
/// </summary>
public static class CrucifixSetupBuilder
{
    [MenuItem("Tools/Cyrulik/Setup Crucifix & Cinema Effects", false, 16)]
    public static void SetupCrucifixAndCinema()
    {
        // 1. Upewnij się, że CinematicEffectsManager istnieje w scenie
        EnsureCinematicEffectsManager();

        // 2. Stwórz lub zaktualizuj obiekt Krzyża w scenie
        GameObject crucifixGo = CreateOrGetCrucifix();

        // 3. Oznacz scenę jako zmodyfikowaną
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        Selection.activeGameObject = crucifixGo;
        EditorGUIUtility.PingObject(crucifixGo);

        Debug.Log("<color=#FFD700>[CrucifixSetupBuilder] Pomyślnie skonfigurowano Krzyż z napisem LORD HAVE MERCY i anielskimi efektami zbliżenia [PPM]!</color>");
    }

    private static void EnsureCinematicEffectsManager()
    {
        var manager = Object.FindAnyObjectByType<CinematicEffectsManager>();
        if (manager == null)
        {
            GameObject mgrGo = new GameObject("=== CinematicEffectsManager ===", typeof(CinematicEffectsManager));
            Undo.RegisterCreatedObjectUndo(mgrGo, "Create CinematicEffectsManager");
        }
    }

    private static GameObject CreateOrGetCrucifix()
    {
        GameObject existing = GameObject.Find("Crucifix_Interactable");
        if (existing != null)
        {
            EnsureCrucifixComponents(existing);
            EnsurePlaque(existing);
            return existing;
        }

        // Tworzymy nadrzędny obiekt krzyża
        GameObject root = new GameObject("Crucifix_Interactable");
        Undo.RegisterCreatedObjectUndo(root, "Create Crucifix");

        // Pozycjonujemy go w klimatycznym miejscu na ścianie przy sypialni / nad łóżkiem
        GameObject bed = GameObject.Find("Old_bed") ?? GameObject.Find("Bed") ?? GameObject.Find("Łóżko");
        if (bed != null)
        {
            root.transform.position = bed.transform.position + new Vector3(0f, 1.65f, 1.25f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            root.transform.position = new Vector3(0.5f, 1.6f, 22.2f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        // Znajdź materiał drewna i metalu
        Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/3rdParty/Models_credits/wardrobe/Materials/wood_wardrobe.mat")
                       ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/3rdParty/Materials/WoodFloor_Material.mat");

        Material metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/3rdParty/Models_credits/LightSwitch_model/Materials/light_switch_light_switch_Metallic.mat");

        // 1. Belka pionowa
        GameObject verticalBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        verticalBeam.name = "VerticalBeam";
        verticalBeam.transform.SetParent(root.transform, false);
        verticalBeam.transform.localPosition = Vector3.zero;
        verticalBeam.transform.localScale = new Vector3(0.08f, 0.55f, 0.035f);
        if (woodMat != null && verticalBeam.TryGetComponent<MeshRenderer>(out var mrV)) mrV.sharedMaterial = woodMat;
        Object.DestroyImmediate(verticalBeam.GetComponent<Collider>());

        // 2. Belka pozioma
        GameObject horizontalBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        horizontalBeam.name = "HorizontalBeam";
        horizontalBeam.transform.SetParent(root.transform, false);
        horizontalBeam.transform.localPosition = new Vector3(0f, 0.12f, 0.005f);
        horizontalBeam.transform.localScale = new Vector3(0.36f, 0.075f, 0.032f);
        if (woodMat != null && horizontalBeam.TryGetComponent<MeshRenderer>(out var mrH)) mrH.sharedMaterial = woodMat;
        Object.DestroyImmediate(horizontalBeam.GetComponent<Collider>());

        // 3. Tabliczka INRI na górze
        GameObject inri = GameObject.CreatePrimitive(PrimitiveType.Cube);
        inri.name = "INRI_Plaque";
        inri.transform.SetParent(root.transform, false);
        inri.transform.localPosition = new Vector3(0f, 0.23f, 0.022f);
        inri.transform.localScale = new Vector3(0.09f, 0.045f, 0.015f);
        if (metalMat != null && inri.TryGetComponent<MeshRenderer>(out var mrI)) mrI.sharedMaterial = metalMat;
        Object.DestroyImmediate(inri.GetComponent<Collider>());

        // 4. Metalowa figura / Chrystus
        GameObject corpusBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        corpusBody.name = "Corpus_Body";
        corpusBody.transform.SetParent(root.transform, false);
        corpusBody.transform.localPosition = new Vector3(0f, 0.06f, 0.025f);
        corpusBody.transform.localScale = new Vector3(0.055f, 0.22f, 0.03f);
        if (metalMat != null && corpusBody.TryGetComponent<MeshRenderer>(out var mrB)) mrB.sharedMaterial = metalMat;
        Object.DestroyImmediate(corpusBody.GetComponent<Collider>());

        // Ramiona
        GameObject corpusArms = GameObject.CreatePrimitive(PrimitiveType.Cube);
        corpusArms.name = "Corpus_Arms";
        corpusArms.transform.SetParent(root.transform, false);
        corpusArms.transform.localPosition = new Vector3(0f, 0.12f, 0.026f);
        corpusArms.transform.localScale = new Vector3(0.26f, 0.038f, 0.025f);
        if (metalMat != null && corpusArms.TryGetComponent<MeshRenderer>(out var mrA)) mrA.sharedMaterial = metalMat;
        Object.DestroyImmediate(corpusArms.GetComponent<Collider>());

        // Głowa
        GameObject corpusHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        corpusHead.name = "Corpus_Head";
        corpusHead.transform.SetParent(root.transform, false);
        corpusHead.transform.localPosition = new Vector3(0f, 0.17f, 0.03f);
        corpusHead.transform.localRotation = Quaternion.Euler(15f, 15f, 0f);
        corpusHead.transform.localScale = new Vector3(0.048f, 0.052f, 0.048f);
        if (metalMat != null && corpusHead.TryGetComponent<MeshRenderer>(out var mrHd)) mrHd.sharedMaterial = metalMat;
        Object.DestroyImmediate(corpusHead.GetComponent<Collider>());

        // 5. Dolna tabliczka z inskrypcją LORD HAVE MERCY
        EnsurePlaque(root);

        // Dodaj BoxCollider na root
        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0f, -0.05f, 0.01f);
        boxCol.size = new Vector3(0.48f, 0.85f, 0.2f);

        // Ustaw warstwę (Layer) na interaktywną
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            root.layer = interactableLayer;
            foreach (Transform child in root.transform) child.gameObject.layer = interactableLayer;
        }

        // Dodaj komponent CrucifixInteractable
        CrucifixInteractable interactable = root.AddComponent<CrucifixInteractable>();

        // Zapisz prefab w Assets/Prefabs
        EnsurePrefabCreated(root);

        return root;
    }

    private static void EnsurePlaque(GameObject root)
    {
        Transform existingPlaque = root.transform.Find("LordHaveMercy_Plaque");
        if (existingPlaque != null) return;

        Material metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/3rdParty/Models_credits/LightSwitch_model/Materials/light_switch_light_switch_Metallic.mat");

        // Tabliczka mosiężna pod krzyżem
        GameObject plaque = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plaque.name = "LordHaveMercy_Plaque";
        plaque.transform.SetParent(root.transform, false);
        plaque.transform.localPosition = new Vector3(0f, -0.34f, 0.02f);
        plaque.transform.localScale = new Vector3(0.38f, 0.09f, 0.018f);
        if (metalMat != null && plaque.TryGetComponent<MeshRenderer>(out var mrP)) mrP.sharedMaterial = metalMat;
        Object.DestroyImmediate(plaque.GetComponent<Collider>());

        // Napis TextMeshPro na tabliczce
        GameObject textGo = new GameObject("Plaque_Text");
        textGo.transform.SetParent(plaque.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, 0.55f);
        textGo.transform.localRotation = Quaternion.identity;
        textGo.transform.localScale = new Vector3(0.018f, 0.08f, 0.018f);

        TextMeshPro tmp = textGo.AddComponent<TextMeshPro>();
        tmp.text = "LORD HAVE MERCY";
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.98f, 0.88f, 0.55f, 1f); // Złoty grawer
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void EnsureCrucifixComponents(GameObject go)
    {
        if (!go.TryGetComponent<BoxCollider>(out var boxCol))
        {
            boxCol = go.AddComponent<BoxCollider>();
            boxCol.center = new Vector3(0f, -0.05f, 0.01f);
            boxCol.size = new Vector3(0.48f, 0.85f, 0.2f);
        }

        if (!go.TryGetComponent<CrucifixInteractable>(out var interactable))
        {
            go.AddComponent<CrucifixInteractable>();
        }
    }

    private static void EnsurePrefabCreated(GameObject root)
    {
        string prefabPath = "Assets/Prefabs/Crucifix_Interactable.prefab";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
    }
}
#endif
