#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kreator i edytor interfejsu minigry podcięcia gardła (Throat Cut Minigame).
/// Automatycznie tworzy hierarchię Canvas, linie fali i krwi, wskaźnik iskry lontu oraz kursor brzytwy.
/// Dostępny w menu: Tools -> Cyrulik -> Minigames -> Create Throat Cut Minigame UI
/// </summary>
public static class ThroatCutMinigameBuilder
{
    [MenuItem("Tools/Cyrulik/Minigames/Create Throat Cut Minigame UI", false, 15)]
    [MenuItem("GameObject/UI/Cyrulik - Throat Cut Minigame UI", false, 18)]
    public static void CreateThroatCutMinigameUI()
    {
        // 1. Sprawdź czy już istnieje w scenie
        ThroatCutMinigame existing = Object.FindAnyObjectByType<ThroatCutMinigame>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("<color=#70FF70>[ThroatCutMinigameBuilder] ThroatCutMinigame już istnieje w scenie!</color>");
            return;
        }

        // 2. Utwórz dedykowany Canvas o wysokim sortingOrder (950)
        GameObject canvasGo = new GameObject("ThroatCut_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Throat Cut Canvas");

        // 3. Główny obiekt ThroatCutMinigame
        GameObject minigameGo = new GameObject("ThroatCutMinigame", typeof(RectTransform), typeof(CanvasGroup), typeof(ThroatCutMinigame));
        minigameGo.transform.SetParent(canvasGo.transform, false);

        RectTransform mainRect = minigameGo.GetComponent<RectTransform>();
        mainRect.anchorMin = Vector2.zero;
        mainRect.anchorMax = Vector2.one;
        mainRect.sizeDelta = Vector2.zero;

        CanvasGroup canvasGroup = minigameGo.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        ThroatCutMinigame minigame = minigameGo.GetComponent<ThroatCutMinigame>();
        SerializedObject so = new SerializedObject(minigame);

        so.FindProperty("minigameCanvasGroup").objectReferenceValue = canvasGroup;

        // 4. Rozbryzg krwi na pełnym ekranie (Screen Blood Splatter)
        GameObject bloodOverlayGo = new GameObject("ScreenBloodSplatter", typeof(RectTransform), typeof(Image));
        bloodOverlayGo.transform.SetParent(minigameGo.transform, false);
        RectTransform bloodOverlayRect = bloodOverlayGo.GetComponent<RectTransform>();
        bloodOverlayRect.anchorMin = Vector2.zero;
        bloodOverlayRect.anchorMax = Vector2.one;
        bloodOverlayRect.sizeDelta = Vector2.zero;

        Image bloodOverlayImg = bloodOverlayGo.GetComponent<Image>();
        bloodOverlayImg.color = new Color(0.68f, 0.04f, 0.04f, 0f);
        bloodOverlayImg.raycastTarget = false;
        bloodOverlayGo.SetActive(false);
        so.FindProperty("screenBloodSplatter").objectReferenceValue = bloodOverlayImg;

        // 5. Kontener roboczy nacięcia gardła (na wysokości szyi na ekranie)
        GameObject containerGo = new GameObject("ThroatCut_Container", typeof(RectTransform));
        containerGo.transform.SetParent(minigameGo.transform, false);
        RectTransform containerRect = containerGo.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, -60f); // nieco poniżej środka, idealnie na szyi Jurka
        containerRect.sizeDelta = new Vector2(900f, 320f);
        so.FindProperty("containerRect").objectReferenceValue = containerRect;

        // 6. Linia Sinusoidy Pomocniczej (Wave Guide Line - płonący lont)
        GameObject waveGo = new GameObject("WaveGuideLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(ThroatCutLineRenderer));
        waveGo.transform.SetParent(containerGo.transform, false);
        RectTransform waveRect = waveGo.GetComponent<RectTransform>();
        waveRect.anchorMin = Vector2.zero;
        waveRect.anchorMax = Vector2.one;
        waveRect.sizeDelta = Vector2.zero;

        ThroatCutLineRenderer waveRenderer = waveGo.GetComponent<ThroatCutLineRenderer>();
        waveRenderer.LineWidth = 7.0f;
        waveRenderer.StartColor = new Color(1.0f, 0.45f, 0.12f, 0.75f);
        waveRenderer.EndColor = new Color(1.0f, 0.18f, 0.08f, 0.85f);
        waveRenderer.raycastTarget = false;
        so.FindProperty("waveGuideRenderer").objectReferenceValue = waveRenderer;

        // 7. Dynamiczna Linia Krwi (Blood Cut Line - tętnicze nacięcie)
        GameObject bloodLineGo = new GameObject("BloodCutLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(ThroatCutLineRenderer));
        bloodLineGo.transform.SetParent(containerGo.transform, false);
        RectTransform bloodLineRect = bloodLineGo.GetComponent<RectTransform>();
        bloodLineRect.anchorMin = Vector2.zero;
        bloodLineRect.anchorMax = Vector2.one;
        bloodLineRect.sizeDelta = Vector2.zero;

        ThroatCutLineRenderer bloodRenderer = bloodLineGo.GetComponent<ThroatCutLineRenderer>();
        bloodRenderer.LineWidth = 10.0f;
        bloodRenderer.StartColor = new Color(0.88f, 0.04f, 0.04f, 0.95f);
        bloodRenderer.EndColor = new Color(0.55f, 0.01f, 0.01f, 1.0f);
        bloodRenderer.raycastTarget = false;
        so.FindProperty("bloodCutRenderer").objectReferenceValue = bloodRenderer;

        // 8. Iskra Płonącego Lontu (Spark Indicator)
        GameObject sparkGo = new GameObject("SparkIndicator", typeof(RectTransform), typeof(Image));
        sparkGo.transform.SetParent(containerGo.transform, false);
        RectTransform sparkRect = sparkGo.GetComponent<RectTransform>();
        sparkRect.sizeDelta = new Vector2(30f, 30f);
        sparkRect.pivot = new Vector2(0.5f, 0.5f);

        Image sparkImg = sparkGo.GetComponent<Image>();
        sparkImg.color = new Color(1.0f, 0.95f, 0.35f, 1.0f);
        sparkImg.raycastTarget = false;
        so.FindProperty("sparkIndicator").objectReferenceValue = sparkRect;
        so.FindProperty("sparkGlowImage").objectReferenceValue = sparkImg;

        // 9. Kursor Brzytwy (Razor Cursor)
        GameObject razorGo = new GameObject("RazorCursor", typeof(RectTransform), typeof(Image));
        razorGo.transform.SetParent(containerGo.transform, false);
        RectTransform razorRect = razorGo.GetComponent<RectTransform>();
        razorRect.sizeDelta = new Vector2(52f, 18f);
        razorRect.pivot = new Vector2(0.2f, 0.5f);

        Image razorImg = razorGo.GetComponent<Image>();
        razorImg.color = new Color(0.92f, 0.94f, 0.98f, 0.95f);
        razorImg.raycastTarget = false;
        so.FindProperty("razorCursor").objectReferenceValue = razorRect;
        so.FindProperty("razorCursorImage").objectReferenceValue = razorImg;

        // 10. Miernik Precyzji HUD (Góra ekranu)
        GameObject hudPanelGo = new GameObject("HUD_Panel", typeof(RectTransform));
        hudPanelGo.transform.SetParent(minigameGo.transform, false);
        RectTransform hudRect = hudPanelGo.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0.5f, 1.0f);
        hudRect.anchorMax = new Vector2(0.5f, 1.0f);
        hudRect.pivot = new Vector2(0.5f, 1.0f);
        hudRect.anchoredPosition = new Vector2(0f, -40f);
        hudRect.sizeDelta = new Vector2(600f, 90f);

        // Tekst precyzji
        GameObject precTextGo = new GameObject("PrecisionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        precTextGo.transform.SetParent(hudRect, false);
        RectTransform precTextRect = precTextGo.GetComponent<RectTransform>();
        precTextRect.anchorMin = new Vector2(0f, 0.5f);
        precTextRect.anchorMax = new Vector2(1f, 1f);
        precTextRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI precText = precTextGo.GetComponent<TextMeshProUGUI>();
        precText.text = "PRECISION: <color=#00FF80>100%</color>";
        precText.fontSize = 32;
        precText.fontStyle = FontStyles.Bold;
        precText.alignment = TextAlignmentOptions.Center;
        so.FindProperty("precisionText").objectReferenceValue = precText;

        // Status Feedback Text (np. SURGICAL TEMPO)
        GameObject statusTextGo = new GameObject("StatusFeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusTextGo.transform.SetParent(hudRect, false);
        RectTransform statusTextRect = statusTextGo.GetComponent<RectTransform>();
        statusTextRect.anchorMin = new Vector2(0f, 0f);
        statusTextRect.anchorMax = new Vector2(1f, 0.5f);
        statusTextRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI statusText = statusTextGo.GetComponent<TextMeshProUGUI>();
        statusText.text = "<color=#20FF80>SURGICAL TEMPO</color>";
        statusText.fontSize = 20;
        statusText.alignment = TextAlignmentOptions.Center;
        so.FindProperty("statusFeedbackText").objectReferenceValue = statusText;

        // 11. Pasek postępu precyzji (Tło + Wypełnienie)
        GameObject barBgGo = new GameObject("PrecisionBar_Background", typeof(RectTransform), typeof(Image));
        barBgGo.transform.SetParent(hudRect, false);
        RectTransform barBgRect = barBgGo.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.1f, 0f);
        barBgRect.anchorMax = new Vector2(0.9f, 0f);
        barBgRect.anchoredPosition = new Vector2(0f, -12f);
        barBgRect.sizeDelta = new Vector2(0f, 8f);

        Image barBgImg = barBgGo.GetComponent<Image>();
        barBgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        GameObject barFillGo = new GameObject("PrecisionBar_Fill", typeof(RectTransform), typeof(Image));
        barFillGo.transform.SetParent(barBgGo.transform, false);
        RectTransform barFillRect = barFillGo.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.sizeDelta = Vector2.zero;

        Image barFillImg = barFillGo.GetComponent<Image>();
        barFillImg.type = Image.Type.Filled;
        barFillImg.fillMethod = Image.FillMethod.Horizontal;
        barFillImg.fillAmount = 1f;
        barFillImg.color = new Color(0.1f, 0.95f, 0.45f, 1f);
        so.FindProperty("precisionBarFill").objectReferenceValue = barFillImg;

        // 12. Pasek Instrukcji (Dół ekranu)
        GameObject insTextGo = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        insTextGo.transform.SetParent(minigameGo.transform, false);
        RectTransform insTextRect = insTextGo.GetComponent<RectTransform>();
        insTextRect.anchorMin = new Vector2(0.5f, 0f);
        insTextRect.anchorMax = new Vector2(0.5f, 0f);
        insTextRect.pivot = new Vector2(0.5f, 0f);
        insTextRect.anchoredPosition = new Vector2(0f, 50f);
        insTextRect.sizeDelta = new Vector2(900f, 45f);

        TextMeshProUGUI insText = insTextGo.GetComponent<TextMeshProUGUI>();
        insText.text = "HOLD [LMB] & TRACE THE INCISION LINE (FOLLOW THE BURNING FUSE)";
        insText.fontSize = 22;
        insText.fontStyle = FontStyles.Bold;
        insText.alignment = TextAlignmentOptions.Center;
        insText.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
        so.FindProperty("instructionText").objectReferenceValue = insText;

        // Przypisz dźwięki z projektu jeśli istnieją
        AudioClip sliceClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/razor_minigame_sounds/ostrzenie szybkie.wav");
        if (sliceClip != null) so.FindProperty("razorSliceLoopClip").objectReferenceValue = sliceClip;

        AudioClip spurtClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/SFX/Effekt_sfx_8.ogg");
        if (spurtClip != null) so.FindProperty("arterialBloodSpurtClip").objectReferenceValue = spurtClip;

        AudioClip botchedClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/Negative/error_dound.ogg");
        if (botchedClip != null) so.FindProperty("botchedCutClip").objectReferenceValue = botchedClip;

        AudioClip screamClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/SFX/jesus_chis.ogg");
        if (screamClip != null) so.FindProperty("jurekScreamClip").objectReferenceValue = screamClip;

        so.ApplyModifiedProperties();

        Selection.activeGameObject = minigameGo;
        Undo.RegisterCreatedObjectUndo(minigameGo, "Create Throat Cut Minigame");

        Debug.Log("<color=#70FF70>[ThroatCutMinigameBuilder] Pomyślnie utworzono Throat Cut Minigame UI!</color>");
    }
}
#endif
