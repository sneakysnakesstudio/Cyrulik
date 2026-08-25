#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automatyczny konfigurator zadania z piecem (Stove Quest: Clean Towel) w Unity Editor.
/// Konfiguruje lub tworzy obiekty: Ogień, Garnek na piecu, Parę wodną, Ręcznik w kotle,
/// oraz upewnia się, że Zlew i Garnek mają właściwe komponenty.
/// </summary>
public static class StoveQuestSetupBuilder
{
    [MenuItem("Tools/Cyrulik/3. Setup Stove Quest Visuals & References", false, 3)]
    public static void SetupStoveQuest()
    {
        // 1. Znajdź StoveController w scenie
        StoveController stoveController = Object.FindAnyObjectByType<StoveController>();
        if (stoveController == null)
        {
            GameObject stoveObj = GameObject.Find("Stove") ?? GameObject.Find("StoveQuest");
            if (stoveObj != null)
            {
                stoveController = stoveObj.AddComponent<StoveController>();
            }
            else
            {
                EditorUtility.DisplayDialog("Błąd", "Nie znaleziono obiektu Stove ani StoveQuest w scenie! Upewnij się, że piec znajduje się w scenie.", "OK");
                return;
            }
        }

        Transform stoveTransform = stoveController.transform;
        Undo.RegisterFullObjectHierarchyUndo(stoveController.gameObject, "Setup Stove Quest");

        // 2. Upewnij się, że jest BoxCollider na piecu do interakcji
        if (stoveController.GetComponent<Collider>() == null)
        {
            BoxCollider col = stoveController.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.4f, 0f);
            col.size = new Vector3(0.8f, 0.8f, 0.8f);
        }

        // 3. Punkt montażowy / Snap point garnka
        Transform snapPoint = stoveTransform.Find("Pot_SnapPoint");
        if (snapPoint == null)
        {
            GameObject snapGo = new GameObject("Pot_SnapPoint");
            snapGo.transform.SetParent(stoveTransform, false);
            snapGo.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            snapPoint = snapGo.transform;
        }

        // 4. Wizualia ognia (Fire Visual & Light)
        Transform fireTrans = stoveTransform.Find("Fire_Visual");
        GameObject fireGo;
        Light fireLight;
        if (fireTrans == null)
        {
            fireGo = new GameObject("Fire_Visual");
            fireGo.transform.SetParent(stoveTransform, false);
            fireGo.transform.localPosition = new Vector3(0f, 0.25f, 0.15f);

            GameObject lightGo = new GameObject("Fire_PointLight");
            lightGo.transform.SetParent(fireGo.transform, false);
            fireLight = lightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.15f);
            fireLight.intensity = 2.0f;
            fireLight.range = 3.5f;
            fireLight.enabled = false;
        }
        else
        {
            fireGo = fireTrans.gameObject;
            fireLight = fireGo.GetComponentInChildren<Light>();
        }

        // 5. Garnek na piecu (Pot on Stove Visual)
        Transform potVisualTrans = stoveTransform.Find("Pot_On_Stove");
        GameObject potVisualGo;
        if (potVisualTrans == null)
        {
            potVisualGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            potVisualGo.name = "Pot_On_Stove";
            potVisualGo.transform.SetParent(stoveTransform, false);
            potVisualGo.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            potVisualGo.transform.localScale = new Vector3(0.28f, 0.15f, 0.28f);
            Object.DestroyImmediate(potVisualGo.GetComponent<Collider>());
        }
        else
        {
            potVisualGo = potVisualTrans.gameObject;
        }

        // 6. Tafla wody w garnku
        Transform waterTrans = potVisualGo.transform.Find("Water_Surface");
        GameObject waterGo;
        if (waterTrans == null)
        {
            waterGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waterGo.name = "Water_Surface";
            waterGo.transform.SetParent(potVisualGo.transform, false);
            waterGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            waterGo.transform.localScale = new Vector3(0.92f, 0.05f, 0.92f);
            Object.DestroyImmediate(waterGo.GetComponent<Collider>());

            // Niebieskawy materiał wody
            Material waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            waterMat.color = new Color(0.18f, 0.45f, 0.7f, 0.85f);
            waterGo.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
        }
        else
        {
            waterGo = waterTrans.gameObject;
        }

        // 7. Ręcznik w garnku (Towel in pot visual)
        Transform towelInPotTrans = potVisualGo.transform.Find("Towel_In_Pot");
        GameObject towelInPotGo;
        if (towelInPotTrans == null)
        {
            towelInPotGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            towelInPotGo.name = "Towel_In_Pot";
            towelInPotGo.transform.SetParent(potVisualGo.transform, false);
            towelInPotGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            towelInPotGo.transform.localRotation = Quaternion.Euler(15f, 25f, -10f);
            towelInPotGo.transform.localScale = new Vector3(0.55f, 0.25f, 0.55f);
            Object.DestroyImmediate(towelInPotGo.GetComponent<Collider>());

            Material towelMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            towelMat.color = new Color(0.88f, 0.85f, 0.78f, 1f); // Czysty biały/kremowy materiał
            towelInPotGo.GetComponent<MeshRenderer>().sharedMaterial = towelMat;
        }
        else
        {
            towelInPotGo = towelInPotTrans.gameObject;
        }

        // 8. Para wodna (Steam Visual)
        Transform steamTrans = potVisualGo.transform.Find("Steam_Particles");
        GameObject steamGo;
        if (steamTrans == null)
        {
            steamGo = new GameObject("Steam_Particles");
            steamGo.transform.SetParent(potVisualGo.transform, false);
            steamGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);

            ParticleSystem ps = steamGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = 1.8f;
            main.startSpeed = 0.25f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 1f, 1f, 0.35f);

            var emission = ps.emission;
            emission.rateOverTime = 10;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = 0f;
            vel.y = 0.35f;
            vel.z = 0f;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.4f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = grad;

            ParticleSystemRenderer rend = steamGo.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
        }
        else
        {
            steamGo = steamTrans.gameObject;
        }

        // 9. Podpięcie referencji w StoveController przez SerializedObject
        SerializedObject so = new SerializedObject(stoveController);
        so.FindProperty("fireVisual").objectReferenceValue = fireGo;
        so.FindProperty("fireLight").objectReferenceValue = fireLight;
        so.FindProperty("potSnapPoint").objectReferenceValue = snapPoint;
        so.FindProperty("potOnStoveVisual").objectReferenceValue = potVisualGo;
        so.FindProperty("waterInPotVisual").objectReferenceValue = waterGo;
        so.FindProperty("steamVisual").objectReferenceValue = steamGo;
        so.FindProperty("towelInPotVisual").objectReferenceValue = towelInPotGo;
        so.FindProperty("towelTaskId").stringValue = "clean_towel";

        // Automatyczne wykrywanie drzwiczek w modelu pieca
        Transform stoveDoor = FindDoorTransform(stoveTransform);
        if (stoveDoor != null)
        {
            so.FindProperty("stoveDoor").objectReferenceValue = stoveDoor;
            so.FindProperty("requireDoorOpenToLight").boolValue = true;
        }

        so.ApplyModifiedProperties();

        // Wyłączamy wizualia na start (zostaną włączone podczas rozgrywki)
        fireGo.SetActive(false);
        potVisualGo.SetActive(false);
        waterGo.SetActive(false);
        steamGo.SetActive(false);
        towelInPotGo.SetActive(false);

        Selection.activeGameObject = stoveController.gameObject;
        EditorGUIUtility.PingObject(stoveController.gameObject);

        string doorMsg = stoveDoor != null ? $"\n(Wykryto drzwiczki: {stoveDoor.name})" : "";
        EditorUtility.DisplayDialog("Sukces!", $"Piec (StoveQuest) został w pełni skonfigurowany ze wszystkimi wizualiami (ogień, garnek, woda, para, ręcznik)!{doorMsg}", "OK");
        Debug.Log($"[Cyrulik] StoveQuest został pomyślnie skonfigurowany w scenie!{doorMsg}");
    }

    private static Transform FindDoorTransform(Transform root)
    {
        string[] doorKeywords = { "door", "drzwi", "drzwiczki", "hatch", "gate", "klapa" };
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root) continue;
            string nameLower = child.name.ToLowerInvariant();
            foreach (string kw in doorKeywords)
            {
                if (nameLower.Contains(kw))
                {
                    return child;
                }
            }
        }
        return null;
    }
}
#endif
