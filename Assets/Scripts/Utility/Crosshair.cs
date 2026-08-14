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
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color interactableColor = Color.yellow;

    [Header("Interactable Pulse")]
    [SerializeField] private float pulseScale = 1.15f;
    [SerializeField] private float pulseDuration = 0.35f;

    [Header("Interaction Blink")]
    [SerializeField] private float blinkScale = 1.5f;
    [SerializeField] private float blinkSmallScale = 0.9f;
    [SerializeField] private float blinkDuration = 0.07f;

    [Header("Fade")]
    [SerializeField] private float colorFadeDuration = 0.15f;
    [SerializeField] private float textFadeInDuration = 0.2f;
    [SerializeField] private float textFadeOutDuration = 0.15f;

    private Vector3 _defaultScale;

    private bool _hasInteractable;

    private Tween _pulseTween;
    private Tween _scaleTween;
    private Tween _colorTween;
    private Tween _textTween;
    private Tween _interactionBlinkTween;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

        _defaultScale =
            crosshairImage.transform.localScale;

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
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.OnInteractableChanged -=
                HandleInteractableChanged;

            playerMovement.OnInteractionPerformed -=
                HandleInteractionPerformed;
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

        interactionNameText.text =
            interactable.InteractionName;

        // Kolor kropki
        _colorTween = crosshairImage
            .DOColor(
                interactableColor,
                colorFadeDuration
            )
            .SetLink(
                crosshairImage.gameObject,
                LinkBehaviour.KillOnDestroy
            );

        // Fade In tekstu
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
        _pulseTween?.Kill();
        _scaleTween?.Kill();
        _interactionBlinkTween?.Kill();
        _colorTween?.Kill();
        _textTween?.Kill();

        _pulseTween = null;
        _interactionBlinkTween = null;

        // Powrót koloru
        _colorTween = crosshairImage
            .DOColor(
                normalColor,
                colorFadeDuration
            )
            .SetLink(
                crosshairImage.gameObject,
                LinkBehaviour.KillOnDestroy
            );

        // Powrót kropki do normalnego rozmiaru
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

        // Fade Out tekstu
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

        // Zatrzymujemy zwykłe pulsowanie,
        // bo teraz robimy mocniejszy feedback kliknięcia.
        _pulseTween?.Kill();
        _scaleTween?.Kill();
        _interactionBlinkTween?.Kill();

        _pulseTween = null;

        Sequence blinkSequence =
            DOTween.Sequence();

        // POP
        blinkSequence.Append(
            crosshairImage.transform
                .DOScale(
                    _defaultScale * blinkScale,
                    blinkDuration
                )
                .SetEase(Ease.OutQuad)
        );

        // Lekko do środka
        blinkSequence.Append(
            crosshairImage.transform
                .DOScale(
                    _defaultScale * blinkSmallScale,
                    blinkDuration
                )
                .SetEase(Ease.InOutQuad)
        );

        // Powrót
        blinkSequence.Append(
            crosshairImage.transform
                .DOScale(
                    _defaultScale,
                    blinkDuration
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

    private void KillTweens()
    {
        _pulseTween?.Kill();
        _scaleTween?.Kill();
        _colorTween?.Kill();
        _textTween?.Kill();
        _interactionBlinkTween?.Kill();

        _pulseTween = null;
        _scaleTween = null;
        _colorTween = null;
        _textTween = null;
        _interactionBlinkTween = null;
    }
}