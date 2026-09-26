#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Automatyczny kreator całego systemu Item Showcase (3D Studio + Canvas UI) w Unity Editor.
/// Tworzy kompletną architekturę z poziomu menu:
/// Tools -> Cyrulik -> Create Full Item Showcase System
/// </summary>
public static class ItemShowcaseBuilder
{
    private const string CANVAS_NAME = "ItemShowcase_Canvas";
    private const string STUDIO_NAME = "ItemShowcase_Studio";

    [MenuItem("Tools/Cyrulik/★ Setup All: Showcase System, Scissors & Comments", false, 0)]
    public static void SetupAllItemShowcaseScissorsAndComments()
    {
        // 1. Studio & Canvas UI
        ItemShowcaseStudio studio = CreateOrGetStudio();
        ItemShowcaseUI ui = CreateOrGetUI(studio);

        // 2. Prefab i postawienie nożyczek
        ScissorsPrefabBuilder.PlaceScissorsInSalon();

        // 3. Wzbogacenie komentarzy do obiektów w scenie
        ItemCommentsEnhancer.ApplyRichItemComments();

        Selection.activeGameObject = ui.gameObject;
        EditorGUIUtility.PingObject(ui.gameObject);

        EditorUtility.DisplayDialog("Cyrulik - Sukces!", "Wszystko gotowe!\n\n1. System 3D Showcase (Studio + UI)\n2. Nożyczki (SCISSORS) postawione na stole z pełnym widokiem 3D\n3. Wzbogacone komentarze do obiektów w scenie!", "Super!");
    }

    [MenuItem("Tools/Cyrulik/Create Full Item Showcase System", false, 5)]
    public static void CreateFullShowcaseSystem()
    {
        // 1. Stwórz lub pobierz Studio 3D
        ItemShowcaseStudio studio = CreateOrGetStudio();

        // 2. Stwórz lub pobierz Canvas UI
        ItemShowcaseUI ui = CreateOrGetUI(studio);

        Selection.activeGameObject = ui.gameObject;
        EditorGUIUtility.PingObject(ui.gameObject);

        EditorUtility.DisplayDialog("Sukces!", "System Item Showcase (3D Studio + Canvas UI) został pomyślnie skonfigurowany!", "Super");
        Debug.Log("[ItemShowcaseBuilder] Pomyślnie utworzono system Item Showcase!");
    }

    public static ItemShowcaseStudio CreateOrGetStudio()
    {
        GameObject existingGo = GameObject.Find(STUDIO_NAME);
        if (existingGo != null && existingGo.TryGetComponent<ItemShowcaseStudio>(out var existingStudio))
        {
            return existingStudio;
        }

        // Studio umieszczamy na Y = -350f, by było kompletnie odizolowane od geometrii poziomu
        GameObject studioRoot = new GameObject(STUDIO_NAME, typeof(ItemShowcaseStudio));
        studioRoot.transform.position = new Vector3(0f, -350f, 0f);
        studioRoot.transform.rotation = Quaternion.identity;

        ItemShowcaseStudio studio = studioRoot.GetComponent<ItemShowcaseStudio>();

        // Punkt montażu modelu (Pivot)
        GameObject mountPoint = new GameObject("Item_MountPoint");
        mountPoint.transform.SetParent(studioRoot.transform, false);
        mountPoint.transform.localPosition = new Vector3(0f, 0f, 0f);

        // Kamera studia
        GameObject camGo = new GameObject("Showcase_Camera", typeof(Camera));
        camGo.transform.SetParent(studioRoot.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.05f, -1.35f);
        camGo.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);

        Camera cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 28f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.enabled = false;

        // Światło główne (ciepłe, vintage studio light)
        GameObject mainLightGo = new GameObject("Showcase_MainLight", typeof(Light));
        mainLightGo.transform.SetParent(studioRoot.transform, false);
        mainLightGo.transform.localPosition = new Vector3(0.6f, 0.8f, -0.8f);
        mainLightGo.transform.localRotation = Quaternion.Euler(45f, -35f, 0f);

        Light mainL = mainLightGo.GetComponent<Light>();
        mainL.type = LightType.Directional;
        mainL.color = new Color(1.0f, 0.95f, 0.88f, 1f);
        mainL.intensity = 1.6f;
        mainL.enabled = false;

        // Światło konturowe (Rim Light - chłodny zarys krawędzi)
        GameObject rimLightGo = new GameObject("Showcase_RimLight", typeof(Light));
        rimLightGo.transform.SetParent(studioRoot.transform, false);
        rimLightGo.transform.localPosition = new Vector3(-0.8f, -0.4f, 0.8f);
        rimLightGo.transform.localRotation = Quaternion.Euler(-25f, 145f, 0f);

        Light rimL = rimLightGo.GetComponent<Light>();
        rimL.type = LightType.Directional;
        rimL.color = new Color(0.65f, 0.8f, 1.0f, 1f);
        rimL.intensity = 0.8f;
        rimL.enabled = false;

        // Powiąż SerializedObject
        SerializedObject so = new SerializedObject(studio);
        so.FindProperty("showcaseCamera").objectReferenceValue = cam;
        so.FindProperty("itemMountPoint").objectReferenceValue = mountPoint.transform;
        so.FindProperty("mainLight").objectReferenceValue = mainL;
        so.FindProperty("rimLight").objectReferenceValue = rimL;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(studioRoot, "Create Item Showcase Studio");
        return studio;
    }

    public static ItemShowcaseUI CreateOrGetUI(ItemShowcaseStudio studio)
    {
        GameObject existingGo = GameObject.Find(CANVAS_NAME);
        if (existingGo != null && existingGo.TryGetComponent<ItemShowcaseUI>(out var existingUI))
        {
            return existingUI;
        }

        // 1. Canvas Root
        GameObject canvasGo = new GameObject(CANVAS_NAME, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(ItemShowcaseUI));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 95; // Tuż pod menu pauzy, nad zwykłym HUDem

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup rootCanvasGroup = canvasGo.GetComponent<CanvasGroup>();
        ItemShowcaseUI ui = canvasGo.GetComponent<ItemShowcaseUI>();

        // Pobierz stylowe fonty projektu
        TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rye-Regular SDF.asset")
                               ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset");
        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset") ?? titleFont;

        // 2. Przyciemnienie tła (Vignette / Dimmer)
        GameObject dimmerGo = new GameObject("Background_Dimmer", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dimmerGo.transform.SetParent(canvasGo.transform, false);
        RectTransform dimmerRect = dimmerGo.GetComponent<RectTransform>();
        dimmerRect.anchorMin = Vector2.zero;
        dimmerRect.anchorMax = Vector2.one;
        dimmerRect.sizeDelta = Vector2.zero;

        Image dimmerImg = dimmerGo.GetComponent<Image>();
        dimmerImg.color = new Color(0.04f, 0.04f, 0.05f, 0.85f);
        CanvasGroup dimmerCg = dimmerGo.GetComponent<CanvasGroup>();

        // 3. Nagłówek (Tytuł przedmiotu na górze, np. SCISSORS)
        GameObject titleGo = new GameObject("Title_Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(canvasGo.transform, false);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -65f);
        titleRect.sizeDelta = new Vector2(900f, 90f);

        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        if (titleFont != null) titleTmp.font = titleFont;
        titleTmp.text = "SCISSORS";
        titleTmp.fontSize = 58;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.characterSpacing = 12f;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.95f, 0.88f, 0.72f, 1f); // Ciepłe stare złoto

        // Ozdobna linia pod tytułem
        GameObject lineGo = new GameObject("Title_Underline", typeof(RectTransform), typeof(Image));
        lineGo.transform.SetParent(titleGo.transform, false);
        RectTransform lineRect = lineGo.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0f);
        lineRect.anchorMax = new Vector2(0.5f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = new Vector2(0f, -4f);
        lineRect.sizeDelta = new Vector2(380f, 2.5f);
        Image lineImg = lineGo.GetComponent<Image>();
        lineImg.color = new Color(0.75f, 0.62f, 0.38f, 0.7f);

        // 4. Środkowy podgląd 3D (RawImage z RenderTexture)
        GameObject viewportGo = new GameObject("Model_Viewport", typeof(RectTransform), typeof(RawImage));
        viewportGo.transform.SetParent(canvasGo.transform, false);
        RectTransform vpRect = viewportGo.GetComponent<RectTransform>();
        vpRect.anchorMin = new Vector2(0.5f, 0.5f);
        vpRect.anchorMax = new Vector2(0.5f, 0.5f);
        vpRect.pivot = new Vector2(0.5f, 0.5f);
        vpRect.anchoredPosition = new Vector2(0f, 35f);
        vpRect.sizeDelta = new Vector2(720f, 540f);

        RawImage vpRaw = viewportGo.GetComponent<RawImage>();
        vpRaw.color = Color.white;
        if (studio != null)
        {
            vpRaw.texture = studio.GetRenderTexture();
        }

        // 5. Dolna ramka komentarza / myśli fryzjera
        GameObject commentBox = new GameObject("Comment_Box", typeof(RectTransform), typeof(Image));
        commentBox.transform.SetParent(canvasGo.transform, false);
        RectTransform boxRect = commentBox.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0f);
        boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.anchoredPosition = new Vector2(0f, 70f);
        boxRect.sizeDelta = new Vector2(980f, 130f);

        Image boxImg = commentBox.GetComponent<Image>();
        boxImg.color = new Color(0.08f, 0.09f, 0.11f, 0.92f);

        // Ramka / Border
        GameObject borderGo = new GameObject("Box_Border", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(commentBox.transform, false);
        RectTransform borderRect = borderGo.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        Image borderImg = borderGo.GetComponent<Image>();
        borderImg.color = new Color(0.55f, 0.45f, 0.32f, 0.55f);

        // Tekst komentarza
        GameObject textGo = new GameObject("Comment_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(commentBox.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 15f);
        textRect.offsetMax = new Vector2(-150f, -15f);

        TextMeshProUGUI commentTmp = textGo.GetComponent<TextMeshProUGUI>();
        if (bodyFont != null) commentTmp.font = bodyFont;
        commentTmp.text = "<i>\"Heavy barber scissors. Cold Swedish steel, keen enough to snip through anything.\"</i>";
        commentTmp.fontSize = 24;
        commentTmp.fontStyle = FontStyles.Italic;
        commentTmp.color = new Color(0.92f, 0.94f, 0.96f, 1f);
        commentTmp.alignment = TextAlignmentOptions.MidlineLeft;
        commentTmp.textWrappingMode = TextWrappingModes.Normal;

        // 6. Wskaźnik [E] Take / Continue na dole po prawej
        GameObject promptGo = new GameObject("Prompt_Continue", typeof(RectTransform), typeof(CanvasGroup));
        promptGo.transform.SetParent(commentBox.transform, false);
        RectTransform promptRect = promptGo.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(1f, 0.5f);
        promptRect.anchorMax = new Vector2(1f, 0.5f);
        promptRect.pivot = new Vector2(1f, 0.5f);
        promptRect.anchoredPosition = new Vector2(-22f, 0f);
        promptRect.sizeDelta = new Vector2(110f, 50f);

        CanvasGroup promptCg = promptGo.GetComponent<CanvasGroup>();

        // Klawisz [E]
        GameObject keyGo = new GameObject("Key_Badge", typeof(RectTransform), typeof(Image));
        keyGo.transform.SetParent(promptGo.transform, false);
        RectTransform keyRect = keyGo.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 0.5f);
        keyRect.anchorMax = new Vector2(0f, 0.5f);
        keyRect.pivot = new Vector2(0f, 0.5f);
        keyRect.anchoredPosition = new Vector2(0f, 0f);
        keyRect.sizeDelta = new Vector2(38f, 38f);
        Image keyImg = keyGo.GetComponent<Image>();
        keyImg.color = new Color(0.24f, 0.22f, 0.18f, 0.95f);

        GameObject keyTextGo = new GameObject("Key_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        keyTextGo.transform.SetParent(keyGo.transform, false);
        RectTransform keyTextRect = keyTextGo.GetComponent<RectTransform>();
        keyTextRect.anchorMin = Vector2.zero;
        keyTextRect.anchorMax = Vector2.one;
        keyTextRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI keyTmp = keyTextGo.GetComponent<TextMeshProUGUI>();
        keyTmp.text = "E";
        keyTmp.fontSize = 22;
        keyTmp.fontStyle = FontStyles.Bold;
        keyTmp.alignment = TextAlignmentOptions.Center;
        keyTmp.color = Color.white;

        // Tekst akcji (Take / Continue)
        GameObject actionTextGo = new GameObject("Action_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        actionTextGo.transform.SetParent(promptGo.transform, false);
        RectTransform actionTextRect = actionTextGo.GetComponent<RectTransform>();
        actionTextRect.anchorMin = new Vector2(0f, 0f);
        actionTextRect.anchorMax = new Vector2(1f, 1f);
        actionTextRect.offsetMin = new Vector2(46f, 0f);
        actionTextRect.offsetMax = Vector2.zero;
        TextMeshProUGUI actionTmp = actionTextGo.GetComponent<TextMeshProUGUI>();
        actionTmp.text = "Take";
        actionTmp.fontSize = 20;
        actionTmp.fontStyle = FontStyles.Bold;
        actionTmp.color = new Color(0.92f, 0.85f, 0.65f, 1f);
        actionTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // 7. Powiązanie w SerializedObject
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("mainCanvasGroup").objectReferenceValue = rootCanvasGroup;
        so.FindProperty("backgroundDimmer").objectReferenceValue = dimmerCg;
        so.FindProperty("titleText").objectReferenceValue = titleTmp;
        so.FindProperty("modelViewport").objectReferenceValue = vpRaw;
        so.FindProperty("commentText").objectReferenceValue = commentTmp;
        so.FindProperty("continuePromptGroup").objectReferenceValue = promptCg;
        so.FindProperty("promptActionText").objectReferenceValue = actionTmp;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Item Showcase Canvas");
        return ui;
    }
}
#endif
