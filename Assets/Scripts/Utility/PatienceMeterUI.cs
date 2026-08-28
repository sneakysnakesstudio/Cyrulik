using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wskaźnik Cierpliwości Klienta (Patience Meter / Impatience Meter).
/// Wyświetla stylizowany retro pasek postępu oraz procentowy wskaźnik cierpliwości
/// klienta oczekującego w salonie na podejście fryzjera.
/// 
/// Funkcje:
/// - Płynna animacja wjazdu i zjazdu (DOTween)
/// - Dynamiczna zmiana barwy (Złocisto-Bursztynowy -> Ostrzegawczy Pomarańcz -> Pulsująca Czerwień Krytyczna)
/// - Procentowy odczyt (0% - 100%) oraz pasek postępu
/// - Efekt pulsowania i ostrzeżenia audio przy krytycznym poziomie cierpliwości (<25%)
/// - Samowystarczalny (automatyczny build UI jeśli brak w scenie)
/// </summary>
public class PatienceMeterUI : MonoBehaviour
{
    public static PatienceMeterUI Instance { get; private set; }

    public enum DisplayMode
    {
        PatienceRemaining, // 100% -> 0% (pasek maleje w miarę uciekającego czasu)
        ImpatienceRising   // 0% -> 100% (pasek rośnie aż do 100% wkurzenia/zniecierpliwienia)
    }

    [Header("Tryb Wyświetlania")]
    [Tooltip("PatienceRemaining: 100% -> 0% (pasek maleje) | ImpatienceRising: 0% -> 100% (pasek zniecierpliwienia rośnie do 100%)")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.ImpatienceRising;

    [Header("Referencje UI")]
    [Tooltip("Główny CanvasGroup całego wskaźnika.")]
    [SerializeField] private CanvasGroup meterCanvasGroup;

    [Tooltip("Główny kontener RectTransform (do animacji wsuwania z góry).")]
    [SerializeField] private RectTransform containerRectTransform;

    [Tooltip("Tekst nagłówka (np. 'CLIENT PATIENCE • JUREK' lub 'ZNIECIERPLIWIENIE KLIENTA').")]
    [SerializeField] private TextMeshProUGUI headerText;

    [Tooltip("Tekst wartości procentowej (np. '75%').")]
    [SerializeField] private TextMeshProUGUI percentageText;

    [Tooltip("Wypełnienie paska (Image z ImageType = Filled).")]
    [SerializeField] private Image progressBarFill;

    [Tooltip("Tło paska (Image pod spodem).")]
    [SerializeField] private Image progressBarBackground;

    [Header("Kolorystyka Progresywna (Color Gradient)")]
    [Tooltip("Kolor przy spokojnym poziomie (np. zrelaksowany złocisto-bursztynowy).")]
    [SerializeField] private Color normalColor = new Color(0.95f, 0.72f, 0.22f, 1f);

    [Tooltip("Kolor przy poziomie ostrzegawczym (>50% zniecierpliwienia).")]
    [SerializeField] private Color warningColor = new Color(0.95f, 0.45f, 0.12f, 1f);

    [Tooltip("Kolor przy poziomie krytycznym (>75% zniecierpliwienia / blisko wyjścia).")]
    [SerializeField] private Color criticalColor = new Color(0.92f, 0.18f, 0.18f, 1f);

    [Header("Animacje (DOTween)")]
    [Tooltip("Ukryta pozycja Y (poza górną krawędzią ekranu).")]
    [SerializeField] private float hiddenPosY = 90f;

    [Tooltip("Widoczna pozycja Y na ekranie.")]
    [SerializeField] private float visiblePosY = -40f;

    [Tooltip("Czas wjeżdżania wskaźnika.")]
    [SerializeField] private float slideInDuration = 0.35f;

    [Tooltip("Czas chowania wskaźnika.")]
    [SerializeField] private float slideOutDuration = 0.3f;

    [Header("Audio SFX")]
    [Tooltip("Nazwa dźwięku ostrzegawczego w AudioManager (np. 'timer_tick' lub 'patience_warning').")]
    [SerializeField] private string soundWarning = "patience_warning";

    [Tooltip("Opcjonalny klip audio ostrzeżenia (fallback).")]
    [SerializeField] private AudioClip customWarningClip;
    [SerializeField] private AudioSource customAudioSource;

    [Header("Avatar")]
    [Tooltip("Obrazek z twarzą klienta (mordka).")]
    [SerializeField] private Image avatarImage;
    [Tooltip("Domyślny sprite twarzy.")]
    [SerializeField] private Sprite defaultAvatarSprite;
    private Tween _avatarShakeTween;

    private bool _isVisible = false;
    private float _currentPatience = 1f; // 0..1
    private float _totalDuration = 30f;
    private Tween _moveTween;
    private Tween _fadeTween;
    private Tween _criticalPulseTween;
    private float _lastWarningSoundTime = 0f;

    public bool IsVisible => _isVisible;
    public float NormalizedImpatience => 1f - _currentPatience; // 0..1 (0 = spokój, 1 = 100% zniecierpliwienia)
    public float NormalizedPatience => _currentPatience;       // 1..0 (1 = pełna cierpliwość, 0 = brak)

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoRunPatienceMeter()
    {
        // Automatyczne dodanie do sceny przy uruchomieniu gry
        GameObject go = new GameObject("PatienceMeterUI_Auto");
        go.AddComponent<PatienceMeterUI>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureCanvasSetup();

        if (meterCanvasGroup == null || containerRectTransform == null || progressBarFill == null)
        {
            BuildDefaultUI();
        }

        HideInstant();
    }

    private void EnsureCanvasSetup()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 91; // tuż pod powiadomieniami zadań

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        var raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    /// <summary>
    /// Pokazuje wskaźnik cierpliwości dla wchodzącego klienta.
    /// </summary>
    /// <param name="duration">Maksymalny czas oczekiwania w sekundach (np. 30s).</param>
    /// <param name="clientName">Imię klienta wyświetlane w nagłówku (np. 'Jurek').</param>
    public void Show(float duration, string clientName = "Jurek")
    {
        _totalDuration = Mathf.Max(1f, duration);
        _currentPatience = 1f;

        if (headerText != null)
        {
            headerText.text = displayMode == DisplayMode.ImpatienceRising
                ? $"IMPATIENCE • {clientName.ToUpper()}"
                : $"PATIENCE • {clientName.ToUpper()}";
        }

        UpdateVisuals(1f);

        _isVisible = true;

        if (containerRectTransform != null)
        {
            _moveTween?.Kill();
            containerRectTransform.anchoredPosition = new Vector2(0f, hiddenPosY);
            _moveTween = containerRectTransform.DOAnchorPosY(visiblePosY, slideInDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        if (meterCanvasGroup != null)
        {
            _fadeTween?.Kill();
            meterCanvasGroup.alpha = 0f;
            _fadeTween = meterCanvasGroup.DOFade(1f, slideInDuration)
                .SetUpdate(true);
        }

        if (avatarImage != null)
        {
            if (defaultAvatarSprite != null) avatarImage.sprite = defaultAvatarSprite;
            
            _avatarShakeTween?.Kill();
            avatarImage.rectTransform.localRotation = Quaternion.identity;
            _avatarShakeTween = avatarImage.rectTransform.DOShakeRotation(2f, new Vector3(0, 0, 12f), 20, 90f, false)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// Aktualizuje aktualny stan czasu cierpliwości.
    /// </summary>
    /// <param name="remainingSeconds">Pozostały czas w sekundach.</param>
    /// <param name="totalDuration">Całkowity czas w sekundach.</param>
    public void UpdateProgress(float remainingSeconds, float totalDuration)
    {
        if (!_isVisible) return;

        _totalDuration = Mathf.Max(0.1f, totalDuration);
        _currentPatience = Mathf.Clamp01(remainingSeconds / _totalDuration);

        UpdateVisuals(_currentPatience);
    }

    private void UpdateVisuals(float patienceNorm)
    {
        float impatienceNorm = 1f - patienceNorm; // 0..1 (0% -> 100%)

        // 1. Ustawienie wartości paska postępu
        float fillAmount = displayMode == DisplayMode.ImpatienceRising ? impatienceNorm : patienceNorm;
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = fillAmount;
        }

        // 2. Ustawienie tekstu procentowego
        int displayPercent = Mathf.RoundToInt(fillAmount * 100f);
        if (percentageText != null)
        {
            percentageText.text = $"{displayPercent}%";
        }

        // 3. Kolorystyka w zależności od poziomu zniecierpliwienia
        Color currentColor;
        if (impatienceNorm < 0.5f)
        {
            // Spokój -> Ostrzeżenie (0% - 50%)
            float t = impatienceNorm / 0.5f;
            currentColor = Color.Lerp(normalColor, warningColor, t);
        }
        else
        {
            // Ostrzeżenie -> Krytyczny (50% - 100%)
            float t = (impatienceNorm - 0.5f) / 0.5f;
            currentColor = Color.Lerp(warningColor, criticalColor, t);
        }

        if (progressBarFill != null)
        {
            progressBarFill.color = currentColor;
        }

        if (percentageText != null)
        {
            percentageText.color = impatienceNorm > 0.75f ? criticalColor : Color.white;
        }

        // 4. Efekt pulsowania przy poziomie krytycznym (>75% zniecierpliwienia)
        if (impatienceNorm >= 0.75f)
        {
            if (_criticalPulseTween == null || !_criticalPulseTween.IsActive())
            {
                if (containerRectTransform != null)
                {
                    _criticalPulseTween = containerRectTransform.DOScale(1.04f, 0.25f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true);
                }
            }

            // Dźwięk ostrzeżenia co 1.0 sekundy
            if (Time.time - _lastWarningSoundTime > 1.0f)
            {
                _lastWarningSoundTime = Time.time;
                PlayWarningSound();
            }
        }
        else
        {
            StopCriticalPulse();
        }
    }

    private void StopCriticalPulse()
    {
        if (_criticalPulseTween != null)
        {
            _criticalPulseTween.Kill();
            _criticalPulseTween = null;
            if (containerRectTransform != null)
            {
                containerRectTransform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>
    /// Ukrywa wskaźnik cierpliwości (np. gdy gracz podszedł i zagadał do klienta).
    /// </summary>
    /// <param name="success">Jeśli true, pasek na ułamek sekundy błyśnie na zielono (sukces interakcji).</param>
    public void Hide(bool success = true)
    {
        if (!_isVisible) return;
        _isVisible = false;
        StopCriticalPulse();
        _avatarShakeTween?.Kill();

        if (success && progressBarFill != null)
        {
            // Sukces - krótki zielony błysk potwierdzający podejście do klienta
            progressBarFill.DOColor(new Color(0.3f, 0.95f, 0.4f, 1f), 0.15f).SetUpdate(true);
        }

        _moveTween?.Kill();
        if (containerRectTransform != null)
        {
            _moveTween = containerRectTransform.DOAnchorPosY(hiddenPosY, slideOutDuration)
                .SetEase(Ease.InBack)
                .SetDelay(success ? 0.2f : 0f)
                .SetUpdate(true);
        }

        _fadeTween?.Kill();
        if (meterCanvasGroup != null)
        {
            _fadeTween = meterCanvasGroup.DOFade(0f, slideOutDuration)
                .SetDelay(success ? 0.2f : 0f)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// Wywoływane gdy czas minął i klient ucieka (100% zniecierpliwienia).
    /// </summary>
    public void TriggerTimeout()
    {
        StopCriticalPulse();
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = displayMode == DisplayMode.ImpatienceRising ? 1f : 0f;
            progressBarFill.color = criticalColor;
        }

        if (percentageText != null)
        {
            percentageText.text = "100%";
            percentageText.color = criticalColor;
        }

        if (containerRectTransform != null)
        {
            containerRectTransform.DOShakePosition(0.4f, 8f, 20).SetUpdate(true);
        }

        Hide(false);
    }

    public void HideInstant()
    {
        _isVisible = false;
        StopCriticalPulse();
        _avatarShakeTween?.Kill();

        _moveTween?.Kill();
        _fadeTween?.Kill();

        if (containerRectTransform != null)
        {
            containerRectTransform.anchoredPosition = new Vector2(0f, hiddenPosY);
            containerRectTransform.localScale = Vector3.one;
        }

        if (meterCanvasGroup != null)
        {
            meterCanvasGroup.alpha = 0f;
        }
    }

    private void PlayWarningSound()
    {
        if (customWarningClip != null)
        {
            if (customAudioSource != null)
            {
                customAudioSource.PlayOneShot(customWarningClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(customWarningClip, Camera.main != null ? Camera.main.transform.position : transform.position, 0.6f);
            }
            return;
        }

        if (!string.IsNullOrEmpty(soundWarning) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundWarning);
        }
    }

    /// <summary>
    /// Automatyczne tworzenie struktury wizualnej UI w locie, jeśli prefab/obiekt nie został skonfigurowany w scenie.
    /// </summary>
    public void BuildDefaultUI()
    {
        EnsureCanvasSetup();

        // 1. Główny kontener
        GameObject containerGo = new GameObject("PatienceMeter_Container", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        containerGo.transform.SetParent(transform, false);

        containerRectTransform = containerGo.GetComponent<RectTransform>();
        containerRectTransform.anchorMin = new Vector2(0.5f, 1f);
        containerRectTransform.anchorMax = new Vector2(0.5f, 1f);
        containerRectTransform.pivot = new Vector2(0.5f, 1f);
        containerRectTransform.sizeDelta = new Vector2(360f, 62f);
        containerRectTransform.anchoredPosition = new Vector2(0f, hiddenPosY);

        meterCanvasGroup = containerGo.GetComponent<CanvasGroup>();
        meterCanvasGroup.alpha = 0f;
        meterCanvasGroup.blocksRaycasts = false;

        // Tło retro panelu
        var bg = containerGo.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.06f, 0.94f);

        // Złota/bursztynowa obwódka
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

        avatarImage = avatarGo.GetComponent<Image>();
        if (defaultAvatarSprite != null)
        {
            avatarImage.sprite = defaultAvatarSprite;
            avatarImage.color = Color.white;
        }
        else
        {
            avatarImage.color = new Color(0.15f, 0.14f, 0.13f, 1f); // Ciemny placeholder
        }

        // 2. Nagłówek (Header Text)
        GameObject headerGo = new GameObject("Header_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGo.transform.SetParent(containerGo.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 0.5f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.offsetMin = new Vector2(16f, 0f);
        headerRect.offsetMax = new Vector2(-60f, -6f);

        headerText = headerGo.GetComponent<TextMeshProUGUI>();
        headerText.text = "IMPATIENCE • JUREK";
        headerText.fontSize = 14f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = new Color(0.95f, 0.76f, 0.28f, 1f);
        headerText.alignment = TextAlignmentOptions.Left;

        // 3. Procenty (Percentage Text)
        GameObject percentGo = new GameObject("Percentage_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        percentGo.transform.SetParent(containerGo.transform, false);
        var percentRect = percentGo.GetComponent<RectTransform>();
        percentRect.anchorMin = new Vector2(1f, 0.5f);
        percentRect.anchorMax = new Vector2(1f, 1f);
        percentRect.pivot = new Vector2(1f, 0.5f);
        percentRect.sizeDelta = new Vector2(60f, 26f);
        percentRect.anchoredPosition = new Vector2(-16f, -14f);

        percentageText = percentGo.GetComponent<TextMeshProUGUI>();
        percentageText.text = "0%";
        percentageText.fontSize = 14f;
        percentageText.fontStyle = FontStyles.Bold;
        percentageText.color = Color.white;
        percentageText.alignment = TextAlignmentOptions.Right;

        // 4. Pasek postępu - Tło (Progress Bar Background)
        GameObject barBgGo = new GameObject("ProgressBar_Background", typeof(RectTransform), typeof(Image));
        barBgGo.transform.SetParent(containerGo.transform, false);
        var barBgRect = barBgGo.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0f, 0f);
        barBgRect.anchorMax = new Vector2(1f, 0f);
        barBgRect.pivot = new Vector2(0.5f, 0f);
        barBgRect.sizeDelta = new Vector2(-32f, 14f);
        barBgRect.anchoredPosition = new Vector2(0f, 10f);

        progressBarBackground = barBgGo.GetComponent<Image>();
        progressBarBackground.color = new Color(0.04f, 0.04f, 0.04f, 0.9f);

        // 5. Pasek postępu - Wypełnienie (Progress Bar Fill)
        GameObject fillGo = new GameObject("ProgressBar_Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(barBgGo.transform, false);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        Texture2D whiteTex = Texture2D.whiteTexture;
        Sprite whiteSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

        progressBarFill = fillGo.GetComponent<Image>();
        progressBarFill.sprite = whiteSprite;
        progressBarFill.type = Image.Type.Filled;
        progressBarFill.fillMethod = Image.FillMethod.Horizontal;
        progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressBarFill.fillAmount = 0f;
        progressBarFill.color = normalColor;
    }
}
