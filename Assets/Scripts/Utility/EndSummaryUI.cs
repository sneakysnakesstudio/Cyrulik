using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Czysty, bezkompromisowy ekran w stylu GTA "YOU FAILED":
/// - Pełny, w 100% czarny ekran w tle (Solid Black Canvas, sortingOrder 999)
/// - Ukrycie celownika i wszelkich promptów HUD
/// - Tylko i wyłącznie:
///   1. Wielki czerwony napis "YOU FAILED"
///   2. Powód przegranej w języku angielskim
///   3. Pasek z przyciskami [ RESTART (SPACE) ] i [ QUIT (ESC) ]
/// </summary>
public class EndSummaryUI : MonoBehaviour
{
    public static EndSummaryUI Instance { get; private set; }

    [Header("UI Containers & References")]
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private TextMeshProUGUI mainTitleText;
    [SerializeField] private TextMeshProUGUI reasonDescriptionText;

    [Header("Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TextMeshProUGUI restartButtonText;
    [SerializeField] private TextMeshProUGUI quitButtonText;

    [Header("Styling & Colors")]
    [SerializeField] private Color failureTitleColor = new Color(0.92f, 0.12f, 0.12f, 1f); // GTA Red
    [SerializeField] private Color victoryTitleColor = new Color(0.95f, 0.78f, 0.22f, 1f); // Amber / Gold
    [SerializeField] private Color subtitleColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("Cinematic & Slow Motion")]
    [Tooltip("Czy spowolnić czas w momencie przegranej?")]
    [SerializeField] private bool useSlowMotion = true;
    [Range(0.05f, 1f)]
    [SerializeField] private float slowMotionTimeScale = 0.25f;

    [Header("Audio")]
    [SerializeField] private string soundVictory = "task_complete";
    [SerializeField] private string soundDefeat = "sharpen_miss";
    [SerializeField] private AudioClip customFailClip;

    [Header("Scenes & Transitions")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";
    [SerializeField] private float fadeDuration = 0.4f;

    private bool _isVisible = false;
    private bool _canAcceptInput = false;
    private Tween _fadeTween;
    private Tween _titleScaleTween;

    public bool IsVisible => _isVisible;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (mainCanvasGroup == null)
        {
            BuildDefaultUI();
        }

        SetupButtonListeners();
        HideInstant();
    }

    private void SetupButtonListeners()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartRun);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Time.timeScale = 1f;
        _fadeTween?.Kill();
        _titleScaleTween?.Kill();
    }

    private void Update()
    {
        if (!_isVisible || !_canAcceptInput) return;

        // Natychmiastowy restart / wyjście klawiaturą
        if (WasRestartPressed())
        {
            RestartRun();
        }
        else if (WasMenuPressed())
        {
            ReturnToMainMenu();
        }
    }

    private bool WasRestartPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                Keyboard.current.rKey.wasPressedThisFrame)
                return true;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.R))
            return true;
#endif

        return false;
    }

    private bool WasMenuPressed()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#endif

        return false;
    }

    /// <summary>
    /// Wyświetla czysty, pełnoekranowy czarny ekran GTA "YOU FAILED" z powodem i przyciskami.
    /// </summary>
    public void ShowEndScreen(string reasonText, bool isVictory = false)
    {
        if (_isVisible) return;
        _isVisible = true;
        _canAcceptInput = false;

        // Ukryj celownik i prompty interakcji
        Crosshair crosshair = FindAnyObjectByType<Crosshair>();
        if (crosshair != null)
        {
            crosshair.gameObject.SetActive(false);
        }

        // Odblokuj kursor myszy do klikania przycisków
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchToUI(true);
        }

        // Spowolnienie czasu
        if (useSlowMotion && !isVictory)
        {
            Time.timeScale = slowMotionTimeScale;
        }

        // Dźwięk
        PlayEndAudio(isVictory);

        // Formatowanie głównego napisu
        if (mainTitleText != null)
        {
            mainTitleText.text = isVictory ? "THANK YOU FOR PLAYING" : "YOU FAILED";
            mainTitleText.color = isVictory ? victoryTitleColor : failureTitleColor;
        }

        // Tłumaczenie / upewnienie się że powód jest w 100% po angielsku
        string englishReason = isVictory ? "Thank you for playing Cyrulik Demo!" : SanitizeToEnglish(reasonText, false);
        if (reasonDescriptionText != null)
        {
            reasonDescriptionText.text = englishReason;
        }

        if (quitButtonText != null)
        {
            quitButtonText.text = "EXIT (ESC)";
        }

        // Animacja wejścia na czarnym tle
        if (mainCanvasGroup != null)
        {
            _fadeTween?.Kill();
            _titleScaleTween?.Kill();

            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.blocksRaycasts = true;
            mainCanvasGroup.interactable = true;

            if (mainTitleText != null)
            {
                mainTitleText.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
                _titleScaleTween = mainTitleText.transform.DOScale(1f, 0.35f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }

            _fadeTween = mainCanvasGroup.DOFade(1f, 0.35f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _canAcceptInput = true;
                });
        }
        else
        {
            _canAcceptInput = true;
        }
    }

    private string SanitizeToEnglish(string input, bool isVictory)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return isVictory ? "All preparation procedures completed successfully." : "The salon was closed prematurely.";
        }

        string lower = input.ToLowerInvariant();
        if (lower.Contains("ponur") || lower.Contains("gloomy") || lower.Contains("atmosfer"))
            return "The client felt the atmosphere was too gloomy and left.";
        if (lower.Contains("mysz") || lower.Contains("rat") || lower.Contains("mouse"))
            return "The client saw a rat in the salon and ran away!";
        if (lower.Contains("zniecierpliwi") || lower.Contains("czeka") || lower.Contains("impatient") || lower.Contains("wait"))
            return "The client waited too long and left.";

        return input;
    }

    private void PlayEndAudio(bool isVictory)
    {
        if (customFailClip != null && !isVictory)
        {
            AudioSource.PlayClipAtPoint(customFailClip, Camera.main != null ? Camera.main.transform.position : transform.position);
            return;
        }

        if (AudioManager.Instance != null)
        {
            string soundToPlay = isVictory ? soundVictory : soundDefeat;
            if (!string.IsNullOrEmpty(soundToPlay))
            {
                AudioManager.Instance.Play(soundToPlay);
            }
        }
    }

    public void RestartRun()
    {
        _canAcceptInput = false;
        Time.timeScale = 1f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play("button_hover");
        }

        string activeScene = SceneManager.GetActiveScene().name;
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.LoadScene(activeScene, fadeDuration);
        }
        else
        {
            SceneManager.LoadScene(activeScene);
        }
    }

    public void ReturnToMainMenu()
    {
        _canAcceptInput = false;
        Time.timeScale = 1f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play("button_hover");
        }

        string targetScene = !string.IsNullOrEmpty(mainMenuSceneName) ? mainMenuSceneName : "MainMenuScene";
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.LoadScene(targetScene, fadeDuration);
        }
        else
        {
            SceneManager.LoadScene(targetScene);
        }
    }

    private void HideInstant()
    {
        _isVisible = false;
        _canAcceptInput = false;
        Time.timeScale = 1f;

        _fadeTween?.Kill();
        _titleScaleTween?.Kill();

        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.blocksRaycasts = false;
            mainCanvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// Tworzy w 100% czarny canvas z samym YOU FAILED, powodem i przyciskami.
    /// </summary>
    private void BuildDefaultUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("EndSummary_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Najwyższy priorytet nad całym HUD-em

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            canvas.sortingOrder = 999;
        }

        // 1. W 100% CZARNY EKRAN W TLE (Solid Black)
        GameObject rootPanel = new GameObject("EndSummary_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootPanel.transform.SetParent(canvas.transform, false);
        var rootRect = rootPanel.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        mainCanvasGroup = rootPanel.GetComponent<CanvasGroup>();
        var rootBg = rootPanel.GetComponent<Image>();
        rootBg.color = Color.black; // W 100% czarne tło!

        // 2. Kontener na środku po lewej stronie
        GameObject leftContainerGo = new GameObject("GTA_Content_Container", typeof(RectTransform));
        leftContainerGo.transform.SetParent(rootPanel.transform, false);
        contentContainer = leftContainerGo.GetComponent<RectTransform>();
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

        mainTitleText = titleGo.GetComponent<TextMeshProUGUI>();
        mainTitleText.text = "YOU FAILED";
        mainTitleText.fontSize = 76f;
        mainTitleText.fontStyle = FontStyles.Bold;
        mainTitleText.characterSpacing = 4f;
        mainTitleText.textWrappingMode = TextWrappingModes.NoWrap;
        mainTitleText.alignment = TextAlignmentOptions.Left;
        mainTitleText.color = failureTitleColor;

        // 3.5. KRESKA POD NAPISEM (Separator Line)
        GameObject lineGo = new GameObject("Separator_Line", typeof(RectTransform), typeof(Image));
        lineGo.transform.SetParent(leftContainerGo.transform, false);
        var lineRect = lineGo.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(0f, 1f);
        lineRect.pivot = new Vector2(0f, 1f);
        lineRect.anchoredPosition = new Vector2(0f, -85f);
        lineRect.sizeDelta = new Vector2(850f, 4f);

        var lineImg = lineGo.GetComponent<Image>();
        lineImg.color = new Color(0.88f, 0.88f, 0.88f, 0.8f);

        // 4. Podtytuł z powodem (English)
        GameObject reasonGo = new GameObject("Reason_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        reasonGo.transform.SetParent(leftContainerGo.transform, false);
        var rRect = reasonGo.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0f, 1f);
        rRect.anchorMax = new Vector2(0f, 1f);
        rRect.pivot = new Vector2(0f, 1f);
        rRect.anchoredPosition = new Vector2(0f, -110f);
        rRect.sizeDelta = new Vector2(850f, 50f);

        reasonDescriptionText = reasonGo.GetComponent<TextMeshProUGUI>();
        reasonDescriptionText.text = "The client felt the atmosphere was too gloomy and left.";
        reasonDescriptionText.fontSize = 24f;
        reasonDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        reasonDescriptionText.alignment = TextAlignmentOptions.Left;
        reasonDescriptionText.color = subtitleColor;

        // 5. Pasek z przyciskami [ TRY AGAIN (SPACE) ] i [ EXIT (ESC) ]
        GameObject btnBarGo = new GameObject("Buttons_Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnBarGo.transform.SetParent(leftContainerGo.transform, false);
        var bBarRect = btnBarGo.GetComponent<RectTransform>();
        bBarRect.anchorMin = new Vector2(0f, 1f);
        bBarRect.anchorMax = new Vector2(0f, 1f);
        bBarRect.pivot = new Vector2(0f, 1f);
        bBarRect.anchoredPosition = new Vector2(0f, -190f);
        bBarRect.sizeDelta = new Vector2(520f, 54f);

        var hlg = btnBarGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Przycisk TRY AGAIN
        restartButton = CreateGTAButton(btnBarGo.transform, "TryAgain_Button", "TRY AGAIN  [SPACE]", new Color(0.85f, 0.65f, 0.22f, 1f));
        restartButtonText = restartButton.GetComponentInChildren<TextMeshProUGUI>();

        // Przycisk EXIT
        quitButton = CreateGTAButton(btnBarGo.transform, "Exit_Button", "EXIT  [ESC]", new Color(0.35f, 0.35f, 0.38f, 1f));
        quitButtonText = quitButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    private Button CreateGTAButton(Transform parent, string name, string label, Color accentColor)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(MenuButtonEffects));
        btnGo.transform.SetParent(parent, false);
        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(230f, 52f);

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

        var outline = btnGo.GetComponent<Outline>();
        outline.effectColor = accentColor;
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
