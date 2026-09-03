#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kreator i automatyczny konfigurator celownika (Crosshair, Hold Ring, Square Morph, Pytajnik [?], Ręka, Kłódka).
/// Dodaje menu: Tools -> Cyrulik -> Auto-Setup Crosshair & Icons (Pytajnik, Kółko, Kwadrat).
/// </summary>
public static class CrosshairSetupBuilder
{
    [MenuItem("Tools/Cyrulik/Auto-Setup Crosshair & Icons (Pytajnik, Kółko, Kwadrat)", false, 10)]
    public static void SetupCrosshairAndIcons()
    {
        // 1. Upewnij się, że sprite'y w Assets/Art/UI_HoldIcons/ są wygenerowane
        HoldSpritesGenerator.GenerateAllSprites();

        // 2. Znajdź komponent Crosshair w scenie
        Crosshair crosshair = Object.FindAnyObjectByType<Crosshair>(FindObjectsInactive.Include);
        if (crosshair == null)
        {
            Debug.LogError("[CrosshairSetupBuilder] Nie znaleziono komponentu Crosshair w scenie!");
            return;
        }

        Undo.RecordObject(crosshair.gameObject, "Setup Crosshair Icons");
        SerializedObject so = new SerializedObject(crosshair);

        // Załaduj wygenerowane sprite'y
        Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/HoldRing_Smooth.png");
        Sprite clockworkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/HoldRing_Clockwork.png");
        Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/SquareMorph_RoundedBox.png");
        Sprite questionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_QuestionMark.png");
        Sprite exclamationSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_ExclamationMark.png");
        Sprite ellipsisSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Ellipsis.png");
        Sprite dotSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Default_Dot.png");
        Sprite handSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_HandGrip.png");
        Sprite lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Lock.png");

        // 3. Skonfiguruj lub stwórz SunRaysImage (Słoneczko) w UI
        Transform canvasTransform = crosshair.transform.parent;
        Sprite sunRaysSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Hold_SunRays.png");
        Transform sunTransform = canvasTransform.Find("SunRaysImage");
        if (sunTransform == null)
        {
            GameObject sunGo = new GameObject("SunRaysImage", typeof(RectTransform), typeof(Image));
            sunGo.transform.SetParent(canvasTransform, false);
            sunGo.transform.position = crosshair.transform.position;
            sunTransform = sunGo.transform;
        }

        var sunRect = sunTransform.GetComponent<RectTransform>();
        sunRect.sizeDelta = new Vector2(36f, 36f);
        sunRect.anchoredPosition = Vector2.zero;

        var sunImg = sunTransform.GetComponent<Image>();
        sunImg.sprite = sunRaysSprite;
        sunImg.color = new Color(1.0f, 0.86f, 0.32f, 0f);
        sunImg.raycastTarget = false;
        sunTransform.gameObject.SetActive(false);

        // 4. Skonfiguruj lub stwórz HoldProgressRing w UI
        Transform ringTransform = canvasTransform.Find("HoldProgressRing");
        if (ringTransform == null)
        {
            GameObject ringGo = new GameObject("HoldProgressRing", typeof(RectTransform), typeof(Image));
            ringGo.transform.SetParent(canvasTransform, false);
            ringGo.transform.position = crosshair.transform.position;
            ringTransform = ringGo.transform;
        }

        var ringRect = ringTransform.GetComponent<RectTransform>();
        ringRect.sizeDelta = new Vector2(32f, 32f);
        ringRect.anchoredPosition = Vector2.zero;

        var ringImg = ringTransform.GetComponent<Image>();
        ringImg.sprite = ringSprite;
        ringImg.color = new Color(0.98f, 0.82f, 0.35f, 0.95f); // Ciepły złoty
        ringImg.type = Image.Type.Filled;
        ringImg.fillMethod = Image.FillMethod.Radial360;
        ringImg.fillOrigin = (int)Image.Origin360.Top;
        ringImg.fillClockwise = true;
        ringImg.fillAmount = 0f;
        ringImg.raycastTarget = false;
        ringTransform.gameObject.SetActive(false);

        // 5. Skonfiguruj lub stwórz SquareMorphFrame w UI
        Transform sqTransform = canvasTransform.Find("SquareMorphFrame");
        if (sqTransform == null)
        {
            GameObject sqGo = new GameObject("SquareMorphFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
            sqGo.transform.SetParent(canvasTransform, false);
            sqGo.transform.position = crosshair.transform.position;
            sqTransform = sqGo.transform;
        }

        var sqRect = sqTransform.GetComponent<RectTransform>();
        sqRect.sizeDelta = new Vector2(24f, 24f);
        sqRect.anchoredPosition = Vector2.zero;

        var sqImg = sqTransform.GetComponent<Image>();
        sqImg.sprite = squareSprite;
        sqImg.color = new Color(1f, 1f, 1f, 0f);
        sqImg.raycastTarget = false;

        var outline = sqTransform.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color(0.95f, 0.8f, 0.35f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
        sqTransform.gameObject.SetActive(false);

        // 6. Skonfiguruj lub stwórz CrosshairFadeTransition (płynny dwuwarstwowy crossfade)
        Transform fadeTransform = canvasTransform.Find("CrosshairFadeTransition");
        if (fadeTransform == null)
        {
            GameObject fadeGo = new GameObject("CrosshairFadeTransition", typeof(RectTransform), typeof(Image));
            fadeGo.transform.SetParent(canvasTransform, false);
            fadeGo.transform.SetSiblingIndex(crosshair.transform.GetSiblingIndex() + 1);
            fadeGo.transform.position = crosshair.transform.position;
            fadeTransform = fadeGo.transform;
        }

        var fadeRect = fadeTransform.GetComponent<RectTransform>();
        fadeRect.sizeDelta = new Vector2(8f, 8f);
        fadeRect.anchoredPosition = Vector2.zero;

        var fadeImg = fadeTransform.GetComponent<Image>();
        fadeImg.color = new Color(1f, 1f, 1f, 0f);
        fadeImg.raycastTarget = false;
        fadeTransform.gameObject.SetActive(false);

        // 7. Przypisz referencje do pól komponentu Crosshair
        so.FindProperty("sunRaysImage").objectReferenceValue = sunImg;
        so.FindProperty("holdProgressRing").objectReferenceValue = ringImg;
        so.FindProperty("squareMorphFrame").objectReferenceValue = sqImg;
        so.FindProperty("fadeTransitionImage").objectReferenceValue = fadeImg;

        so.FindProperty("defaultDotSprite").objectReferenceValue = dotSprite;
        so.FindProperty("inspectQuestionSprite").objectReferenceValue = questionSprite;
        so.FindProperty("exclamationSprite").objectReferenceValue = exclamationSprite;
        so.FindProperty("ellipsisSprite").objectReferenceValue = ellipsisSprite;
        so.FindProperty("interactHandSprite").objectReferenceValue = handSprite;
        so.FindProperty("clockworkRingSprite").objectReferenceValue = clockworkSprite;
        so.FindProperty("lockedKeySprite").objectReferenceValue = lockSprite;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(crosshair.gameObject);

        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        Selection.activeGameObject = crosshair.gameObject;
        EditorGUIUtility.PingObject(crosshair.gameObject);

        Debug.Log("<color=#70FF70>[CrosshairSetupBuilder] Pomyślnie skonfigurowano celownik: oddychanie kropki w idlu, płynne przejścia (?, !, ...) oraz Słoneczko Hold!</color>");
    }
}
#endif
