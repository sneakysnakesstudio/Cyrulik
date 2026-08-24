#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MusicCreditUIBuilder
{
    [MenuItem("Tools/Cyrulik/Create Music Credit UI", false, 5)]
    [MenuItem("GameObject/UI/Cyrulik - Music Credit UI", false, 13)]
    public static void CreateMusicCreditUI()
    {
        Canvas canvas = null;
        Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        foreach (var c in allCanvases)
        {
            if (c.name.Contains("CrossHair") || c.name.Contains("HUD"))
            {
                canvas = c;
                break;
            }
        }
        if (canvas == null && allCanvases.Length > 0)
        {
            canvas = allCanvases[0];
        }

        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD Canvas");
        }

        // Sprawdź czy już istnieje
        MusicCreditUI existing = canvas.GetComponentInChildren<MusicCreditUI>(true);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BarlowCondensed-SemiBold SDF.asset");

        // 1. Główny obiekt MusicCreditUI
        GameObject managerGo = new GameObject("MusicCreditUI", typeof(MusicCreditUI));
        managerGo.transform.SetParent(canvas.transform, false);
        MusicCreditUI creditUI = managerGo.GetComponent<MusicCreditUI>();

        // 2. Kontener Banera (Anchor lewy górny róg)
        GameObject bannerGo = new GameObject("MusicCredit_Banner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        bannerGo.transform.SetParent(managerGo.transform, false);

        var bannerRect = bannerGo.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0f, 1f);
        bannerRect.anchorMax = new Vector2(0f, 1f);
        bannerRect.pivot = new Vector2(0f, 1f);
        bannerRect.sizeDelta = new Vector2(400f, 68f);
        bannerRect.anchoredPosition = new Vector2(-420f, -35f);

        var bannerCg = bannerGo.GetComponent<CanvasGroup>();
        bannerCg.alpha = 0f;
        bannerCg.blocksRaycasts = false;
        bannerCg.interactable = false;

        var bannerBg = bannerGo.GetComponent<Image>();
        bannerBg.color = new Color(0.07f, 0.07f, 0.08f, 0.94f);
        bannerBg.raycastTarget = false;

        var outline = bannerGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.68f, 0.28f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 3. Pasek akcentujący z lewej strony
        GameObject barGo = new GameObject("Accent_Bar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(bannerGo.transform, false);
        var barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.sizeDelta = new Vector2(4f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        var barImg = barGo.GetComponent<Image>();
        barImg.color = new Color(0.95f, 0.78f, 0.32f, 1f);
        barImg.raycastTarget = false;

        // 4. Ikona / Nutka ♫
        GameObject iconGo = new GameObject("Icon_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        iconGo.transform.SetParent(bannerGo.transform, false);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(16f, 0f);
        iconRect.sizeDelta = new Vector2(30f, 40f);

        var iconTmp = iconGo.GetComponent<TextMeshProUGUI>();
        iconTmp.text = "♫";
        iconTmp.fontSize = 26f;
        iconTmp.fontStyle = FontStyles.Bold;
        iconTmp.color = new Color(0.96f, 0.80f, 0.35f, 1f);
        iconTmp.alignment = TextAlignmentOptions.Center;
        iconTmp.raycastTarget = false;
        if (defaultFont != null) iconTmp.font = defaultFont;

        // 5. Nagłówek kategorii ("RADIO • NOW PLAYING")
        GameObject subheaderGo = new GameObject("Subheader_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        subheaderGo.transform.SetParent(bannerGo.transform, false);
        var subheaderRect = subheaderGo.GetComponent<RectTransform>();
        subheaderRect.anchorMin = new Vector2(0f, 0.5f);
        subheaderRect.anchorMax = new Vector2(1f, 1f);
        subheaderRect.offsetMin = new Vector2(52f, 0f);
        subheaderRect.offsetMax = new Vector2(-12f, -6f);

        var subheaderTmp = subheaderGo.GetComponent<TextMeshProUGUI>();
        subheaderTmp.text = "RADIO • NOW PLAYING";
        subheaderTmp.fontSize = 13f;
        subheaderTmp.fontStyle = FontStyles.Bold;
        subheaderTmp.characterSpacing = 3f;
        subheaderTmp.color = new Color(0.85f, 0.68f, 0.32f, 0.9f);
        subheaderTmp.alignment = TextAlignmentOptions.Left;
        subheaderTmp.raycastTarget = false;
        if (defaultFont != null) subheaderTmp.font = defaultFont;

        // 6. Główny tekst ("Music by 'Tymon Urbańczyk'")
        GameObject authorGo = new GameObject("Author_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        authorGo.transform.SetParent(bannerGo.transform, false);
        var authorRect = authorGo.GetComponent<RectTransform>();
        authorRect.anchorMin = new Vector2(0f, 0f);
        authorRect.anchorMax = new Vector2(1f, 0.5f);
        authorRect.offsetMin = new Vector2(52f, 6f);
        authorRect.offsetMax = new Vector2(-12f, 0f);

        var authorTmp = authorGo.GetComponent<TextMeshProUGUI>();
        authorTmp.text = "Music by 'Tymon Urbańczyk'";
        authorTmp.fontSize = 20f;
        authorTmp.fontStyle = FontStyles.Bold;
        authorTmp.color = new Color(0.96f, 0.94f, 0.90f, 1f);
        authorTmp.alignment = TextAlignmentOptions.Left;
        authorTmp.raycastTarget = false;
        if (defaultFont != null) authorTmp.font = defaultFont;

        // Serializacja pól w MusicCreditUI
        SerializedObject so = new SerializedObject(creditUI);
        so.FindProperty("bannerCanvasGroup").objectReferenceValue = bannerCg;
        so.FindProperty("bannerRectTransform").objectReferenceValue = bannerRect;
        so.FindProperty("subheaderText").objectReferenceValue = subheaderTmp;
        so.FindProperty("authorText").objectReferenceValue = authorTmp;
        so.FindProperty("iconText").objectReferenceValue = iconTmp;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(managerGo, "Create Music Credit UI");
        Selection.activeGameObject = managerGo;

        Debug.Log("[MusicCreditUIBuilder] Utworzono MusicCreditUI w lewym górnym rogu ekranu!");
    }
}
#endif
