#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ControlsUIBuilder
{
    [MenuItem("Tools/Cyrulik/Create Controls UI (Klawiszologia)", false, 3)]
    [MenuItem("GameObject/UI/Cyrulik - Controls UI (Klawiszologia)", false, 12)]
    public static void CreateControlsUI()
    {
        // 1. Znajdź lub stwórz nadrzędny Canvas
        GameObject targetCanvasGo = GameObject.Find("CrossHair_Canvas");
        if (targetCanvasGo == null)
        {
            targetCanvasGo = GameObject.Find("Intro_Canvas");
        }
        if (targetCanvasGo == null)
        {
            targetCanvasGo = Object.FindAnyObjectByType<Canvas>()?.gameObject;
        }

        if (targetCanvasGo == null)
        {
            targetCanvasGo = new GameObject("Controls_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = targetCanvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 30;

            var scaler = targetCanvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(targetCanvasGo, "Create Controls Canvas");
        }

        // 2. Usuń poprzedni panel Controls_Panel jeśli istnieje
        Transform existing = targetCanvasGo.transform.Find("Controls_Panel");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // Pobierz czcionki
        TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rye-Regular SDF.asset")
                               ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset");
        TMP_FontAsset textFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset");

        // 3. Główny Panel Controls_Panel
        GameObject panelGo = new GameObject("Controls_Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGo.transform.SetParent(targetCanvasGo.transform, false);
        Undo.RegisterCreatedObjectUndo(panelGo, "Create Controls Panel");

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.04f, 0.04f, 0.95f);
        panelImage.raycastTarget = true;

        var canvasGroup = panelGo.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // Domyślnie schowany, IntroSequence go pokaże
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 4. Ramka wewnętrzna / Kontener środkowy
        GameObject boxGo = new GameObject("Controls_Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        boxGo.transform.SetParent(panelGo.transform, false);

        var boxRect = boxGo.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(900f, 620f);
        boxRect.anchoredPosition = Vector2.zero;

        var boxImage = boxGo.GetComponent<Image>();
        boxImage.color = new Color(0.1f, 0.09f, 0.08f, 0.9f);

        var layout = boxGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 35, 35);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 5. Nagłówek Tytułowy
        GameObject titleGo = new GameObject("Title_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(boxGo.transform, false);
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        if (titleFont != null) titleTmp.font = titleFont;
        titleTmp.text = "STEROWANIE";
        titleTmp.fontSize = 44f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.96f, 0.82f, 0.51f, 1f); // Złocisty / Amber
        titleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 60f);

        // Linia rozdzielająca
        CreateDivider(boxGo.transform);

        // 6. Wiersze klawiszy (Klawisze + Opis)
        CreateControlRow(boxGo.transform, "W  S  A  D", "Chodzenie / Poruszanie się", textFont);
        CreateControlRow(boxGo.transform, "E   /   LPM", "Interakcja (Użyj / Podnieś / Otwórz)", textFont);
        CreateControlRow(boxGo.transform, "G", "Upuszczenie trzymanego przedmiotu", textFont);
        CreateControlRow(boxGo.transform, "SHIFT", "Bieg (Sprint)", textFont);
        CreateControlRow(boxGo.transform, "ESC", "Menu pauzy", textFont);

        // Linia rozdzielająca
        CreateDivider(boxGo.transform);

        // 7. Podpowiedź na dole
        GameObject hintGo = new GameObject("Hint_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(boxGo.transform, false);
        var hintTmp = hintGo.GetComponent<TextMeshProUGUI>();
        if (textFont != null) hintTmp.font = textFont;
        hintTmp.text = "Naciśnij [ SPACJĘ ] lub poczekaj, aby kontynuować...";
        hintTmp.fontSize = 22f;
        hintTmp.fontStyle = FontStyles.Italic;
        hintTmp.alignment = TextAlignmentOptions.Center;
        hintTmp.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        hintGo.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 40f);

        // 8. Przypnij utworzony panel do IntroSequence w scenie
        IntroSequence introSeq = Object.FindAnyObjectByType<IntroSequence>();
        if (introSeq != null)
        {
            SerializedObject so = new SerializedObject(introSeq);
            SerializedProperty prop = so.FindProperty("controlsCanvasGroup");
            if (prop != null)
            {
                prop.objectReferenceValue = canvasGroup;
                so.ApplyModifiedProperties();
                Debug.Log("[ControlsUIBuilder] Przypisano controlsCanvasGroup do komponentu IntroSequence!");
            }
            EditorUtility.SetDirty(introSeq);
        }

        Selection.activeGameObject = panelGo;
        EditorUtility.SetDirty(targetCanvasGo);
        Debug.Log("[ControlsUIBuilder] Pomyślnie utworzono UI Klawiszologii (Controls_Panel)!");
    }

    private static void CreateControlRow(Transform parent, string keyText, string actionText, TMP_FontAsset font)
    {
        GameObject rowGo = new GameObject($"Row_{keyText}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(parent, false);

        var rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(800f, 50f);

        var hLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 25f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;

        // Badge z klawiszem (np. [ WSAD ])
        GameObject keyBadgeGo = new GameObject("Key_Badge", typeof(RectTransform), typeof(Image));
        keyBadgeGo.transform.SetParent(rowGo.transform, false);
        var badgeRect = keyBadgeGo.GetComponent<RectTransform>();
        badgeRect.sizeDelta = new Vector2(230f, 45f);

        var badgeImg = keyBadgeGo.GetComponent<Image>();
        badgeImg.color = new Color(0.22f, 0.2f, 0.18f, 0.95f);

        GameObject keyLabelGo = new GameObject("Key_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        keyLabelGo.transform.SetParent(keyBadgeGo.transform, false);
        var keyLabelRect = keyLabelGo.GetComponent<RectTransform>();
        keyLabelRect.anchorMin = Vector2.zero;
        keyLabelRect.anchorMax = Vector2.one;
        keyLabelRect.sizeDelta = Vector2.zero;

        var keyTmp = keyLabelGo.GetComponent<TextMeshProUGUI>();
        if (font != null) keyTmp.font = font;
        keyTmp.text = $"[ {keyText} ]";
        keyTmp.fontSize = 24f;
        keyTmp.fontStyle = FontStyles.Bold;
        keyTmp.alignment = TextAlignmentOptions.Center;
        keyTmp.color = new Color(1f, 0.9f, 0.65f, 1f); // Kremowo-złoty

        // Strzałka / Separator
        GameObject arrowGo = new GameObject("Arrow_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrowGo.transform.SetParent(rowGo.transform, false);
        arrowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(30f, 45f);
        var arrowTmp = arrowGo.GetComponent<TextMeshProUGUI>();
        if (font != null) arrowTmp.font = font;
        arrowTmp.text = ">";
        arrowTmp.fontSize = 26f;
        arrowTmp.alignment = TextAlignmentOptions.Center;
        arrowTmp.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        // Opis akcji (np. Chodzenie)
        GameObject actionLabelGo = new GameObject("Action_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        actionLabelGo.transform.SetParent(rowGo.transform, false);
        actionLabelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(480f, 45f);

        var actionTmp = actionLabelGo.GetComponent<TextMeshProUGUI>();
        if (font != null) actionTmp.font = font;
        actionTmp.text = actionText;
        actionTmp.fontSize = 24f;
        actionTmp.alignment = TextAlignmentOptions.Left;
        actionTmp.color = new Color(0.92f, 0.92f, 0.92f, 1f);
    }

    private static void CreateDivider(Transform parent)
    {
        GameObject divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divGo.transform.SetParent(parent, false);
        var rect = divGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(800f, 2f);

        var img = divGo.GetComponent<Image>();
        img.color = new Color(0.4f, 0.35f, 0.28f, 0.5f);
    }
}
#endif
