#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Narzędzie edytora Unity do szybkiego budowania i zamiany zielonego prostopadłościanu
/// na profesjonalny, klimatyczny wiszący pas skórzany (Razor Strop) w stylu PSX/Retro.
/// </summary>
public static class RazorStropBuilder
{
    [MenuItem("Tools/Cyrulik/1. Create or Find ParticleManager in Scene", false, 1)]
    public static void CreateParticleManager()
    {
        ParticleManager existing = Object.FindAnyObjectByType<ParticleManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("[Cyrulik] ParticleManager już istnieje w scenie.");
            return;
        }

        GameObject pmGo = new GameObject("ParticleManager", typeof(ParticleManager));
        Undo.RegisterCreatedObjectUndo(pmGo, "Create ParticleManager");
        Selection.activeGameObject = pmGo;
        EditorGUIUtility.PingObject(pmGo);
        Debug.Log("[Cyrulik] Utworzono ParticleManager w scenie!");
    }

    [MenuItem("Tools/Cyrulik/2. Build PSX Razor Hanging Strop (Pas do brzytwy)", false, 2)]
    public static void BuildRazorStrop()
    {
        // 1. Sprawdzamy czy w scenie jest już RazorMinigame_Object lub zaznaczony obiekt
        GameObject targetParent = Selection.activeGameObject;
        Vector3 spawnPos = new Vector3(0f, 1.2f, 4.05f);
        Quaternion spawnRot = Quaternion.identity;

        if (targetParent != null)
        {
            spawnPos = targetParent.transform.position;
            spawnRot = targetParent.transform.rotation;
        }
        else
        {
            GameObject existingObj = GameObject.Find("RazorMinigame_Object");
            if (existingObj != null)
            {
                spawnPos = existingObj.transform.position;
                spawnRot = existingObj.transform.rotation;
            }
        }

        // 2. Główny Root Wiszącego Pasa
        GameObject stropRoot = new GameObject("RazorStrop_Hanging");
        stropRoot.transform.position = spawnPos;
        stropRoot.transform.rotation = spawnRot;
        Undo.RegisterCreatedObjectUndo(stropRoot, "Create Razor Strop Hanging");

        // 3. Montaż ścienny / Haczyk (Hook)
        GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hook.name = "Wall_Hook";
        hook.transform.SetParent(stropRoot.transform, false);
        hook.transform.localPosition = new Vector3(0f, 0.45f, 0.03f);
        hook.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hook.transform.localScale = new Vector3(0.025f, 0.03f, 0.025f);

        // Usuwamy collider z haczyka
        Object.DestroyImmediate(hook.GetComponent<Collider>());

        // 4. Punkt Obrotu Pasa (Pivot na samej górze)
        GameObject pivotGo = new GameObject("Strap_Pivot");
        pivotGo.transform.SetParent(stropRoot.transform, false);
        pivotGo.transform.localPosition = new Vector3(0f, 0.45f, 0.04f);

        // 5. Skórzany Pasek (Główna bryła)
        GameObject leatherStrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leatherStrap.name = "Leather_Strap_Mesh";
        leatherStrap.transform.SetParent(pivotGo.transform, false);
        // Środek cube'a przesunięty w dół od pivota
        leatherStrap.transform.localPosition = new Vector3(0f, -0.42f, 0f);
        leatherStrap.transform.localScale = new Vector3(0.09f, 0.85f, 0.012f);
        Object.DestroyImmediate(leatherStrap.GetComponent<Collider>());

        // 6. Dolny pierścień / rączka (D-Ring / Handle)
        GameObject bottomRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottomRing.name = "Bottom_Handle_Ring";
        bottomRing.transform.SetParent(pivotGo.transform, false);
        bottomRing.transform.localPosition = new Vector3(0f, -0.86f, 0f);
        bottomRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bottomRing.transform.localScale = new Vector3(0.045f, 0.008f, 0.045f);
        Object.DestroyImmediate(bottomRing.GetComponent<Collider>());

        // 7. Przypisanie Tekstur / Materiałów
        Material leatherMat = FindOrCreateLeatherMaterial();
        if (leatherMat != null)
        {
            leatherStrap.GetComponent<MeshRenderer>().sharedMaterial = leatherMat;
        }

        Material brassMat = FindOrCreateBrassMaterial();
        if (brassMat != null)
        {
            hook.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
            bottomRing.GetComponent<MeshRenderer>().sharedMaterial = brassMat;
        }

        // 8. BoxCollider na całym obiekcie (dla łatwej interakcji)
        BoxCollider boxCol = stropRoot.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0f, 0f, 0.04f);
        boxCol.size = new Vector3(0.18f, 0.95f, 0.12f);

        // 9. Komponenty fizyki i cząsteczek
        HangingStrapSway sway = stropRoot.AddComponent<HangingStrapSway>();
        var pivotField = typeof(HangingStrapSway).GetField("pivotTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (pivotField != null) pivotField.SetValue(sway, pivotGo.transform);

        InteractiveParticleHint hint = stropRoot.AddComponent<InteractiveParticleHint>();
        var offsetField = typeof(InteractiveParticleHint).GetField("localOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (offsetField != null) offsetField.SetValue(hint, new Vector3(0f, 0f, 0.05f));

        Selection.activeGameObject = stropRoot;
        EditorGUIUtility.PingObject(stropRoot);

        Debug.Log("[Cyrulik] Gotowy wiszący pas (Razor Strop) został wygenerowany w scenie!");
    }

    private static Material FindOrCreateLeatherMaterial()
    {
        // Szukamy istniejących tekstur w projekcie
        string[] guids = AssetDatabase.FindAssets("strop_szkic_ciemna_skora t:Texture2D");
        if (guids.Length == 0) guids = AssetDatabase.FindAssets("strop_pasek_plaski t:Texture2D");
        if (guids.Length == 0) guids = AssetDatabase.FindAssets("strop_pelny_oryginal t:Texture2D");

        Texture2D leatherTex = null;
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            leatherTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Szukamy lub tworzymy materiał
        string matPath = "Assets/Art/Razorminigame_art/Materials/M_HangingLeatherStrop.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            Shader psxOrUrp = Shader.Find("PSX/RetroSurface") 
                           ?? Shader.Find("Universal Render Pipeline/Lit") 
                           ?? Shader.Find("Standard");

            mat = new Material(psxOrUrp);
            if (leatherTex != null)
            {
                mat.mainTexture = leatherTex;
            }
            else
            {
                mat.color = new Color(0.35f, 0.20f, 0.12f, 1f); // Skórzany brąz
            }

            // Tworzymy folder jeśli nie istnieje
            if (!AssetDatabase.IsValidFolder("Assets/Art/Razorminigame_art/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Art/Razorminigame_art", "Materials");
            }

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }

        return mat;
    }

    private static Material FindOrCreateBrassMaterial()
    {
        string matPath = "Assets/Art/Razorminigame_art/Materials/M_StropBrassHardware.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.color = new Color(0.65f, 0.52f, 0.28f, 1f); // Antyczny mosiądz

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.75f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }

        return mat;
    }
}
#endif
