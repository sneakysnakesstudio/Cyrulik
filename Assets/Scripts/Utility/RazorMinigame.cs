using System;
using System.Collections;
using Unity.Cinemachine;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Minigra ostrzenia brzytwy napędzana bezpiecznym silnikiem DOTween.
/// Wymaga przyniesienia żyletki z pudełka przed uruchomieniem.
/// </summary>
public class RazorMinigame : MonoBehaviour, IConditionalInteractable
{
    public static event Action<float> OnMinigameCompleted;

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
    [Tooltip("Akcja wejścia (np. Space).")]
    [SerializeField] private InputActionReference hitAction;

    // ──────────────────────────────────────────────────────────
    // BLOKADA KAMERY I GRACZA
    // ──────────────────────────────────────────────────────────

    [Header("Blokada gracza i kamery")]
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHands playerHands;

    // ──────────────────────────────────────────────────────────
    // REFERENCJE UI
    // ──────────────────────────────────────────────────────────

    [Header("UI References")]
    [SerializeField] private CanvasGroup minigameCanvasGroup;
    [SerializeField] private RectTransform razorIndicator;
    [SerializeField] private RectTransform sharpnessMarker;
    [SerializeField] private TMP_Text attemptsText;
    [SerializeField] private TMP_Text feedbackText;

    [Tooltip("Panel / Canvas z poradnikiem / instrukcją (Guide), który wyświetla się na starcie przed naciśnięciem spacji.")]
    [SerializeField] private GameObject guideOverlayUI;

    [Tooltip("Obiekt z tekstem 'Press SPACE to start'.")]
    [SerializeField] private GameObject pressToStartUI;

    // ──────────────────────────────────────────────────────────
    // PUNKTY TRASY I STREFY
    // ──────────────────────────────────────────────────────────

    [Header("Trasa (Anchory)")]
    [Tooltip("Dolny punkt startowy.")]
    [SerializeField] private RectTransform bottomAnchor;

    [Tooltip("Górny punkt szczytowy.")]
    [SerializeField] private RectTransform topAnchor;

    [Header("Strefy Hit")]
    [Tooltip("Transform / Panel strefy GOOD.")]
    [SerializeField] private RectTransform zoneGood;

    [Tooltip("Transform / Panel strefy PERFECT.")]
    [SerializeField] private RectTransform zonePerfect;

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
    // CZASY PRZEJAZDU (W SEKUNDACH)
    // ──────────────────────────────────────────────────────────

    [Header("Czasy (w sekundach)")]
    [Tooltip("Czas przejazdu w górę w 1. próbie (domyślnie 2.5s).")]
    [SerializeField] private float initialTravelTime = 2.5f;

    [Tooltip("O ile sekund krócej trwa każda kolejna próba (domyślnie 0.25s).")]
    [SerializeField] private float timeReductionPerAttempt = 0.25f;

    [Tooltip("Minimalny czas przejazdu w górę (domyślnie 1.0s).")]
    [SerializeField] private float minTravelTime = 1.0f;

    [Tooltip("Czas powrotu brzytwy na dół (domyślnie 0.8s).")]
    [SerializeField] private float returnDuration = 0.8f;

    [Tooltip("Pauza na dole między próbami (domyślnie 0.6s).")]
    [SerializeField] private float pauseAtBottom = 0.6f;

    // ──────────────────────────────────────────────────────────
    // PASEK OSTROŚCI
    // ──────────────────────────────────────────────────────────

    [Header("Pasek Ostrości")]
    [SerializeField] private float barHalfWidth = 300f;
    [SerializeField] private float sharpnessGainPerfect = 0.28f;
    [SerializeField] private float sharpnessGainGood = 0.15f;
    [SerializeField] private float markerMoveDuration = 0.25f;

    // ──────────────────────────────────────────────────────────
    // KONFIGURACJA
    // ──────────────────────────────────────────────────────────

    [Header("Konfiguracja")]
    [SerializeField] private int totalAttempts = 5;
    [SerializeField] private float endDelay = 1.2f;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float sharpThreshold = 0.5f;

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
        WaitingForStart,  // Czeka na pierwsze wciśnięcie spacji
        MovingUp,         // Płynnie leci w górę przez DOTween
        ReturningDown,    // Płynnie zjeżdża w dół przez DOTween
        PausedAtBottom,   // Krótka pauza przed kolejnym ruchem
        Finished          // Koniec minigry
    }

    private State _state = State.Inactive;
    private int _attemptsDone = 0;
    private float _sharpness = 0f;
    private bool _isCompleted = false;

    private Vector2 _bottomPos;
    private Vector2 _topPos;
    private float _goodZoneT = 0.35f;
    private float _perfectZoneT = 0.80f;

    private Tween _razorTween;
    private Tween _markerTween;
    private Tween _fadeTween;
    private Tween _shakeTween;
    private Tween _feedbackTween;
    private Coroutine _loopRoutine;

    // ──────────────────────────────────────────────────────────
    // CYKL ŻYCIA
    // ──────────────────────────────────────────────────────────

    private void Awake()
    {
        HideInstant();
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    private void KillAllTweens()
    {
        _razorTween?.Kill();
        _markerTween?.Kill();
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
        // Zabezpieczenie przed zerowymi wartościami z serializacji Unity
        ValidateTimings();

        // Oblicz i zweryfikuj punkty trasy
        SetupAnchorsAndZones();

        _attemptsDone = 0;
        _sharpness = 0f;
        _state = State.WaitingForStart;

        ResetZoneHighlights();

        // Ustaw brzytwę na pozycji startowej
        if (razorIndicator != null)
        {
            _razorTween?.Kill();
            razorIndicator.anchoredPosition = _bottomPos;
        }

        LockPlayer(true);

        UpdateAttemptsText();
        UpdateSharpnessMarker(animate: false);
        ShowFeedback(string.Empty);

        if (guideOverlayUI != null)
            guideOverlayUI.SetActive(true);

        if (pressToStartUI != null)
            pressToStartUI.SetActive(true);

        ShowUI();
    }

    private void ValidateTimings()
    {
        if (initialTravelTime < 0.5f) initialTravelTime = 2.5f;
        if (timeReductionPerAttempt < 0.05f) timeReductionPerAttempt = 0.25f;
        if (minTravelTime < 0.5f) minTravelTime = 1.0f;
        if (returnDuration < 0.2f) returnDuration = 0.8f;
        if (pauseAtBottom < 0.1f) pauseAtBottom = 0.6f;
        if (totalAttempts < 1) totalAttempts = 5;
    }

    // ──────────────────────────────────────────────────────────
    // OBSŁUGA INPUTU (UPDATE)
    // ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (_state == State.Inactive || _state == State.Finished)
            return;

        if (WasSpaceOrHitPressed())
        {
            HandlePlayerInput();
        }
    }

    private bool WasSpaceOrHitPressed()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            return true;

        if (hitAction != null && hitAction.action != null && hitAction.action.WasPressedThisFrame())
            return true;

        return false;
    }

    private void HandlePlayerInput()
    {
        if (_state == State.WaitingForStart)
        {
            // Wciśnięcie spacji na starcie: ukryj poradnik i wystartuj ruch w górę
            if (guideOverlayUI != null)
                guideOverlayUI.SetActive(false);

            if (pressToStartUI != null)
                pressToStartUI.SetActive(false);

            ShowFeedback(string.Empty);
            StartPassUpwards();
            return;
        }

        if (_state == State.MovingUp)
        {
            // Gracz uderzył podczas lotu w górę
            _razorTween?.Kill();

            HitResult result = EvaluateHit();
            RegisterResult(result);

            // Płynny powrót w dół
            StartPassDownwards();
        }
    }

    // ──────────────────────────────────────────────────────────
    // PRZEBIEG PRÓBY (DOTWEEN)
    // ──────────────────────────────────────────────────────────

    private void StartPassUpwards()
    {
        _state = State.MovingUp;

        // Resetujemy podświetlenie stref do stanu początkowego na nowy ruch
        ResetZoneHighlights();

        // Odtwarzamy dźwięk przesunięcia brzytwy po pasie w górę
        if (!string.IsNullOrEmpty(soundPassUp))
        {
            AudioManager.Instance?.Play(soundPassUp);
        }

        float duration = Mathf.Max(minTravelTime, initialTravelTime - (_attemptsDone * timeReductionPerAttempt));

        _razorTween?.Kill();
        _razorTween = razorIndicator
            .DOAnchorPos(_topPos, duration)
            .SetEase(Ease.Linear)
            .SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(OnReachedTopWithoutHit);
    }

    private void OnReachedTopWithoutHit()
    {
        if (_state != State.MovingUp) return;

        // Brak reakcji gracza do samego szczytu = MISS
        RegisterResult(HitResult.Miss);

        StartPassDownwards();
    }

    private void StartPassDownwards()
    {
        _state = State.ReturningDown;

        _razorTween?.Kill();
        _razorTween = razorIndicator
            .DOAnchorPos(_bottomPos, returnDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(OnReturnedToBottom);
    }

    private void OnReturnedToBottom()
    {
        _state = State.PausedAtBottom;

        if (_loopRoutine != null)
            StopCoroutine(_loopRoutine);

        _loopRoutine = StartCoroutine(PauseAndNextPassRoutine());
    }

    private IEnumerator PauseAndNextPassRoutine()
    {
        yield return new WaitForSeconds(pauseAtBottom);
        ShowFeedback(string.Empty);

        if (_attemptsDone >= totalAttempts)
        {
            _state = State.Finished;
            StartCoroutine(EndMinigameRoutine());
        }
        else
        {
            // Brzytwa jedzie automatycznie w górę po krótkiej pauzie na dole
            StartPassUpwards();
        }
    }

    // ──────────────────────────────────────────────────────────
    // OCENA TRAFIENIA
    // ──────────────────────────────────────────────────────────

    private HitResult EvaluateHit()
    {
        if (razorIndicator == null) return HitResult.Miss;

        float totalDistance = Vector2.Distance(_bottomPos, _topPos);
        if (totalDistance <= 0.001f) return HitResult.Miss;

        float currentDistance = Vector2.Distance(_bottomPos, razorIndicator.anchoredPosition);
        float t = Mathf.Clamp01(currentDistance / totalDistance);

        if (t >= _perfectZoneT) return HitResult.Perfect;
        if (t >= _goodZoneT) return HitResult.Good;
        return HitResult.Miss;
    }

    private void RegisterResult(HitResult result)
    {
        _attemptsDone++;
        UpdateAttemptsText();

        switch (result)
        {
            case HitResult.Perfect:
                _sharpness = Mathf.Clamp01(_sharpness + sharpnessGainPerfect);
                ShowFeedback("PERFECT!", perfectColor, isPunch: true);
                AudioManager.Instance?.Play(soundPerfect);
                break;

            case HitResult.Good:
                _sharpness = Mathf.Clamp01(_sharpness + sharpnessGainGood);
                ShowFeedback("GOOD", goodColor, isPunch: false);
                AudioManager.Instance?.Play(soundGood);
                break;

            case HitResult.Miss:
                ShowFeedback("MISS", missColor, isPunch: false);
                AudioManager.Instance?.Play(soundMiss);
                break;
        }

        TriggerJuiceEffects(result);
        UpdateSharpnessMarker(animate: true);
    }

    private void TriggerJuiceEffects(HitResult result)
    {
        RectTransform targetShake = shakeTransform != null 
            ? shakeTransform 
            : (minigameCanvasGroup != null ? minigameCanvasGroup.GetComponent<RectTransform>() : null);

        _shakeTween?.Kill();

        switch (result)
        {
            case HitResult.Perfect:
                // Mocny shake ekranu
                if (targetShake != null && perfectShakeStrength > 0.01f)
                {
                    _shakeTween = targetShake
                        .DOShakePosition(0.25f, strength: new Vector3(perfectShakeStrength, perfectShakeStrength, 0f), vibrato: 20, randomness: 90, snapping: false, fadeOut: true)
                        .SetLink(targetShake.gameObject, LinkBehaviour.KillOnDestroy);
                }

                // Soczysty punch brzytwy
                if (razorIndicator != null)
                {
                    razorIndicator.DOPunchScale(new Vector3(0.35f, 0.35f, 0f), 0.25f, vibrato: 10, elasticity: 1f)
                        .SetLink(razorIndicator.gameObject, LinkBehaviour.KillOnDestroy);
                }

                // Punch wskaźnika ostrości
                if (sharpnessMarker != null)
                {
                    sharpnessMarker.DOPunchScale(new Vector3(0.45f, 0.45f, 0f), 0.28f, vibrato: 10, elasticity: 1f)
                        .SetLink(sharpnessMarker.gameObject, LinkBehaviour.KillOnDestroy);
                }

                // Rozbłysk / Bloom strefy PERFECT
                FlashPerfectZone();
                break;

            case HitResult.Good:
                // Delikatny shake
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

                // Rozbłysk / Bloom strefy GOOD
                FlashGoodZone();
                break;

            case HitResult.Miss:
                // Lekki wstrząs w poziomie (zawód)
                if (targetShake != null && missShakeStrength > 0.01f)
                {
                    _shakeTween = targetShake
                        .DOShakePosition(0.18f, strength: new Vector3(missShakeStrength, 0f, 0f), vibrato: 14, randomness: 45, snapping: false, fadeOut: true)
                        .SetLink(targetShake.gameObject, LinkBehaviour.KillOnDestroy);
                }
                break;
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
            zonePerfectImage.color = new Color(2f, 1.8f, 1.2f, 1f); // rozbłysk
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
            zoneGoodImage.color = new Color(1.5f, 2f, 1.5f, 1f); // zielony rozbłysk
            zoneGoodImage.DOColor(Color.white, zoneGlowDuration).SetEase(Ease.OutQuad).SetLink(zoneGoodImage.gameObject, LinkBehaviour.KillOnDestroy);
            zoneGoodImage.rectTransform.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), zoneGlowDuration, vibrato: 6).SetLink(zoneGoodImage.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else if (zoneGood != null && zoneGoodGlow == null)
        {
            zoneGood.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), zoneGlowDuration, vibrato: 6).SetLink(zoneGood.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    /// <summary>
    /// Natychmiastowo wygasza podświetlenia stref i resetuje ich skalę do wartości domyślnych (np. przy nowym ruchu).
    /// </summary>
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

        bool fadeFinished = false;
        HideUI(onComplete: () => fadeFinished = true);
        yield return new WaitUntil(() => fadeFinished);

        PreparationStateManager.Instance?.SetTaskState("razor_sharpened", isSharp);
        OnMinigameCompleted?.Invoke(_sharpness);

        _isCompleted = true;
        _state = State.Inactive;
        LockPlayer(false);
    }

    // ──────────────────────────────────────────────────────────
    // OBLICZANIE ANCHORÓW I STREF
    // ──────────────────────────────────────────────────────────

    private void SetupAnchorsAndZones()
    {
        _bottomPos = bottomAnchor != null ? bottomAnchor.anchoredPosition : new Vector2(0f, -250f);
        _topPos = topAnchor != null ? topAnchor.anchoredPosition : new Vector2(0f, 250f);

        // Zabezpieczenie przed nałożeniem punktów na siebie
        if (Vector2.Distance(_bottomPos, _topPos) < 20f)
        {
            _bottomPos = new Vector2(0f, -250f);
            _topPos = new Vector2(0f, 250f);
        }

        _goodZoneT = CalculateZoneT(zoneGood, 0.35f);
        _perfectZoneT = CalculateZoneT(zonePerfect, 0.80f);
    }

    private float CalculateZoneT(RectTransform panel, float fallback)
    {
        if (panel == null) return fallback;

        float totalDistance = Vector2.Distance(_bottomPos, _topPos);
        if (totalDistance < 0.001f) return fallback;

        float bottomEdgeY = panel.anchoredPosition.y - (panel.rect.height * 0.5f);
        float totalRangeY = _topPos.y - _bottomPos.y;

        if (Mathf.Abs(totalRangeY) < 1f) return fallback;

        return Mathf.Clamp01((bottomEdgeY - _bottomPos.y) / totalRangeY);
    }

    // ──────────────────────────────────────────────────────────
    // BLOKADY I POMOCNIKI UI
    // ──────────────────────────────────────────────────────────

    private void LockPlayer(bool locked)
    {
        if (playerMovement != null) playerMovement.enabled = !locked;
        if (playerHands != null) playerHands.enabled = !locked;
        if (cinemachineBrain != null) cinemachineBrain.enabled = !locked;
    }

    private void UpdateAttemptsText()
    {
        if (attemptsText != null)
            attemptsText.text = $"{_attemptsDone} / {totalAttempts}";
    }

    private void UpdateSharpnessMarker(bool animate)
    {
        if (sharpnessMarker == null) return;

        float targetX = Mathf.Lerp(-barHalfWidth, barHalfWidth, _sharpness);
        _markerTween?.Kill();

        if (animate)
        {
            _markerTween = sharpnessMarker
                .DOAnchorPosX(targetX, markerMoveDuration)
                .SetEase(Ease.OutBack)
                .SetLink(sharpnessMarker.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            sharpnessMarker.anchoredPosition = new Vector2(targetX, sharpnessMarker.anchoredPosition.y);
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

    private enum HitResult { Miss, Good, Perfect }
}
