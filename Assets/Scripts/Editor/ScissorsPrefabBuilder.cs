#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kreator nożyczek fryzjerskich (Vintage Barber Scissors).
/// Generuje estetyczny model retro nożyczek z dwoma skrzyżowanymi ostrzami,
/// oczkami na palce, śrubą centralną oraz w pełni skonfigurowanym komponentem PickupItem i Showcase.
/// </summary>
public static class ScissorsPrefabBuilder
{
    private const string PREFAB_PATH = "Assets/Prefabs/Scissors.prefab";

    [MenuItem("Tools/Cyrulik/Create Scissors Prefab", false, 11)]
    public static GameObject CreateOrUpdateScissorsPrefab()
    {
        GameObject scissorsRoot = new GameObject("Scissors");

        // Materiał stalowy
        Material steelMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
        {
            name = "M_BarberSteel",
            color = new Color(0.78f, 0.80f, 0.82f, 1f)
        };
        if (steelMat.HasProperty("_Metallic")) steelMat.SetFloat("_Metallic", 0.85f);
        if (steelMat.HasProperty("_Smoothness")) steelMat.SetFloat("_Smoothness", 0.65f);

        Material screwMat = new Material(steelMat)
        {
            name = "M_BrassScrew",
            color = new Color(0.85f, 0.75f, 0.42f, 1f)
        };
        if (screwMat.HasProperty("_Metallic")) screwMat.SetFloat("_Metallic", 0.9f);

        // Zapisz materiały do projektu jeśli nie istnieją
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        string steelMatPath = "Assets/Materials/M_BarberSteel.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(steelMatPath) == null)
        {
            AssetDatabase.CreateAsset(steelMat, steelMatPath);
        }
        else
        {
            steelMat = AssetDatabase.LoadAssetAtPath<Material>(steelMatPath);
        }

        // 1. Śruba centralna (Pivot)
        GameObject pivotScrew = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pivotScrew.name = "PivotScrew";
        pivotScrew.transform.SetParent(scissorsRoot.transform, false);
        pivotScrew.transform.localScale = new Vector3(0.018f, 0.008f, 0.018f);
        pivotScrew.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        pivotScrew.GetComponent<MeshRenderer>().sharedMaterial = screwMat;
        Object.DestroyImmediate(pivotScrew.GetComponent<Collider>());

        // 2. Ramię i ostrze A (lewe, kąt +8 stopni)
        GameObject armA = new GameObject("Blade_Arm_A");
        armA.transform.SetParent(scissorsRoot.transform, false);
        armA.transform.localRotation = Quaternion.Euler(0f, 0f, 7f);
        CreateBladeAndHandle(armA.transform, steelMat, false);

        // 3. Ramię i ostrze B (prawe, kąt -8 stopni)
        GameObject armB = new GameObject("Blade_Arm_B");
        armB.transform.SetParent(scissorsRoot.transform, false);
        armB.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
        CreateBladeAndHandle(armB.transform, steelMat, true);

        // 4. Collider & Rigidbody
        BoxCollider boxCol = scissorsRoot.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(0.12f, 0.28f, 0.025f);
        boxCol.center = new Vector3(0f, 0.02f, 0f);

        Rigidbody rb = scissorsRoot.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 5. Konfiguracja PickupItem z Showcase
        PickupItem pickup = scissorsRoot.AddComponent<PickupItem>();
        pickup.ItemId = "scissors";
        pickup.InteractionName = "Pick up scissors";
        pickup.InHandPosition = new Vector3(0.12f, -0.08f, 0.32f);
        pickup.InHandRotation = new Vector3(15f, -90f, 45f);
        pickup.InHandScale = new Vector3(1f, 1f, 1f);

        // Włączenie 3D Showcase
        pickup.EnableShowcaseOnPickup = true;
        pickup.ShowcaseTitle = "SCISSORS";
        pickup.ShowcaseComment = "Heavy barber scissors. Cold Swedish steel, perfectly balanced. They've snipped thousands of locks... and they're still keen enough for something deeper.";
        pickup.ShowcaseModelScale = 1.4f;
        pickup.ShowcaseModelRotation = new Vector3(0f, 0f, 15f);

        // Ustawienie warstwy Interactable
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer != -1)
        {
            scissorsRoot.layer = interactableLayer;
            foreach (Transform child in scissorsRoot.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = interactableLayer;
            }
        }

        // Zapisz jako prefab
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(scissorsRoot, PREFAB_PATH);
        Object.DestroyImmediate(scissorsRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ScissorsPrefabBuilder] Utworzono prefab nożyczek: {PREFAB_PATH}");
        return prefab;
    }

    private static void CreateBladeAndHandle(Transform armParent, Material mat, bool flip)
    {
        // Ostrze (smukły prostopadłościan zwężający się ku czubkowi)
        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "Blade";
        blade.transform.SetParent(armParent, false);
        blade.transform.localPosition = new Vector3(flip ? -0.006f : 0.006f, 0.075f, flip ? 0.003f : -0.003f);
        blade.transform.localScale = new Vector3(0.016f, 0.13f, 0.004f);
        blade.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(blade.GetComponent<Collider>());

        // Czubek ostrza
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tip.name = "BladeTip";
        tip.transform.SetParent(armParent, false);
        tip.transform.localPosition = new Vector3(flip ? -0.004f : 0.004f, 0.138f, flip ? 0.003f : -0.003f);
        tip.transform.localRotation = Quaternion.Euler(0f, 0f, flip ? -18f : 18f);
        tip.transform.localScale = new Vector3(0.010f, 0.024f, 0.003f);
        tip.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(tip.GetComponent<Collider>());

        // Trzonek rękojeści
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Shaft";
        shaft.transform.SetParent(armParent, false);
        shaft.transform.localPosition = new Vector3(flip ? 0.014f : -0.014f, -0.045f, 0f);
        shaft.transform.localRotation = Quaternion.Euler(0f, 0f, flip ? 14f : -14f);
        shaft.transform.localScale = new Vector3(0.012f, 0.08f, 0.008f);
        shaft.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(shaft.GetComponent<Collider>());

        // Oczko / Uchwyt na palec (złożone z 4 zaokrąglonych segmentów tworzących owal)
        GameObject handleRing = new GameObject("HandleRing");
        handleRing.transform.SetParent(armParent, false);
        handleRing.transform.localPosition = new Vector3(flip ? 0.024f : -0.024f, -0.095f, 0f);

        CreateRingSegment(handleRing.transform, new Vector3(0f, 0.022f, 0f), new Vector3(0.028f, 0.006f, 0.008f), mat);
        CreateRingSegment(handleRing.transform, new Vector3(0f, -0.022f, 0f), new Vector3(0.028f, 0.006f, 0.008f), mat);
        CreateRingSegment(handleRing.transform, new Vector3(-0.016f, 0f, 0f), new Vector3(0.006f, 0.042f, 0.008f), mat);
        CreateRingSegment(handleRing.transform, new Vector3(0.016f, 0f, 0f), new Vector3(0.006f, 0.042f, 0.008f), mat);

        // Opcjonalny mały haczyk na mały palec (tzw. finger tang) na jednym ramieniu
        if (!flip)
        {
            GameObject tang = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tang.name = "FingerTang";
            tang.transform.SetParent(handleRing.transform, false);
            tang.transform.localPosition = new Vector3(-0.022f, -0.024f, 0f);
            tang.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            tang.transform.localScale = new Vector3(0.006f, 0.018f, 0.006f);
            tang.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(tang.GetComponent<Collider>());
        }
    }

    private static void CreateRingSegment(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seg.transform.SetParent(parent, false);
        seg.transform.localPosition = localPos;
        seg.transform.localScale = scale;
        seg.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(seg.GetComponent<Collider>());
    }

    [MenuItem("Tools/Cyrulik/Place Scissors in Salon", false, 12)]
    public static void PlaceScissorsInSalon()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null)
        {
            prefab = CreateOrUpdateScissorsPrefab();
        }

        // Szukamy biurka, stolika lub miejsca na brzytwę
        GameObject deskSpot = GameObject.Find("Razor_Desk_Spot") ?? GameObject.Find("Desk") ?? GameObject.Find("table");
        Vector3 spawnPos = new Vector3(0.15f, 0.95f, -0.55f);

        if (deskSpot != null)
        {
            spawnPos = deskSpot.transform.position + new Vector3(0.2f, 0.02f, -0.05f);
        }

        GameObject existingScissors = GameObject.Find("Scissors");
        if (existingScissors != null)
        {
            Selection.activeGameObject = existingScissors;
            EditorGUIUtility.PingObject(existingScissors);
            Debug.Log("[ScissorsPrefabBuilder] Nożyczki już istnieją w scenie! Zaznaczono istniejący obiekt.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Scissors";
        instance.transform.position = spawnPos;
        instance.transform.rotation = Quaternion.Euler(0f, 45f, 90f); // Leżą płasko na stole
        instance.transform.localScale = new Vector3(1f, 1f, 1f);

        Undo.RegisterCreatedObjectUndo(instance, "Place Scissors");
        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);

        Debug.Log($"[ScissorsPrefabBuilder] Pomyślnie postawiono nożyczki (SCISSORS) w salonie na pozycji: {spawnPos}!");
    }
}
#endif
