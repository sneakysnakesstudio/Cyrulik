using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class Crosshair : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color interactableColor = Color.yellow;

    [Header("Pulse")]
    [SerializeField] private float pulseScale = 1.25f;
    [SerializeField] private float pulseDuration = 0.35f;

    private Image _image;

    private Vector3 _defaultScale;
    private Tween _pulseTween;
    private Tween _colorTween;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _image = GetComponent<Image>();

        _defaultScale = transform.localScale;

        _image.color = normalColor;
    }

    private void OnEnable()
    {
        if (playerMovement != null)
        {
            playerMovement.OnInteractableStateChanged +=
                HandleInteractableStateChanged;
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.OnInteractableStateChanged -=
                HandleInteractableStateChanged;
        }

        _pulseTween?.Kill();
        _colorTween?.Kill();
    }

    private void HandleInteractableStateChanged(bool canInteract)
    {
        if (canInteract)
        {
            StartInteractionEffect();
        }
        else
        {
            StopInteractionEffect();
        }
    }

    private void StartInteractionEffect()
    {
        _pulseTween?.Kill();
        _colorTween?.Kill();

        _colorTween = _image
            .DOColor(interactableColor, 0.15f);

        _pulseTween = transform
            .DOScale(
                _defaultScale * pulseScale,
                pulseDuration
            )
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopInteractionEffect()
    {
        _pulseTween?.Kill();
        _colorTween?.Kill();

        _colorTween = _image
            .DOColor(normalColor, 0.15f);

        transform
            .DOScale(_defaultScale, 0.15f)
            .SetEase(Ease.OutQuad);
    }
}