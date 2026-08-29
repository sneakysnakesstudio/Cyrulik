using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Ekran kalibracji jasności wyświetlany przy pierwszym uruchomieniu gry (lub z menu pauzy).
/// Pozwala graczowi dostosować jasność ekranu przed rozpoczęciem rozgrywki,
/// podobnie jak w Silent Hill, Outlast i innych grach horrorowych.
/// Wartość zapisywana do PlayerPrefs i stosowana do ColorAdjustments.postExposure w URP Volume.
/// </summary>
public class BrightnessCalibrationUI : MonoBehaviour
{
    public static BrightnessCalibrationUI Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("CanvasGroup głównego panelu kalibracji. Jeśli puste, zostanie zbudowane automatycznie.")]
    [SerializeField] private CanvasGroup calibrationCanvasGroup;

    [Tooltip("Suwak jasności. Zakres: -1.5 do 1.0.")]
    [SerializeField] private Slider brightnessSlider;

    [Tooltip("Przycisk zatwierdzenia ustawień.")]
    [SerializeField] private Button confirmButton;

    [Header("Post-Processing")]
    [Tooltip("URP Volume ze sceną post-processingu. Jeśli puste, zostanie znaleziony automatycznie.")]
    [SerializeField] private Volume targetVolume;

    [Header("Settings")]
    [Tooltip("Czas animacji fade in/out panelu.")]
    [SerializeField] private float fadeDuration = 0.4f;

    [Tooltip("Pokaż ekran kalibracji przy pierwszym uruchomieniu (gdy brak zapisanej wartości).")]
    [SerializeField] private bool showOnFirstLaunch = true;

    [Tooltip("Klucz PlayerPrefs do przechowywania wartości jasności.")]
    [SerializeField] private string playerPrefKey = "Cyrulik_Brightness";

    [Header("Player Lock (Opcjonalnie)")]
    [Tooltip("PlayerMovement do zablokowania podczas kalibracji.")]
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("PlayerHands do zablokowania podczas kalibracji.")]
    [SerializeField] private PlayerHands playerHands;

    private float _currentBrightness = -0.2f;
    private bool _isVisible = false;
    private Tween _fadeTween;

    // Domyślna wartość jasności gdy brak zapisu
    private const float DefaultBrightness = -0.2f;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Wczytaj zapisaną wartość z PlayerPrefs
        _currentBrightness = PlayerPrefs.GetFloat(playerPrefKey, DefaultBrightness);

        // Auto-resolve targetVolume
        if (targetVolume == null)
            targetVolume = FindAnyObjectByType<Volume>();

        // Auto-resolve player references
        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        // Zbuduj UI jeśli nie przypisano
        if (calibrationCanvasGroup == null)
            BuildUI();

        // Ukryj na starcie
        if (calibrationCanvasGroup != null)
        {
            calibrationCanvasGroup.alpha = 0f;
            calibrationCanvasGroup.interactable = false;
            calibrationCanvasGroup.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        // Zastosuj zapisaną wartość jasności od razu
        ApplyBrightness(targetVolume, _currentBrightness);

        // Pokaż ekran kalibracji przy pierwszym uruchomieniu
        if (showOnFirstLaunch && !PlayerPrefs.HasKey(playerPrefKey))
        {
            Show();
        }
    }

    private void Update()
    {
        // ENTER / Spacja zatwierdza kalibrację
        if (_isVisible && Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                Confirm();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // PUBLICZNE API
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Wyświetla ekran kalibracji jasności i blokuje ruch gracza.
    /// </summary>
    public void Show()
    {
        if (_isVisible) return;
        _isVisible = true;

        // Zablokuj gracza
        LockPlayer(true);

        if (calibrationCanvasGroup != null)
        {
            calibrationCanvasGroup.gameObject.SetActive(true);
            calibrationCanvasGroup.interactable = true;
            calibrationCanvasGroup.blocksRaycasts = true;

            _fadeTween?.Kill();
            _fadeTween = calibrationCanvasGroup
                .DOFade(1f, fadeDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        // Ustaw suwak na aktualną wartość
        if (brightnessSlider != null)
            brightnessSlider.value = _currentBrightness;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Ukrywa ekran kalibracji i odblokowuje gracza.
    /// </summary>
    public void Hide()
    {
        if (!_isVisible) return;
        _isVisible = false;

        if (calibrationCanvasGroup != null)
        {
            calibrationCanvasGroup.interactable = false;
            calibrationCanvasGroup.blocksRaycasts = false;

            _fadeTween?.Kill();
            _fadeTween = calibrationCanvasGroup
                .DOFade(0f, fadeDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (calibrationCanvasGroup != null)
                        calibrationCanvasGroup.gameObject.SetActive(false);
                });
        }

        // Odblokuj gracza
        LockPlayer(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Zatwierdza wybraną jasność, zapisuje do PlayerPrefs i ukrywa panel.
    /// </summary>
    public void Confirm()
    {
        float value = brightnessSlider != null ? brightnessSlider.value : _currentBrightness;
        _currentBrightness = value;

        PlayerPrefs.SetFloat(playerPrefKey, value);
        PlayerPrefs.Save();

        ApplyBrightness(targetVolume, value);

        Hide();
    }

    /// <summary>
    /// Statyczna metoda do stosowania jasności z dowolnego miejsca (np. z PauseMenu).
    /// Modyfikuje ColorAdjustments.postExposure w podanym Volume.
    /// </summary>
    public static void ApplyBrightness(Volume vol, float value)
    {
        if (vol == null || vol.profile == null) return;

        if (!vol.profile.TryGet<ColorAdjustments>(out var ca))
        {
            ca = vol.profile.Add<ColorAdjustments>(true);
        }

        ca.active = true;
        ca.postExposure.overrideState = true;
        ca.postExposure.value = value;
    }

    // ─────────────────────────────────────────────────────────────
    // EVENTY SUWAKA
    // ─────────────────────────────────────────────────────────────

    private void OnSliderChanged(float value)
    {
        _currentBrightness = value;
        ApplyBrightness(targetVolume, value);
    }

    // ─────────────────────────────────────────────────────────────
    // BLOKOWANIE GRACZA
    // ─────────────────────────────────────────────────────────────

    private void LockPlayer(bool locked)
    {
        if (playerMovement != null)
            playerMovement.enabled = !locked;

        if (playerHands != null)
            playerHands.enabled = !locked;
    }

    // ─────────────────────────────────────────────────────────────
    // BUDOWANIE UI PROCEDURALNIE
    // ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas
        GameObject canvasGo = new GameObject("BrightnessCalibration_Canvas");
        DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel główny z CanvasGroup
        GameObject panelGo = new GameObject("CalibrationPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        Image panelImg = panelGo.GetComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.92f);
        calibrationCanvasGroup = panelGo.GetComponent<CanvasGroup>();

        // Kontener pionowy na środku ekranu
        GameObject containerGo = new GameObject("Container", typeof(RectTransform), typeof(VerticalLayoutGroup));
        containerGo.transform.SetParent(panelGo.transform, false);
        RectTransform containerRect = containerGo.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(880f, 640f);
        containerRect.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup vLayout = containerGo.GetComponent<VerticalLayoutGroup>();
        vLayout.spacing = 20f;
        vLayout.padding = new RectOffset(0, 0, 20, 20);
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        // Tytuł
        CreateTMP(containerGo.transform, "KALIBRACJA JASNOŚCI", 36f, FontStyles.Bold,
            new Color(0.96f, 0.78f, 0.26f, 1f), 60f);

        // Opis
        CreateTMP(containerGo.transform,
            "Ustaw jasność tak, aby środkowy panel był ledwo widoczny na tle czarnego ekranu.",
            20f, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f, 1f), 50f);

        // --- Trzy prostokąty testowe ---
        CreateSwatchRow(containerGo.transform);

        // Separator
        CreateSeparator(containerGo.transform);

        // Label suwaka
        CreateTMP(containerGo.transform, "JASNOŚĆ:", 22f, FontStyles.Bold,
            new Color(0.9f, 0.9f, 0.9f, 1f), 32f);

        // Suwak
        brightnessSlider = CreateSlider(containerGo.transform);
        brightnessSlider.value = _currentBrightness;
        brightnessSlider.onValueChanged.AddListener(OnSliderChanged);

        // Separator
        CreateSeparator(containerGo.transform);

        // Przycisk Zatwierdź
        confirmButton = CreateConfirmButton(containerGo.transform);
        confirmButton.onClick.AddListener(Confirm);

        // Hint na dole
        CreateTMP(containerGo.transform,
            "Możesz zmienić jasność w dowolnym czasie z Menu Pauzy → Ustawienia",
            17f, FontStyles.Italic, new Color(0.65f, 0.65f, 0.65f, 0.85f), 38f);
    }

    private void CreateSwatchRow(Transform parent)
    {
        GameObject rowGo = new GameObject("SwatchRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(parent, false);
        rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(880f, 120f);
        HorizontalLayoutGroup hLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 16f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = true;

        CreateSwatch(rowGo.transform, new Color(0.04f, 0.04f, 0.04f), "PRAWIE NIEWIDOCZNY",  "Powinien być ledwo dostrzegalny");
        CreateSwatch(rowGo.transform, new Color(0.12f, 0.12f, 0.12f), "SUBTELNIE WIDOCZNY",  "Powinien być niewyraźny");
        CreateSwatch(rowGo.transform, new Color(0.28f, 0.28f, 0.28f), "WYRAŹNIE WIDOCZNY",   "Powinien być czytelny");
    }

    private void CreateSwatch(Transform parent, Color color, string label, string hint)
    {
        GameObject swatchGo = new GameObject("Swatch_" + label, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Image));
        swatchGo.transform.SetParent(parent, false);
        Image img = swatchGo.GetComponent<Image>();
        img.color = color;
        VerticalLayoutGroup v = swatchGo.GetComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = true;
        v.padding = new RectOffset(4, 4, 4, 4);

        // Etykieta
        GameObject lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGo.transform.SetParent(swatchGo.transform, false);
        TextMeshProUGUI lbl = lblGo.GetComponent<TextMeshProUGUI>();
        lbl.text = label;
        lbl.fontSize = 13f;
        lbl.fontStyle = FontStyles.Bold;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);

        // Podpowiedź
        GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintGo.transform.SetParent(swatchGo.transform, false);
        TextMeshProUGUI hintTmp = hintGo.GetComponent<TextMeshProUGUI>();
        hintTmp.text = hint;
        hintTmp.fontSize = 11f;
        hintTmp.fontStyle = FontStyles.Italic;
        hintTmp.alignment = TextAlignmentOptions.Center;
        hintTmp.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
    }

    private Slider CreateSlider(Transform parent)
    {
        GameObject sliderGo = new GameObject("BrightnessSlider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        sliderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(820f, 40f);

        Slider sl = sliderGo.GetComponent<Slider>();
        sl.minValue = -1.5f;
        sl.maxValue = 1.0f;
        sl.value = DefaultBrightness;

        // Background
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sliderGo.transform, false);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.sizeDelta = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
        sl.targetGraphic = bg.GetComponent<Image>();

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = new Vector2(0f, 0.25f);
        faRect.anchorMax = new Vector2(1f, 0.75f);
        faRect.sizeDelta = new Vector2(-20f, 0f);
        faRect.anchoredPosition = new Vector2(10f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.96f, 0.78f, 0.26f);
        sl.fillRect = fill.GetComponent<RectTransform>();

        // Handle Slide Area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        RectTransform haRect = handleArea.GetComponent<RectTransform>();
        haRect.anchorMin = new Vector2(0f, 0f);
        haRect.anchorMax = new Vector2(1f, 1f);
        haRect.sizeDelta = new Vector2(-20f, 0f);
        haRect.anchoredPosition = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 0f);
        handle.GetComponent<Image>().color = Color.white;
        sl.handleRect = handle.GetComponent<RectTransform>();

        return sl;
    }

    private Button CreateConfirmButton(Transform parent)
    {
        GameObject btnGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Button), typeof(Image));
        btnGo.transform.SetParent(parent, false);
        btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 52f);
        btnGo.GetComponent<Image>().color = new Color(0.12f, 0.11f, 0.10f);

        Button btn = btnGo.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = new Color(0.12f, 0.11f, 0.10f);
        cb.highlightedColor = new Color(0.22f, 0.20f, 0.18f);
        cb.pressedColor     = new Color(0.08f, 0.07f, 0.06f);
        btn.colors = cb;
        btn.targetGraphic = btnGo.GetComponent<Image>();

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "ZATWIERDŹ";
        tmp.fontSize = 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.96f, 0.78f, 0.26f, 1f);

        return btn;
    }

    private static void CreateTMP(Transform parent, string text, float size, FontStyles style, Color color, float height)
    {
        GameObject go = new GameObject("Text_" + text.Substring(0, Mathf.Min(12, text.Length)),
            typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(880f, height);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.enableWordWrapping = true;
    }

    private static void CreateSeparator(Transform parent)
    {
        GameObject sep = new GameObject("Separator", typeof(RectTransform), typeof(Image));
        sep.transform.SetParent(parent, false);
        sep.GetComponent<RectTransform>().sizeDelta = new Vector2(880f, 1f);
        sep.GetComponent<Image>().color = new Color(0.3f, 0.28f, 0.25f, 0.6f);
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        if (Instance == this)
            Instance = null;
    }
}
