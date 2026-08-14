using System;
using DG.Tweening;
using UnityEngine;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    public event Action<bool> OnDoorStateChanged;

    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("Interaction")]
    [SerializeField] private string interactionName = "Door";

    [Header("References")]
    [SerializeField] private Transform doorPivot;

    [Header("Rotation")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;

    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openDuration = 0.6f;

    [Header("Animation")]
    [SerializeField] private Ease openEase = Ease.OutQuad;
    [SerializeField] private Ease closeEase = Ease.InOutQuad;

    public string InteractionName => interactionName;
    public bool IsOpen => _isOpen;

    private Vector3 _closedRotation;
    private Vector3 _openRotation;

    private bool _isOpen;
    private bool _isAnimating;

    private Tween _doorTween;

    private void Awake()
    {
        if (doorPivot == null)
        {
            Debug.LogError(
                "DoorInteractable: Door Pivot is not assigned!",
                this
            );

            enabled = false;
            return;
        }

        _closedRotation = doorPivot.localEulerAngles;

        CalculateOpenRotation();
    }

    private void CalculateOpenRotation()
    {
        Vector3 rotationOffset = Vector3.zero;

        switch (rotationAxis)
        {
            case RotationAxis.X:
                rotationOffset = new Vector3(
                    openAngle,
                    0f,
                    0f
                );
                break;

            case RotationAxis.Y:
                rotationOffset = new Vector3(
                    0f,
                    openAngle,
                    0f
                );
                break;

            case RotationAxis.Z:
                rotationOffset = new Vector3(
                    0f,
                    0f,
                    openAngle
                );
                break;
        }

        _openRotation =
            _closedRotation + rotationOffset;
    }

    public void Interact()
    {
        if (_isAnimating)
            return;

        if (_isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void Open()
    {
        _isAnimating = true;
        _isOpen = true;

        // Opcjonalny event.
        // Jeśli nic go nie nasłuchuje, po prostu nic się nie dzieje.
        OnDoorStateChanged?.Invoke(true);

        _doorTween?.Kill();

        _doorTween = doorPivot
            .DOLocalRotate(
                _openRotation,
                openDuration
            )
            .SetEase(openEase)
            .SetLink(
                doorPivot.gameObject,
                LinkBehaviour.KillOnDestroy
            )
            .OnComplete(() =>
            {
                _isAnimating = false;
            });
    }

    private void Close()
    {
        _isAnimating = true;
        _isOpen = false;

        OnDoorStateChanged?.Invoke(false);

        _doorTween?.Kill();

        _doorTween = doorPivot
            .DOLocalRotate(
                _closedRotation,
                openDuration
            )
            .SetEase(closeEase)
            .SetLink(
                doorPivot.gameObject,
                LinkBehaviour.KillOnDestroy
            )
            .OnComplete(() =>
            {
                _isAnimating = false;
            });
    }

    private void OnDisable()
    {
        _doorTween?.Kill();

        _doorTween = null;
        _isAnimating = false;
    }

    private void OnDestroy()
    {
        _doorTween?.Kill();
    }
}