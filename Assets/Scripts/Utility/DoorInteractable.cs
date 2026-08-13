using DG.Tweening;
using UnityEngine;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private Transform doorPivot;

    [Header("Rotation")]
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float openDuration = 0.6f;

    [Header("Animation")]
    [SerializeField] private Ease openEase = Ease.OutQuad;
    [SerializeField] private Ease closeEase = Ease.InOutQuad;

    private Vector3 _closedRotation;
    private Vector3 _openRotation;

    private bool _isOpen;
    private bool _isAnimating;

    private Tween _doorTween;

    private void Awake()
    {
        _closedRotation = doorPivot.localEulerAngles;

        _openRotation = _closedRotation + new Vector3(
                0f,
                0f,
                openAngle
            );
    }

    public void Interact()
    {
        if (_isAnimating)
            return;

        if (_isOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        _isAnimating = true;

        _doorTween?.Kill();

        _doorTween = doorPivot
            .DOLocalRotate(_openRotation, openDuration)
            .SetEase(openEase)
            .OnComplete(() =>
            {
                _isOpen = true;
                _isAnimating = false;
            });
    }

    private void Close()
    {
        _isAnimating = true;

        _doorTween?.Kill();

        _doorTween = doorPivot
            .DOLocalRotate(_closedRotation, openDuration)
            .SetEase(closeEase)
            .OnComplete(() =>
            {
                _isOpen = false;
                _isAnimating = false;
            });
    }

    private void OnDisable()
    {
        _doorTween?.Kill();
    }
}