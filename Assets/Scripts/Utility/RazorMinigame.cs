using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RazorMinigame : MonoBehaviour, IInteractable
{
    public string InteractionName => "Sharpen Razor";
    public void Interact() => StartMinigame();
    // ──────────────────────────────────────────────────────────
    // UI REFERENCES & CANVAS
    // ──────────────────────────────────────────────────────────

    [Header("UI Canvas & Panels")]
    [Tooltip("CanvasGroup of the whole minigame UI (fade in/out).")]
    [SerializeField] private CanvasGroup minigameCanvasGroup;

    [Tooltip("Moving razor indicator image.")]
    [SerializeField] private RectTransform razorIndicator;

    [Tooltip("Sharpness progress bar fill image.")]
    [SerializeField] private Image sharpnessFillImage;

    [Tooltip("Text displaying current sharpness percentage (e.g. 75%).")]
    [SerializeField] private TextMeshProUGUI sharpnessPercentageText;

    [Tooltip("Text displaying attempts / passes (e.g. Pass: 1 / 4).")]
    [SerializeField] private TextMeshProUGUI attemptsText;

    [Tooltip("Floating result feedback text (GOOD!, TOO EARLY!, TOO LATE!).")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Tooltip("Instructions / controls text (e.g. HOLD [SPACE] / [LMB] & DRAG UP).")]
    [SerializeField] private TextMeshProUGUI instructionText;

    [Tooltip("Target sharpness marker (dashed line / notch on progress bar).")]
    [SerializeField] private RectTransform sharpnessTargetMarker;

    [Tooltip("Guide / tutorial overlay panel object.")]
    [SerializeField] private GameObject guideOverlayUI;

    [Tooltip("CanvasGroup for smooth tutorial overlay fade in/out.")]
    [SerializeField] private CanvasGroup guideOverlayCanvasGroup;

    [Tooltip("Display tutorial card automatically on first start?")]
    [SerializeField] private bool showTutorialOnStart = true;

    [Tooltip("Transform used for screen-shake juice effects (typically Main Canvas or Background).")]
    [SerializeField] private RectTransform shakeTransform;

    [Tooltip("UI panel displayed before starting (e.g. 'Press Space to Start').")]
    [SerializeField] private GameObject pressToStartUI;

    // ──────────────────────────────────────────────────────────
    // PATH & HIT ZONES
    // ──────────────────────────────────────────────────────────

    [Header("Razor Path (Waypoints)")]
    [Tooltip("Waypoints defining the razor's stropping path. Defaults to 5 points from bottom to top.")]
    [SerializeField] private Vector2[] waypoints = new Vector2[5]
    {
        new Vector2(45.98469f, -114.9189f), // P1 - Start / Bottom Anchor
        new Vector2(153.98f, 8.08f),         // P2 - Lower Strop
        new Vector2(261.99f, 131.08f),       // P3 - Good Zone Center
        new Vector2(369.99f, 254.08f),       // P4 - Upper Strop
        new Vector2(478.00f, 377.00f)        // P5 - Top / Top Anchor
    };

    [Tooltip("(Optional) Transform GameObjects in the hierarchy for waypoints (e.g. Waypoint_1..Waypoint_5). If assigned, their positions take priority.")]
    [SerializeField] private RectTransform[] waypointTransforms;

    [Tooltip("Draw path line and waypoint gizmos in Unity Scene View?")]
    [SerializeField] private bool showPathGizmos = true;

    [Tooltip("Bottom start anchor (fallback).")]
    [SerializeField] private RectTransform bottomAnchor;

    [Tooltip("Top apex anchor (fallback).")]
    [SerializeField] private RectTransform topAnchor;

    [Header("Gentle Wrist Flip on Return (In Place)")]
    [Tooltip("Small angle flip/tilt upon returning to the bottom (degrees, default: 18°).")]
    [SerializeField] private float returnFlipAngle = 18f;

    [Tooltip("Duration of the gentle flip/tilt after returning (seconds).")]
    [SerializeField] private float flipDuration = 0.25f;

    [Header("Hit Zones (Transforms)")]
    [Tooltip("Transform / Panel of the GOOD hit zone.")]
    [SerializeField] private RectTransform zoneGood;

    [Header("Zone Bounds (0..1 along path)")]
    [Tooltip("Automatically calculate zone boundaries from UI RectTransform?")]
    [SerializeField] private bool autoDetectZoneBounds = true;

    [Tooltip("Start of the GOOD zone (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float goodZoneMinT = 0.10f;

    [Tooltip("End of the GOOD zone (0..1).")]
    [Range(0f, 1f)] [SerializeField] private float goodZoneMaxT = 0.48f;

    [Header("Zone Glow / Feedback Effects")]
    [Tooltip("(Optional) Image component for the GOOD zone highlight.")]
    [SerializeField] private Image zoneGoodImage;

    [Tooltip("(Optional) CanvasGroup for GOOD zone glow pulse.")]
    [SerializeField] private CanvasGroup zoneGoodGlow;

    [Tooltip("Duration of the zone glow burst upon hit (seconds).")]
    [SerializeField] private float zoneGlowDuration = 0.35f;

    // ──────────────────────────────────────────────────────────
    // TIMINGS & MOVEMENT (SECONDS)
    // ──────────────────────────────────────────────────────────

    [Header("Stroke Timings & Speedup (Seconds)")]
    [Tooltip("Full travel time from bottom to top in attempt 1 while holding stroke (default: 2.0s).")]
    [SerializeField] private float initialTravelTime = 2.0f;

    [Tooltip("Noticeable time reduction per consecutive attempt (makes stroke faster each pass, default: 0.35s).")]
    [SerializeField] private float timeReductionPerAttempt = 0.35f;

    [Tooltip("Minimum stroke travel time cap (default: 0.8s).")]
    [SerializeField] private float minTravelTime = 0.8f;

    [Tooltip("Duration to return blade downwards to bottom after stroke (default: 0.4s).")]
    [SerializeField] private float returnDuration = 0.4f;

    [Header("In-Stroke Acceleration (Realistic Pull)")]
    [Tooltip("Acceleration curve exponent for the single stroke motion (1.0 = linear, 1.75 = starts slower at bottom and accelerates faster and faster up the strop).")]
    [SerializeField] private float strokeAccelerationExponent = 1.75f;

    [Header("Dynamic Blade Lean & Strop Engagement")]
    [Tooltip("Angle to smoothly lean/rotate the razor into the leather strop during the stroke (degrees, default: 6°).")]
    [SerializeField] private float strokeLeanAngle = 6.0f;

    [Tooltip("Subtle breathing sway of the blade along the leather (degrees, default: 0.8°).")]
    [SerializeField] private float subtleSwayAngle = 0.8f;

    // ──────────────────────────────────────────────────────────
    // SHARPNESS & GOAL (FILL TO 100%)
    // ──────────────────────────────────────────────────────────

    [Header("Sharpness & Goal")]
    [Tooltip("Initial sharpness percentage (0..100%).")]
    [Range(0f, 100f)] [SerializeField] private float initialSharpness = 0f;

    [Tooltip("Sharpness percentage threshold required for success (default: 100%).")]
    [Range(10f, 100f)] [SerializeField] private float sharpThreshold = 100f;

    [Tooltip("Sharpness gained per GOOD hit (default: 20%).")]
    [SerializeField] private float gainGood = 20f;

    [Tooltip("Sharpness penalty on MISS (default: 10%).")]
    [SerializeField] private float penaltyMiss = 10f;

    [Tooltip("Reference attempts for speed scaling.")]
    [SerializeField] private int totalAttempts = 4;
    public int TotalAttempts => totalAttempts;

    [Tooltip("Delay before closing minigame after completion (seconds).")]
    [SerializeField] private float endDelay = 1.0f;

    // ──────────────────────────────────────────────────────────
    // PROMPTS & MESSAGES (ENGLISH)
    // ──────────────────────────────────────────────────────────

    [Header("English UI Prompts")]
    [SerializeField] private string promptStartMinigame = "PRESS [SPACE] OR CLICK TO START";
    [SerializeField] private string promptHoldToSharpen = "HOLD [SPACE] / [LMB] & DRAG UP  |  PRESS [E] IN GOOD ZONE";
    [SerializeField] private string promptStrokeInFlight = "PRESS [E] / [INTERACT] IN GOOD ZONE!";
    [SerializeField] private string promptReturning = "FLIPPING BLADE...";

    [Header("Hit Result Texts")]
    [SerializeField] private string textGood = "GOOD!";
    [SerializeField] private string textTooEarly = "TOO EARLY!";
    [SerializeField] private string textTooLate = "TOO LATE!";
    [SerializeField] private string textBladeSharp = "RAZOR SHARPENED!";
    [SerializeField] private string textBladeDull = "RAZOR IS TOO DULL!";

    // ──────────────────────────────────────────────────────────
    // JUICE & VISUALS
    // ──────────────────────────────────────────────────────────

    [Header("Juice & Shake")]
    [Tooltip("Screen shake strength on GOOD hit.")]
    [SerializeField] private float goodShakeStrength = 7f;

    [Tooltip("Screen shake strength on MISS.")]
    [SerializeField] private float missShakeStrength = 8f;

    [Tooltip("Text color for GOOD hit.")]
    [SerializeField] private Color goodColor = new Color(0.4f, 0.9f, 0.4f, 1f);

    [Tooltip("Text color for MISS.")]
    [SerializeField] private Color missColor = new Color(0.9f, 0.25f, 0.25f, 1f);

    // ──────────────────────────────────────────────────────────
    // AUDIO
    // ──────────────────────────────────────────────────────────

    [Header("Audio SFX Names")]
    [SerializeField] private string soundPassUp = "ostrzenie_wolne";
    [SerializeField] private string soundGood = "sharpen_good";
    [SerializeField] private string soundMiss = "sharpen_miss";

    // ──────────────────────────────────────────────────────────
    // EXTERNAL INTEGRATION
    // ──────────────────────────────────────────────────────────

    [Header("External Integration")]
    [Tooltip("Requires razor blade item in player hands before starting?")]
    [SerializeField] private bool requireBladeItem = false;

    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHands playerHands;
    [SerializeField] private Unity.Cinemachine.CinemachineBrain cinemachineBrain;
    [SerializeField] private HeadBobbing headBobbing;

    [Header("Input System Actions")]
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference dragAction;

    // Events
    public static event Action<float> OnMinigameCompleted;

    // Internal State
    private enum State
    {
        Inactive,
        Tutorial,
        ReadyForStroke,
        HoldingStroke,
        EvaluatingHit,
        ReturningDown,
        Finished
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

    private Vector2 _bottomPos = new Vector2(45.98469f, -114.9189f);
    private Vector2 _topPos = new Vector2(478f, 377f);
    private float _baseZRotation = -73.61f;
    private float _calculatedGoodMinT = 0.10f;
    private float _calculatedGoodMaxT = 0.48f;

    private Tween _razorTween;
    private Tween _markerTween;
    private Tween _fillTween;
    private Tween _fadeTween;
    private Tween _shakeTween;
    private Tween _feedbackTween;

    // ──────────────────────────────────────────────────────────
    // LIFECYCLE
    // ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        if (minigameCanvasGroup == null)
        {
            minigameCanvasGroup = GetComponent<CanvasGroup>() ?? GetComponentInChildren<CanvasGroup>(true);
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasLayerManager.LAYER_MINIGAME;
        }

        HideUIImmediate();
    }

    private void Start()
    {
        _sharpness = initialSharpness;
        SetupAnchorsAndZones();
    }

    private void Update()
    {
        if (_state == State.Inactive || _state == State.Finished) return;

        switch (_state)
        {
            case State.Tutorial:
                HandleTutorialState();
                break;

            case State.ReadyForStroke:
                HandleReadyState();
                break;

            case State.HoldingStroke:
                HandleHoldingState();
                break;
        }
    }

    private void OnDisable()
    {
        KillAllTweens();
    }

    // ──────────────────────────────────────────────────────────
    // MINIGAME CONTROL & INITIALIZATION
    // ──────────────────────────────────────────────────────────

    public void StartMinigame()
    {
        if (_state != State.Inactive) return;

        if (requireBladeItem && !IsPlayerHoldingRazor())
        {
            Debug.Log("[RazorMinigame] Player must hold a razor blade in hands to use the strop!");
            return;
        }

        _state = State.ReadyForStroke;
        _attemptsDone = 0;
        _sharpness = initialSharpness;
        _strokeProgress = 0f;
        _isCompleted = false;

        SetupAnchorsAndZones();
        ShowUI();

        // Enable input actions
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.Enable();
        }
        if (dragAction != null && dragAction.action != null)
        {
            dragAction.action.Enable();
        }

        // Set razor to bottom start position
        if (razorIndicator != null)
        {
            _razorTween?.Kill();
            razorIndicator.anchoredPosition = GetStartWaypoint();
            razorIndicator.localScale = Vector3.one;
            razorIndicator.localRotation = Quaternion.Euler(0f, 0f, _baseZRotation);
        }

        LockPlayer(true);

        UpdateAttemptsText();
        UpdateSharpnessMarker(animate: false);
        ShowFeedback(string.Empty);

        // Guide Overlay
        if (guideOverlayUI == null && minigameCanvasGroup != null)
        {
            Transform guideChild = minigameCanvasGroup.transform.Find("Guide");
            if (guideChild != null) guideOverlayUI = guideChild.gameObject;
        }

        if (guideOverlayUI != null)
        {
            guideOverlayUI.transform.SetAsLastSibling();
        }

        if (showTutorialOnStart && guideOverlayUI != null)
        {
            _state = State.Tutorial;
            _tutorialStartTime = Time.unscaledTime;
            guideOverlayUI.SetActive(true);
            if (guideOverlayCanvasGroup != null)
            {
                guideOverlayCanvasGroup.alpha = 1f;
                guideOverlayCanvasGroup.blocksRaycasts = true;
                guideOverlayCanvasGroup.interactable = true;
            }
            SetInstructionPrompt(promptStartMinigame);
        }
        else
        {
            if (guideOverlayUI != null) guideOverlayUI.SetActive(false);
            _state = State.ReadyForStroke;
            SetInstructionPrompt(promptHoldToSharpen);
        }
    }

    public void CloseMinigame()
    {
        if (_state == State.Inactive) return;

        KillAllTweens();
        _state = State.Inactive;
        HideUI();
        LockPlayer(false);
    }

    // ──────────────────────────────────────────────────────────
    // STATE LOGIC
    // ──────────────────────────────────────────────────────────

    private void HandleTutorialState()
    {
        if (Time.unscaledTime - _tutorialStartTime < 0.25f) return;

        if (WasTutorialDismissPressed())
        {
            DismissTutorial();
        }
    }

    public void DismissTutorial()
    {
        if (_state != State.Tutorial) return;

        if (guideOverlayCanvasGroup != null)
        {
            guideOverlayCanvasGroup.DOFade(0f, 0.25f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (guideOverlayUI != null) guideOverlayUI.SetActive(false);
                });
        }
        else if (guideOverlayUI != null)
        {
            guideOverlayUI.SetActive(false);
        }

        _state = State.ReadyForStroke;
        _strokeProgress = 0f;
        SetInstructionPrompt(promptHoldToSharpen);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play("card_flip");
        }
    }

    private void HandleReadyState()
    {
        if (IsStrokeInputHeld())
        {
            _state = State.HoldingStroke;
            _flightTimer = 0f;
            SetInstructionPrompt(promptStrokeInFlight);

            if (AudioManager.Instance != null && !string.IsNullOrEmpty(soundPassUp))
            {
                AudioManager.Instance.Play(soundPassUp);
            }
        }
    }

    private void HandleHoldingState()
    {
        // 1. If player released space/drag before striking
        if (!IsStrokeInputHeld())
        {
            HandleEarlyReleaseMiss();
            return;
        }

        // 2. Advance stroke progress with realistic in-stroke acceleration (starts slower, builds momentum and accelerates up the leather)
        _flightTimer += Time.deltaTime;
        float duration = CalculateCurrentTravelDuration();

        // Physical mouse drag up support
        if (Mouse.current != null)
        {
            float mouseDeltaY = Mouse.current.delta.y.ReadValue();
            if (mouseDeltaY > 0f)
            {
                _flightTimer += (mouseDeltaY * 0.0015f);
            }
        }

        float normalizedTime = Mathf.Clamp01(_flightTimer / duration);
        _strokeProgress = Mathf.Pow(normalizedTime, strokeAccelerationExponent);
        _strokeProgress = Mathf.Clamp01(_strokeProgress);

        // 3. Move smoothly along waypoints path with natural leather engagement tilt
        if (razorIndicator != null)
        {
            razorIndicator.anchoredPosition = EvaluatePathPosition(_strokeProgress);

            // Natural pressure lean into the leather strop (peaks smoothly in the middle / good zone)
            float leatherEngagementLean = Mathf.Sin(_strokeProgress * Mathf.PI) * strokeLeanAngle;
            float gentleSway = Mathf.Sin(Time.time * 6f) * subtleSwayAngle;

            razorIndicator.localRotation = Quaternion.Euler(0f, 0f, _baseZRotation + leatherEngagementLean + gentleSway);
            razorIndicator.localScale = Vector3.one;
        }

        // 4. Hover zone glow
        UpdateHoverZoneGlow(_strokeProgress);

        // 5. Strike on Interact (E / LMB)
        if (WasInteractStrikePressed())
        {
            if (_flightTimer >= 0.05f)
            {
                EvaluateAndHandleStrike();
                return;
            }
        }

        // 6. Overshot top without pressing Interact -> Miss
        if (normalizedTime >= 1f || _strokeProgress >= 1f)
        {
            HandleReachedTopMiss();
        }
    }

    private bool WasTutorialDismissPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
                return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        if (interactAction != null && interactAction.action != null && interactAction.action.WasPressedThisFrame())
            return true;

        return false;
    }

    private bool IsStrokeInputHeld()
    {
        if (dragAction != null && dragAction.action != null && dragAction.action.IsPressed())
            return true;

        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;

        return false;
    }

    private bool WasInteractStrikePressed()
    {
        if (interactAction != null && interactAction.action != null && interactAction.action.WasPressedThisFrame())
            return true;

        if (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        return false;
    }

    // ──────────────────────────────────────────────────────────
    // HIT EVALUATION & SCORING
    // ──────────────────────────────────────────────────────────

    private void EvaluateAndHandleStrike()
    {
        _state = State.EvaluatingHit;

        float currentT = _strokeProgress;
        HitResult result;

        if (currentT < _calculatedGoodMinT)
        {
            result = HitResult.TooEarly;
        }
        else if (currentT <= _calculatedGoodMaxT)
        {
            result = HitResult.Good;
        }
        else
        {
            result = HitResult.TooLate;
        }

        ApplyHitOutcome(result);
        StartReturnDownwards(wasInterrupted: false);
    }

    private void HandleEarlyReleaseMiss()
    {
        _state = State.EvaluatingHit;
        ApplyHitOutcome(HitResult.TooEarly);
        StartReturnDownwards(wasInterrupted: true);
    }

    private void HandleReachedTopMiss()
    {
        _state = State.EvaluatingHit;
        ApplyHitOutcome(HitResult.TooLate);
        StartReturnDownwards(wasInterrupted: false);
    }

    private void ApplyHitOutcome(HitResult result)
    {
        switch (result)
        {
            case HitResult.Good:
                _sharpness = Mathf.Clamp(_sharpness + gainGood, 0f, 100f);
                ShowFeedback(textGood, goodColor, isPunch: true);
                FlashZoneGlow(zoneGoodGlow, zoneGoodImage, goodColor);
                if (AudioManager.Instance != null && !string.IsNullOrEmpty(soundGood))
                {
                    AudioManager.Instance.Play(soundGood);
                }
                break;

            case HitResult.TooEarly:
                _sharpness = Mathf.Clamp(_sharpness - penaltyMiss, 0f, 100f);
                ShowFeedback(textTooEarly, missColor, isPunch: false);
                if (AudioManager.Instance != null && !string.IsNullOrEmpty(soundMiss))
                {
                    AudioManager.Instance.Play(soundMiss);
                }
                break;

            case HitResult.TooLate:
                _sharpness = Mathf.Clamp(_sharpness - penaltyMiss, 0f, 100f);
                ShowFeedback(textTooLate, missColor, isPunch: false);
                if (AudioManager.Instance != null && !string.IsNullOrEmpty(soundMiss))
                {
                    AudioManager.Instance.Play(soundMiss);
                }
                break;
        }

        TriggerJuiceEffects(result);
        UpdateSharpnessMarker(animate: true);
    }

    private void StartReturnDownwards(bool wasInterrupted = false)
    {
        _state = State.ReturningDown;
        SetInstructionPrompt(wasInterrupted ? promptHoldToSharpen : promptReturning);

        _razorTween?.Kill();
        if (razorIndicator != null)
        {
            razorIndicator.DORotate(new Vector3(0f, 0f, _baseZRotation), returnDuration * 0.4f).SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy);
            _razorTween = razorIndicator
                .DOAnchorPos(GetStartWaypoint(), wasInterrupted ? (returnDuration * 0.4f) : returnDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (wasInterrupted)
                    {
                        _state = State.ReadyForStroke;
                        _strokeProgress = 0f;
                        SetInstructionPrompt(promptHoldToSharpen);
                    }
                    else
                    {
                        OnReturnedToBottom();
                    }
                });
        }
        else
        {
            if (wasInterrupted)
            {
                _state = State.ReadyForStroke;
                _strokeProgress = 0f;
                SetInstructionPrompt(promptHoldToSharpen);
            }
            else
            {
                OnReturnedToBottom();
            }
        }
    }

    private void OnReturnedToBottom()
    {
        _attemptsDone++;
        UpdateAttemptsText();

        if (_sharpness >= sharpThreshold)
        {
            _state = State.Finished;
            StartCoroutine(EndMinigameRoutine());
        }
        else
        {
            // Pełny obrót o 360 stopni (FastBeyond360) przy każdym powrocie
            if (razorIndicator != null)
            {
                razorIndicator.DOKill();
                razorIndicator.anchoredPosition = GetStartWaypoint();
                razorIndicator.localScale = Vector3.one;
                razorIndicator.localRotation = Quaternion.Euler(0f, 0f, _baseZRotation);

                Sequence flipSeq = DOTween.Sequence();
                // OBRÓT O 360 STOPNI:
                flipSeq.Append(razorIndicator.DORotate(new Vector3(0f, 0f, _baseZRotation - 360f), flipDuration, RotateMode.FastBeyond360).SetEase(Ease.InOutBack));
                
                flipSeq.SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy);
                flipSeq.OnComplete(() =>
                {
                    razorIndicator.localRotation = Quaternion.Euler(0f, 0f, _baseZRotation);
                    FlashRazorReadyGleam();
                    _state = State.ReadyForStroke;
                    _strokeProgress = 0f;
                    SetInstructionPrompt(promptHoldToSharpen);
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

        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlaySparkles(razorIndicator.position, Color.white, 0.85f);
        }
    }

    // ──────────────────────────────────────────────────────────
    // JUICE & SHAKE
    // ──────────────────────────────────────────────────────────

    private void TriggerJuiceEffects(HitResult result)
    {
        RectTransform targetShake = shakeTransform != null 
            ? shakeTransform 
            : (minigameCanvasGroup != null ? minigameCanvasGroup.GetComponent<RectTransform>() : null);

        if (targetShake != null)
        {
            _shakeTween?.Kill();
            float str = (result == HitResult.Good) ? goodShakeStrength : missShakeStrength;
            _shakeTween = targetShake.DOShakeAnchorPos(0.25f, str, vibrato: 14, randomness: 90f)
                .SetLink(targetShake.gameObject, LinkBehaviour.KillOnDestroy);
        }

        if (result == HitResult.Good && ParticleManager.Instance != null && razorIndicator != null)
        {
            ParticleManager.Instance.PlaySparkles(razorIndicator.position, goodColor, 1.2f);
        }
    }

    private void FlashZoneGlow(CanvasGroup glowCg, Image zoneImg, Color flashCol)
    {
        if (glowCg != null)
        {
            glowCg.DOKill();
            glowCg.alpha = 1f;
            glowCg.DOFade(0f, zoneGlowDuration).SetEase(Ease.OutQuad).SetLink(glowCg.gameObject, LinkBehaviour.KillOnDestroy);
        }

        if (zoneImg != null)
        {
            zoneImg.DOKill();
            zoneImg.rectTransform.DOKill();
            zoneImg.color = flashCol * 1.6f;
            zoneImg.DOColor(Color.white, zoneGlowDuration).SetLink(zoneImg.gameObject, LinkBehaviour.KillOnDestroy);
            zoneImg.rectTransform.DOPunchScale(new Vector3(0.08f, 0.08f, 0f), zoneGlowDuration, vibrato: 6, elasticity: 0.8f)
                .SetLink(zoneImg.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void UpdateHoverZoneGlow(float t)
    {
        if (zoneGoodGlow != null)
        {
            bool isInside = (t >= _calculatedGoodMinT && t <= _calculatedGoodMaxT);
            float targetAlpha = isInside ? 0.85f : 0f;
            zoneGoodGlow.alpha = Mathf.MoveTowards(zoneGoodGlow.alpha, targetAlpha, Time.deltaTime * 8f);
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
    }

    // ──────────────────────────────────────────────────────────
    // END OF MINIGAME
    // ──────────────────────────────────────────────────────────

    private IEnumerator EndMinigameRoutine()
    {
        yield return new WaitForSeconds(endDelay);

        bool isSharp = _sharpness >= sharpThreshold;
        ShowFeedback(isSharp ? textBladeSharp : textBladeDull, isSharp ? goodColor : missColor, isPunch: true);

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
    // ANCHORS & ZONES CALCULATION
    // ──────────────────────────────────────────────────────────

    private void SetupAnchorsAndZones()
    {
        _bottomPos = bottomAnchor != null ? bottomAnchor.anchoredPosition : new Vector2(45.98469f, -114.9189f);
        _topPos = topAnchor != null ? topAnchor.anchoredPosition : new Vector2(478f, 377f);

        if (razorIndicator != null)
        {
            _baseZRotation = razorIndicator.localEulerAngles.z;
            if (_baseZRotation > 180f) _baseZRotation -= 360f;
            if (Mathf.Abs(_baseZRotation) < 0.1f) _baseZRotation = -73.61f;
        }

        // Safety against overlapping points
        if (Vector2.Distance(_bottomPos, _topPos) < 20f)
        {
            _bottomPos = new Vector2(45.98469f, -114.9189f);
            _topPos = new Vector2(478f, 377f);
        }

        if (autoDetectZoneBounds && zoneGood != null)
        {
            CalculateZoneRange(zoneGood, out _calculatedGoodMinT, out _calculatedGoodMaxT, goodZoneMinT, goodZoneMaxT);
        }
        else
        {
            _calculatedGoodMinT = goodZoneMinT;
            _calculatedGoodMaxT = goodZoneMaxT;
        }

        Debug.Log($"[RazorMinigame] Active Zone: Good=[{_calculatedGoodMinT:F2} - {_calculatedGoodMaxT:F2}], StartPos={GetStartWaypoint()}, Rotation={_baseZRotation:F1}°");
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
    }

    private float CalculateCurrentTravelDuration()
    {
        float dur = initialTravelTime - (_attemptsDone * timeReductionPerAttempt);
        return Mathf.Max(dur, minTravelTime);
    }

    // ──────────────────────────────────────────────────────────
    // UI UPDATES & TWEENS
    // ──────────────────────────────────────────────────────────

    private void ShowUI()
    {
        if (minigameCanvasGroup == null) return;

        _fadeTween?.Kill();
        minigameCanvasGroup.gameObject.SetActive(true);
        _fadeTween = minigameCanvasGroup.DOFade(1f, 0.35f)
            .SetEase(Ease.OutQuad)
            .SetLink(minigameCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void HideUI(Action onComplete = null)
    {
        if (minigameCanvasGroup == null)
        {
            onComplete?.Invoke();
            return;
        }

        _fadeTween?.Kill();
        _fadeTween = minigameCanvasGroup.DOFade(0f, 0.35f)
            .SetEase(Ease.InQuad)
            .SetLink(minigameCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                minigameCanvasGroup.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private void HideUIImmediate()
    {
        if (minigameCanvasGroup == null) return;
        minigameCanvasGroup.alpha = 0f;
        minigameCanvasGroup.gameObject.SetActive(false);
    }

    private void UpdateAttemptsText()
    {
        if (attemptsText != null)
        {
            attemptsText.text = $"SHARPNESS: {Mathf.RoundToInt(_sharpness)}%";
        }
    }

    private void UpdateSharpnessMarker(bool animate = true)
    {
        float ratio = Mathf.Clamp01(_sharpness / 100f);

        // 1. Smooth fill of the gradient bar
        if (sharpnessFillImage != null)
        {
            _fillTween?.Kill();
            if (animate)
            {
                _fillTween = sharpnessFillImage.DOFillAmount(ratio, 0.35f)
                    .SetEase(Ease.OutQuad)
                    .SetLink(sharpnessFillImage.gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                sharpnessFillImage.fillAmount = ratio;
            }
        }

        // 2. Percentage Text (if assigned)
        if (sharpnessPercentageText != null)
        {
            sharpnessPercentageText.text = $"{Mathf.RoundToInt(_sharpness)}%";
        }

        // 3. Hide target needle marker for clean progress bar
        if (sharpnessTargetMarker != null && sharpnessTargetMarker.gameObject.activeSelf)
        {
            sharpnessTargetMarker.gameObject.SetActive(false);
        }

        UpdateAttemptsText();
    }

    private void ShowFeedback(string text, Color? color = null, bool isPunch = false)
    {
        if (feedbackText == null) return;

        _feedbackTween?.Kill();
        feedbackText.text = text;
        feedbackText.color = color ?? Color.white;

        if (string.IsNullOrEmpty(text))
        {
            feedbackText.alpha = 0f;
            return;
        }

        feedbackText.alpha = 1f;
        feedbackText.transform.localScale = Vector3.one;

        if (isPunch)
        {
            feedbackText.transform.DOPunchScale(Vector3.one * 0.25f, 0.35f, vibrato: 8, elasticity: 0.8f)
                .SetLink(feedbackText.gameObject, LinkBehaviour.KillOnDestroy);
        }

        _feedbackTween = feedbackText.DOFade(0f, 0.6f)
            .SetDelay(0.5f)
            .SetLink(feedbackText.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void SetInstructionPrompt(string text)
    {
        if (instructionText != null)
        {
            instructionText.text = text;
        }
    }

    private void LockPlayer(bool lockState)
    {
        if (playerMovement != null)
        {
            playerMovement.enabled = !lockState;
        }

        if (playerHands != null)
        {
            playerHands.enabled = !lockState;
        }

        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = !lockState;
        }

        if (headBobbing != null)
        {
            headBobbing.enabled = !lockState;
        }

        Cursor.lockState = lockState ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = lockState;
    }

    private bool IsPlayerHoldingRazor()
    {
        if (playerHands == null) return true; // Fallback, jeśli ręce nie są podpięte
        if (!playerHands.HasItem) return false;

        string itemName = playerHands.HeldItem.name.ToLowerInvariant();
        // Sprawdzamy czy nazwa przedmiotu zawiera kluczowe słowa
        return itemName.Contains("razor") || itemName.Contains("brzytwa") || itemName.Contains("ostrze");
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
    // PUBLIC API & CHEATS
    // ──────────────────────────────────────────────────────────

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

    /// <summary>
    /// Calculates position along the multi-waypoint path (t in 0..1).
    /// </summary>
    public Vector2[] GetEffectiveWaypoints()
    {
        if (waypointTransforms != null && waypointTransforms.Length >= 2)
        {
            Vector2[] result = new Vector2[waypointTransforms.Length];
            for (int i = 0; i < waypointTransforms.Length; i++)
            {
                result[i] = waypointTransforms[i] != null 
                    ? waypointTransforms[i].anchoredPosition 
                    : ((waypoints != null && i < waypoints.Length) ? waypoints[i] : Vector2.zero);
            }
            return result;
        }

        if (waypoints != null && waypoints.Length >= 2)
        {
            return waypoints;
        }

        return new Vector2[] { _bottomPos, _topPos };
    }

    public Vector2 EvaluatePathPosition(float t)
    {
        t = Mathf.Clamp01(t);
        Vector2[] pts = GetEffectiveWaypoints();

        if (pts != null && pts.Length >= 2)
        {
            float totalSegments = pts.Length - 1;
            float scaledT = t * totalSegments;
            int segIndex = Mathf.Clamp((int)scaledT, 0, pts.Length - 2);
            float segT = scaledT - segIndex;
            return Vector2.Lerp(pts[segIndex], pts[segIndex + 1], segT);
        }

        return Vector2.Lerp(_bottomPos, _topPos, t);
    }

    public Vector2 GetStartWaypoint()
    {
        Vector2[] pts = GetEffectiveWaypoints();
        if (pts != null && pts.Length > 0) return pts[0];
        return _bottomPos;
    }

    public Vector2 GetEndWaypoint()
    {
        Vector2[] pts = GetEffectiveWaypoints();
        if (pts != null && pts.Length > 0) return pts[pts.Length - 1];
        return _topPos;
    }

    public Vector2[] Waypoints
    {
        get => waypoints;
        set => waypoints = value;
    }

    public RectTransform[] WaypointTransforms
    {
        get => waypointTransforms;
        set => waypointTransforms = value;
    }

    private void OnDrawGizmos()
    {
        if (!showPathGizmos) return;

        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect == null && minigameCanvasGroup != null)
            parentRect = minigameCanvasGroup.GetComponent<RectTransform>();

        if (parentRect == null) return;

        Vector2[] points = GetEffectiveWaypoints();
        if (points == null || points.Length < 2) return;

        // Draw path lines
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 w0 = parentRect.TransformPoint(points[i]);
            Vector3 w1 = parentRect.TransformPoint(points[i + 1]);
            Gizmos.DrawLine(w0, w1);
        }

        // Draw numbered waypoint spheres
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 wp = parentRect.TransformPoint(points[i]);
            Gizmos.color = (i == 0) ? Color.green : (i == points.Length - 1 ? Color.red : (i == 2 ? Color.yellow : Color.cyan));
            Gizmos.DrawSphere(wp, 10f);
        }
    }

    private enum HitResult { TooEarly, Good, Perfect, TooLate }

#if UNITY_EDITOR
    [ContextMenu("Preview Start Position")]
    public void EditorPreviewStart()
    {
        if (razorIndicator != null)
        {
            UnityEditor.Undo.RecordObject(razorIndicator, "Preview Razor Start");
            razorIndicator.anchoredPosition = GetStartWaypoint();
            razorIndicator.localRotation = Quaternion.Euler(0f, 0f, _baseZRotation);
            razorIndicator.localScale = Vector3.one;
            Debug.Log($"[RazorMinigame] Preview start position set: Pos={GetStartWaypoint()}, Rot={_baseZRotation:F1}°");
        }
    }

    [ContextMenu("Capture Scene Position as Start (P1)")]
    public void EditorCaptureStart()
    {
        if (razorIndicator != null)
        {
            UnityEditor.Undo.RecordObject(this, "Capture Razor Start");
            if (waypoints != null && waypoints.Length > 0)
            {
                waypoints[0] = razorIndicator.anchoredPosition;
            }
            _bottomPos = razorIndicator.anchoredPosition;
            float z = razorIndicator.localEulerAngles.z;
            if (z > 180f) z -= 360f;
            _baseZRotation = z;
            Debug.Log($"[RazorMinigame] Captured new Start position P1: Pos={_bottomPos}, Rot={_baseZRotation:F1}°");
        }
    }

    [ContextMenu("Distribute 5 Waypoints Evenly")]
    public void EditorDistributeWaypointsEvenly()
    {
        UnityEditor.Undo.RecordObject(this, "Distribute Waypoints");
        Vector2 start = (waypoints != null && waypoints.Length > 0) ? waypoints[0] : _bottomPos;
        Vector2 end = (waypoints != null && waypoints.Length > 1) ? waypoints[waypoints.Length - 1] : _topPos;

        waypoints = new Vector2[5];
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            waypoints[i] = Vector2.Lerp(start, end, t);
        }
        Debug.Log("[RazorMinigame] Distributed 5 waypoints evenly along the path.");
    }
#endif
}
