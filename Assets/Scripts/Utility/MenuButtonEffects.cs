using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Zapewnia natychmiastową reakcję wizualną (powiększenie, podświetlenie koloru, dźwięk hover) po najechaniu myszką.
/// </summary>
public class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Visual Effects")]
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float clickScale = 0.96f;
    [SerializeField] private float animDuration = 0.12f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.15f, 0.13f, 0.11f, 0.95f);
    [SerializeField] private Color hoverColor = new Color(0.88f, 0.68f, 0.28f, 1f);
    [SerializeField] private Color normalTextColor = new Color(0.96f, 0.93f, 0.86f, 1f);
    [SerializeField] private Color hoverTextColor = new Color(0.1f, 0.08f, 0.05f, 1f);

    private Image _image;
    private TMP_Text _text;
    private Vector3 _originalScale;
    private Tween _scaleTween;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _text = GetComponentInChildren<TMP_Text>();
        _originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(_originalScale * hoverScale, animDuration).SetUpdate(true);

        if (_image != null) _image.color = hoverColor;
        if (_text != null) _text.color = hoverTextColor;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play("button_hover");
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(_originalScale, animDuration).SetUpdate(true);

        if (_image != null) _image.color = normalColor;
        if (_text != null) _text.color = normalTextColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(_originalScale * clickScale, 0.06f).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(_originalScale * hoverScale, 0.08f).SetUpdate(true);
    }

    private void OnDisable()
    {
        _scaleTween?.Kill();
        transform.localScale = _originalScale;
        if (_image != null) _image.color = normalColor;
        if (_text != null) _text.color = normalTextColor;
    }
}
