using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stylowy retro wskaźnik staminy/sprintu po lewej stronie ekranu.
/// Pokazuje stan energii do sprintu (3.5 - 5s zrywu) z płynnym ubytkiem,
/// regeneracją oraz ostrzegawczym czerwonym miganiem przy wyczerpaniu.
/// </summary>
public class SprintStaminaUI : MonoBehaviour
{
    public static SprintStaminaUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Outline barOutline;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.92f, 0.75f, 0.32f, 0.95f); // Bursztyn / Vintage Gold
    [SerializeField] private Color lowStaminaColor = new Color(0.95f, 0.42f, 0.18f, 0.95f); // Pomarańcz
    [SerializeField] private Color exhaustedColor = new Color(0.92f, 0.22f, 0.22f, 1f); // Czerwień ostrzegawcza

    [Header("Animation Settings")]
    [Tooltip("Czy ukrywać pasek gdy stamina jest w 100% pełna (auto-fade)?")]
    [SerializeField] private bool autoHideWhenFull = true;
    [SerializeField] private float fadeDuration = 0.25f;

    private Tween _fillTween;
    private Tween _fadeTween;
    private Tween _exhaustedFlashTween;
    private bool _isExhausted = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (mainCanvasGroup == null)
        {
            BuildDefaultUI();
        }

        if (autoHideWhenFull && mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _fillTween?.Kill();
        _fadeTween?.Kill();
        _exhaustedFlashTween?.Kill();
    }

    /// <summary>
    /// Aktualizuje stan paska staminy (0..1).
    /// </summary>
    public void UpdateStamina(float currentStamina, float maxStamina, bool isExhausted, bool isSprinting)
    {
        if (fillImage == null || maxStamina <= 0.001f) return;

        float ratio = Mathf.Clamp01(currentStamina / maxStamina);
        fillImage.fillAmount = ratio;

        // Kolorowanie w zależności od poziomu energii
        if (isExhausted)
        {
            fillImage.color = exhaustedColor;
            if (!_isExhausted)
            {
                TriggerExhaustedEffect();
            }
        }
        else if (ratio < 0.3f)
        {
            fillImage.color = lowStaminaColor;
            _isExhausted = false;
        }
        else
        {
            fillImage.color = normalColor;
            _isExhausted = false;
        }

        // Auto-fade paska
        if (autoHideWhenFull && mainCanvasGroup != null)
        {
            bool shouldBeVisible = ratio < 0.999f || isSprinting || isExhausted;
            float targetAlpha = shouldBeVisible ? 1f : 0f;

            if (Mathf.Abs(mainCanvasGroup.alpha - targetAlpha) > 0.05f)
            {
                _fadeTween?.Kill();
                _fadeTween = mainCanvasGroup.DOFade(targetAlpha, fadeDuration).SetUpdate(true);
            }
        }
    }

    private void TriggerExhaustedEffect()
    {
        _isExhausted = true;
        _exhaustedFlashTween?.Kill();

        if (barOutline != null)
        {
            Color origOutline = new Color(0.6f, 0.45f, 0.2f, 0.6f);
            _exhaustedFlashTween = barOutline.DOColor(Color.red, 0.12f)
                .SetLoops(4, LoopType.Yoyo)
                .OnComplete(() => barOutline.effectColor = origOutline);
        }
    }

    /// <summary>
    /// Tworzy domyślny, estetyczny retro wskaźnik po lewej stronie ekranu.
    /// </summary>
    public void BuildDefaultUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("SprintStamina_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasLayerManager.LAYER_CROSSHAIR_HUD;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Główny kontener po lewej stronie (środek-lewo)
        GameObject rootGo = new GameObject("Stamina_Container", typeof(RectTransform), typeof(CanvasGroup));
        rootGo.transform.SetParent(canvas.transform, false);
        var rootRect = rootGo.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0.5f);
        rootRect.anchorMax = new Vector2(0f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = new Vector2(40f, -40f);
        rootRect.sizeDelta = new Vector2(14f, 150f);

        mainCanvasGroup = rootGo.GetComponent<CanvasGroup>();

        // Tło paska
        GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(Outline));
        bgGo.transform.SetParent(rootGo.transform, false);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        backgroundImage = bgGo.GetComponent<Image>();
        backgroundImage.color = new Color(0.08f, 0.07f, 0.06f, 0.85f);

        barOutline = bgGo.GetComponent<Outline>();
        barOutline.effectColor = new Color(0.6f, 0.45f, 0.2f, 0.6f);
        barOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Wypełnienie paska (Pionowe od dołu do góry)
        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fRect = fillGo.GetComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero;
        fRect.anchorMax = Vector2.one;
        fRect.offsetMin = new Vector2(2f, 2f);
        fRect.offsetMax = new Vector2(-2f, -2f);

        fillImage = fillGo.GetComponent<Image>();
        fillImage.color = normalColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Vertical;
        fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        fillImage.fillAmount = 1f;
    }
}
