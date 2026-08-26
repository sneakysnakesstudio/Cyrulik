using UnityEngine;
using UnityEditor;

public static class OutsideBuilderHelper
{
    [MenuItem("Tools/Cyrulik/⚡ Build Quick Outside Courtyard (1-Click)", false, 50)]
    public static void BuildQuickOutside()
    {
        // 1. Root container
        GameObject existingRoot = GameObject.Find("[Outside_Environment]");
        if (existingRoot != null)
        {
            if (EditorUtility.DisplayDialog("Zastąpienie zewnątrz", 
                "Obiekt [Outside_Environment] już istnieje w scenie. Czy chcesz go przebudować?", "Tak, przebuduj", "Anuluj"))
            {
                Undo.DestroyObjectImmediate(existingRoot);
            }
            else
            {
                Selection.activeGameObject = existingRoot;
                return;
            }
        }

        GameObject root = new GameObject("[Outside_Environment]");
        Undo.RegisterCreatedObjectUndo(root, "Build Quick Outside");

        // Material helpers
        Material groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/3rdParty/TexturePack/BasicPack/Materials/Stone3.mat")
                          ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/3rdParty/TexturePack/BasicPack/Materials/Sidewalk1.mat");
        if (groundMat == null)
        {
            Texture2D groundTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3rdParty/TexturePack/BasicPack/32x32Sidewalk1.png")
                               ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3rdParty/TexturePack/BasicPack/32x32Stone3.png");
            if (groundTex != null)
            {
                groundMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                groundMat.mainTexture = groundTex;
                groundMat.mainTextureScale = new Vector2(8, 8);
            }
        }

        Material brickMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/3rdParty/TexturePack/BasicPack/Materials/Bricks.mat");
        if (brickMat == null)
        {
            Texture2D brickTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3rdParty/TexturePack/BasicPack/32x32Bricks.png");
            if (brickTex != null)
            {
                brickMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                brickMat.mainTexture = brickTex;
                brickMat.mainTextureScale = new Vector2(10, 6);
            }
        }

        // 2. Podłoże podwórka (Courtyard Ground)
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Courtyard_Ground";
        ground.transform.SetParent(root.transform);
        ground.transform.position = new Vector3(16f, -1.5f, 6f);
        ground.transform.localScale = new Vector3(3f, 1f, 3f); // 30x30m
        if (groundMat != null) ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // 3. Przeciwległa kamienica / Mur tła (Backdrop Tenement Wall)
        GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backWall.name = "Backdrop_Tenement_Wall";
        backWall.transform.SetParent(root.transform);
        backWall.transform.position = new Vector3(16f, 4.5f, 21f);
        backWall.transform.localScale = new Vector3(30f, 12f, 1f);
        if (brickMat != null) backWall.GetComponent<MeshRenderer>().sharedMaterial = brickMat;

        // 4. Boczny mur podwórka (Side Yard Wall)
        GameObject sideWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sideWall.name = "Side_Yard_Wall";
        sideWall.transform.SetParent(root.transform);
        sideWall.transform.position = new Vector3(31f, 4.5f, 6f);
        sideWall.transform.localScale = new Vector3(1f, 12f, 30f);
        if (brickMat != null) sideWall.GetComponent<MeshRenderer>().sharedMaterial = brickMat;

        // 5. Latarnia uliczna (Street Lamp Post & Point Light)
        GameObject lampPost = new GameObject("StreetLamp_Post");
        lampPost.transform.SetParent(root.transform);
        lampPost.transform.position = new Vector3(11f, -1.5f, 11f);

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(lampPost.transform);
        pole.transform.localPosition = new Vector3(0, 2f, 0);
        pole.transform.localScale = new Vector3(0.15f, 2f, 0.15f);

        GameObject lantern = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lantern.name = "Lantern_Head";
        lantern.transform.SetParent(lampPost.transform);
        lantern.transform.localPosition = new Vector3(0, 4.1f, 0);
        lantern.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);

        GameObject lightObj = new GameObject("StreetLight_Warm");
        lightObj.transform.SetParent(lantern.transform);
        lightObj.transform.localPosition = Vector3.zero;
        Light lightComp = lightObj.AddComponent<Light>();
        lightComp.type = LightType.Point;
        lightComp.color = new Color(1.0f, 0.82f, 0.55f);
        lightComp.range = 14f;
        lightComp.intensity = 1.6f;
        lightComp.shadows = LightShadows.Soft;

        // 6. Płotki z prefabu SmallFence
        GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SmallFence.prefab");
        if (fencePrefab != null)
        {
            GameObject fenceGroup = new GameObject("Fence_Line");
            fenceGroup.transform.SetParent(root.transform);
            for (int i = 0; i < 4; i++)
            {
                GameObject f = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab, fenceGroup.transform);
                f.transform.position = new Vector3(6f + (i * 2.5f), -1.5f, 15f);
                f.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
        }

        // 7. Skrzynki / Rekwizyty z Trash and Debris
        GameObject trashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3rdParty/trash-and-debris/source/Trash and Debris/TrashAndDebrisPreview.fbx");
        if (trashPrefab != null)
        {
            GameObject debris = (GameObject)PrefabUtility.InstantiatePrefab(trashPrefab, root.transform);
            debris.name = "Yard_Trash_And_Boxes";
            debris.transform.position = new Vector3(26f, -1.5f, 16f);
            debris.transform.localScale = Vector3.one * 1.2f;
        }

        // 8. Włącz klimatyczną mgłę w scenie (PSX Fog)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.14f, 0.12f, 0.11f, 1f); // Nastrojowy ciemny grafit / sepia
        RenderSettings.fogDensity = 0.032f;

        Selection.activeGameObject = root;
        Debug.Log("<color=#70FF70>[Cyrulik Outside Builder] Zbudowano klimatyczne podwórko i ulicę w 1 kliknięcie!</color>");
    }
}