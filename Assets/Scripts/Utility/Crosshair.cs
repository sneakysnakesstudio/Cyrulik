using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Image crosshairImage;
    [SerializeField] private TextMeshProUGUI interactionNameText;

    [Header("Colors")]
    [SerializeField] private Color normalColor =
        new Color(0.784f, 0.784f, 0.784f, 1f); // #C8C8C8

    [SerializeField] private Color interactableColor =
        new Color(0.396f, 0.780f, 0.851f, 1f); // #65C7D9

    [SerializeField] private Color requirementMissingColor =
        new Color(0.839f, 0.659f, 0.310f, 1f); // #D6A84F

    [SerializeField] private Color successColor =
        new Color(0.451f, 0.769f, 0.467f, 1f); // #73C477

    [SerializeField] private Color blockedColor =
        new Color(0.851f, 0.361f, 0.361f, 1f); // #D95C5C

    [Header("Idle Breathing (Spokojny Oddech Kropki w Idlu)")]
    [Tooltip("Czy domyślna kropka celownika ma spokojnie oddychać w stanie spoczynku (gdy gracz nie patrzy na żaden przedmiot)?")]
    [SerializeField] private bool enableIdleBreathing = true;
    [Tooltip("Maksymalna skala powiększenia kropki w szczycie wdechu (np. 1.16x).")]
    [SerializeField] private float idleBreathScale = 1.16f;
    [Tooltip("Czas trwania połowy cyklu oddechu w sekundach (wdech/wydech np. 2.0s).")]
    [SerializeField] private float idleBreathDuration = 2.0f;

    [Header("Hover Pulse (Pulsowanie po najechaniu na obiekt)")]
    [Tooltip("Czy kropka ma pulsować po najechaniu na obiekt interaktywny?")]
    [SerializeField] private bool enableHoverPulse = true;
    [Tooltip("Maksymalna skala powiększenia kropki podczas pulsowania na obiekcie (np. 1.35x).")]
    [SerializeField] private float hoverPulseScale = 1.35f;
    [Tooltip("Czas trwania połowy cyklu pulsowania (np. 0.35s).")]
    [SerializeField] private float hoverPulseDuration = 0.35f;

    [Header("Interaction Blink")]
    [SerializeField] private float blinkScale = 1.5f;
    [SerializeField] private float blinkSmallScale = 0.9f;
    [SerializeField] private float blinkDuration = 0.07f;

    [Header("Success Interaction")]
    [SerializeField] private float successColorDuration = 0.07f;
    [SerializeField] private float successColorHoldDuration = 0.08f;
    [SerializeField] private float successColorReturnDuration = 0.15f;

    [Header("Blocked Interaction")]
    [SerializeField] private float blockedShakeDuration = 0.25f;
    [SerializeField] private float blockedShakeStrength = 8f;
    [SerializeField] private int blockedShakeVibrato = 12;
    [SerializeField] private float blockedColorReturnDuration = 0.15f;

    [Header("Blocked Audio")]
    [Tooltip("Nazwa dźwięku błędu w AudioManager (domyślnie 'error_sound').")]
    [SerializeField] private string errorSoundGroup = "error_sound";
    [SerializeField] private AudioClip customErrorClip;

    [Header("Fade")]
    [SerializeField] private float colorFadeDuration = 0.15f;
    [SerializeField] private float textFadeInDuration = 0.25f;
    [SerializeField] private float textFadeOutDuration = 0.20f;

    [Header("Font & Styling")]
    [Tooltip("Opcjonalny customowy font dla napisów interakcji (np. BarlowCondensed-SemiBold SDF dla czystego, prostego tekstu). Jeśli przypisany, nadpisuje domyślny font.")]
    [SerializeField] private TMP_FontAsset interactionFont;
    [Tooltip("Rozmiar czcionki dla napisów interakcji (domyślnie 20, zmniejszony o ~30% dla czytelnego, subtelnego wyglądu).")]
    [SerializeField] private float interactionTextFontSize = 20f;

    [Header("Transition Fade / Płynne Przejście")]
    [Tooltip("Czas trwania płynnego przejścia (fade in) w znak po najechaniu na obiekt.")]
    [SerializeField] private float transitionFadeInDuration = 0.22f;

    [Tooltip("Czas trwania płynnego powrotu do kropki po odwróceniu wzroku.")]
    [SerializeField] private float transitionFadeOutDuration = 0.18f;

    [Tooltip("Dedykowany obrazek do płynnego przenikania (crossfade) bez żadnego migotania.")]
    [SerializeField] private Image fadeTransitionImage;

    private Vector3 _defaultScale;
    private Vector2 _defaultAnchoredPosition;

    private bool _hasInteractable;

    private IInteractable _currentInteractable;

    private CanvasGroup _textCanvasGroup;
    private Tween _pulseTween;
    private Tween _scaleTween;
    private Tween _colorTween;
    private Tween _textTween;
    private Tween _interactionBlinkTween;
    private Tween _blockedTween;
    private Tween _idleBreathTween;
    private Tween _transitionTween;
    private Sprite _currentTargetSprite;

    public enum HoldVisualMode
    {
        SunburstRays, // Kropka napełnia się energią i wyłaniają się obracające promyczki słoneczka
        RingToSquare  // Kółeczko napełnia się i rozszerza w kwadratową ramkę
    }

    [Header("Hold Interaction - Styl Słoneczka / Kwadratu")]
    [Tooltip("Styl wizualny przytrzymania: Słoneczko z promyczkami (Sunburst) lub Kółko->Kwadrat.")]
    [SerializeField] private HoldVisualMode holdVisualMode = HoldVisualMode.SunburstRays;

    [Tooltip("Czy każda standardowa interakcja (np. lampka, drzwi, włącznik) ma wymagać krótkiego przytrzymania (0.5s)?")]
    [SerializeField] private bool requireHoldForStandardInteractions = true;

    [Tooltip("Domyślny czas przytrzymania (w sekundach), np. 0.5s.")]
    [SerializeField] private float defaultHoldDuration = 0.5f;

    [Header("Sunburst Hold Visuals (Słoneczko z Promyczkami)")]
    [Tooltip("Obiekt promyków słoneczka.")]
    [SerializeField] private Image sunRaysImage;
    [SerializeField] private Color sunRaysColor = new Color(1.0f, 0.86f, 0.32f, 0.95f); // Ciepły złoty
    [SerializeField] private float maxSunRaysScale = 1.45f;
    [SerializeField] private float sunRaysRotationSpeed = 100f;

    [Header("Hold Interaction (Kółeczko -> Kwadrat)")]
    [Tooltip("Pasek kołowy ładowania przytrzymania (Image Type: Filled Radial 360).")]
    [SerializeField] private Image holdProgressRing;
    public static Crosshair Instance { get; private set; }

    [Tooltip("Rozszerzająca się ramka kwadratu podczas ładowania holda.")]
    [SerializeField] private Image squareMorphFrame;

    [Header("Contextual Icons (Kontekstowe Ikony Celownika)")]
    [Tooltip("Czy celownik ma podmieniać kropkę na ikony kontekstowe (?, !, klamki itp.)? Domyślnie wyłączone – tylko kropka.")]
    [SerializeField] private bool enableContextualIcons = false;

    [Header("Crosshair Icons / Ikony Celownika")]
    [Tooltip("Domyślna kropka celownika w spoczynku.")]
    [SerializeField] private Sprite defaultDotSprite;

    [Tooltip("Ikona pytajnika [?] przy badaniu otoczenia, myślach i tajemnicach (Inspect / Thought / Krzyż).")]
    [SerializeField] private Sprite inspectQuestionSprite;

    [Tooltip("Ikona wykrzyknika [!] przy bezpośrednich akcjach, zadaniach i manipulacji otoczeniem.")]
    [SerializeField] private Sprite exclamationSprite;

    [Tooltip("Ikona wielokropka [...] przy dialogach, nasłuchiwaniu, radiu i oczekiwaniu.")]
    [SerializeField] private Sprite ellipsisSprite;

    [Tooltip("Ikona dłoni przy standardowych interakcjach [E] (włącznik, proste przedmioty).")]
    [SerializeField] private Sprite interactHandSprite;

    [Tooltip("Ikona ząbkowanego kółka zegara (HoldRing_Clockwork) przy szufladach, szafach i drzwiach.")]
    [SerializeField] private Sprite clockworkRingSprite;

    [Tooltip("Ikona kłódki przy zablokowanych interakcjach wymagających klucza.")]
    [SerializeField] private Sprite lockedKeySprite;

    [Header("Crosshair Icons — Kontekstowe")]
    [Tooltip("Ikona chwytu/podnoszenia przedmiotu (Icon_HandGrip.png).")]
    [SerializeField] private Sprite pickupHandSprite;

    [Tooltip("Ikona oka do oglądania z bliska bez podnoszenia (Icon_Eye.png).")]
    [SerializeField] private Sprite eyeSprite;

    [Tooltip("Ikona brzytwy do golenia/ostrzenia (Icon_Razor.png).")]
    [SerializeField] private Sprite razorSprite;

    [Tooltip("Ikona chmurki rozmowy z NPC (Icon_SpeechBubble.png).")]
    [SerializeField] private Sprite speechBubbleSprite;

    [Tooltip("Ikona lupy do szczegółowej inspekcji (Icon_Magnifier.png).")]
    [SerializeField] private Sprite magnifierSprite;

    [Tooltip("Ikona klucza do użycia klucza (Icon_Key.png).")]
    [SerializeField] private Sprite keySprite;

    [Header("Globalna Skala Ikon Celownika")]
    [Tooltip("Mnożnik skali dla wszystkich ikon akcji (1.0 = domyślny powiększony, 1.2+ = jeszcze większy).")]
    [Range(0.5f, 3.0f)]
    [SerializeField] private float iconScaleMultiplier = 1.0f;

    [Header("Rozmiary Ikon (Powiększone dla czytelności)")]
    [SerializeField] private Vector2 defaultDotSize       = new Vector2(10f, 10f);
    [SerializeField] private Vector2 inspectIconSize      = new Vector2(46f, 46f);
    [SerializeField] private Vector2 exclamationIconSize  = new Vector2(40f, 50f);
    [SerializeField] private Vector2 ellipsisIconSize     = new Vector2(50f, 26f);
    [SerializeField] private Vector2 interactIconSize     = new Vector2(44f, 44f);
    [SerializeField] private Vector2 clockworkIconSize    = new Vector2(52f, 52f);
    [SerializeField] private Vector2 pickupHandIconSize   = new Vector2(48f, 48f);
    [SerializeField] private Vector2 eyeIconSize          = new Vector2(52f, 36f);
    [SerializeField] private Vector2 razorIconSize        = new Vector2(50f, 50f);
    [SerializeField] private Vector2 speechBubbleIconSize = new Vector2(50f, 46f);
    [SerializeField] private Vector2 magnifierIconSize    = new Vector2(48f, 48f);
    [SerializeField] private Vector2 keyIconSize          = new Vector2(46f, 46f);

    [Header("Kolory Per-Symbol")]
    [Tooltip("Kolor ikony podnoszenia – miętowo-zielony.")]
    [SerializeField] private Color pickupColor  = new Color(0.45f, 0.87f, 0.55f, 1f);
    [Tooltip("Kolor ikony brzytwy – ciepła złota stalówka.")]
    [SerializeField] private Color razorColor   = new Color(0.95f, 0.82f, 0.35f, 1f);
    [Tooltip("Kolor chmurki dialogu – błękitno-szarawy.")]
    [SerializeField] private Color speechColor  = new Color(0.55f, 0.78f, 0.95f, 1f);
    [Tooltip("Kolor lupy i oka – liliowo-fioletowy.")]
    [SerializeField] private Color inspectColor = new Color(0.75f, 0.55f, 0.95f, 1f);

    private RectTransform _crosshairRect;
    private float _holdTimer = 0f;
    private bool _isHolding = false;
    private Tween _concussionTween;
    private Sprite _initialSprite;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void Awake()
    {
        Instance = this;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasLayerManager.LAYER_CROSSHAIR_HUD;
        }

        if (crosshairImage == null)
        {
            DevLog.LogError(
                "Crosshair: Crosshair Image nie jest przypisany!",
                this
            );

            return;
        }

        if (interactionNameText == null)
        {
            DevLog.LogError(
                "Crosshair: Interaction Name Text nie jest przypisany!",
                this
            );

            return;
        }

        _crosshairRect = crosshairImage.rectTransform;

        _initialSprite = crosshairImage.sprite;
        if (defaultDotSprite == null)
        {
            defaultDotSprite = _initialSprite;
        }

#if UNITY_EDITOR
        if (inspectQuestionSprite == null)
            inspectQuestionSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_QuestionMark.png");
        if (exclamationSprite == null)
            exclamationSprite     = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_ExclamationMark.png");
        if (ellipsisSprite == null)
            ellipsisSprite        = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Ellipsis.png");
        if (interactHandSprite == null)
            interactHandSprite    = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_HandGrip.png");
        if (pickupHandSprite == null)
            pickupHandSprite      = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_HandGrip.png");
        if (eyeSprite == null)
            eyeSprite             = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Eye.png");
        if (razorSprite == null)
            razorSprite           = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Razor.png");
        if (speechBubbleSprite == null)
            speechBubbleSprite    = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_SpeechBubble.png");
        if (magnifierSprite == null)
            magnifierSprite       = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Magnifier.png");
        if (lockedKeySprite == null)
            lockedKeySprite       = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Lock.png");
        if (keySprite == null)
            keySprite             = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Key.png");
        if (clockworkRingSprite == null)
            clockworkRingSprite   = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/HoldRing_Clockwork.png");
        if (defaultDotSprite == null)
            defaultDotSprite      = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Default_Dot.png");
#endif

        _defaultScale =
            crosshairImage.transform.localScale;

        _defaultAnchoredPosition =
            _crosshairRect.anchoredPosition;

        crosshairImage.color = normalColor;

        EnsureTextCanvasGroup();
        if (_textCanvasGroup != null)
        {
            _textCanvasGroup.alpha = 0f;
        }

        if (interactionNameText != null)
        {
            interactionNameText.text = string.Empty;
            interactionNameText.alpha = 1f;

            if (interactionFont != null)
            {
                interactionNameText.font = interactionFont;
            }

            if (interactionTextFontSize > 0f)
            {
                interactionNameText.fontSize = interactionTextFontSize;
            }
        }

        // Wymuszenie czytelnych, powiększonych rozmiarów (nadpisuje ewentualne stare małe wartości ze sceny)
        if (defaultDotSize.x < 14f) defaultDotSize = new Vector2(14f, 14f);
        if (inspectIconSize.x < 48f) inspectIconSize = new Vector2(48f, 48f);
        if (exclamationIconSize.x < 42f) exclamationIconSize = new Vector2(42f, 52f);
        if (ellipsisIconSize.x < 52f) ellipsisIconSize = new Vector2(52f, 28f);
        if (interactIconSize.x < 46f) interactIconSize = new Vector2(46f, 46f);
        if (clockworkIconSize.x < 54f) clockworkIconSize = new Vector2(54f, 54f);
        if (pickupHandIconSize.x < 50f) pickupHandIconSize = new Vector2(50f, 50f);
        if (eyeIconSize.x < 54f) eyeIconSize = new Vector2(54f, 38f);
        if (razorIconSize.x < 52f) razorIconSize = new Vector2(52f, 52f);
        if (speechBubbleIconSize.x < 52f) speechBubbleIconSize = new Vector2(52f, 48f);
        if (magnifierIconSize.x < 50f) magnifierIconSize = new Vector2(50f, 50f);
        if (keyIconSize.x < 48f) keyIconSize = new Vector2(48f, 48f);

        if (iconScaleMultiplier < 1.15f)
        {
            iconScaleMultiplier = 1.25f;
        }

        _currentTargetSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;

        if (!enableContextualIcons && defaultDotSprite != null && crosshairImage != null)
        {
            crosshairImage.sprite = defaultDotSprite;
            _crosshairRect.sizeDelta = defaultDotSize;
        }

        StartIdleBreathing();
    }

    private void EnsureHoldUI()
    {
        // 1. Promyczki Słoneczka (Sunburst Rays)
        if (sunRaysImage == null && crosshairImage != null)
        {
            Transform existingSun = crosshairImage.transform.parent.Find("SunRaysImage");
            if (existingSun != null)
            {
                sunRaysImage = existingSun.GetComponent<Image>();
            }
            else
            {
                GameObject sunGo = new GameObject("SunRaysImage", typeof(RectTransform), typeof(Image));
                sunGo.transform.SetParent(crosshairImage.transform.parent, false);
                sunGo.transform.position = crosshairImage.transform.position;

                var sunRect = sunGo.GetComponent<RectTransform>();
                sunRect.sizeDelta = new Vector2(36f, 36f);
                sunRect.anchoredPosition = Vector2.zero;

                sunRaysImage = sunGo.GetComponent<Image>();
                sunRaysImage.color = new Color(sunRaysColor.r, sunRaysColor.g, sunRaysColor.b, 0f);
                sunRaysImage.raycastTarget = false;
                sunGo.SetActive(false);
            }
        }

        // 2. Pierścień ładowania (Hold Progress Ring)
        if (holdProgressRing == null && crosshairImage != null)
        {
            GameObject ringGo = new GameObject("HoldProgressRing", typeof(RectTransform), typeof(Image));
            ringGo.transform.SetParent(crosshairImage.transform.parent, false);
            ringGo.transform.position = crosshairImage.transform.position;

            var rRect = ringGo.GetComponent<RectTransform>();
            rRect.sizeDelta = new Vector2(28f, 28f);

            holdProgressRing = ringGo.GetComponent<Image>();
            holdProgressRing.color = new Color(0.95f, 0.8f, 0.35f, 0.95f);
            holdProgressRing.type = Image.Type.Filled;
            holdProgressRing.fillMethod = Image.FillMethod.Radial360;
            holdProgressRing.fillOrigin = (int)Image.Origin360.Top;
            holdProgressRing.fillClockwise = true;
            holdProgressRing.fillAmount = 0f;
            holdProgressRing.raycastTarget = false;
        }

        // 3. Kwadratowa ramka (Square Morph Frame)
        if (squareMorphFrame == null && crosshairImage != null)
        {
            GameObject sqGo = new GameObject("SquareMorphFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
            sqGo.transform.SetParent(crosshairImage.transform.parent, false);
            sqGo.transform.position = crosshairImage.transform.position;

            var sqRect = sqGo.GetComponent<RectTransform>();
            sqRect.sizeDelta = new Vector2(20f, 20f);

            squareMorphFrame = sqGo.GetComponent<Image>();
            squareMorphFrame.color = new Color(1f, 1f, 1f, 0f);
            squareMorphFrame.raycastTarget = false;

            var outline = sqGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.8f, 0.35f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            squareMorphFrame.gameObject.SetActive(false);
        }

        // 4. Obrazek do płynnego przejścia fade-in (Fade Transition Image)
        if (fadeTransitionImage == null && crosshairImage != null)
        {
            Transform existingFade = crosshairImage.transform.parent.Find("CrosshairFadeTransition");
            if (existingFade != null)
            {
                fadeTransitionImage = existingFade.GetComponent<Image>();
            }
            else
            {
                GameObject fadeGo = new GameObject("CrosshairFadeTransition", typeof(RectTransform), typeof(Image));
                fadeGo.transform.SetParent(crosshairImage.transform.parent, false);
                fadeGo.transform.SetSiblingIndex(crosshairImage.transform.GetSiblingIndex() + 1);
                fadeGo.transform.position = crosshairImage.transform.position;

                var fRect = fadeGo.GetComponent<RectTransform>();
                fRect.sizeDelta = defaultDotSize;
                fRect.anchoredPosition = _defaultAnchoredPosition;

                fadeTransitionImage = fadeGo.GetComponent<Image>();
                fadeTransitionImage.color = new Color(1f, 1f, 1f, 0f);
                fadeTransitionImage.raycastTarget = false;
                fadeGo.SetActive(false);
            }
        }
    }

    private void OnEnable()
    {
        StartIdleBreathing();

        if (playerMovement == null)
            return;

        playerMovement.OnInteractableChanged +=
            HandleInteractableChanged;

        playerMovement.OnInteractionPerformed +=
            HandleInteractionPerformed;

        playerMovement.OnInteractionBlocked +=
            HandleInteractionBlocked;
    }

    private void OnDisable()
    {
        StopIdleBreathing(true);

        if (playerMovement != null)
        {
            playerMovement.OnInteractableChanged -=
                HandleInteractableChanged;

            playerMovement.OnInteractionPerformed -=
                HandleInteractionPerformed;

            playerMovement.OnInteractionBlocked -=
                HandleInteractionBlocked;
        }

        KillTweens();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        KillTweens();
    }

    private void HandleInteractableChanged(
        IInteractable interactable
    )
    {
        _currentInteractable = interactable;

        _hasInteractable =
            interactable != null;

        if (_hasInteractable)
        {
            ShowInteractable(interactable);
        }
        else
        {
            HideInteractable();
        }
    }

    private void ShowInteractable(
        IInteractable interactable
    )
    {
        _colorTween?.Kill();
        _textTween?.Kill();
        _scaleTween?.Kill();
        _pulseTween?.Kill();
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();

        StopIdleBreathing(false);

        _crosshairRect.anchoredPosition =
            _defaultAnchoredPosition;

        Color targetColor =
            GetCurrentInteractionColor();

        if (enableContextualIcons)
        {
            // Płynne przejście w odpowiedni znak (?, !, ...) z miękkim fade-in
            TransitionToInteractableIcon(interactable, targetColor);
        }
        else
        {
            // Kropka pozostaje zawsze kropką – brak podmiany na ikony/klamki
            Sprite dotSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
            if (crosshairImage != null)
            {
                if (dotSprite != null && crosshairImage.sprite != dotSprite)
                {
                    crosshairImage.sprite = dotSprite;
                }
                _crosshairRect.sizeDelta = defaultDotSize;

                _colorTween = crosshairImage
                    .DOColor(targetColor, colorFadeDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(crosshairImage.gameObject, LinkBehaviour.KillOnDestroy);
            }

            StartHoverPulse();
        }

        AnimateInteractionText(interactable != null ? interactable.InteractionName : null);
    }

    private void TransitionToInteractableIcon(IInteractable interactable, Color targetColor)
    {
        if (crosshairImage == null) return;

        if (!enableContextualIcons)
        {
            Sprite dot = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
            if (crosshairImage.sprite != dot && dot != null)
            {
                crosshairImage.sprite = dot;
            }
            _crosshairRect.sizeDelta = defaultDotSize;
            _colorTween?.Kill();
            _colorTween = crosshairImage
                .DOColor(targetColor, colorFadeDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(crosshairImage.gameObject, LinkBehaviour.KillOnDestroy);
            StartHoverPulse();
            return;
        }

        Sprite targetSprite;
        Vector2 targetSize;
        GetSymbolInfo(interactable, out targetSprite, out targetSize);

        if (targetSprite == null)
        {
            targetSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
            targetSize = defaultDotSize;
        }

        // Jeśli już wyświetlamy ten symbol lub jesteśmy w trakcie jego pokazywania,
        // aktualizujemy tylko kolor bez restartowania całego przejścia (zapobiega migotaniu)
        if (_currentTargetSprite == targetSprite)
        {
            _colorTween?.Kill();
            _colorTween = crosshairImage
                .DOColor(targetColor, colorFadeDuration)
                .SetLink(crosshairImage.gameObject, LinkBehaviour.KillOnDestroy);
            return;
        }

        _currentTargetSprite = targetSprite;

        // Płynne dwuwarstwowe przejście fade-in (dissolve) bez nagłego przeskoku i bez mrugania
        PerformCrossfade(targetSprite, targetSize, targetColor, transitionFadeInDuration);
    }

    public void GetSymbolInfo(IInteractable interactable, out Sprite targetSprite, out Vector2 targetSize)
    {
        if (!enableContextualIcons)
        {
            targetSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
            targetSize = defaultDotSize;
            return;
        }

        ReticleSymbolType symbolType = ReticleSymbolType.Auto;

        if (interactable is ICrosshairSymbolProvider provider)
        {
            symbolType = provider.CrosshairSymbol;
        }

        // ── Zablokowane: zawsze kłódka, priorytet ponad reszta ──────────────
        bool isLocked = (interactable is IConditionalInteractable cond && !cond.CanInteract);
        if (isLocked)
        {
            targetSprite = lockedKeySprite != null ? lockedKeySprite : defaultDotSprite;
            targetSize   = interactIconSize;
            return;
        }

        if (symbolType == ReticleSymbolType.Auto)
        {
            symbolType = DetectSymbolType(interactable);
        }

        switch (symbolType)
        {
            // ── Podnoszenie przedmiotu ──────────────────────────────────────
            case ReticleSymbolType.PickupHand:
                targetSprite = pickupHandSprite != null ? pickupHandSprite
                             : (interactHandSprite != null ? interactHandSprite : defaultDotSprite);
                targetSize   = pickupHandIconSize;
                break;

            // ── Interakcja dłonią (ogólna) ─────────────────────────────────
            case ReticleSymbolType.Hand:
                targetSprite = interactHandSprite != null ? interactHandSprite : defaultDotSprite;
                targetSize   = interactIconSize;
                break;

            // ── Tylko patrzenie / oglądanie (bez podnoszenia) ──────────────
            case ReticleSymbolType.Eye:
                targetSprite = eyeSprite != null ? eyeSprite
                             : (inspectQuestionSprite != null ? inspectQuestionSprite : defaultDotSprite);
                targetSize   = eyeIconSize;
                break;

            // ── Szczegółowa inspekcja z lupą ───────────────────────────────
            case ReticleSymbolType.Magnifier:
                targetSprite = magnifierSprite != null ? magnifierSprite
                             : (inspectQuestionSprite != null ? inspectQuestionSprite : defaultDotSprite);
                targetSize   = magnifierIconSize;
                break;

            // ── Pytajnik [?] – badanie, myśli, tajemnice ───────────────────
            case ReticleSymbolType.QuestionMark:
                targetSprite = inspectQuestionSprite != null ? inspectQuestionSprite : defaultDotSprite;
                targetSize   = inspectIconSize;
                break;

            // ── Golenie / brzytwa ───────────────────────────────────────────
            case ReticleSymbolType.Razor:
                targetSprite = razorSprite != null ? razorSprite
                             : (exclamationSprite != null ? exclamationSprite : defaultDotSprite);
                targetSize   = razorIconSize;
                break;

            // ── Dialog z NPC ────────────────────────────────────────────────
            case ReticleSymbolType.SpeechBubble:
                targetSprite = speechBubbleSprite != null ? speechBubbleSprite
                             : (ellipsisSprite != null ? ellipsisSprite : defaultDotSprite);
                targetSize   = speechBubbleIconSize;
                break;

            // ── Wielokropek [...] ────────────────────────────────────────────
            case ReticleSymbolType.Ellipsis:
                targetSprite = ellipsisSprite != null ? ellipsisSprite
                             : (inspectQuestionSprite != null ? inspectQuestionSprite : defaultDotSprite);
                targetSize   = ellipsisIconSize;
                break;

            // ── Klucz ────────────────────────────────────────────────────────
            case ReticleSymbolType.Key:
                targetSprite = keySprite != null ? keySprite
                             : (lockedKeySprite != null ? lockedKeySprite : defaultDotSprite);
                targetSize   = keyIconSize;
                break;

            // ── Kłódka (zablokowane przez skrypt zewnętrzny) ────────────────
            case ReticleSymbolType.Lock:
                targetSprite = lockedKeySprite != null ? lockedKeySprite : defaultDotSprite;
                targetSize   = interactIconSize;
                break;

            // ── Wykrzyknik [!] – akcje bezpośrednie ─────────────────────────
            case ReticleSymbolType.ExclamationMark:
                targetSprite = exclamationSprite != null ? exclamationSprite
                             : (interactHandSprite != null ? interactHandSprite : defaultDotSprite);
                targetSize   = exclamationIconSize;
                break;

            // ── Domyślna kropka ──────────────────────────────────────────────
            case ReticleSymbolType.Dot:
            default:
                targetSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
                targetSize   = defaultDotSize;
                break;
        }

        // Skalowanie globalne z Inspektora (dla wszystkich ikon oprócz domyślnej małej kropki)
        if (symbolType != ReticleSymbolType.Dot)
        {
            targetSize *= Mathf.Max(0.2f, iconScaleMultiplier);
        }
    }

    private ReticleSymbolType DetectSymbolType(IInteractable interactable)
    {
        if (interactable == null) return ReticleSymbolType.Dot;

        string name = interactable.InteractionName ?? string.Empty;

        // ── 1. Podnoszenie przedmiotu z ziemi/blatu ──────────────────────────
        bool isPickup = (interactable is PickupItem) ||
                        name.IndexOf("Pick up",   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Podnieś",   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Take",      System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Grab",      System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (isPickup) return ReticleSymbolType.PickupHand;

        // ── 2. Golenie / brzytwa ─────────────────────────────────────────────
        bool isRazor = (interactable is RazorMinigame) ||
                       (interactable is RazorStropInteractable) ||
                       name.IndexOf("Shave",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("Sharpen",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("Strop",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("Golenie",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("Brzytwa",  System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (isRazor) return ReticleSymbolType.Razor;

        // ── 3. Dialog / rozmowa z NPC ────────────────────────────────────────
        bool isNpcTalk = (interactable is CustomerJurek) ||
                         name.IndexOf("Talk",      System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Speak",     System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Jurek",     System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Give",      System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Podaj",     System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Klient",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Client",    System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (isNpcTalk) return ReticleSymbolType.SpeechBubble;

        // ── 4. Radio / dźwięk / nasłuch / czekanie ──────────────────────────
        bool isListening = (interactable is RadioInteractable) ||
                           name.Contains("...") ||
                           name.IndexOf("Radio",     System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Listen",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Słuchaj",   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Posłuchaj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Dialog",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Czekaj",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("Wait",      System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (isListening) return ReticleSymbolType.Ellipsis;

        // ── 5. Inspect / Thought / oglądanie bez podnoszenia ────────────────
        bool isInspect = (interactable is InspectThoughtInteractable) ||
                         (interactable is CrucifixInteractable) ||
                         name.Contains("?") ||
                         name.IndexOf("Look",      System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Examine",   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Inspect",   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Zbadaj",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Spójrz",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Obejrzyj",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Read",      System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Czytaj",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Myśl",      System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("Note",      System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (isInspect) return ReticleSymbolType.QuestionMark;

        // ── 6. Fallback – akcja bezpośrednia (!, drzwi, piec, szuflada…) ────
        return ReticleSymbolType.ExclamationMark;
    }

    private void PerformCrossfade(Sprite newSprite, Vector2 newSize, Color newColor, float duration)
    {
        _transitionTween?.Kill();
        _colorTween?.Kill();
        _scaleTween?.Kill();

        if (crosshairImage == null) return;

        // Natychmiast podmieniamy sprite bez wygaszania (brak migotania / stroboskopu)
        crosshairImage.sprite = newSprite;

        // Płynna, solidna transformacja rozmiaru i koloru BEZ znikania kropki
        Sequence seq = DOTween.Sequence();
        seq.Join(_crosshairRect.DOSizeDelta(newSize, duration).SetEase(Ease.OutQuad));
        seq.Join(crosshairImage.DOColor(newColor, duration).SetEase(Ease.OutQuad));
        seq.Join(crosshairImage.transform.DOScale(_defaultScale, duration).SetEase(Ease.OutQuad));
        seq.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _transitionTween = seq;
    }

    private void HideInteractable()
    {
        _currentInteractable = null;

        StopHoverPulse(false);
        _scaleTween?.Kill();
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();
        _colorTween?.Kill();
        _textTween?.Kill();

        _interactionBlinkTween = null;
        _blockedTween = null;

        _crosshairRect.anchoredPosition =
            _defaultAnchoredPosition;

        Sprite dotSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
        _currentTargetSprite = dotSprite;

        // Płynne przejście z powrotem do kropki
        if (crosshairImage != null)
        {
            if (enableContextualIcons)
            {
                PerformCrossfade(dotSprite, defaultDotSize, normalColor, transitionFadeOutDuration);
            }
            else
            {
                if (crosshairImage.sprite != dotSprite && dotSprite != null)
                {
                    crosshairImage.sprite = dotSprite;
                }
                _crosshairRect.sizeDelta = defaultDotSize;

                _colorTween = crosshairImage
                    .DOColor(normalColor, colorFadeDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(crosshairImage.gameObject, LinkBehaviour.KillOnDestroy);

                _scaleTween = crosshairImage.transform
                    .DOScale(_defaultScale, colorFadeDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(crosshairImage.gameObject, LinkBehaviour.KillOnDestroy);
            }
        }

        AnimateInteractionText(null);
    }

    private void AnimateInteractionText(string newText)
    {
        if (interactionNameText == null) return;
        EnsureTextCanvasGroup();

        _textTween?.Kill();

        if (string.IsNullOrEmpty(newText))
        {
            // Płynne wygaszanie (Fade Out)
            if (_textCanvasGroup != null)
            {
                if (_textCanvasGroup.alpha <= 0.001f)
                {
                    interactionNameText.text = string.Empty;
                    return;
                }

                _textTween = _textCanvasGroup
                    .DOFade(0f, textFadeOutDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (interactionNameText != null)
                        {
                            interactionNameText.text = string.Empty;
                        }
                    })
                    .SetLink(interactionNameText.gameObject, LinkBehaviour.KillOnDestroy);
            }
            return;
        }

        // Jeśli ten sam tekst jest już w pełni widoczny, nie przerywaj animacji
        if (interactionNameText.text == newText && _textCanvasGroup != null && _textCanvasGroup.alpha >= 0.98f)
        {
            return;
        }

        if (_textCanvasGroup != null)
        {
            // Jeśli patrzymy z jednego obiektu na inny bez przerwy na pustą przestrzeń
            if (_textCanvasGroup.alpha > 0.1f && interactionNameText.text != newText && !string.IsNullOrEmpty(interactionNameText.text))
            {
                // Szybki crossfade (0.08s ściemnienie -> podmiana -> płynne rozjaśnienie nowego)
                _textTween = _textCanvasGroup
                    .DOFade(0f, 0.08f)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (interactionNameText != null)
                        {
                            interactionNameText.text = newText;
                            _textTween = _textCanvasGroup
                                .DOFade(1f, textFadeInDuration)
                                .SetEase(Ease.OutQuad)
                                .SetUpdate(true)
                                .SetLink(interactionNameText.gameObject, LinkBehaviour.KillOnDestroy);
                        }
                    })
                    .SetLink(interactionNameText.gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                // Płynne wejście od zera (Fade In)
                interactionNameText.text = newText;
                if (_textCanvasGroup.alpha <= 0.05f)
                {
                    _textCanvasGroup.alpha = 0f;
                }

                _textTween = _textCanvasGroup
                    .DOFade(1f, textFadeInDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(interactionNameText.gameObject, LinkBehaviour.KillOnDestroy);
            }
        }
        else
        {
            interactionNameText.text = newText;
        }
    }

    private void EnsureTextCanvasGroup()
    {
        if (_textCanvasGroup == null && interactionNameText != null)
        {
            _textCanvasGroup = interactionNameText.GetComponent<CanvasGroup>();
            if (_textCanvasGroup == null)
            {
                _textCanvasGroup = interactionNameText.gameObject.AddComponent<CanvasGroup>();
            }
            _textCanvasGroup.blocksRaycasts = false;
            _textCanvasGroup.interactable = false;
        }

        // Po wygaszeniu symbolu kropka spokojnie wznawia oddychanie w idlu
        float delay = enableContextualIcons ? transitionFadeOutDuration : colorFadeDuration;
        DOVirtual.DelayedCall(delay, () =>
        {
            if (!_hasInteractable && !_isHolding)
            {
                StartIdleBreathing();
            }
        }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void StartIdleBreathing()
    {
        if (!enableIdleBreathing || _hasInteractable || _isHolding)
            return;

        _idleBreathTween?.Kill();

        if (crosshairImage != null)
        {
            crosshairImage.transform.localScale = _defaultScale;

            _idleBreathTween = crosshairImage.transform
                .DOScale(_defaultScale * idleBreathScale, idleBreathDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(crosshairImage.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void StopIdleBreathing(bool resetScale = false)
    {
        _idleBreathTween?.Kill();
        _idleBreathTween = null;

        if (resetScale && crosshairImage != null)
        {
            crosshairImage.transform.localScale = _defaultScale;
        }
    }

    private void StartHoverPulse()
    {
        if (!enableHoverPulse || crosshairImage == null)
            return;

        _pulseTween?.Kill();
        crosshairImage.transform.localScale = _defaultScale;

        _pulseTween = crosshairImage.transform
            .DOScale(_defaultScale * hoverPulseScale, hoverPulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(crosshairImage.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void StopHoverPulse(bool resetScale = true)
    {
        _pulseTween?.Kill();
        _pulseTween = null;

        if (resetScale && crosshairImage != null)
        {
            crosshairImage.transform.DOScale(_defaultScale, 0.15f)
                .SetEase(Ease.OutQuad)
                .SetLink(crosshairImage.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void StartPulse()
    {
        StartHoverPulse();
    }

    private void HandleInteractionPerformed()
    {
        if (!_hasInteractable)
            return;

        _pulseTween?.Kill();
        _scaleTween?.Kill();
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();
        _colorTween?.Kill();

        _pulseTween = null;

        Sequence blinkSequence =
            DOTween.Sequence();

        // Zielony = akcja wykonana poprawnie.
        blinkSequence.Append(
            crosshairImage
                .DOColor(
                    successColor,
                    successColorDuration
                )
                .SetEase(Ease.OutQuad)
        );

        // Powiększenie kropki.
        blinkSequence.Join(
            crosshairImage.transform
                .DOScale(
                    _defaultScale * blinkScale,
                    blinkDuration
                )
                .SetEase(Ease.OutQuad)
        );

        blinkSequence.Append(
            crosshairImage.transform
                .DOScale(
                    _defaultScale * blinkSmallScale,
                    blinkDuration
                )
                .SetEase(Ease.InOutQuad)
        );

        blinkSequence.Append(
            crosshairImage.transform
                .DOScale(
                    _defaultScale,
                    blinkDuration
                )
                .SetEase(Ease.OutQuad)
        );

        blinkSequence.AppendInterval(
            successColorHoldDuration
        );

        // Po sukcesie wraca do aktualnego koloru interakcji.
        blinkSequence.Append(
            crosshairImage
                .DOColor(
                    GetCurrentInteractionColor(),
                    successColorReturnDuration
                )
                .SetEase(Ease.OutQuad)
        );

        blinkSequence.OnComplete(() =>
        {
            _interactionBlinkTween = null;

            if (_hasInteractable && !_isHolding)
            {
                StartHoverPulse();
            }
            else if (!_hasInteractable && !_isHolding)
            {
                StartIdleBreathing();
            }
        });

        blinkSequence.SetLink(
            crosshairImage.gameObject,
            LinkBehaviour.KillOnDestroy
        );

        _interactionBlinkTween =
            blinkSequence;
    }

    private void HandleInteractionBlocked(string blockedMessage)
    {
        if (!_hasInteractable)
            return;

        // Odtwarzanie dźwięku błędu z AudioManager (lub custom clip)
        PlayErrorSound();

        _pulseTween?.Kill();
        _scaleTween?.Kill();
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();
        _colorTween?.Kill();

        _pulseTween = null;

        crosshairImage.transform.localScale =
            _defaultScale;

        _crosshairRect.anchoredPosition =
            _defaultAnchoredPosition;

        Sequence blockedSequence =
            DOTween.Sequence();

        // Czerwony = gracz spróbował wykonać akcję,
        // ale akcja się nie udała.
        blockedSequence.Append(
            crosshairImage
                .DOColor(
                    blockedColor,
                    0.05f
                )
        );

        // Jednocześnie shake.
        blockedSequence.Join(
            _crosshairRect
                .DOShakeAnchorPos(
                    blockedShakeDuration,
                    new Vector2(
                        blockedShakeStrength,
                        0f
                    ),
                    blockedShakeVibrato,
                    0f,
                    false,
                    true
                )
        );

        // Po błędzie wraca do koloru wynikającego
        // z aktualnego stanu obiektu.
        blockedSequence.Append(
            crosshairImage
                .DOColor(
                    GetCurrentInteractionColor(),
                    blockedColorReturnDuration
                )
                .SetEase(Ease.OutQuad)
        );

        blockedSequence.OnComplete(() =>
        {
            _blockedTween = null;

            _crosshairRect.anchoredPosition =
                _defaultAnchoredPosition;

            if (_hasInteractable && !_isHolding)
            {
                StartHoverPulse();
            }
            else if (!_hasInteractable && !_isHolding)
            {
                StartIdleBreathing();
            }
        });

        blockedSequence.SetLink(
            crosshairImage.gameObject,
            LinkBehaviour.KillOnDestroy
        );

        _blockedTween =
            blockedSequence;
    }

    private void PlayErrorSound()
    {
        // 1. Zawsze odtwarzaj przez AudioManager (zgodnie z bazą AudioDatabaseSO i jej ustawieniami Pitch/Volume)
        if (AudioManager.Instance != null)
        {
            string soundName = !string.IsNullOrEmpty(errorSoundGroup) ? errorSoundGroup : "error_sound";
            AudioManager.Instance.Play(soundName);
            return;
        }

        // 2. Fallback gdyby AudioManager nie istniał
        if (customErrorClip != null)
        {
            AudioSource.PlayClipAtPoint(customErrorClip, Camera.main != null ? Camera.main.transform.position : transform.position);
        }
    }

    private Color GetCurrentInteractionColor()
    {
        if (_currentInteractable == null)
            return normalColor;

        // Zablokowane – kolor ostrzeżenia (pomarańczowy)
        if (_currentInteractable is IConditionalInteractable cond && !cond.CanInteract)
            return requirementMissingColor;

        // Pobierz aktualny symbol żeby dobrać kolor
        ReticleSymbolType sym = ReticleSymbolType.Auto;
        if (_currentInteractable is ICrosshairSymbolProvider provider)
            sym = provider.CrosshairSymbol;
        if (sym == ReticleSymbolType.Auto)
            sym = DetectSymbolType(_currentInteractable);

        switch (sym)
        {
            case ReticleSymbolType.PickupHand:
            case ReticleSymbolType.Hand:
                return pickupColor;

            case ReticleSymbolType.Razor:
                return razorColor;

            case ReticleSymbolType.SpeechBubble:
            case ReticleSymbolType.Ellipsis:
                return speechColor;

            case ReticleSymbolType.Eye:
            case ReticleSymbolType.Magnifier:
            case ReticleSymbolType.QuestionMark:
                return inspectColor;

            case ReticleSymbolType.Lock:
                return requirementMissingColor;

            default:
                return interactableColor;
        }
    }

    private void Update()
    {
        HandleHoldProgress();
    }

    private void HandleHoldProgress()
    {
        bool isHoldCandidate = false;
        float holdDuration = defaultHoldDuration;

        if (_currentInteractable is IHoldInteractable holdInteractable && holdInteractable.RequiresHold)
        {
            isHoldCandidate = true;
            holdDuration = holdInteractable.HoldDuration;
        }
        else if (_hasInteractable && requireHoldForStandardInteractions)
        {
            // Standardowa interakcja z przytrzymaniem (np. lampka, drzwi, włącznik)
            isHoldCandidate = true;
            holdDuration = defaultHoldDuration;
        }

        if (isHoldCandidate)
        {
            bool isPressing = false;
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                isPressing = UnityEngine.InputSystem.Keyboard.current.eKey.isPressed;
            }
            if (!isPressing && UnityEngine.InputSystem.Mouse.current != null)
            {
                isPressing = UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
            }

            if (isPressing)
            {
                if (!_isHolding)
                {
                    _isHolding = true;
                    StopIdleBreathing(false);
                    StopHoverPulse(false);
                    _transitionTween?.Kill();
                    if (fadeTransitionImage != null)
                    {
                        fadeTransitionImage.gameObject.SetActive(false);
                    }
                }
                _holdTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_holdTimer / Mathf.Max(0.05f, holdDuration));

                bool isClockworkTarget = enableContextualIcons && (
                                         (crosshairImage != null && crosshairImage.sprite == clockworkRingSprite) ||
                                         (_currentInteractable is DoorInteractable));

                if (isClockworkTarget)
                {
                    // ─── TRYB ZĄBKOWANEGO ZEGARA (Obrót o 360 stopni przy szufladach/szafach) ───
                    if (crosshairImage != null)
                    {
                        // Kółeczko obraca się dokładnie o 360 stopni i lekko pulsuje jak nakręcany mechanizm
                        crosshairImage.transform.localEulerAngles = new Vector3(0f, 0f, -progress * 360f);
                        crosshairImage.transform.localScale = Vector3.Lerp(_defaultScale, _defaultScale * 1.25f, progress);
                        crosshairImage.color = Color.Lerp(GetCurrentInteractionColor(), sunRaysColor, progress);
                    }
                }
                else if (holdVisualMode == HoldVisualMode.SunburstRays)
                {
                    // ─── TRYB SŁONECZKA Z PROMYCZKAMI ───
                    if (sunRaysImage != null)
                    {
                        sunRaysImage.gameObject.SetActive(true);
                        sunRaysImage.color = new Color(sunRaysColor.r, sunRaysColor.g, sunRaysColor.b, Mathf.Pow(progress, 1.2f) * 0.95f);
                        sunRaysImage.transform.localScale = Vector3.Lerp(Vector3.one * 0.15f, Vector3.one * maxSunRaysScale, progress);
                        sunRaysImage.transform.Rotate(0f, 0f, -sunRaysRotationSpeed * Time.deltaTime);
                    }

                    if (crosshairImage != null)
                    {
                        // Kropka rośnie i jaśnieje słonecznym blaskiem
                        crosshairImage.transform.localScale = Vector3.Lerp(_defaultScale, _defaultScale * 1.45f, progress);
                        crosshairImage.color = Color.Lerp(GetCurrentInteractionColor(), sunRaysColor, progress);
                    }
                }
                else
                {
                    // ─── TRYB KÓŁECZKO -> KWADRAT ───
                    if (holdProgressRing != null)
                    {
                        holdProgressRing.gameObject.SetActive(true);
                        holdProgressRing.fillAmount = progress;
                    }

                    if (squareMorphFrame != null)
                    {
                        squareMorphFrame.gameObject.SetActive(true);
                        squareMorphFrame.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.35f, progress);
                        squareMorphFrame.color = new Color(1f, 1f, 1f, progress * 0.9f);
                    }

                    if (crosshairImage != null)
                    {
                        crosshairImage.transform.localScale = Vector3.Lerp(_defaultScale, _defaultScale * 0.3f, progress);
                    }
                }

                // Sukces po pełnym obrocie 360° / napełnieniu (100%)
                if (progress >= 1f)
                {
                    _holdTimer = 0f;
                    _isHolding = false;

                    // Mechaniczny rozbłysk / kliknięcie
                    if (crosshairImage != null)
                    {
                        crosshairImage.transform.DOPunchScale(Vector3.one * 0.45f, 0.22f, 8, 1);
                    }

                    if (sunRaysImage != null && holdVisualMode == HoldVisualMode.SunburstRays && !isClockworkTarget)
                    {
                        sunRaysImage.transform.DOPunchScale(Vector3.one * 0.45f, 0.22f, 8, 1);
                    }

                    ResetHoldVisuals();

                    if (playerMovement != null)
                    {
                        playerMovement.PerformInteraction();
                    }
                }
            }
            else if (_isHolding)
            {
                _holdTimer = 0f;
                _isHolding = false;
                ResetHoldVisuals();
            }
        }
        else
        {
            if (_isHolding)
            {
                _holdTimer = 0f;
                _isHolding = false;
                ResetHoldVisuals();
            }
        }
    }

    private void ResetHoldVisuals()
    {
        if (fadeTransitionImage != null)
        {
            fadeTransitionImage.gameObject.SetActive(false);
        }

        if (sunRaysImage != null)
        {
            sunRaysImage.color = new Color(sunRaysColor.r, sunRaysColor.g, sunRaysColor.b, 0f);
            sunRaysImage.transform.localScale = Vector3.zero;
            sunRaysImage.gameObject.SetActive(false);
        }

        if (holdProgressRing != null)
        {
            holdProgressRing.fillAmount = 0f;
            holdProgressRing.gameObject.SetActive(false);
        }

        if (squareMorphFrame != null)
        {
            squareMorphFrame.transform.localScale = Vector3.zero;
            squareMorphFrame.gameObject.SetActive(false);
        }

        if (crosshairImage != null)
        {
            crosshairImage.transform.localScale = _defaultScale;
            crosshairImage.transform.localRotation = Quaternion.identity;
            crosshairImage.color = GetCurrentInteractionColor();
        }

        if (_hasInteractable && !_isHolding)
        {
            StartHoverPulse();
        }
        else if (!_hasInteractable && !_isHolding)
        {
            StartIdleBreathing();
        }
    }

    /// <summary>
    /// Efekt wstrząsu / uderzenia obuchem — kropka celownika drży i trzęsie się na ekranie.
    /// </summary>
    /// <param name="duration">Czas trwania trzęsienia w sekundach.</param>
    /// <param name="strength">Siła przesunięcia kropki w pikselach.</param>
    /// <param name="vibrato">Częstotliwość drgań.</param>
    public void PlayConcussionShake(float duration = 1.5f, float strength = 14f, int vibrato = 25)
    {
        if (_crosshairRect == null) return;

        _concussionTween?.Kill();
        _blockedTween?.Kill();

        _crosshairRect.anchoredPosition = _defaultAnchoredPosition;
        _concussionTween = _crosshairRect
            .DOShakeAnchorPos(duration, new Vector2(strength, strength * 0.75f), vibrato, 90, false, true)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                if (_crosshairRect != null)
                {
                    _crosshairRect.anchoredPosition = _defaultAnchoredPosition;
                }
            });
    }

    private void KillTweens()
    {
        _pulseTween?.Kill();
        _idleBreathTween?.Kill();
        _transitionTween?.Kill();
        _scaleTween?.Kill();
        _colorTween?.Kill();
        _textTween?.Kill();
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();
        _concussionTween?.Kill();

        _pulseTween = null;
        _idleBreathTween = null;
        _transitionTween = null;
        _scaleTween = null;
        _colorTween = null;
        _textTween = null;
        _interactionBlinkTween = null;
        _blockedTween = null;
        _concussionTween = null;
    }
}