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

    private RectTransform _crosshairRect;

    private void Awake()
    {
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

    private Color GetCurrentInteractionColor()
    {
        if (_currentInteractable == null)
        {
            return normalColor;
        }

        return interactableColor;
    }

    private void KillTweens()
    {
        _pulseTween?.Kill();
        _scaleTween?.Kill();
        _colorTween?.Kill();
        _textTween?.Kill();
        _interactionBlinkTween?.Kill();
        _blockedTween?.Kill();

        _pulseTween = null;
        _scaleTween = null;
        _colorTween = null;
        _textTween = null;
        _interactionBlinkTween = null;
        _blockedTween = null;
    }
}