#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EndSummaryUIBuilder
{
    [MenuItem("Tools/Cyrulik/Create End Summary UI (GTA Style)", false, 5)]
    [MenuItem("GameObject/UI/Cyrulik - End Summary UI (GTA Style)", false, 13)]
    public static void CreateEndSummaryUI()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD Canvas");
        }
        else
        {
            canvas.sortingOrder = 999;
        }

        // Główny obiekt EndSummaryUI
        GameObject managerGo = new GameObject("EndSummaryUI", typeof(EndSummaryUI));
        managerGo.transform.SetParent(canvas.transform, false);
        EndSummaryUI summaryUI = managerGo.GetComponent<EndSummaryUI>();

        // 1. W 100% CZARNY EKRAN W TLE (Solid Black)
        GameObject rootPanel = new GameObject("EndSummary_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootPanel.transform.SetParent(managerGo.transform, false);
        var rootRect = rootPanel.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        var mainCg = rootPanel.GetComponent<CanvasGroup>();
        mainCg.alpha = 0f;
        mainCg.blocksRaycasts = false;
        mainCg.interactable = false;

        var rootBg = rootPanel.GetComponent<Image>();
        rootBg.color = Color.black; // W 100% czarne tło

        // 2. Kontener na środku po lewej stronie
        GameObject leftContainerGo = new GameObject("GTA_Content_Container", typeof(RectTransform));
        leftContainerGo.transform.SetParent(rootPanel.transform, false);
        var contentContainer = leftContainerGo.GetComponent<RectTransform>();
        contentContainer.anchorMin = new Vector2(0.1f, 0.5f);
        contentContainer.anchorMax = new Vector2(0.1f, 0.5f);
        contentContainer.pivot = new Vector2(0f, 0.5f);
        contentContainer.sizeDelta = new Vector2(900f, 260f);
        contentContainer.anchoredPosition = Vector2.zero;

        // 3. WIELKI CZERWONY NAPIS: YOU FAILED
        GameObject titleGo = new GameObject("Title_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(leftContainerGo.transform, false);
        var tRect = titleGo.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0f, 1f);
        tRect.anchorMax = new Vector2(0f, 1f);
        tRect.pivot = new Vector2(0f, 1f);
        tRect.anchoredPosition = new Vector2(0f, 0f);
        tRect.sizeDelta = new Vector2(900f, 90f);

        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "YOU FAILED";
        titleTmp.fontSize = 76f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.characterSpacing = 4f;
        titleTmp.enableWordWrapping = false;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.color = new Color(0.92f, 0.12f, 0.12f, 1f);

        // 4. Podtytuł z powodem (English)
        GameObject reasonGo = new GameObject("Reason_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        reasonGo.transform.SetParent(leftContainerGo.transform, false);
        var rRect = reasonGo.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0f, 1f);
        rRect.anchorMax = new Vector2(0f, 1f);
        rRect.pivot = new Vector2(0f, 1f);
        rRect.anchoredPosition = new Vector2(0f, -95f);
        rRect.sizeDelta = new Vector2(850f, 50f);

        var reasonTmp = reasonGo.GetComponent<TextMeshProUGUI>();
        reasonTmp.text = "The client felt the atmosphere was too gloomy and left.";
        reasonTmp.fontSize = 24f;
        reasonTmp.enableWordWrapping = true;
        reasonTmp.alignment = TextAlignmentOptions.Left;
        reasonTmp.color = new Color(0.88f, 0.88f, 0.88f, 1f);

        // 5. Pasek z przyciskami [ RESTART ] obok [ QUIT ]
        GameObject btnBarGo = new GameObject("Buttons_Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnBarGo.transform.SetParent(leftContainerGo.transform, false);
        var bBarRect = btnBarGo.GetComponent<RectTransform>();
        bBarRect.anchorMin = new Vector2(0f, 1f);
        bBarRect.anchorMax = new Vector2(0f, 1f);
        bBarRect.pivot = new Vector2(0f, 1f);
        bBarRect.anchoredPosition = new Vector2(0f, -170f);
        bBarRect.sizeDelta = new Vector2(520f, 54f);

        var hlg = btnBarGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        Button restartBtn = CreateButton(btnBarGo.transform, "Restart_Button", "RESTART  [SPACE]", new Color(0.85f, 0.65f, 0.22f, 1f));
        Button quitBtn = CreateButton(btnBarGo.transform, "Quit_Button", "QUIT  [ESC]", new Color(0.35f, 0.35f, 0.38f, 1f));

        // Serializacja pól w EndSummaryUI
        SerializedObject so = new SerializedObject(summaryUI);
        so.FindProperty("mainCanvasGroup").objectReferenceValue = mainCg;
        so.FindProperty("contentContainer").objectReferenceValue = contentContainer;
        so.FindProperty("mainTitleText").objectReferenceValue = titleTmp;
        so.FindProperty("reasonDescriptionText").objectReferenceValue = reasonTmp;
        so.FindProperty("restartButton").objectReferenceValue = restartBtn;
        so.FindProperty("quitButton").objectReferenceValue = quitBtn;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(managerGo, "Create End Summary UI (GTA Style)");
        Selection.activeGameObject = managerGo;

        Debug.Log("[EndSummaryUIBuilder] Utworzono EndSummaryUI w stylu GTA 'YOU FAILED' (Solid Black & Minimal)!");
    }

    private static Button CreateButton(Transform parent, string name, string label, Color outlineColor)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(MenuButtonEffects));
        btnGo.transform.SetParent(parent, false);
        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(230f, 52f);

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

        var outline = btnGo.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(btnGo.transform, false);
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 17f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        return btnGo.GetComponent<Button>();
    }
}
#endif
