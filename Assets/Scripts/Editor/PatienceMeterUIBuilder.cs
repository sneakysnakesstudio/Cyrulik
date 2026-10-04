#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PatienceMeterUIBuilder
{
    [MenuItem("Tools/Cyrulik/Create Patience Meter UI", false, 5)]
    [MenuItem("GameObject/UI/Cyrulik - Patience Meter UI", false, 13)]
    public static void CreatePatienceMeterUI()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
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

        // Główny obiekt PatienceMeterUI
        GameObject managerGo = new GameObject("PatienceMeterUI", typeof(PatienceMeterUI));
        managerGo.transform.SetParent(canvas.transform, false);
        PatienceMeterUI meterUI = managerGo.GetComponent<PatienceMeterUI>();

        // 1. Główny kontener
        GameObject containerGo = new GameObject("PatienceMeter_Container", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        containerGo.transform.SetParent(managerGo.transform, false);

        var containerRect = containerGo.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 1f);
        containerRect.anchorMax = new Vector2(0.5f, 1f);
        containerRect.pivot = new Vector2(0.5f, 1f);
        containerRect.sizeDelta = new Vector2(360f, 62f);
        containerRect.anchoredPosition = new Vector2(0f, -40f);

        var meterCg = containerGo.GetComponent<CanvasGroup>();
        meterCg.alpha = 1f;
        meterCg.blocksRaycasts = false;

        var bg = containerGo.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.06f, 0.94f);

        var outline = containerGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.62f, 0.18f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Avatar (mordka) po lewej stronie paska
        GameObject avatarGo = new GameObject("Avatar_Image", typeof(RectTransform), typeof(Image));
        avatarGo.transform.SetParent(containerGo.transform, false);
        var avatarRect = avatarGo.GetComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0f, 0.5f);
        avatarRect.anchorMax = new Vector2(0f, 0.5f);
        avatarRect.pivot = new Vector2(1f, 0.5f);
        avatarRect.sizeDelta = new Vector2(56f, 56f);
        avatarRect.anchoredPosition = new Vector2(-12f, 0f);

        var avatarImg = avatarGo.GetComponent<Image>();
        avatarImg.color = new Color(0.15f, 0.14f, 0.13f, 1f); // Ciemny placeholder

        // 2. Nagłówek (Header Text)
        GameObject headerGo = new GameObject("Header_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGo.transform.SetParent(containerGo.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 0.5f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.offsetMin = new Vector2(16f, 0f);
        headerRect.offsetMax = new Vector2(-60f, -6f);

        var headerTmp = headerGo.GetComponent<TextMeshProUGUI>();
        headerTmp.text = "IMPATIENCE • JUREK";
        headerTmp.fontSize = 14f;
        headerTmp.fontStyle = FontStyles.Bold;
        headerTmp.color = new Color(0.95f, 0.76f, 0.28f, 1f);
        headerTmp.alignment = TextAlignmentOptions.Left;

        // 3. Procenty (Percentage Text)
        GameObject percentGo = new GameObject("Percentage_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        percentGo.transform.SetParent(containerGo.transform, false);
        var percentRect = percentGo.GetComponent<RectTransform>();
        percentRect.anchorMin = new Vector2(1f, 0.5f);
        percentRect.anchorMax = new Vector2(1f, 1f);
        percentRect.pivot = new Vector2(1f, 0.5f);
        percentRect.sizeDelta = new Vector2(60f, 26f);
        percentRect.anchoredPosition = new Vector2(-16f, -14f);

        var percentTmp = percentGo.GetComponent<TextMeshProUGUI>();
        percentTmp.text = "0%";
        percentTmp.fontSize = 14f;
        percentTmp.fontStyle = FontStyles.Bold;
        percentTmp.color = Color.white;
        percentTmp.alignment = TextAlignmentOptions.Right;

        // 4. Pasek postępu - Tło (Progress Bar Background)
        GameObject barBgGo = new GameObject("ProgressBar_Background", typeof(RectTransform), typeof(Image));
        barBgGo.transform.SetParent(containerGo.transform, false);
        var barBgRect = barBgGo.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0f, 0f);
        barBgRect.anchorMax = new Vector2(1f, 0f);
        barBgRect.pivot = new Vector2(0.5f, 0f);
        barBgRect.sizeDelta = new Vector2(-32f, 14f);
        barBgRect.anchoredPosition = new Vector2(0f, 10f);

        var barBgImg = barBgGo.GetComponent<Image>();
        barBgImg.color = new Color(0.04f, 0.04f, 0.04f, 0.9f);

        // 5. Pasek postępu - Wypełnienie (Progress Bar Fill)
        GameObject fillGo = new GameObject("ProgressBar_Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(barBgGo.transform, false);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        Texture2D whiteTex = Texture2D.whiteTexture;
        Sprite whiteSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

        var fillImg = fillGo.GetComponent<Image>();
        fillImg.sprite = whiteSprite;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0.35f;
        fillImg.color = new Color(0.95f, 0.72f, 0.22f, 1f);

        // Przypisz w Inspectorze przez SerializedObject
        SerializedObject so = new SerializedObject(meterUI);
        so.FindProperty("meterCanvasGroup").objectReferenceValue = meterCg;
        so.FindProperty("containerRectTransform").objectReferenceValue = containerRect;
        so.FindProperty("headerText").objectReferenceValue = headerTmp;
        so.FindProperty("percentageText").objectReferenceValue = percentTmp;
        so.FindProperty("progressBarFill").objectReferenceValue = fillImg;
        so.FindProperty("progressBarBackground").objectReferenceValue = barBgImg;
        so.FindProperty("avatarImage").objectReferenceValue = avatarImg;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(managerGo, "Create Patience Meter UI");
        Selection.activeGameObject = managerGo;
        Debug.Log("<color=#F5B838>[PatienceMeterUI] Pomyślnie utworzono wskaźnik cierpliwości klienta w hierarchii UI!</color>");
    }
}
#endif
