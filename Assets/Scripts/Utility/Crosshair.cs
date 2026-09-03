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
    [SerializeField] private float textFadeInDuration = 0.2f;
    [SerializeField] private float textFadeOutDuration = 0.15f;

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

    [Header("Rozmiary Ikon")]
    [SerializeField] private Vector2 defaultDotSize = new Vector2(8f, 8f);
    [SerializeField] private Vector2 inspectIconSize = new Vector2(20f, 20f);
    [SerializeField] private Vector2 exclamationIconSize = new Vector2(18f, 22f);
    [SerializeField] private Vector2 ellipsisIconSize = new Vector2(22f, 12f);
    [SerializeField] private Vector2 interactIconSize = new Vector2(18f, 18f);
    [SerializeField] private Vector2 clockworkIconSize = new Vector2(24f, 24f);

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
            Debug.LogError(
                "Crosshair: Crosshair Image nie jest przypisany!",
                this
            );

            return;
        }

        if (interactionNameText == null)
        {
            Debug.LogError(
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
        {
            inspectQuestionSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_QuestionMark.png");
        }
        if (exclamationSprite == null)
        {
            exclamationSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_ExclamationMark.png");
        }
        if (ellipsisSprite == null)
        {
            ellipsisSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI_HoldIcons/Icon_Ellipsis.png");
        }
#endif

        _defaultScale =
            crosshairImage.transform.localScale;

        _defaultAnchoredPosition =
            _crosshairRect.anchoredPosition;

        crosshairImage.color = normalColor;

        interactionNameText.text = string.Empty;
        interactionNameText.alpha = 0f;

        EnsureHoldUI();

        _currentTargetSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
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
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();

        StopIdleBreathing(false);

        _crosshairRect.anchoredPosition =
            _defaultAnchoredPosition;

        interactionNameText.text =
            interactable.InteractionName;

        Color targetColor =
            GetCurrentInteractionColor();

        // Płynne przejście w odpowiedni znak (?, !, ...) z miękkim fade-in
        TransitionToInteractableIcon(interactable, targetColor);

        interactionNameText.alpha = 0f;

        _textTween = interactionNameText
            .DOFade(
                1f,
                textFadeInDuration
            )
            .SetEase(Ease.OutQuad)
            .SetLink(
                interactionNameText.gameObject,
                LinkBehaviour.KillOnDestroy
            );

        // Kropka NIE mruga już ciągle w pętli – pozostaje stabilna i czytelna
    }

    private void TransitionToInteractableIcon(IInteractable interactable, Color targetColor)
    {
        if (crosshairImage == null) return;

        Sprite targetSprite;
        Vector2 targetSize;
        GetSymbolInfo(interactable, out targetSprite, out targetSize);

        if (targetSprite == null)
        {
            targetSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
            targetSize = defaultDotSize;
        }

        // Jeśli już wyświetlamy ten symbol, aktualizujemy tylko kolor bez restartowania przejścia
        if (_currentTargetSprite == targetSprite && crosshairImage.sprite == targetSprite)
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
        ReticleSymbolType symbolType = ReticleSymbolType.Auto;

        if (interactable is ICrosshairSymbolProvider provider)
        {
            symbolType = provider.CrosshairSymbol;
        }

        // Sprawdzenie stanu zablokowania
        bool isLocked = (interactable is IConditionalInteractable cond && !cond.CanInteract);
        if (isLocked && lockedKeySprite != null)
        {
            targetSprite = lockedKeySprite;
            targetSize = interactIconSize;
            return;
        }

        if (symbolType == ReticleSymbolType.Auto)
        {
            symbolType = DetectSymbolType(interactable);
        }

        switch (symbolType)
        {
            case ReticleSymbolType.QuestionMark:
                targetSprite = inspectQuestionSprite != null ? inspectQuestionSprite : defaultDotSprite;
                targetSize = inspectIconSize;
                break;

            case ReticleSymbolType.ExclamationMark:
                targetSprite = exclamationSprite != null ? exclamationSprite : (interactHandSprite != null ? interactHandSprite : defaultDotSprite);
                targetSize = exclamationIconSize;
                break;

            case ReticleSymbolType.Ellipsis:
                targetSprite = ellipsisSprite != null ? ellipsisSprite : (inspectQuestionSprite != null ? inspectQuestionSprite : defaultDotSprite);
                targetSize = ellipsisIconSize;
                break;

            case ReticleSymbolType.Dot:
            default:
                targetSprite = defaultDotSprite != null ? defaultDotSprite : _initialSprite;
                targetSize = defaultDotSize;
                break;
        }
    }

    private ReticleSymbolType DetectSymbolType(IInteractable interactable)
    {
        if (interactable == null) return ReticleSymbolType.Dot;

        string name = interactable.InteractionName ?? string.Empty;

        // 1. Pytajnik [?] – Badanie, rozmyślanie, oglądanie, tajemnice, krzyż, notatki
        bool isInspectOrThought = (interactable is InspectThoughtInteractable) ||
                                  (interactable is CrucifixInteractable) ||
                                  name.Contains("?") ||
                                  name.IndexOf("Look", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Examine", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Spójrz", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Zbadaj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Inspect", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Obejrzyj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Oglądaj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Co to", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Pomyśl", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Myśl", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Read", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Czytaj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Notatk", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Note", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (isInspectOrThought)
        {
            return ReticleSymbolType.QuestionMark;
        }

        // 2. Wielokropek [...] – Mowa, dialog, nasłuchiwanie, radio, chwile skupienia/oczekiwania
        bool isDialogueOrListening = (interactable is RadioInteractable) ||
                                     name.Contains("...") ||
                                     name.IndexOf("Talk", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Rozmawiaj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Mów", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Speak", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Listen", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Słuchaj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Posłuchaj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Radio", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Dialog", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Klient", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Client", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Czekaj", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("Wait", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (isDialogueOrListening)
        {
            return ReticleSymbolType.Ellipsis;
        }

        // 3. Wykrzyknik [!] – Akcje bezpośrednie, zadania, narzędzia, szafa, drzwi, zlew, brzytwa
        return ReticleSymbolType.ExclamationMark;
    }

    private void PerformCrossfade(Sprite newSprite, Vector2 newSize, Color newColor, float duration)
    {
        _transitionTween?.Kill();
        _colorTween?.Kill();
        _scaleTween?.Kill();

        if (fadeTransitionImage != null)
        {
            // Dwuwarstwowy miękki crossfade: stary sprite łagodnie cichnie, nowy wchodzi z lekkim fade-inem i subtelnym zoomem
            fadeTransitionImage.DOKill();
            fadeTransitionImage.gameObject.SetActive(true);
            fadeTransitionImage.sprite = newSprite;
            fadeTransitionImage.rectTransform.sizeDelta = newSize;
            fadeTransitionImage.transform.localScale = _defaultScale * 0.86f;
            fadeTransitionImage.color = new Color(newColor.r, newColor.g, newColor.b, 0f);

            Sequence seq = DOTween.Sequence();
            seq.Join(crosshairImage.DOFade(0f, duration * 0.7f).SetEase(Ease.InQuad));
            seq.Join(fadeTransitionImage.DOFade(newColor.a, duration).SetEase(Ease.OutQuad));
            seq.Join(fadeTransitionImage.transform.DOScale(_defaultScale, duration).SetEase(Ease.OutCubic));

            seq.OnComplete(() =>
            {
                if (crosshairImage != null)
                {
                    crosshairImage.sprite = newSprite;
                    crosshairImage.rectTransform.sizeDelta = newSize;
                    crosshairImage.color = newColor;
                    crosshairImage.transform.localScale = _defaultScale;
                }
                if (fadeTransitionImage != null)
                {
                    fadeTransitionImage.gameObject.SetActive(false);
                }
            });
            seq.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _transitionTween = seq;
        }
        else
        {
            // Jednowarstwowy płynny fallback
            Sequence seq = DOTween.Sequence();
            seq.Append(crosshairImage.DOFade(0.2f, duration * 0.35f).SetEase(Ease.InQuad));
            seq.AppendCallback(() =>
            {
                crosshairImage.sprite = newSprite;
                _crosshairRect.sizeDelta = newSize;
            });
            seq.Append(crosshairImage.DOColor(newColor, duration * 0.65f).SetEase(Ease.OutQuad));
            seq.Join(crosshairImage.transform.DOScale(_defaultScale, duration * 0.65f).From(_defaultScale * 0.86f).SetEase(Ease.OutCubic));
            seq.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _transitionTween = seq;
        }
    }

    private void HideInteractable()
    {
        _currentInteractable = null;

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
            PerformCrossfade(dotSprite, defaultDotSize, normalColor, transitionFadeOutDuration);
        }

        _textTween = interactionNameText
            .DOFade(
                0f,
                textFadeOutDuration
            )
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (interactionNameText != null)
                {
                    interactionNameText.text =
                        string.Empty;
                }
            })
            .SetLink(
                interactionNameText.gameObject,
                LinkBehaviour.KillOnDestroy
            );

        // Po wygaszeniu symbolu kropka spokojnie wznawia oddychanie w idlu
        DOVirtual.DelayedCall(transitionFadeOutDuration, () =>
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

    private void StartPulse()
    {
        // Pętla ciągłego pulsowania została wyłączona – celownik jest stabilny
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

            if (!_hasInteractable && !_isHolding)
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

            if (!_hasInteractable && !_isHolding)
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
        {
            return normalColor;
        }

        return interactableColor;
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
                    _transitionTween?.Kill();
                    if (fadeTransitionImage != null)
                    {
                        fadeTransitionImage.gameObject.SetActive(false);
                    }
                }
                _holdTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_holdTimer / Mathf.Max(0.05f, holdDuration));

                bool isClockworkTarget = (crosshairImage != null && crosshairImage.sprite == clockworkRingSprite) ||
                                         (_currentInteractable is DoorInteractable);

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

        if (!_hasInteractable && !_isHolding)
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