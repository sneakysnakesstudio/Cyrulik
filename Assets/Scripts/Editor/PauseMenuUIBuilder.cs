#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class PauseMenuUIBuilder
{
    [MenuItem("Tools/Cyrulik/Create In-Game Pause Menu UI (ESC Menu)", false, 2)]
    [MenuItem("GameObject/UI/Cyrulik - In-Game Pause Menu UI", false, 11)]
    public static void CreatePauseMenuUI()
    {
        // 1. Sprawdź / utwórz EventSystem z InputSystemUIInputModule
        EventSystem es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
        }
        else
        {
            var oldModule = es.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Object.DestroyImmediate(oldModule);
            }

            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        // 2. Pobierz lub stwórz dedykowany Canvas Menu Pauzy (sortingOrder = 25)
        GameObject canvasGo = GameObject.Find("PauseMenu_Canvas");
        Canvas canvas;
        if (canvasGo == null)
        {
            canvasGo = new GameObject("PauseMenu_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create PauseMenu Canvas");
        }
        else
        {
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.sortingOrder = 25;
            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
            }
        }

        // Usuń CanvasGroup z głównego Canvasu jeśli istnieje (CanvasGroup będzie na PauseMenu_Manager)
        var rogueCg = canvasGo.GetComponent<CanvasGroup>();
        if (rogueCg != null)
        {
            Object.DestroyImmediate(rogueCg);
        }

        // Usuń stare menu jeśli istnieje
        Transform existingManager = canvasGo.transform.Find("PauseMenu_Manager");
        if (existingManager != null)
        {
            Undo.DestroyObjectImmediate(existingManager.gameObject);
        }

        // Pobierz czcionki z projektu
        TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rye-Regular SDF.asset");
        TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset");

        // 3. Główny kontener Menu Pauzy
        GameObject menuRoot = new GameObject("PauseMenu_Manager", typeof(RectTransform), typeof(CanvasGroup), typeof(PauseMenu));
        menuRoot.transform.SetParent(canvas.transform, false);
        var rootRect = menuRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        CanvasGroup rootCg = menuRoot.GetComponent<CanvasGroup>();
        rootCg.alpha = 0f;
        rootCg.interactable = false;
        rootCg.blocksRaycasts = false;
        menuRoot.SetActive(true);

        PauseMenu pauseMenu = menuRoot.GetComponent<PauseMenu>();

        // 4. Przyciemniające tło (Dim Backdrop)
        GameObject backdropGo = new GameObject("Dim_Backdrop", typeof(RectTransform), typeof(Image));
        backdropGo.transform.SetParent(menuRoot.transform, false);
        var backdropRect = backdropGo.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.sizeDelta = Vector2.zero;
        var backdropImg = backdropGo.GetComponent<Image>();
        backdropImg.color = new Color(0.04f, 0.03f, 0.03f, 0.72f);
        backdropImg.raycastTarget = true;

        // 5. Przycisk RESTART w prawym górnym rogu
        GameObject restartBtnGo = new GameObject("Restart_Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(MenuButtonEffects));
        restartBtnGo.transform.SetParent(menuRoot.transform, false);
        var restartRect = restartBtnGo.GetComponent<RectTransform>();
        restartRect.anchorMin = new Vector2(1f, 1f);
        restartRect.anchorMax = new Vector2(1f, 1f);
        restartRect.pivot = new Vector2(1f, 1f);
        restartRect.anchoredPosition = new Vector2(-45f, -40f);
        restartRect.sizeDelta = new Vector2(180f, 48f);

        var restartImg = restartBtnGo.GetComponent<Image>();
        restartImg.color = new Color(0.18f, 0.14f, 0.11f, 0.95f);
        restartImg.raycastTarget = true;

        var restartOutline = restartBtnGo.GetComponent<Outline>();
        restartOutline.effectColor = new Color(0.85f, 0.65f, 0.28f, 0.85f);
        restartOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var restartBtn = restartBtnGo.GetComponent<Button>();
        restartBtn.targetGraphic = restartImg;

        GameObject restartTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        restartTextGo.transform.SetParent(restartBtnGo.transform, false);
        var restartTextRect = restartTextGo.GetComponent<RectTransform>();
        restartTextRect.anchorMin = Vector2.zero;
        restartTextRect.anchorMax = Vector2.one;
        restartTextRect.offsetMin = Vector2.zero;
        restartTextRect.offsetMax = Vector2.zero;

        var restartTmp = restartTextGo.GetComponent<TextMeshProUGUI>();
        restartTmp.text = "RESTART";
        restartTmp.fontSize = 22;
        restartTmp.fontStyle = FontStyles.Bold;
        restartTmp.color = new Color(0.96f, 0.88f, 0.72f, 1f);
        restartTmp.alignment = TextAlignmentOptions.Center;
        restartTmp.raycastTarget = false;
        if (defaultFont != null) restartTmp.font = defaultFont;

        UnityEventTools.AddPersistentListener(restartBtn.onClick, pauseMenu.RestartGame);

        // 6. Napis wersji w prawym dolnym rogu
        GameObject versionGo = new GameObject("Version_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        versionGo.transform.SetParent(menuRoot.transform, false);
        var versionRect = versionGo.GetComponent<RectTransform>();
        versionRect.anchorMin = new Vector2(1f, 0f);
        versionRect.anchorMax = new Vector2(1f, 0f);
        versionRect.pivot = new Vector2(1f, 0f);
        versionRect.anchoredPosition = new Vector2(-35f, 30f);
        versionRect.sizeDelta = new Vector2(450f, 40f);

        var versionTmp = versionGo.GetComponent<TextMeshProUGUI>();
        versionTmp.text = "PROTOTYPE VERSION 0.0.3";
        versionTmp.fontSize = 22;
        versionTmp.fontStyle = FontStyles.Bold;
        versionTmp.color = new Color(0.92f, 0.78f, 0.45f, 0.75f);
        versionTmp.alignment = TextAlignmentOptions.Right;
        versionTmp.raycastTarget = false;
        if (defaultFont != null) versionTmp.font = defaultFont;

        // 7. Panel Główny (Tytuł + Przyciski: RESUME, SETTINGS, CREDITS, MAIN MENU, QUIT)
        GameObject mainPanelGo = new GameObject("MainButtons_Panel", typeof(RectTransform), typeof(CanvasGroup));
        mainPanelGo.transform.SetParent(menuRoot.transform, false);
        var mainPanelRect = mainPanelGo.GetComponent<RectTransform>();
        mainPanelRect.anchorMin = Vector2.zero;
        mainPanelRect.anchorMax = Vector2.one;
        mainPanelRect.sizeDelta = Vector2.zero;
        var mainPanelCg = mainPanelGo.GetComponent<CanvasGroup>();
        mainPanelCg.alpha = 1f;
        mainPanelCg.blocksRaycasts = true;
        mainPanelCg.interactable = true;

        // Tytuł gry "CYRULIK"
        GameObject titleGo = new GameObject("GameTitle_Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        titleGo.transform.SetParent(mainPanelGo.transform, false);
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(0f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.anchoredPosition = new Vector2(140f, 225f);
        titleRect.sizeDelta = new Vector2(650f, 130f);

        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "CYRULIK";
        titleTmp.fontSize = 86;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.96f, 0.88f, 0.72f, 1f);
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.raycastTarget = false;
        if (titleFont != null) titleTmp.font = titleFont;

        var titleShadow = titleGo.GetComponent<Shadow>();
        titleShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        titleShadow.effectDistance = new Vector2(4f, -4f);

        // Podtytuł
        GameObject subTitleGo = new GameObject("Subtitle_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        subTitleGo.transform.SetParent(mainPanelGo.transform, false);
        var subTitleRect = subTitleGo.GetComponent<RectTransform>();
        subTitleRect.anchorMin = new Vector2(0f, 0.5f);
        subTitleRect.anchorMax = new Vector2(0f, 0.5f);
        subTitleRect.pivot = new Vector2(0f, 0.5f);
        subTitleRect.anchoredPosition = new Vector2(145f, 150f);
        subTitleRect.sizeDelta = new Vector2(650f, 40f);

        var subTitleTmp = subTitleGo.GetComponent<TextMeshProUGUI>();
        subTitleTmp.text = "A RETRO BARBER EXPERIENCE";
        subTitleTmp.fontSize = 19;
        subTitleTmp.fontStyle = FontStyles.Bold;
        subTitleTmp.characterSpacing = 8;
        subTitleTmp.color = new Color(0.78f, 0.62f, 0.38f, 0.85f);
        subTitleTmp.alignment = TextAlignmentOptions.Left;
        subTitleTmp.raycastTarget = false;
        if (defaultFont != null) subTitleTmp.font = defaultFont;

        // Kontener na przyciski menu z dużymi odstępami (spacing: 22f)
        GameObject btnContainer = new GameObject("Buttons_Container", typeof(RectTransform), typeof(VerticalLayoutGroup));
        btnContainer.transform.SetParent(mainPanelGo.transform, false);
        var btnContainerRect = btnContainer.GetComponent<RectTransform>();
        btnContainerRect.anchorMin = new Vector2(0f, 0.5f);
        btnContainerRect.anchorMax = new Vector2(0f, 0.5f);
        btnContainerRect.pivot = new Vector2(0f, 0.5f);
        btnContainerRect.anchoredPosition = new Vector2(140f, -95f);
        btnContainerRect.sizeDelta = new Vector2(340f, 380f);

        var layout = btnContainer.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        // Utwórz przyciski: RESUME, SETTINGS, CREDITS, MAIN MENU, QUIT
        Button resumeBtn = CreateMenuButton("Resume_Button", "RESUME", btnContainer.transform, defaultFont);
        Button settingsBtn = CreateMenuButton("Settings_Button", "SETTINGS", btnContainer.transform, defaultFont);
        Button creditsBtn = CreateMenuButton("Credits_Button", "CREDITS", btnContainer.transform, defaultFont);
        Button mainMenuBtn = CreateMenuButton("MainMenu_Button", "MAIN MENU", btnContainer.transform, defaultFont);
        Button quitBtn = CreateMenuButton("Quit_Button", "QUIT", btnContainer.transform, defaultFont);

        // Podpięcie zdarzeń do przycisków
        UnityEventTools.AddPersistentListener(resumeBtn.onClick, pauseMenu.ResumeGame);
        UnityEventTools.AddPersistentListener(settingsBtn.onClick, pauseMenu.OpenSettings);
        UnityEventTools.AddPersistentListener(creditsBtn.onClick, pauseMenu.OpenCredits);
        UnityEventTools.AddPersistentListener(mainMenuBtn.onClick, pauseMenu.ReturnToMainMenu);
        UnityEventTools.AddPersistentListener(quitBtn.onClick, pauseMenu.QuitGame);

        // 8. Panel Ustawień (SETTINGS)
        GameObject settingsPanelGo = CreateModalPanel("Settings_Panel", menuRoot.transform, "SETTINGS", titleFont, defaultFont, out Button settingsBackBtn, out Slider volSlider, out Toggle fsToggle);
        var settingsCg = settingsPanelGo.GetComponent<CanvasGroup>();
        UnityEventTools.AddPersistentListener(settingsBackBtn.onClick, pauseMenu.CloseSettings);

        // 9. Panel Twórców (CREDITS)
        GameObject creditsPanelGo = CreateCreditsPanel("Credits_Panel", menuRoot.transform, "CREDITS", titleFont, defaultFont, out Button creditsBackBtn);
        var creditsCg = creditsPanelGo.GetComponent<CanvasGroup>();
        UnityEventTools.AddPersistentListener(creditsBackBtn.onClick, pauseMenu.CloseCredits);

        // 10. Przypisanie referencji w PauseMenu
        SerializedObject so = new SerializedObject(pauseMenu);
        so.FindProperty("pauseCanvas").objectReferenceValue = canvas;
        so.FindProperty("pauseCanvasGroup").objectReferenceValue = rootCg;
        so.FindProperty("mainButtonsPanel").objectReferenceValue = mainPanelCg;
        so.FindProperty("settingsPanel").objectReferenceValue = settingsCg;
        so.FindProperty("creditsPanel").objectReferenceValue = creditsCg;
        so.FindProperty("restartButton").objectReferenceValue = restartBtn;
        so.FindProperty("versionLabel").objectReferenceValue = versionTmp;
        so.FindProperty("volumeSlider").objectReferenceValue = volSlider;
        so.FindProperty("fullscreenToggle").objectReferenceValue = fsToggle;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(menuRoot, "Create PauseMenu UI");
        Selection.activeGameObject = menuRoot;
        Debug.Log("[PauseMenuUIBuilder] Pomyślnie utworzono In-Game Menu Pauzy pod klawisz ESC z przyciskiem RESTART w prawym górnym rogu!");
    }

    private static Button CreateMenuButton(string objName, string label, Transform parent, TMP_FontAsset font)
    {
        GameObject btnGo = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(LayoutElement), typeof(MenuButtonEffects));
        btnGo.transform.SetParent(parent, false);

        var rect = btnGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320f, 52f);

        var layoutElement = btnGo.GetComponent<LayoutElement>();
        layoutElement.minHeight = 52f;
        layoutElement.preferredHeight = 52f;
        layoutElement.flexibleHeight = 0;

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.15f, 0.13f, 0.11f, 0.95f);
        img.raycastTarget = true;

        var outline = btnGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.65f, 0.5f, 0.28f, 0.55f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = true;

        // Label
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(btnGo.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 0f);
        textRect.offsetMax = new Vector2(-18f, 0f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 25;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.96f, 0.93f, 0.86f, 1f);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        return btn;
    }

    private static GameObject CreateModalPanel(string name, Transform parent, string title, TMP_FontAsset titleFont, TMP_FontAsset font, out Button backBtn, out Slider volSlider, out Toggle fsToggle)
    {
        GameObject panelGo = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        panelGo.transform.SetParent(parent, false);

        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(580f, 440f);

        var img = panelGo.GetComponent<Image>();
        img.color = new Color(0.08f, 0.07f, 0.06f, 0.95f);
        img.raycastTarget = true;

        var outline = panelGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.55f, 0.3f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Header
        GameObject headerGo = new GameObject("Header_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGo.transform.SetParent(panelGo.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 1f);
        headerRect.anchorMax = new Vector2(0.5f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -25f);
        headerRect.sizeDelta = new Vector2(500f, 50f);

        var headerTmp = headerGo.GetComponent<TextMeshProUGUI>();
        headerTmp.text = title;
        headerTmp.fontSize = 36;
        headerTmp.fontStyle = FontStyles.Bold;
        headerTmp.color = new Color(0.95f, 0.8f, 0.45f, 1f);
        headerTmp.alignment = TextAlignmentOptions.Center;
        headerTmp.raycastTarget = false;
        if (titleFont != null) headerTmp.font = titleFont;

        // Master Volume Label & Slider
        GameObject volLabelGo = new GameObject("Volume_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        volLabelGo.transform.SetParent(panelGo.transform, false);
        var volLabelRect = volLabelGo.GetComponent<RectTransform>();
        volLabelRect.anchoredPosition = new Vector2(-120f, 40f);
        volLabelRect.sizeDelta = new Vector2(200f, 30f);
        var volLabelTmp = volLabelGo.GetComponent<TextMeshProUGUI>();
        volLabelTmp.text = "MASTER VOLUME";
        volLabelTmp.fontSize = 20;
        volLabelTmp.fontStyle = FontStyles.Bold;
        volLabelTmp.color = Color.white;
        volLabelTmp.raycastTarget = false;
        if (font != null) volLabelTmp.font = font;

        GameObject sliderGo = new GameObject("Volume_Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(panelGo.transform, false);
        var sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(100f, 40f);
        sliderRect.sizeDelta = new Vector2(200f, 22f);
        volSlider = sliderGo.GetComponent<Slider>();
        volSlider.minValue = 0.0001f;
        volSlider.maxValue = 1f;
        volSlider.value = 0.8f;

        // Background Slider
        GameObject sliderBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        sliderBg.transform.SetParent(sliderGo.transform, false);
        var sBgRect = sliderBg.GetComponent<RectTransform>();
        sBgRect.anchorMin = Vector2.zero;
        sBgRect.anchorMax = Vector2.one;
        sBgRect.sizeDelta = Vector2.zero;
        sliderBg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        var faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero;
        faRect.anchorMax = Vector2.one;
        faRect.sizeDelta = Vector2.zero;

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fRect = fill.GetComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero;
        fRect.anchorMax = Vector2.one;
        fRect.sizeDelta = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = new Color(0.9f, 0.7f, 0.3f, 1f);
        volSlider.fillRect = fRect;

        // Fullscreen Toggle
        GameObject fsGo = new GameObject("Fullscreen_Toggle", typeof(RectTransform), typeof(Toggle));
        fsGo.transform.SetParent(panelGo.transform, false);
        var fsRect = fsGo.GetComponent<RectTransform>();
        fsRect.anchoredPosition = new Vector2(0f, -30f);
        fsRect.sizeDelta = new Vector2(400f, 32f);
        fsToggle = fsGo.GetComponent<Toggle>();

        GameObject fsLabelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        fsLabelGo.transform.SetParent(fsGo.transform, false);
        var fsLabelRect = fsLabelGo.GetComponent<RectTransform>();
        fsLabelRect.anchoredPosition = new Vector2(25f, 0f);
        fsLabelRect.sizeDelta = new Vector2(300f, 30f);
        var fsLabelTmp = fsLabelGo.GetComponent<TextMeshProUGUI>();
        fsLabelTmp.text = "FULLSCREEN MODE";
        fsLabelTmp.fontSize = 20;
        fsLabelTmp.fontStyle = FontStyles.Bold;
        fsLabelTmp.color = Color.white;
        fsLabelTmp.raycastTarget = false;
        if (font != null) fsLabelTmp.font = font;

        // Back Button
        backBtn = CreateMenuButton("Back_Button", "BACK", panelGo.transform, font);
        var backBtnRect = backBtn.GetComponent<RectTransform>();
        backBtnRect.anchorMin = new Vector2(0.5f, 0f);
        backBtnRect.anchorMax = new Vector2(0.5f, 0f);
        backBtnRect.pivot = new Vector2(0.5f, 0f);
        backBtnRect.anchoredPosition = new Vector2(0f, 25f);
        backBtnRect.sizeDelta = new Vector2(200f, 48f);
        backBtn.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        return panelGo;
    }

    private static GameObject CreateCreditsPanel(string name, Transform parent, string title, TMP_FontAsset titleFont, TMP_FontAsset font, out Button backBtn)
    {
        GameObject panelGo = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        panelGo.transform.SetParent(parent, false);

        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(620f, 480f);

        var img = panelGo.GetComponent<Image>();
        img.color = new Color(0.08f, 0.07f, 0.06f, 0.95f);
        img.raycastTarget = true;

        var outline = panelGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.55f, 0.3f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Header
        GameObject headerGo = new GameObject("Header_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGo.transform.SetParent(panelGo.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 1f);
        headerRect.anchorMax = new Vector2(0.5f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -25f);
        headerRect.sizeDelta = new Vector2(500f, 50f);

        var headerTmp = headerGo.GetComponent<TextMeshProUGUI>();
        headerTmp.text = title;
        headerTmp.fontSize = 36;
        headerTmp.fontStyle = FontStyles.Bold;
        headerTmp.color = new Color(0.95f, 0.8f, 0.45f, 1f);
        headerTmp.alignment = TextAlignmentOptions.Center;
        headerTmp.raycastTarget = false;
        if (titleFont != null) headerTmp.font = titleFont;

        // Content
        GameObject contentGo = new GameObject("Credits_Content", typeof(RectTransform), typeof(TextMeshProUGUI));
        contentGo.transform.SetParent(panelGo.transform, false);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = new Vector2(0f, 15f);
        contentRect.sizeDelta = new Vector2(520f, 250f);

        var contentTmp = contentGo.GetComponent<TextMeshProUGUI>();
        contentTmp.text = "<b>CYRULIK</b>\n" +
                          "<size=16><color=#C0A060>A Psychological Horror Barber Experience</color></size>\n\n" +
                          "<b>Developed by:</b> SneakySnakesStudio\n" +
                          "<b>Inspired by:</b> <i>\"Chciałbym się ogolić\" (1966)</i>\n" +
                          "<b>Special Thanks:</b> To all testers & supporters!\n\n" +
                          "<size=14><color=#808080>All rights reserved. Poland 1980s retro aesthetic.</color></size>";
        contentTmp.fontSize = 19;
        contentTmp.color = new Color(0.9f, 0.88f, 0.82f, 1f);
        contentTmp.alignment = TextAlignmentOptions.Center;
        contentTmp.raycastTarget = false;
        if (font != null) contentTmp.font = font;

        // Back Button
        backBtn = CreateMenuButton("Back_Button", "BACK", panelGo.transform, font);
        var backBtnRect = backBtn.GetComponent<RectTransform>();
        backBtnRect.anchorMin = new Vector2(0.5f, 0f);
        backBtnRect.anchorMax = new Vector2(0.5f, 0f);
        backBtnRect.pivot = new Vector2(0.5f, 0f);
        backBtnRect.anchoredPosition = new Vector2(0f, 25f);
        backBtnRect.sizeDelta = new Vector2(200f, 48f);
        backBtn.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        return panelGo;
    }
}
#endif
