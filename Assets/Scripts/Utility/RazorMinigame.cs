using System;
using System.Collections;
using Unity.Cinemachine;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minigra ostrzenia brzytwy.
///
/// ── Zasady ruchu ──────────────────────────────────────────
///  • Po fade in brzytwa STOI na dolnym punkcie.
///  • Pierwsze wciśnięcie Space = START (nie liczy się jako próba).
///  • Brzytwa jedzie po ukosie od bottomAnchor do topAnchor.
///  • Gracz ma JEDNĄ szansę wcisnąć Space podczas przejścia w górę.
///  • Wciśnięcie → ocena trafienia → natychmiastowy powrót po ukosie w dół.
///  • Dotarcie do góry bez wciśnięcia = automatyczny MISS → powrót.
///  • Powrót na dół = koniec próby, start kolejnej (szybciej).
///
/// ── Hierarchia UI ──────────────────────────────────────────
///   [Canvas] RazorMinigameCanvas  [CanvasGroup]
///     └── StropHolder
///           ├── BottomAnchor   ← pusty RectTransform, dolny kraniec trasy
///           ├── TopAnchor      ← pusty RectTransform, górny kraniec trasy
///           ├── ZoneGood       ← pusty panel - strefa GOOD
///           ├── ZonePerfect    ← pusty panel - strefa PERFECT
///           └── RazorIndicator ← obraz brzytwy
///     └── SharpnessBarHolder
///           └── SharpnessMarker
/// </summary>
public class RazorMinigame : MonoBehaviour, IInteractable
{
    public static event Action<float> OnMinigameCompleted;

    // ──────────────────────────────────────────────────────────
    // INTERACTION
    // ──────────────────────────────────────────────────────────

    [Header("Interaction")]
    [SerializeField] private string interactionName = "Ostrzałka";
    public string InteractionName => interactionName;

    // ──────────────────────────────────────────────────────────
    // INPUT
    // ──────────────────────────────────────────────────────────

    [Header("Input")]
    [SerializeField] private InputActionReference hitAction;

    // ──────────────────────────────────────────────────────────
    // CAMERA
    // ──────────────────────────────────────────────────────────

    [Header("Camera")]
    [Tooltip("CinemachineBrain — wyłączenie zamraża kamerę podczas minigry.")]
    [SerializeField] private CinemachineBrain cinemachineBrain;

    // ──────────────────────────────────────────────────────────
    // UI REFERENCES
    // ──────────────────────────────────────────────────────────

    [Header("UI References")]
    [SerializeField] private CanvasGroup minigameCanvasGroup;
    [SerializeField] private RectTransform razorIndicator;
    [SerializeField] private RectTransform sharpnessMarker;
    [SerializeField] private TMP_Text attemptsText;
    [SerializeField] private TMP_Text feedbackText;

    [Tooltip("Opcjonalny tekst 'Wciśnij Space aby zacząć', widoczny przed startem.")]
    [SerializeField] private GameObject pressToStartUI;

    // ──────────────────────────────────────────────────────────
    // TRASA BRZYTWY — dwa punkty (mogą być pod ukosem)
    // ──────────────────────────────────────────────────────────

    [Header("Trasa Brzytwy")]
    [Tooltip("Dolny punkt trasy — brzytwa startuje tutaj.")]
    [SerializeField] private RectTransform bottomAnchor;

    [Tooltip("Górny punkt trasy — brzytwa tutaj zawraca.")]
    [SerializeField] private RectTransform topAnchor;

    // ──────────────────────────────────────────────────────────
    // STREFY HIT
    // ──────────────────────────────────────────────────────────

    [Header("Strefy Hit (puste panele na stropie)")]
    [SerializeField] private RectTransform zoneGood;
    [SerializeField] private RectTransform zonePerfect;

    // ──────────────────────────────────────────────────────────
    // PRĘDKOŚĆ
    // ──────────────────────────────────────────────────────────

    [Header("Prędkość")]
    [Tooltip("Prędkość podczas 1. próby w przestrzeni t (0..1 / s). " +
             "0.3 = 3.3 sekundy na pełny przejazd.")]
    [SerializeField] private float baseSpeed = 0.30f;

    [Tooltip("O ile szybciej po każdej próbie (dodatkowe t/s).")]
    [SerializeField] private float speedIncreasePerAttempt = 0.05f;

    [Tooltip("Prędkość powrotu w dół (zawsze stała).")]
    [SerializeField] private float returnSpeed = 0.8f;

    // ──────────────────────────────────────────────────────────
    // PASEK OSTROŚCI
    // ──────────────────────────────────────────────────────────

    [Header("Pasek Ostrości")]
    [SerializeField] private float barHalfWidth = 300f;
    [SerializeField] private float sharpnessGainPerfect = 0.28f;
    [SerializeField] private float sharpnessGainGood    = 0.15f;
    [SerializeField] private float markerMoveDuration   = 0.25f;

    // ──────────────────────────────────────────────────────────
    // KONFIGURACJA
    // ──────────────────────────────────────────────────────────

    [Header("Konfiguracja")]
    [SerializeField] private int totalAttempts = 5;
    [SerializeField] private float endDelay    = 1.5f;
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float sharpThreshold = 0.5f;

    // ──────────────────────────────────────────────────────────
    // AUDIO
    // ──────────────────────────────────────────────────────────

    [Header("Audio")]
    [SerializeField] private string soundGood    = "sharpen_good";
    [SerializeField] private string soundPerfect = "sharpen_perfect";
    [SerializeField] private string soundMiss    = "sharpen_miss";

    // ──────────────────────────────────────────────────────────
    // PLAYER
    // ──────────────────────────────────────────────────────────

    [Header("Player")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHands    playerHands;

    // ──────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ──────────────────────────────────────────────────────────

    // Pozycja brzytwy jako t = 0 (dół) .. 1 (góra) po linii trasy
    private float _t = 0f;

    // Wyliczone pozycje anchorów i progi stref (jako t 0..1)
    private Vector2 _bottomPos;
    private Vector2 _topPos;
    private float   _goodZoneT    = 0.30f;
    private float   _perfectZoneT = 0.80f;

    private enum Phase
    {
        Idle,          // po fade in, czeka na pierwsze wciśnięcie Space
        GoingUp,       // jedzie w górę — gracz może trafić
        GoingDown,     // wraca w dół — brak inputu
        Ending         // fade out i czyszczenie
    }

    private Phase _phase = Phase.Idle;

    private bool  _hitThisPass  = false;
    private float _sharpness    = 0f;
    private int   _attemptsDone = 0;
    private bool  _isRunning    = false;

    private Tween _markerTween;
    private Tween _fadeTween;

    // ──────────────────────────────────────────────────────────
    // UNITY
    // ──────────────────────────────────────────────────────────

    private void Awake()
    {
        HideInstant();
    }

    private void OnEnable()
    {
        if (hitAction != null)
            hitAction.action.started += OnHit;
    }

    private void OnDisable()
    {
        if (hitAction != null)
            hitAction.action.started -= OnHit;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _markerTween?.Kill();
    }

    // ──────────────────────────────────────────────────────────
    // IINTERACTABLE
    // ──────────────────────────────────────────────────────────

    public void Interact()
    {
        if (_isRunning) return;
        StartMinigame();
    }

    // ──────────────────────────────────────────────────────────
    // START
    // ──────────────────────────────────────────────────────────

    private void StartMinigame()
    {
        _isRunning    = true;
        _attemptsDone = 0;
        _sharpness    = 0f;
        _t            = 0f;
        _phase        = Phase.Idle;

        CacheAnchors();

        LockPlayer(true);
        hitAction?.action.Enable();

        // Ustaw brzytwy na dole już przed fade in
        ApplyRazorPosition();
        UpdateAttemptsText();
        UpdateSharpnessMarker(animate: false);
        ShowFeedback(string.Empty);

        // Pokaż "Wciśnij Space aby zacząć"
        if (pressToStartUI != null) pressToStartUI.SetActive(true);

        // Fade in — po jego zakończeniu _phase = Idle, czekamy na Space
        ShowUI();
    }

    // ──────────────────────────────────────────────────────────
    // UPDATE
    // ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isRunning || _phase == Phase.Idle || _phase == Phase.Ending)
            return;

        float speed = _phase == Phase.GoingUp
            ? baseSpeed + speedIncreasePerAttempt * _attemptsDone
            : returnSpeed;

        float delta = speed * Time.deltaTime;

        if (_phase == Phase.GoingUp)
        {
            _t += delta;

            if (_t >= 1f)
            {
                _t = 1f;
                ApplyRazorPosition();

                // Dotarł do góry bez trafienia — MISS, zawróć
                if (!_hitThisPass)
                    RegisterResult(HitResult.Miss);

                _phase = Phase.GoingDown;
                return;
            }
        }
        else if (_phase == Phase.GoingDown)
        {
            _t -= delta;

            if (_t <= 0f)
            {
                _t = 0f;
                ApplyRazorPosition();
                FinishPass();
                return;
            }
        }

        ApplyRazorPosition();
    }

    // ──────────────────────────────────────────────────────────
    // PRZEBIEG JEDNEJ PRÓBY
    // ──────────────────────────────────────────────────────────

    private void BeginNewPass()
    {
        _hitThisPass = false;
        _t           = 0f;
        _phase       = Phase.GoingUp;
        ApplyRazorPosition();
    }

    private void FinishPass()
    {
        ShowFeedback(string.Empty);

        if (_attemptsDone >= totalAttempts)
        {
            _phase = Phase.Ending;
            StartCoroutine(EndMinigame());
        }
        else
        {
            BeginNewPass();
        }
    }

    // ──────────────────────────────────────────────────────────
    // INPUT
    // ──────────────────────────────────────────────────────────

    private void OnHit(InputAction.CallbackContext ctx)
    {
        if (!_isRunning) return;

        // Pierwsze wciśnięcie — start ruchu
        if (_phase == Phase.Idle)
        {
            if (pressToStartUI != null) pressToStartUI.SetActive(false);
            BeginNewPass();
            return;
        }

        // Podczas ruchu w górę — jedna szansa
        if (_phase == Phase.GoingUp && !_hitThisPass)
        {
            _hitThisPass = true;
            RegisterResult(EvaluatePosition());
            _phase = Phase.GoingDown; // natychmiastowy powrót
        }
        // GoingDown i Ending — ignorujemy (anty-spam)
    }

    // ──────────────────────────────────────────────────────────
    // OCENA TRAFIENIA
    // ──────────────────────────────────────────────────────────

    private HitResult EvaluatePosition()
    {
        if (_t >= _perfectZoneT) return HitResult.Perfect;
        if (_t >= _goodZoneT)    return HitResult.Good;
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
                ShowFeedback("PERFECT!");
                AudioManager.Instance?.Play(soundPerfect);
                break;
            case HitResult.Good:
                _sharpness = Mathf.Clamp01(_sharpness + sharpnessGainGood);
                ShowFeedback("GOOD");
                AudioManager.Instance?.Play(soundGood);
                break;
            case HitResult.Miss:
                ShowFeedback("MISS");
                AudioManager.Instance?.Play(soundMiss);
                break;
        }

        UpdateSharpnessMarker(animate: true);
    }

    // ──────────────────────────────────────────────────────────
    // ZAKOŃCZENIE
    // ──────────────────────────────────────────────────────────

    private IEnumerator EndMinigame()
    {
        yield return new WaitForSeconds(endDelay);

        bool isSharp = _sharpness >= sharpThreshold;

        bool fadeFinished = false;
        HideUI(onComplete: () => fadeFinished = true);
        yield return new WaitUntil(() => fadeFinished);

        PreparationStateManager.Instance?.SetTaskState("razor_sharpened", isSharp);
        OnMinigameCompleted?.Invoke(_sharpness);

        _isRunning = false;
        LockPlayer(false);
    }

    // ──────────────────────────────────────────────────────────
    // LOCK
    // ──────────────────────────────────────────────────────────

    private void LockPlayer(bool locked)
    {
        if (playerMovement   != null) playerMovement.enabled   = !locked;
        if (playerHands      != null) playerHands.enabled      = !locked;
        if (cinemachineBrain != null) cinemachineBrain.enabled = !locked;
    }

    // ──────────────────────────────────────────────────────────
    // CACHE ANCHORS — oblicza pozycje i progi z RectTransformów
    // ──────────────────────────────────────────────────────────

    private void CacheAnchors()
    {
        _bottomPos = bottomAnchor != null
            ? bottomAnchor.anchoredPosition
            : new Vector2(0f, -250f);

        _topPos = topAnchor != null
            ? topAnchor.anchoredPosition
            : new Vector2(0f, 250f);

        // Progi stref: konwertujemy anchoredPosition Y panelu
        // na wartość t (0..1) wzdłuż trasy bottomPos→topPos
        _goodZoneT    = GetZoneT(zoneGood,    0.30f);
        _perfectZoneT = GetZoneT(zonePerfect, 0.80f);

        Debug.Log($"[RazorMinigame] Trasa: {_bottomPos} → {_topPos} " +
                  $"| GOOD t>={_goodZoneT:F2} | PERFECT t>={_perfectZoneT:F2}");
    }

    /// <summary>
    /// Oblicza próg t (0..1) na podstawie dolnej krawędzi panelu strefy.
    /// Używa składowej Y, bo to dominujący wymiar trasy.
    /// </summary>
    private float GetZoneT(RectTransform panel, float fallback)
    {
        if (panel == null) return fallback;

        float panelBottomY = panel.anchoredPosition.y - panel.rect.height * 0.5f;
        float totalRangeY  = _topPos.y - _bottomPos.y;

        if (Mathf.Approximately(totalRangeY, 0f)) return fallback;

        return Mathf.Clamp01((panelBottomY - _bottomPos.y) / totalRangeY);
    }

    // ──────────────────────────────────────────────────────────
    // UI HELPERS
    // ──────────────────────────────────────────────────────────

    private void ApplyRazorPosition()
    {
        if (razorIndicator == null) return;

        // Lerp po pełnej pozycji 2D — uwzględnia ukos
        razorIndicator.anchoredPosition = Vector2.Lerp(_bottomPos, _topPos, _t);
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

    private void ShowFeedback(string msg)
    {
        if (feedbackText != null) feedbackText.text = msg;
    }

    private void ShowUI(Action onComplete = null)
    {
        if (minigameCanvasGroup == null) { onComplete?.Invoke(); return; }

        minigameCanvasGroup.alpha          = 0f;
        minigameCanvasGroup.blocksRaycasts = true;
        minigameCanvasGroup.interactable   = true;

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
                minigameCanvasGroup.interactable   = false;
                onComplete?.Invoke();
            });
    }

    private void HideInstant()
    {
        if (minigameCanvasGroup == null) return;
        minigameCanvasGroup.alpha          = 0f;
        minigameCanvasGroup.interactable   = false;
        minigameCanvasGroup.blocksRaycasts = false;
    }

    // ──────────────────────────────────────────────────────────
    // ENUM
    // ──────────────────────────────────────────────────────────

    private enum HitResult { Miss, Good, Perfect }
}
