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

    [Header("Interactable Pulse")]
    [SerializeField] private float pulseScale = 1.15f;
    [SerializeField] private float pulseDuration = 0.35f;

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

    [Header("Hold Interaction (Kółeczko -> Kwadrat)")]
    [SerializeField] private Image holdProgressRing;
    public static Crosshair Instance { get; private set; }

    [SerializeField] private Image squareMorphFrame;

    private RectTransform _crosshairRect;
    private float _holdTimer = 0f;
    private bool _isHolding = false;
    private Tween _concussionTween;

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

        _defaultScale =
            crosshairImage.transform.localScale;

        _defaultAnchoredPosition =
            _crosshairRect.anchoredPosition;

        crosshairImage.color = normalColor;

        interactionNameText.text = string.Empty;
        interactionNameText.alpha = 0f;

        EnsureHoldUI();
    }

    private void EnsureHoldUI()
    {
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
    }

    private void OnEnable()
    {
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

        _crosshairRect.anchoredPosition =
            _defaultAnchoredPosition;

        interactionNameText.text =
            interactable.InteractionName;

        Color targetColor =
            GetCurrentInteractionColor();

        _colorTween = crosshairImage
            .DOColor(
                targetColor,
                colorFadeDuration
            )
            .SetLink(
                crosshairImage.gameObject,
                LinkBehaviour.KillOnDestroy
            );

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

        StartPulse();
    }

    private void HideInteractable()
    {
        _currentInteractable = null;

        _pulseTween?.Kill();
        _scaleTween?.Kill();
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();
        _colorTween?.Kill();
        _textTween?.Kill();

        _pulseTween = null;
        _interactionBlinkTween = null;
        _blockedTween = null;

        _crosshairRect.anchoredPosition =
            _defaultAnchoredPosition;

        _colorTween = crosshairImage
            .DOColor(
                normalColor,
                colorFadeDuration
            )
            .SetLink(
                crosshairImage.gameObject,
                LinkBehaviour.KillOnDestroy
            );

        _scaleTween = crosshairImage.transform
            .DOScale(
                _defaultScale,
                0.15f
            )
            .SetEase(Ease.OutQuad)
            .SetLink(
                crosshairImage.gameObject,
                LinkBehaviour.KillOnDestroy
            );

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
    }

    private void StartPulse()
    {
        if (!_hasInteractable)
            return;

        _pulseTween?.Kill();

        crosshairImage.transform.localScale =
            _defaultScale;

        _pulseTween = crosshairImage.transform
            .DOScale(
                _defaultScale * pulseScale,
                pulseDuration
            )
            .SetEase(Ease.InOutSine)
            .SetLoops(
                -1,
                LoopType.Yoyo
            )
            .SetLink(
                crosshairImage.gameObject,
                LinkBehaviour.KillOnDestroy
            );
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

            if (_hasInteractable)
            {
                StartPulse();
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

            if (_hasInteractable)
            {
                StartPulse();
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
        if (_currentInteractable is IHoldInteractable holdInteractable && holdInteractable.RequiresHold)
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
                _isHolding = true;
                _holdTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_holdTimer / holdInteractable.HoldDuration);

                if (holdProgressRing != null)
                {
                    holdProgressRing.gameObject.SetActive(true);
                    holdProgressRing.fillAmount = progress;
                }

                // Morfowanie z kółeczka w kwadrat
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

                // Sukces po napełnieniu
                if (progress >= 1f)
                {
                    _holdTimer = 0f;
                    _isHolding = false;
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
        _scaleTween?.Kill();
        _colorTween?.Kill();
        _textTween?.Kill();
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();
        _concussionTween?.Kill();

        _pulseTween = null;
        _scaleTween = null;
        _colorTween = null;
        _textTween = null;
        _interactionBlinkTween = null;
        _blockedTween = null;
        _concussionTween = null;
    }
}