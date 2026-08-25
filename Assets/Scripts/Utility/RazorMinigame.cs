using System;
using System.Collections;
using Unity.Cinemachine;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Minigra ostrzenia brzytwy oparta o mechanikę Hold & Release.
/// Gracz przytrzymuje spację, aby płynnie pociągnąć brzytwę po skórzanym pasie,
/// i puszcza ją w odpowiednim momencie w strefie GOOD lub PERFECT.
/// Wymaga przyniesienia żyletki z pudełka przed uruchomieniem.
/// </summary>
public class RazorMinigame : MonoBehaviour, IConditionalInteractable
{
    public static event Action<float> OnMinigameCompleted;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        OnMinigameCompleted = null;
    }
#endif

    // ──────────────────────────────────────────────────────────
    // INTERAKCJA I WYMÓG ŻYLETKI
    // ──────────────────────────────────────────────────────────

    [Header("Wymóg żyletki")]
    [Tooltip("Czy minigra wymaga przyniesienia żyletki w rękach gracza?")]
    [SerializeField] private bool requireBladeItem = true;

    [Tooltip("Wymagany ItemId z PickupItem (domyślnie 'razor_blade').")]
    [SerializeField] private string requiredBladeItemId = "razor_blade";

    [Header("Interaction Prompts")]
    [SerializeField] private string promptNeedBlade = "Sharpening station (Requires razor blade)";
    [SerializeField] private string promptCanSharpen = "Sharpen razor";
    [SerializeField] private string promptAlreadyDone = "Razor has been sharpened";

    public bool CanInteract
    {
        get
        {
            if (_state != State.Inactive) return false;
            if (_isCompleted) return false;
            if (!requireBladeItem) return true;
            return IsPlayerHoldingBlade();
        }
    }

    public string InteractionName
    {
        get
        {
            if (_isCompleted) return promptAlreadyDone;
            if (requireBladeItem && !IsPlayerHoldingBlade()) return promptNeedBlade;
            return promptCanSharpen;
        }
    }

    [Header("Input")]
    [Tooltip("Akcja napędzania brzytwy (np. Space).")]
    [SerializeField] private InputActionReference hitAction;

    [Tooltip("Akcja uderzenia / ostrzenia w strefie (np. Klawisz E / LPM).")]
    [SerializeField] private InputActionReference strikeAction;

    // ──────────────────────────────────────────────────────────
    // BLOKADA KAMERY I GRACZA
    // ──────────────────────────────────────────────────────────

    [Header("Blokada gracza i kamery")]
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHands playerHands;
    [SerializeField] private HeadBobbing headBobbing;

    // ──────────────────────────────────────────────────────────
    // REFERENCJE UI
    // ──────────────────────────────────────────────────────────

    [Header("UI References")]
    [SerializeField] private CanvasGroup minigameCanvasGroup;
    [SerializeField] private RectTransform razorIndicator;
    [SerializeField] private RectTransform sharpnessMarker;
    [Tooltip("Dynamiczne wypełnienie gradientem ostrości (Image typu Filled Horizontal).")]
    [SerializeField] private Image sharpnessFillImage;
    [SerializeField] private TMP_Text attemptsText;
    [SerializeField] private TMP_Text feedbackText;

    [Tooltip("Opcjonalny dedykowany tekst instrukcji / promptu (jeśli pusty, używa pressToStartUI).")]
    [SerializeField] private TMP_Text instructionText;

    [Header("Poradnik / Guide Overlay")]
    [Tooltip("Panel / Canvas z poradnikiem / instrukcją (Guide), który wyświetla się na starcie przed naciśnięciem spacji.")]
    [SerializeField] private GameObject guideOverlayUI;

    [Tooltip("Opcjonalny CanvasGroup do płynnego wygaszania poradnika.")]
    [SerializeField] private CanvasGroup guideOverlayCanvasGroup;

    [Tooltip("Czy pokazywać poradnik / instruktaż na starcie minigry?")]
    [SerializeField] private bool showTutorialOnStart = true;

    [Tooltip("Obiekt z tekstem / promptem instruującym gracza.")]
    [SerializeField] private GameObject pressToStartUI;

    // ──────────────────────────────────────────────────────────
    // PUNKTY TRASY I STREFY
    // ──────────────────────────────────────────────────────────

    [Header("Trasa (Anchory)")]
    [Tooltip("Dolny punkt startowy.")]
    [SerializeField] private RectTransform bottomAnchor;

    [Tooltip("Górny punkt szczytowy.")]
    [SerializeField] private RectTransform topAnchor;

    [Header("Strefy Hit (Transformy)")]
    [Tooltip("Transform / Panel strefy GOOD.")]
    [SerializeField] private RectTransform zoneGood;

    [Tooltip("Transform / Panel strefy PERFECT.")]
    [SerializeField] private RectTransform zonePerfect;

    [Header("Automatyczne lub Ręczne Granice Stref (0..1 wzdłuż paska)")]
    [Tooltip("Czy automatycznie obliczać zakresy stref z ich pozycji w UI?")]
    [SerializeField] private bool autoDetectZoneBounds = true;

    [Tooltip("Początek strefy GOOD (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float goodZoneMinT = 0.38f;

    [Tooltip("Koniec strefy GOOD (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float goodZoneMaxT = 0.72f;

    [Tooltip("Początek strefy PERFECT (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float perfectZoneMinT = 0.72f;

    [Tooltip("Koniec strefy PERFECT (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float perfectZoneMaxT = 0.94f;

    [Header("Podświetlenie / Rozbłysk Stref (Glow / Bloom)")]
    [Tooltip("(Opcjonalnie) Obrazek strefy GOOD do rozbłysku koloru/skali.")]
    [SerializeField] private Image zoneGoodImage;

    [Tooltip("(Opcjonalnie) Osobny obiekt/CanvasGroup błysku GOOD (np. biała poświata fade in/out).")]
    [SerializeField] private CanvasGroup zoneGoodGlow;

    [Tooltip("(Opcjonalnie) Obrazek strefy PERFECT do rozbłysku koloru/skali.")]
    [SerializeField] private Image zonePerfectImage;

    [Tooltip("(Opcjonalnie) Osobny obiekt/CanvasGroup błysku PERFECT (np. złota poświata fade in/out).")]
    [SerializeField] private CanvasGroup zonePerfectGlow;

    [Tooltip("Czas trwania rozbłysku strefy przy trafieniu (sekundy).")]
    [SerializeField] private float zoneGlowDuration = 0.35f;

    // ──────────────────────────────────────────────────────────
    // CZASY PRZEJAZDU I RUCHU (W SEKUNDACH)
    // ──────────────────────────────────────────────────────────

    [Header("Czasy (w sekundach)")]
    [Tooltip("Czas pełnego przejazdu w górę w 1. próbie podczas trzymania spacji (domyślnie 2.2s).")]
    [SerializeField] private float initialTravelTime = 2.2f;

    [Tooltip("O ile sekund krócej trwa ruch w każdej kolejnej próbie (domyślnie 0.15s).")]
    [SerializeField] private float timeReductionPerAttempt = 0.15f;

    [Tooltip("Minimalny czas przejazdu w górę (domyślnie 1.2s).")]
    [SerializeField] private float minTravelTime = 1.2f;

    [Tooltip("Czas powrotu brzytwy na dół po puszczeniu (domyślnie 0.5s).")]
    [SerializeField] private float returnDuration = 0.5f;

    // ──────────────────────────────────────────────────────────
    // PASEK OSTROŚCI
    // ──────────────────────────────────────────────────────────

    [Header("Pasek Ostrości")]
    [SerializeField] private float barHalfWidth = 300f;
    [SerializeField] private float sharpnessGainPerfect = 0.28f;
    [SerializeField] private float sharpnessGainGood = 0.15f;
    [SerializeField] private float markerMoveDuration = 0.25f;

    // ──────────────────────────────────────────────────────────
    // KONFIGURACJA PRÓB I PROGÓW
    // ──────────────────────────────────────────────────────────

    [Header("Konfiguracja")]
    [SerializeField] private int totalAttempts = 5;
    [SerializeField] private float endDelay = 1.2f;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float sharpThreshold = 0.5f;

    [Tooltip("Margines czasu (w sekundach) po starcie ruchu, w którym ignorowane są przypadkowe kliknięcia startowe (np. 0.35s).")]
    [SerializeField] private float startupMarginDelay = 0.35f;

    // ──────────────────────────────────────────────────────────
    // KOMUNIKATY I TEKSTY UI
    // ──────────────────────────────────────────────────────────

    [Header("Komunikaty i Prompty UI (English)")]
    [SerializeField] private string promptStartMinigame = "PRESS [SPACE] TO START";
    [SerializeField] private string promptHoldToSharpen = "PRESS [SPACE] TO STROKE  |  CLICK [LMB] IN ZONE";
    [SerializeField] private string promptStrokeInFlight = "CLICK [LMB] IN ZONE (GOOD / PERFECT)!";
    [SerializeField] private string promptReturning = "FLIPPING BLADE...";
    [SerializeField] private string textPerfect = "PERFECT!";
    [SerializeField] private string textGood = "GOOD!";
    [SerializeField] private string textTooEarly = "TOO EARLY!";
    [SerializeField] private string textTooLate = "TOO LATE!";
    [SerializeField] private string textBladeSharp = "RAZOR SHARPENED!";
    [SerializeField] private string textBladeDull = "RAZOR IS TOO DULL!";

    // ──────────────────────────────────────────────────────────
    // JUICE & EFEKTY WIZUALNE
    // ──────────────────────────────────────────────────────────

    [Header("Juice & Screen Shake")]
    [Tooltip("Transform do potrząsania (np. główny panel minigry lub Canvas). Jeśli pusty, użyje minigameCanvasGroup.")]
    [SerializeField] private RectTransform shakeTransform;

    [Tooltip("Siła trzęsienia ekranu przy PERFECT.")]
    [SerializeField] private float perfectShakeStrength = 16f;

    [Tooltip("Siła trzęsienia przy GOOD.")]
    [SerializeField] private float goodShakeStrength = 5f;

    [Tooltip("Siła trzęsienia przy MISS.")]
    [SerializeField] private float missShakeStrength = 8f;

    [Tooltip("Kolor napisu PERFECT.")]
    [SerializeField] private Color perfectColor = new Color(1f, 0.8f, 0.2f, 1f);

    [Tooltip("Kolor napisu GOOD.")]
    [SerializeField] private Color goodColor = new Color(0.4f, 0.85f, 0.4f, 1f);

    [Tooltip("Kolor napisu MISS.")]
    [SerializeField] private Color missColor = new Color(0.9f, 0.25f, 0.25f, 1f);

    // ──────────────────────────────────────────────────────────
    // AUDIO
    // ──────────────────────────────────────────────────────────

    [Header("Audio")]
    [Tooltip("Dźwięk przesunięcia brzytwy po pasie w górę (domyślnie 'ostrzenie_wolne').")]
    [SerializeField] private string soundPassUp = "ostrzenie_wolne";
    [SerializeField] private string soundGood = "sharpen_good";
    [SerializeField] private string soundPerfect = "sharpen_perfect";
    [SerializeField] private string soundMiss = "sharpen_miss";

    // ──────────────────────────────────────────────────────────
    // STAN WEWNĘTRZNY
    // ──────────────────────────────────────────────────────────

    private enum State
    {
        Inactive,
        ShowingTutorial,  // Wyświetla overlay z instruktażem przed rozpoczęciem gry
        ReadyForStroke,   // Brzytwa na dole, czeka na wciśnięcie i trzymanie spacji
        HoldingStroke,    // Gracz trzyma spację, brzytwa sunie w górę
        ReturningDown,    // Brzytwa płynnie zjeżdża w dół
        Finished          // Minigra zakończona
    }

    public static RazorMinigame Instance { get; private set; }
    public bool IsActive => _state != State.Inactive && _state != State.Finished;

    private State _state = State.Inactive;
    private int _attemptsDone = 0;
    private float _sharpness = 0f;
    private float _strokeProgress = 0f;
    private float _flightTimer = 0f;
    private float _tutorialStartTime = 0f;
    private bool _isCompleted = false;
    private bool _isOtherSide = false;

    private Vector2 _bottomPos;
    private Vector2 _topPos;
    private float _calculatedGoodMinT = 0.38f;
    private float _calculatedGoodMaxT = 0.72f;
    private float _calculatedPerfMinT = 0.72f;
    private float _calculatedPerfMaxT = 0.94f;

    private Tween _razorTween;
    private Tween _markerTween;
    private Tween _fillTween;
    private Tween _fadeTween;
    private Tween _shakeTween;
    private Tween _feedbackTween;

    // ──────────────────────────────────────────────────────────
    // CYKL ŻYCIA
    // ──────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        HideInstant();
    }

    private void OnEnable()
    {
        if (hitAction != null && hitAction.action != null)
        {
            hitAction.action.Enable();
        }
        if (strikeAction != null && strikeAction.action != null)
        {
            strikeAction.action.Enable();
        }
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    private void KillAllTweens()
    {
        _razorTween?.Kill();
        _markerTween?.Kill();
        _fillTween?.Kill();
        _fadeTween?.Kill();
        _shakeTween?.Kill();
        _feedbackTween?.Kill();
    }

    // ──────────────────────────────────────────────────────────
    // INTERAKCJA
    // ──────────────────────────────────────────────────────────

    public void Interact()
    {
        Debug.Log($"[RazorMinigame] Interact() called! State={_state}, isCompleted={_isCompleted}, requireBladeItem={requireBladeItem}");
        if (!CanInteract)
        {
            Debug.LogWarning($"[RazorMinigame] Interact() BLOCKED because CanInteract is FALSE! (HoldingBlade={IsPlayerHoldingBlade()})");
            return;
        }

        // Jeśli wymagana jest żyletka, zabieramy ją z rąk gracza na start
        if (requireBladeItem && IsPlayerHoldingBlade())
        {
            if (playerHands == null)
                playerHands = FindAnyObjectByType<PlayerHands>();

            if (playerHands != null)
            {
                Debug.Log("[RazorMinigame] Destroying held blade from player hands before starting minigame.");
                playerHands.DestroyHeldItem();
            }
        }

        StartMinigame();
    }

    private bool IsPlayerHoldingBlade()
    {
        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands == null)
        {
            Debug.LogWarning("[RazorMinigame] playerHands reference is NULL in scene!");
            return false;
        }

        if (!playerHands.HasItem)
        {
            return false;
        }

        GameObject held = playerHands.HeldItem;
        if (held == null)
        {
            Debug.LogWarning("[RazorMinigame] playerHands.HasItem is true but HeldItem is NULL!");
            return false;
        }

        PickupItem pickup = held.GetComponentInChildren<PickupItem>();
        if (pickup == null)
            pickup = held.GetComponentInParent<PickupItem>();

        string heldId = pickup != null ? pickup.ItemId : "NO_PICKUP_ITEM";
        Debug.Log($"[RazorMinigame] Checking blade in hands: GameObject='{held.name}', PickupItem={(pickup != null ? "Found" : "None")}, ItemId='{heldId}', Required='{requiredBladeItemId}'");

        if (pickup != null && !string.IsNullOrEmpty(pickup.ItemId))
        {
            string id = pickup.ItemId.Trim().ToLowerInvariant();
            string req = string.IsNullOrEmpty(requiredBladeItemId) ? "razor_blade" : requiredBladeItemId.Trim().ToLowerInvariant();

            if (id == req || id == "blade" || id == "razor_blade" || id == "zyletka" || id == "ostrze" || id.Contains("blade") || id.Contains("zyletk"))
            {
                Debug.Log($"[RazorMinigame] Blade accepted by ItemId: '{id}'");
                return true;
            }
        }

        // Dodatkowe sprawdzenie po nazwie obiektu na wypadek braku wpisanego ItemId
        string objName = held.name.ToLowerInvariant();
        if (objName.Contains("blade") || objName.Contains("zyletk") || objName.Contains("ostrze") || objName.Contains("razor"))
        {
            Debug.Log($"[RazorMinigame] Blade accepted by GameObject name: '{held.name}'");
            return true;
        }

        Debug.LogWarning($"[RazorMinigame] Item '{held.name}' (id: '{heldId}') was REJECTED as razor blade!");
        return false;
    }

    private void StartMinigame()
    {
        ValidateTimings();
        SetupAnchorsAndZones();
        ConfigureLeftUIAndBackground();

        _attemptsDone = 0;
        _sharpness = 0f;
        _strokeProgress = 0f;
        _isOtherSide = false;

        ResetZoneHighlights();

        // Ustaw brzytwę na dolnej pozycji startowej
        if (razorIndicator != null)
        {
            _razorTween?.Kill();
            razorIndicator.anchoredPosition = _bottomPos;
            razorIndicator.localScale = Vector3.one;
            razorIndicator.localRotation = Quaternion.identity;
        }

        LockPlayer(true);

        UpdateAttemptsText();
        UpdateSharpnessMarker(animate: false);
        ShowFeedback(string.Empty);

        // Automatyczne odnalezienie lub utworzenie panelu poradnika, jeśli nie przypisano w inspektorze
        if (guideOverlayUI == null && minigameCanvasGroup != null)
        {
            Transform guideChild = minigameCanvasGroup.transform.Find("Guide");
            if (guideChild != null)
            {
                guideOverlayUI = guideChild.gameObject;
            }
            else
            {
                GameObject guideGo = new GameObject("Guide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                guideGo.transform.SetParent(minigameCanvasGroup.transform, false);
                RectTransform gr = guideGo.GetComponent<RectTransform>();
                gr.anchorMin = new Vector2(0.5f, 0.5f);
                gr.anchorMax = new Vector2(0.5f, 0.5f);
                gr.pivot = new Vector2(0.5f, 0.5f);
                gr.anchoredPosition = Vector2.zero;
                gr.sizeDelta = new Vector2(760f, 570f);
                Image gi = guideGo.GetComponent<Image>();
                gi.preserveAspect = true;
                gi.raycastTarget = false;
                guideOverlayUI = guideGo;
            }
        }

        if (guideOverlayUI != null)
        {
            guideOverlayUI.transform.SetAsLastSibling();
        }

        // Jeśli aktywny jest instruktaż na starcie:
        if (showTutorialOnStart && guideOverlayUI != null)
        {
            _state = State.ShowingTutorial;
            _tutorialStartTime = Time.unscaledTime;
            guideOverlayUI.SetActive(true);
            guideOverlayUI.transform.SetAsLastSibling();

            Image guideImg = guideOverlayUI.GetComponent<Image>();
            if (guideImg != null)
            {
                guideImg.color = Color.white;
            }

            if (guideOverlayCanvasGroup == null)
                guideOverlayCanvasGroup = guideOverlayUI.GetComponent<CanvasGroup>();

            if (guideOverlayCanvasGroup != null)
            {
                guideOverlayCanvasGroup.DOKill();
                guideOverlayCanvasGroup.alpha = 0f;
                guideOverlayCanvasGroup.DOFade(1f, 0.25f).SetUpdate(true).SetLink(guideOverlayUI, LinkBehaviour.KillOnDestroy);
            }

            SetInstructionPrompt(promptStartMinigame);
        }
        else
        {
            _state = State.ReadyForStroke;
            if (guideOverlayUI != null)
                guideOverlayUI.SetActive(false);

            SetInstructionPrompt(promptHoldToSharpen);
        }

        ShowUI();
    }

    private void ConfigureLeftUIAndBackground()
    {
        if (minigameCanvasGroup == null) return;

        Transform canvasT = minigameCanvasGroup.transform;

        // 1. Zmiękczenie topornego tła (delikatna winieta zamiast ciężkiej ramki)
        Transform bgTransform = canvasT.Find("Minigame_razor_Background");
        if (bgTransform != null)
        {
            Image bgImg = bgTransform.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = null;
                bgImg.color = new Color(0.02f, 0.02f, 0.04f, 0.40f);
            }
        }

        // 2. Scalenie paska postępu z tekstem prób po lewej stronie oraz osobny gradient wypełnienia
        Transform progressBarT = canvasT.Find("ProgressBar");
        if (progressBarT != null)
        {
            RectTransform pRect = progressBarT.GetComponent<RectTransform>();

            // Konfiguracja oddzielnego gradientu wypełnienia (Sharpness_Fill_Gradient)
            Transform fillT = progressBarT.Find("Sharpness_Fill_Gradient");
            if (fillT == null)
            {
                // Ciemne tło szczeliny paska
                GameObject slotBackdrop = new GameObject("Slot_Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                slotBackdrop.transform.SetParent(progressBarT, false);
                RectTransform sbRect = slotBackdrop.GetComponent<RectTransform>();
                sbRect.anchorMin = new Vector2(0.5f, 0.5f);
                sbRect.anchorMax = new Vector2(0.5f, 0.5f);
                sbRect.pivot = new Vector2(0.5f, 0.5f);
                sbRect.anchoredPosition = new Vector2(0f, 0f);
                sbRect.sizeDelta = new Vector2(390f, 38f);
                Image sbImg = slotBackdrop.GetComponent<Image>();
                sbImg.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
                sbImg.raycastTarget = false;

                // Obiekt wypełnienia gradientem
                GameObject fillGo = new GameObject("Sharpness_Fill_Gradient", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillGo.transform.SetParent(progressBarT, false);
                RectTransform fRect = fillGo.GetComponent<RectTransform>();
                fRect.anchorMin = new Vector2(0.5f, 0.5f);
                fRect.anchorMax = new Vector2(0.5f, 0.5f);
                fRect.pivot = new Vector2(0.5f, 0.5f);
                fRect.anchoredPosition = new Vector2(0f, 0f);
                fRect.sizeDelta = new Vector2(390f, 38f);

                sharpnessFillImage = fillGo.GetComponent<Image>();
                sharpnessFillImage.type = Image.Type.Filled;
                sharpnessFillImage.fillMethod = Image.FillMethod.Horizontal;
                sharpnessFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                sharpnessFillImage.fillAmount = _sharpness;
                sharpnessFillImage.raycastTarget = false;
            }
            else
            {
                sharpnessFillImage = fillT.GetComponent<Image>();
            }

            // Upewniamy się, że znacznik żyletki jest na samej górze
            if (sharpnessMarker != null)
            {
                sharpnessMarker.SetAsLastSibling();
            }

            if (attemptsText != null)
            {
                RectTransform aRect = attemptsText.rectTransform;
                aRect.SetParent(progressBarT, false);
                aRect.anchorMin = new Vector2(0.5f, 0f);
                aRect.anchorMax = new Vector2(0.5f, 0f);
                aRect.pivot = new Vector2(0.5f, 1f);
                aRect.anchoredPosition = new Vector2(0f, -14f);
                aRect.sizeDelta = new Vector2(pRect.sizeDelta.x, 38f);
                aRect.localScale = Vector3.one;

                attemptsText.alignment = TextAlignmentOptions.Center;
                attemptsText.fontSize = 20f;
                attemptsText.color = new Color(0.95f, 0.82f, 0.55f, 1f); // Antyczne złoto
            }
        }
    }

    private void ValidateTimings()
    {
        if (initialTravelTime < 0.5f) initialTravelTime = 2.2f;
        if (timeReductionPerAttempt < 0.01f) timeReductionPerAttempt = 0.15f;
        if (minTravelTime < 0.5f) minTravelTime = 1.2f;
        if (returnDuration < 0.1f) returnDuration = 0.5f;
        if (totalAttempts < 1) totalAttempts = 5;
    }

    // ──────────────────────────────────────────────────────────
    // OBSŁUGA INPUTU I RUCHU (UPDATE)
    // ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (_state == State.Inactive || _state == State.Finished)
            return;

        switch (_state)
        {
            case State.ShowingTutorial:
                AnimatePromptPulse();
                HandleShowingTutorial();
                break;

            case State.ReadyForStroke:
                AnimatePromptPulse();
                HandleReadyState();
                break;

            case State.HoldingStroke:
                HandleHoldingState();
                break;
        }
    }

    private void AnimatePromptPulse()
    {
        float pulse = 1f + Mathf.PingPong(Time.time * 2.5f, 0.05f);
        if (instructionText != null)
        {
            instructionText.transform.localScale = new Vector3(pulse, pulse, 1f);
        }
        else if (pressToStartUI != null)
        {
            pressToStartUI.transform.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    private void HandleShowingTutorial()
    {
        // Ignorujemy input przez pierwsze 0.25s od otwarcia poradnika, aby kliknięcie interakcji go nie zamknęło natychmiast
        if (Time.unscaledTime - _tutorialStartTime < 0.25f)
            return;

        if (WasPropelPressed() || WasStrikePressed())
        {
            DismissTutorialAndStart();
        }
    }

    private void DismissTutorialAndStart()
    {
        if (guideOverlayUI != null)
        {
            if (guideOverlayCanvasGroup == null)
                guideOverlayCanvasGroup = guideOverlayUI.GetComponent<CanvasGroup>();

            if (guideOverlayCanvasGroup != null)
            {
                guideOverlayCanvasGroup.DOKill();
                guideOverlayCanvasGroup.DOFade(0f, 0.2f)
                    .SetLink(guideOverlayUI, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() => guideOverlayUI.SetActive(false));
            }
            else
            {
                guideOverlayUI.SetActive(false);
            }
        }

        _state = State.ReadyForStroke;
        _strokeProgress = 0f;
        SetInstructionPrompt(promptHoldToSharpen);
    }

    private void HandleReadyState()
    {
        if (WasPropelPressed() || IsPropelHeld() || WasStrikePressed())
        {
            // Gracz wciska spację: start automatycznego pociągnięcia brzytwy w górę
            _state = State.HoldingStroke;
            _strokeProgress = 0f;
            _flightTimer = 0f;

            if (guideOverlayUI != null)
                guideOverlayUI.SetActive(false);

            SetInstructionPrompt(promptStrokeInFlight);
            ShowFeedback(string.Empty);

            if (!string.IsNullOrEmpty(soundPassUp))
            {
                AudioManager.Instance?.Play(soundPassUp);
            }
        }
    }

    private void HandleHoldingState()
    {
        // 1. Brzytwa jedzie automatycznie w górę po pasie
        _flightTimer += Time.deltaTime;
        float duration = Mathf.Max(minTravelTime, initialTravelTime - (_attemptsDone * timeReductionPerAttempt));
        _strokeProgress += Time.deltaTime / duration;
        _strokeProgress = Mathf.Clamp01(_strokeProgress);

        // 2. Pozycja i mikrowibracja tarcia brzytwy o skórę
        if (razorIndicator != null)
        {
            razorIndicator.anchoredPosition = Vector2.Lerp(_bottomPos, _topPos, _strokeProgress);
            float frictionWobble = Mathf.Sin(Time.time * 50f) * 1.6f;
            razorIndicator.localRotation = Quaternion.Euler(0f, 0f, frictionWobble);
        }

        // 3. Dynamiczne podświetlanie strefy pod brzytwą w locie
        UpdateHoverZoneGlow(_strokeProgress);

        // 4. KLUCZOWE: Ocena następuje po kliknięciu Lewego Przycisku Myszy (LPM)
        if (WasStrikePressed())
        {
            // Ignorujemy kliknięcia na samym początku (np. pierwsze 0.35s), aby gracz nie spalił próby przypadkowym kliknięciem
            if (_flightTimer >= startupMarginDelay)
            {
                EvaluateAndHandleStrike();
                return;
            }
        }

        // 5. Gracz przejechał do samego szczytu bez kliknięcia LPM -> Za późno (Miss)
        if (_strokeProgress >= 1f)
        {
            HandleReachedTopMiss();
        }
    }

    private bool WasPropelPressed()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            return true;

        if (hitAction != null && hitAction.action != null && hitAction.action.WasPressedThisFrame())
            return true;

        return false;
    }

    private bool IsPropelHeld()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            return true;

        if (hitAction != null && hitAction.action != null && hitAction.action.IsPressed())
            return true;

        return false;
    }

    private bool WasPropelReleased()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame)
            return true;

        if (hitAction != null && hitAction.action != null && hitAction.action.WasReleasedThisFrame())
            return true;

        return false;
    }

    private bool WasStrikePressed()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        if (strikeAction != null && strikeAction.action != null && strikeAction.action.WasPressedThisFrame())
            return true;

        return false;
    }

    // ──────────────────────────────────────────────────────────
    // OCENA TRAFIENIA I POWRÓT
    // ──────────────────────────────────────────────────────────

    private void EvaluateAndHandleStrike()
    {
        ResetZoneHighlights();

        HitResult result = EvaluateHit(_strokeProgress);
        RegisterResult(result);

        StartReturnDownwards();
    }

    private void HandleReachedTopMiss()
    {
        ResetZoneHighlights();

        // Przejechanie całego paska bez uderzenia = MISS (Za późno)
        RegisterResult(HitResult.TooLate);

        StartReturnDownwards();
    }

    private HitResult EvaluateHit(float t)
    {
        if (t >= _calculatedPerfMinT && t <= _calculatedPerfMaxT)
            return HitResult.Perfect;

        if (t >= _calculatedGoodMinT && t <= _calculatedGoodMaxT)
            return HitResult.Good;

        if (t < _calculatedGoodMinT)
            return HitResult.TooEarly;

        return HitResult.TooLate;
    }

    private void RegisterResult(HitResult result)
    {
        switch (result)
        {
            case HitResult.Perfect:
                _sharpness = Mathf.Clamp01(_sharpness + sharpnessGainPerfect);
                ShowFeedback(textPerfect, perfectColor, isPunch: true);
                AudioManager.Instance?.Play(soundPerfect);
                FlashPerfectZone();
                if (ParticleManager.Instance != null && razorIndicator != null)
                {
                    ParticleManager.Instance.PlayBurst(razorIndicator.position, perfectColor, 1.2f);
                }
                break;

            case HitResult.Good:
                _sharpness = Mathf.Clamp01(_sharpness + sharpnessGainGood);
                ShowFeedback(textGood, goodColor, isPunch: false);
                AudioManager.Instance?.Play(soundGood);
                FlashGoodZone();
                if (ParticleManager.Instance != null && razorIndicator != null)
                {
                    ParticleManager.Instance.PlaySparkles(razorIndicator.position, goodColor, 1f);
                }
                break;

            case HitResult.TooEarly:
                ShowFeedback(textTooEarly, missColor, isPunch: false);
                AudioManager.Instance?.Play(soundMiss);
                break;

            case HitResult.TooLate:
                ShowFeedback(textTooLate, missColor, isPunch: false);
                AudioManager.Instance?.Play(soundMiss);
                break;
        }

        TriggerJuiceEffects(result);
        UpdateSharpnessMarker(animate: true);
    }

    private void StartReturnDownwards()
    {
        _state = State.ReturningDown;
        SetInstructionPrompt(promptReturning);

        _razorTween?.Kill();
        if (razorIndicator != null)
        {
            razorIndicator.DORotate(Vector3.zero, returnDuration * 0.4f).SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy);
            _razorTween = razorIndicator
                .DOAnchorPos(_bottomPos, returnDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(OnReturnedToBottom);
        }
        else
        {
            OnReturnedToBottom();
        }
    }

    private void OnReturnedToBottom()
    {
        _attemptsDone++;
        UpdateAttemptsText();

        if (_attemptsDone >= totalAttempts || _sharpness >= 1.0f)
        {
            _state = State.Finished;
            StartCoroutine(EndMinigameRoutine());
        }
        else
        {
            // Obrót brzytwy na drugą stronę (Real-life razor stropping flip)
            _isOtherSide = !_isOtherSide;
            float targetScaleX = _isOtherSide ? -1f : 1f;

            if (razorIndicator != null)
            {
                razorIndicator.DOScaleX(0.05f, 0.12f).SetEase(Ease.InQuad).SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy).OnComplete(() =>
                {
                    razorIndicator.DOScaleX(targetScaleX, 0.12f).SetEase(Ease.OutQuad).SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy).OnComplete(() =>
                    {
                        // Błysk na biało i iskry oznaczające gotowość do kolejnego cyklu
                        FlashRazorReadyGleam();
                        _state = State.ReadyForStroke;
                        _strokeProgress = 0f;
                        SetInstructionPrompt(promptHoldToSharpen);
                    });
                });
            }
            else
            {
                _state = State.ReadyForStroke;
                _strokeProgress = 0f;
                SetInstructionPrompt(promptHoldToSharpen);
            }
        }
    }

    private void FlashRazorReadyGleam()
    {
        if (razorIndicator == null) return;

        Image razorImg = razorIndicator.GetComponent<Image>();
        if (razorImg != null)
        {
            razorImg.DOKill();
            razorImg.color = new Color(2.5f, 2.5f, 2.5f, 1f);
            razorImg.DOColor(Color.white, 0.35f).SetEase(Ease.OutQuad).SetLink(razorImg.gameObject, LinkBehaviour.KillOnDestroy);
        }

        razorIndicator.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), 0.25f, vibrato: 8, elasticity: 1f)
            .SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy);

        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlaySparkles(razorIndicator.position, Color.white, 0.85f);
        }
    }

    // ──────────────────────────────────────────────────────────
    // JUICE, SHAKE I ROZBŁYSKI
    // ──────────────────────────────────────────────────────────

    private void TriggerJuiceEffects(HitResult result)
    {
        RectTransform targetShake = shakeTransform != null 
            ? shakeTransform 
            : (minigameCanvasGroup != null ? minigameCanvasGroup.GetComponent<RectTransform>() : null);

        _shakeTween?.Kill();

        // Kołysanie wiszącego paska w 3D
        HangingStrapSway strapSway = FindAnyObjectByType<HangingStrapSway>();

        switch (result)
        {
            case HitResult.Perfect:
                if (targetShake != null && perfectShakeStrength > 0.01f)
                {
                    _shakeTween = targetShake
                        .DOShakePosition(0.25f, strength: new Vector3(perfectShakeStrength, perfectShakeStrength, 0f), vibrato: 20, randomness: 90, snapping: false, fadeOut: true)
                        .SetLink(targetShake.gameObject, LinkBehaviour.KillOnDestroy);
                }

                if (razorIndicator != null)
                {
                    razorIndicator.DOPunchScale(new Vector3(0.35f, 0.35f, 0f), 0.25f, vibrato: 10, elasticity: 1f)
                        .SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy);
                }

                if (sharpnessMarker != null)
                {
                    sharpnessMarker.DOPunchScale(new Vector3(0.45f, 0.45f, 0f), 0.28f, vibrato: 10, elasticity: 1f)
                        .SetLink(sharpnessMarker.gameObject, LinkBehaviour.KillOnDestroy);
                }

                strapSway?.Sway(12f);
                break;

            case HitResult.Good:
                if (targetShake != null && goodShakeStrength > 0.01f)
                {
                    _shakeTween = targetShake
                        .DOShakePosition(0.14f, strength: new Vector3(goodShakeStrength, goodShakeStrength, 0f), vibrato: 12, randomness: 90, snapping: false, fadeOut: true)
                        .SetLink(targetShake.gameObject, LinkBehaviour.KillOnDestroy);
                }

                if (razorIndicator != null)
                {
                    razorIndicator.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), 0.15f, vibrato: 6, elasticity: 1f)
                        .SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy);
                }

                if (sharpnessMarker != null)
                {
                    sharpnessMarker.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), 0.18f, vibrato: 6, elasticity: 1f)
                        .SetLink(sharpnessMarker.gameObject, LinkBehaviour.KillOnDestroy);
                }

                strapSway?.Sway(6f);
                break;

            case HitResult.TooEarly:
            case HitResult.TooLate:
                if (targetShake != null && missShakeStrength > 0.01f)
                {
                    _shakeTween = targetShake
                        .DOShakePosition(0.18f, strength: new Vector3(missShakeStrength, 0f, 0f), vibrato: 14, randomness: 45, snapping: false, fadeOut: true)
                        .SetLink(targetShake.gameObject, LinkBehaviour.KillOnDestroy);
                }
                break;
        }
    }

    private void UpdateHoverZoneGlow(float t)
    {
        bool inPerf = (t >= _calculatedPerfMinT && t <= _calculatedPerfMaxT);
        bool inGood = (!inPerf && t >= _calculatedGoodMinT && t <= _calculatedGoodMaxT);

        if (zonePerfectGlow != null)
        {
            zonePerfectGlow.alpha = inPerf ? Mathf.PingPong(Time.time * 6f, 0.4f) + 0.6f : 0f;
        }

        if (zoneGoodGlow != null)
        {
            zoneGoodGlow.alpha = inGood ? Mathf.PingPong(Time.time * 6f, 0.3f) + 0.4f : 0f;
        }
    }

    private void FlashPerfectZone()
    {
        if (zonePerfectGlow != null)
        {
            zonePerfectGlow.DOKill();
            zonePerfectGlow.alpha = 1f;
            zonePerfectGlow.DOFade(0f, zoneGlowDuration).SetEase(Ease.OutQuad).SetLink(zonePerfectGlow.gameObject, LinkBehaviour.KillOnDestroy);
            zonePerfectGlow.transform.DOPunchScale(new Vector3(0.25f, 0.25f, 0f), zoneGlowDuration, vibrato: 10, elasticity: 1f).SetLink(zonePerfectGlow.gameObject, LinkBehaviour.KillOnDestroy);
        }

        if (zonePerfectImage != null)
        {
            zonePerfectImage.DOKill();
            zonePerfectImage.color = new Color(2f, 1.8f, 1.2f, 1f);
            zonePerfectImage.DOColor(Color.white, zoneGlowDuration).SetEase(Ease.OutQuad).SetLink(zonePerfectImage.gameObject, LinkBehaviour.KillOnDestroy);
            zonePerfectImage.rectTransform.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), zoneGlowDuration, vibrato: 8).SetLink(zonePerfectImage.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else if (zonePerfect != null && zonePerfectGlow == null)
        {
            zonePerfect.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), zoneGlowDuration, vibrato: 8).SetLink(zonePerfect.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void FlashGoodZone()
    {
        if (zoneGoodGlow != null)
        {
            zoneGoodGlow.DOKill();
            zoneGoodGlow.alpha = 1f;
            zoneGoodGlow.DOFade(0f, zoneGlowDuration).SetEase(Ease.OutQuad).SetLink(zoneGoodGlow.gameObject, LinkBehaviour.KillOnDestroy);
            zoneGoodGlow.transform.DOPunchScale(new Vector3(0.18f, 0.18f, 0f), zoneGlowDuration, vibrato: 8, elasticity: 1f).SetLink(zoneGoodGlow.gameObject, LinkBehaviour.KillOnDestroy);
        }

        if (zoneGoodImage != null)
        {
            zoneGoodImage.DOKill();
            zoneGoodImage.color = new Color(1.5f, 2f, 1.5f, 1f);
            zoneGoodImage.DOColor(Color.white, zoneGlowDuration).SetEase(Ease.OutQuad).SetLink(zoneGoodImage.gameObject, LinkBehaviour.KillOnDestroy);
            zoneGoodImage.rectTransform.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), zoneGlowDuration, vibrato: 6).SetLink(zoneGoodImage.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else if (zoneGood != null && zoneGoodGlow == null)
        {
            zoneGood.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), zoneGlowDuration, vibrato: 6).SetLink(zoneGood.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void ResetZoneHighlights()
    {
        if (zoneGoodGlow != null)
        {
            zoneGoodGlow.DOKill();
            zoneGoodGlow.alpha = 0f;
            zoneGoodGlow.transform.localScale = Vector3.one;
        }

        if (zonePerfectGlow != null)
        {
            zonePerfectGlow.DOKill();
            zonePerfectGlow.alpha = 0f;
            zonePerfectGlow.transform.localScale = Vector3.one;
        }

        if (zoneGoodImage != null)
        {
            zoneGoodImage.DOKill();
            zoneGoodImage.color = Color.white;
            zoneGoodImage.rectTransform.localScale = Vector3.one;
        }
        else if (zoneGood != null)
        {
            zoneGood.DOKill();
            zoneGood.localScale = Vector3.one;
        }

        if (zonePerfectImage != null)
        {
            zonePerfectImage.DOKill();
            zonePerfectImage.color = Color.white;
            zonePerfectImage.rectTransform.localScale = Vector3.one;
        }
        else if (zonePerfect != null)
        {
            zonePerfect.DOKill();
            zonePerfect.localScale = Vector3.one;
        }
    }

    // ──────────────────────────────────────────────────────────
    // ZAKOŃCZENIE MINIGRY
    // ──────────────────────────────────────────────────────────

    private IEnumerator EndMinigameRoutine()
    {
        yield return new WaitForSeconds(endDelay);

        bool isSharp = _sharpness >= sharpThreshold;
        ShowFeedback(isSharp ? textBladeSharp : textBladeDull, isSharp ? perfectColor : missColor, isPunch: true);

        yield return new WaitForSeconds(0.8f);

        bool fadeFinished = false;
        HideUI(onComplete: () => fadeFinished = true);
        yield return new WaitUntil(() => fadeFinished);

        PreparationStateManager.Instance?.SetTaskState("razor_sharpened", isSharp);
        OnMinigameCompleted?.Invoke(_sharpness);

        _isCompleted = isSharp;
        _state = State.Inactive;
        LockPlayer(false);
    }

    // ──────────────────────────────────────────────────────────
    // OBLICZANIE ANCHORÓW I STREF
    // ──────────────────────────────────────────────────────────

    private void SetupAnchorsAndZones()
    {
        _bottomPos = bottomAnchor != null ? bottomAnchor.anchoredPosition : new Vector2(-233f, -442f);
        _topPos = topAnchor != null ? topAnchor.anchoredPosition : new Vector2(478f, 377f);

        // Zabezpieczenie przed nałożeniem punktów na siebie
        if (Vector2.Distance(_bottomPos, _topPos) < 20f)
        {
            _bottomPos = new Vector2(-233f, -442f);
            _topPos = new Vector2(478f, 377f);
        }

        if (autoDetectZoneBounds)
        {
            CalculateZoneRange(zoneGood, out _calculatedGoodMinT, out _calculatedGoodMaxT, goodZoneMinT, goodZoneMaxT);
            CalculateZoneRange(zonePerfect, out _calculatedPerfMinT, out _calculatedPerfMaxT, perfectZoneMinT, perfectZoneMaxT);

            // Jeśli strefy na siebie nachodzą, priorytet strefy Perfect odcina górę strefy Good
            if (_calculatedPerfMinT < _calculatedGoodMaxT && _calculatedPerfMinT > _calculatedGoodMinT)
            {
                _calculatedGoodMaxT = _calculatedPerfMinT;
            }
        }
        else
        {
            _calculatedGoodMinT = goodZoneMinT;
            _calculatedGoodMaxT = goodZoneMaxT;
            _calculatedPerfMinT = perfectZoneMinT;
            _calculatedPerfMaxT = perfectZoneMaxT;
        }

        Debug.Log($"[RazorMinigame] Strefy aktywne: Good=[{_calculatedGoodMinT:F2} - {_calculatedGoodMaxT:F2}], Perfect=[{_calculatedPerfMinT:F2} - {_calculatedPerfMaxT:F2}]");
    }

    private void CalculateZoneRange(RectTransform zoneRect, out float minT, out float maxT, float fallbackMin, float fallbackMax)
    {
        minT = fallbackMin;
        maxT = fallbackMax;

        if (zoneRect == null) return;

        Vector3[] corners = new Vector3[4];
        zoneRect.GetWorldCorners(corners);

        Vector3 p0 = bottomAnchor != null ? bottomAnchor.position : (Vector3)_bottomPos;
        Vector3 p1 = topAnchor != null ? topAnchor.position : (Vector3)_topPos;
        Vector3 axis = p1 - p0;
        float axisLenSq = axis.sqrMagnitude;

        if (axisLenSq < 0.001f) return;

        float tMin = float.MaxValue;
        float tMax = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            float t = Vector3.Dot(corners[i] - p0, axis) / axisLenSq;
            if (t < tMin) tMin = t;
            if (t > tMax) tMax = t;
        }

        minT = Mathf.Clamp01(tMin);
        maxT = Mathf.Clamp01(tMax);

        if (maxT - minT < 0.05f)
        {
            minT = fallbackMin;
            maxT = fallbackMax;
        }
    }

    // ──────────────────────────────────────────────────────────
    // BLOKADY I POMOCNIKI UI
    // ──────────────────────────────────────────────────────────

    private void LockPlayer(bool locked)
    {
        if (InputModeManager.Instance != null)
        {
            if (locked)
                InputModeManager.Instance.SwitchToMinigame(unlockCursor: false);
            else
                InputModeManager.Instance.SwitchToPlayer();
        }

        if (playerMovement != null) playerMovement.enabled = !locked;
        if (playerHands != null) playerHands.enabled = !locked;
        if (cinemachineBrain != null) cinemachineBrain.enabled = !locked;

        if (headBobbing == null)
            headBobbing = UnityEngine.Object.FindAnyObjectByType<HeadBobbing>(FindObjectsInactive.Include);

        if (headBobbing != null)
        {
            headBobbing.enabled = !locked;
        }
    }

    private void UpdateAttemptsText()
    {
        if (attemptsText != null)
        {
            int currentAttempt = Mathf.Min(_attemptsDone + 1, totalAttempts);
            int remaining = Mathf.Max(0, totalAttempts - _attemptsDone);
            int percent = Mathf.RoundToInt(_sharpness * 100f);
            attemptsText.text = $"ATTEMPT: {currentAttempt} / {totalAttempts}   •   LEFT: {remaining}   •   SHARPNESS: {percent}%";
        }
    }

    private void UpdateSharpnessMarker(bool animate)
    {
        UpdateAttemptsText();

        float targetX = Mathf.Lerp(-barHalfWidth, barHalfWidth, _sharpness);
        _markerTween?.Kill();
        _fillTween?.Kill();

        if (animate)
        {
            if (sharpnessMarker != null)
            {
                _markerTween = sharpnessMarker
                    .DOAnchorPosX(targetX, markerMoveDuration)
                    .SetEase(Ease.OutBack)
                    .SetLink(sharpnessMarker.gameObject, LinkBehaviour.KillOnDestroy);
            }

            if (sharpnessFillImage != null)
            {
                _fillTween = sharpnessFillImage
                    .DOFillAmount(_sharpness, markerMoveDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(sharpnessFillImage.gameObject, LinkBehaviour.KillOnDestroy);
            }
        }
        else
        {
            if (sharpnessMarker != null)
            {
                sharpnessMarker.anchoredPosition = new Vector2(targetX, sharpnessMarker.anchoredPosition.y);
            }
            if (sharpnessFillImage != null)
            {
                sharpnessFillImage.fillAmount = _sharpness;
            }
        }
    }

    private void SetInstructionPrompt(string msg)
    {
        if (instructionText != null)
        {
            instructionText.text = msg;
            instructionText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            return;
        }

        if (pressToStartUI != null)
        {
            TMP_Text tmp = pressToStartUI.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = msg;
            }
            pressToStartUI.SetActive(!string.IsNullOrEmpty(msg));
        }
    }

    private void ShowFeedback(string msg, Color? textColor = null, bool isPunch = false)
    {
        if (feedbackText == null) return;

        _feedbackTween?.Kill();
        feedbackText.transform.localScale = Vector3.one;

        if (string.IsNullOrEmpty(msg))
        {
            feedbackText.text = string.Empty;
            return;
        }

        feedbackText.text = msg;
        if (textColor.HasValue)
        {
            feedbackText.color = textColor.Value;
        }

        if (isPunch)
        {
            _feedbackTween = feedbackText.transform
                .DOPunchScale(new Vector3(0.45f, 0.45f, 0f), 0.28f, vibrato: 10, elasticity: 1f)
                .SetLink(feedbackText.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            _feedbackTween = feedbackText.transform
                .DOPunchScale(new Vector3(0.2f, 0.2f, 0f), 0.18f, vibrato: 6, elasticity: 1f)
                .SetLink(feedbackText.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void ShowUI(Action onComplete = null)
    {
        if (minigameCanvasGroup == null) { onComplete?.Invoke(); return; }

        minigameCanvasGroup.alpha = 0f;
        minigameCanvasGroup.blocksRaycasts = true;
        minigameCanvasGroup.interactable = true;

        _fadeTween?.Kill();
        _fadeTween = minigameCanvasGroup
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(minigameCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void HideUI(Action onComplete = null)
    {
        if (minigameCanvasGroup == null) { onComplete?.Invoke(); return; }

        _fadeTween?.Kill();
        _fadeTween = minigameCanvasGroup
            .DOFade(0f, fadeDuration)
            .SetEase(Ease.InQuad)
            .SetLink(minigameCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                minigameCanvasGroup.blocksRaycasts = false;
                minigameCanvasGroup.interactable = false;
                onComplete?.Invoke();
            });
    }

    private void HideInstant()
    {
        if (minigameCanvasGroup != null)
        {
            minigameCanvasGroup.alpha = 0f;
            minigameCanvasGroup.interactable = false;
            minigameCanvasGroup.blocksRaycasts = false;
        }

        if (guideOverlayUI != null)
            guideOverlayUI.SetActive(false);

        ResetZoneHighlights();
    }

    // ──────────────────────────────────────────────────────────
    // DEV / DEBUG METODY
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Wymusza natychmiastowe uruchomienie minigry (omija wymóg trzymania żyletki).
    /// </summary>
    public void ForceStartMinigame()
    {
        if (_state != State.Inactive)
        {
            KillAllTweens();
            _state = State.Inactive;
        }

        _isCompleted = false;
        StartMinigame();
    }

    /// <summary>
    /// Wymusza natychmiastowe zaliczenie minigry z podaną ostrością (np. 100%).
    /// </summary>
    public void ForceCompleteMinigame(float sharpnessPercent = 100f)
    {
        KillAllTweens();
        _sharpness = sharpnessPercent;
        _isCompleted = true;
        _state = State.Inactive;
        HideUI();
        LockPlayer(false);
        PreparationStateManager.Instance?.SetTaskState("razor_sharpened", true);
        OnMinigameCompleted?.Invoke(_sharpness);
    }

    /// <summary>
    /// Resetuje stan minigry, umożliwiając ponowne ostrzenie.
    /// </summary>
    public void ResetMinigameState()
    {
        KillAllTweens();
        _sharpness = 0f;
        _attemptsDone = 0;
        _isCompleted = false;
        _state = State.Inactive;
        HideUI();
        LockPlayer(false);
        PreparationStateManager.Instance?.SetTaskState("razor_sharpened", false);
    }

    public bool RequireBladeItem
    {
        get => requireBladeItem;
        set => requireBladeItem = value;
    }

    public float CurrentSharpness => _sharpness;
    public bool IsCompleted => _isCompleted;
    public string CurrentStateName => _state.ToString();

    private enum HitResult { TooEarly, Good, Perfect, TooLate }
}
