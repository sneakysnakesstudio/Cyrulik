using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Powiadomienie / adnotacja UI o odtwarzanej muzyce (np. w radiu).
/// Wyświetla elegancki retro baner z nazwiskiem twórcy muzyki (np. "Music by 'Tymon Urbańczyk'")
/// w lewym górnym rogu ekranu z płynną animacją wejścia i wyjścia.
/// W 100% samowystarczalny komponent z własnym Canvasem (działa niezawodnie w Edytorze i Buildzie).
/// </summary>
public class MusicCreditUI : MonoBehaviour
{
    public static MusicCreditUI Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Główny CanvasGroup całego powiadomienia.")]
    [SerializeField] private CanvasGroup bannerCanvasGroup;

    [Tooltip("RectTransform powiadomienia (do animacji wysuwania z lewej strony).")]
    [SerializeField] private RectTransform bannerRectTransform;

    [Tooltip("Podpis / nagłówek kategorii (np. 'RADIO • NOW PLAYING').")]
    [SerializeField] private TextMeshProUGUI subheaderText;

    [Tooltip("Główny tekst wyróżnienia (np. 'Music by 'Tymon Urbańczyk'').")]
    [SerializeField] private TextMeshProUGUI authorText;

    [Tooltip("Opcjonalna ikona nutki / audycji.")]
    [SerializeField] private TextMeshProUGUI iconText;

    [Header("Default Texts")]
    [SerializeField] private string defaultSubheader = "RADIO • NOW PLAYING";
    [SerializeField] private string defaultAuthorText = "Music by 'Tymon Urbańczyk'";

    [Header("Animation Settings")]
    [Tooltip("Ukryta pozycja X (poza lewą krawędzią ekranu).")]
    [SerializeField] private float hiddenPosX = -450f;

    [Tooltip("Widoczna pozycja X na ekranie (od lewej krawędzi).")]
    [SerializeField] private float visiblePosX = 35f;

    [Tooltip("Pozycja Y od góry ekranu.")]
    [SerializeField] private float posY = -35f;

    [Tooltip("Czas wsuwania banera (w sekundach).")]
    [SerializeField] private float slideInDuration = 0.45f;

    [Tooltip("Czas wyświetlania banera na ekranie.")]
    [SerializeField] private float defaultDisplayDuration = 4.5f;

    [Tooltip("Czas chowania / zanikania banera.")]
    [SerializeField] private float slideOutDuration = 0.35f;

    private Coroutine _displayCoroutine;
    private Tween _moveTween;
    private Tween _fadeTween;
    private bool _isVisible = false;

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

        EnsureCanvasSetup();

        if (bannerCanvasGroup == null || bannerRectTransform == null)
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
        canvas.sortingOrder = 92;
        canvas.enabled = true;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        KillTweens();
    }

    /// <summary>
    /// Wyświetla powiadomienie o autorze muzyki w lewym górnym rogu ekranu.
    /// </summary>
    public void ShowMusicCredit(string customAuthor = null, string customSubheader = null, float duration = -1f)
    {
        EnsureCanvasSetup();

        if (bannerCanvasGroup == null || bannerRectTransform == null)
        {
            BuildDefaultUI();
        }

        string author = !string.IsNullOrWhiteSpace(customAuthor) ? customAuthor : defaultAuthorText;
        string subheader = !string.IsNullOrWhiteSpace(customSubheader) ? customSubheader : defaultSubheader;
        float showDuration = duration > 0f ? duration : defaultDisplayDuration;

        if (authorText != null)
            authorText.text = author;

        if (subheaderText != null)
            subheaderText.text = subheader;

        Debug.Log($"[MusicCreditUI] ♫ Wyświetlam powiadomienie muzyczne: \"{author}\" ({subheader}) przez {showDuration}s");

        if (_displayCoroutine != null)
        {
            StopCoroutine(_displayCoroutine);
        }

        _displayCoroutine = StartCoroutine(ShowBannerRoutine(showDuration));
    }

    /// <summary>
    /// Płynnie chowa baner z ekranu (np. przy wyłączeniu radia).
    /// </summary>
    public void HideMusicCredit(float fadeDuration = -1f)
    {
        if (!_isVisible) return;

        if (_displayCoroutine != null)
        {
            StopCoroutine(_displayCoroutine);
            _displayCoroutine = null;
        }

        float duration = fadeDuration >= 0f ? fadeDuration : slideOutDuration;
        AnimateOut(duration);
    }

    private IEnumerator ShowBannerRoutine(float displayDuration)
    {
        _isVisible = true;
        AnimateIn();

        float elapsed = 0f;
        while (elapsed < (slideInDuration + displayDuration))
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        AnimateOut(slideOutDuration);

        elapsed = 0f;
        while (elapsed < slideOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _isVisible = false;
        _displayCoroutine = null;
    }

    private void AnimateIn()
    {
        if (bannerCanvasGroup == null || bannerRectTransform == null) return;

        KillTweens();

        bannerRectTransform.anchoredPosition = new Vector2(hiddenPosX, posY);
        bannerCanvasGroup.alpha = 0f;
        bannerCanvasGroup.blocksRaycasts = false;
        bannerCanvasGroup.interactable = false;

        if (bannerCanvasGroup.gameObject != null && !bannerCanvasGroup.gameObject.activeSelf)
        {
            bannerCanvasGroup.gameObject.SetActive(true);
        }

        _moveTween = bannerRectTransform
            .DOAnchorPosX(visiblePosX, slideInDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(bannerRectTransform.gameObject, LinkBehaviour.KillOnDestroy);

        _fadeTween = bannerCanvasGroup
            .DOFade(1f, slideInDuration * 0.8f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(bannerCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void AnimateOut(float duration)
    {
        if (bannerCanvasGroup == null || bannerRectTransform == null) return;

        KillTweens();

        _moveTween = bannerRectTransform
            .DOAnchorPosX(hiddenPosX, duration)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(bannerRectTransform.gameObject, LinkBehaviour.KillOnDestroy);

        _fadeTween = bannerCanvasGroup
            .DOFade(0f, duration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true)
            .SetLink(bannerCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                _isVisible = false;
            });
    }

    private void HideInstant()
    {
        KillTweens();

        if (bannerCanvasGroup != null)
        {
            bannerCanvasGroup.alpha = 0f;
            bannerCanvasGroup.blocksRaycasts = false;
            bannerCanvasGroup.interactable = false;
        }

        if (bannerRectTransform != null)
        {
            bannerRectTransform.anchoredPosition = new Vector2(hiddenPosX, posY);
        }

        _isVisible = false;
    }

    private void KillTweens()
    {
        _moveTween?.Kill();
        _fadeTween?.Kill();
        _moveTween = null;
        _fadeTween = null;
    }

    /// <summary>
    /// Buduje hierarchię UI w przypadku dynamicznego dodania do sceny.
    /// </summary>
    public void BuildDefaultUI()
    {
        EnsureCanvasSetup();

        // Usuń stare elementy jeśli istniały
        foreach (Transform child in transform)
        {
            if (child != null)
                Destroy(child.gameObject);
        }

        // Kontener banera
        GameObject containerGo = new GameObject("MusicCredit_Banner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        containerGo.transform.SetParent(transform, false);

        bannerRectTransform = containerGo.GetComponent<RectTransform>();
        bannerRectTransform.anchorMin = new Vector2(0f, 1f);
        bannerRectTransform.anchorMax = new Vector2(0f, 1f);
        bannerRectTransform.pivot = new Vector2(0f, 1f);
        bannerRectTransform.sizeDelta = new Vector2(400f, 68f);
        bannerRectTransform.anchoredPosition = new Vector2(hiddenPosX, posY);

        bannerCanvasGroup = containerGo.GetComponent<CanvasGroup>();
        bannerCanvasGroup.blocksRaycasts = false;
        bannerCanvasGroup.interactable = false;
        bannerCanvasGroup.alpha = 0f;

        var bgImg = containerGo.GetComponent<Image>();
        bgImg.color = new Color(0.07f, 0.07f, 0.08f, 0.94f);
        bgImg.raycastTarget = false;

        var outline = containerGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.68f, 0.28f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Pasek boczny akcentujący (złoto/bursztyn)
        GameObject accentBar = new GameObject("Accent_Bar", typeof(RectTransform), typeof(Image));
        accentBar.transform.SetParent(containerGo.transform, false);
        var barRect = accentBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.sizeDelta = new Vector2(4f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        var barImg = accentBar.GetComponent<Image>();
        barImg.color = new Color(0.95f, 0.78f, 0.32f, 1f);
        barImg.raycastTarget = false;

        // Ikona Nutki ♫
        GameObject iconGo = new GameObject("Icon_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        iconGo.transform.SetParent(containerGo.transform, false);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(16f, 0f);
        iconRect.sizeDelta = new Vector2(30f, 40f);

        iconText = iconGo.GetComponent<TextMeshProUGUI>();
        iconText.text = "♫";
        iconText.fontSize = 26f;
        iconText.fontStyle = FontStyles.Bold;
        iconText.color = new Color(0.96f, 0.80f, 0.35f, 1f);
        iconText.alignment = TextAlignmentOptions.Center;
        iconText.raycastTarget = false;

        // Subheader ("RADIO • NOW PLAYING")
        GameObject subheaderGo = new GameObject("Subheader_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        subheaderGo.transform.SetParent(containerGo.transform, false);
        var subheaderRect = subheaderGo.GetComponent<RectTransform>();
        subheaderRect.anchorMin = new Vector2(0f, 0.5f);
        subheaderRect.anchorMax = new Vector2(1f, 1f);
        subheaderRect.offsetMin = new Vector2(52f, 0f);
        subheaderRect.offsetMax = new Vector2(-12f, -6f);

        subheaderText = subheaderGo.GetComponent<TextMeshProUGUI>();
        subheaderText.text = defaultSubheader;
        subheaderText.fontSize = 13f;
        subheaderText.fontStyle = FontStyles.Bold;
        subheaderText.characterSpacing = 3f;
        subheaderText.color = new Color(0.85f, 0.68f, 0.32f, 0.9f);
        subheaderText.alignment = TextAlignmentOptions.Left;
        subheaderText.raycastTarget = false;

        // Author Text ("Music by 'Tymon Urbańczyk'")
        GameObject authorGo = new GameObject("Author_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        authorGo.transform.SetParent(containerGo.transform, false);
        var authorRect = authorGo.GetComponent<RectTransform>();
        authorRect.anchorMin = new Vector2(0f, 0f);
        authorRect.anchorMax = new Vector2(1f, 0.5f);
        authorRect.offsetMin = new Vector2(52f, 6f);
        authorRect.offsetMax = new Vector2(-12f, 0f);

        authorText = authorGo.GetComponent<TextMeshProUGUI>();
        authorText.text = defaultAuthorText;
        authorText.fontSize = 20f;
        authorText.fontStyle = FontStyles.Bold;
        authorText.color = new Color(0.96f, 0.94f, 0.90f, 1f);
        authorText.alignment = TextAlignmentOptions.Left;
        authorText.raycastTarget = false;
    }
}
