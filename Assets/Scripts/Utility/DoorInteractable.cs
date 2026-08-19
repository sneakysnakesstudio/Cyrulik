using System;
using DG.Tweening;
using UnityEngine;

public class DoorInteractable : MonoBehaviour, IConditionalInteractable
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

    [Header("Availability")]
    [Tooltip(
        "Zaznacz tylko na drzwiach, które mają być zablokowane na początku."
    )]
    [SerializeField] private bool lockedAtStart = false;

    [Header("References")]
    [SerializeField] private Transform doorPivot;

    [EnumButtons]
    [Header("Rotation")]
    [SerializeField] private RotationAxis rotationAxis =
        RotationAxis.Y;

    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openDuration = 0.6f;

    [Header("Animation")]
    [SerializeField] private Ease openEase =
        Ease.OutQuad;

    [SerializeField] private Ease closeEase =
        Ease.InOutQuad;

    [Header("Audio")]
    [SerializeField] private string openSoundName = "";
    [SerializeField] private float openSoundDelay = 0f;
    [SerializeField] private string closeSoundName = "";
    [SerializeField] private float closeSoundDelay = 0f;

    public string InteractionName =>
        interactionName;

    public bool IsOpen { get; private set; }

    public bool CanInteract =>
        _isUnlocked;

    private bool _isUnlocked;

    private Vector3 _closedRotation;
    private Vector3 _openRotation;

    private Tween _rotationTween;

    private void Awake()
    {
        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        // Wszystkie zwykłe drzwi:
        // lockedAtStart = false
        // więc od razu są odblokowane.
        _isUnlocked =
            !lockedAtStart;

        _closedRotation =
            doorPivot.localEulerAngles;

        _openRotation =
            _closedRotation;

        switch (rotationAxis)
        {
            case RotationAxis.X:
                _openRotation.x +=
                    openAngle;
                break;

            case RotationAxis.Y:
                _openRotation.y +=
                    openAngle;
                break;

            case RotationAxis.Z:
                _openRotation.z +=
                    openAngle;
                break;
        }
    }

    public void Interact()
    {
        if (IsOpen)
        {
            CloseDoor();
        }
        else
        {
            OpenDoor();
        }
    }

    public void Unlock()
    {
        _isUnlocked = true;
    }

    public void Lock()
    {
        _isUnlocked = false;
    }

    private void OpenDoor()
    {
        _rotationTween?.Kill();

        IsOpen = true;

        if (!string.IsNullOrEmpty(openSoundName) && AudioManager.Instance != null)
        {
            if (openSoundDelay > 0f)
            {
                DOVirtual.DelayedCall(openSoundDelay, () =>
                {
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.Play(openSoundName);
                }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                AudioManager.Instance.Play(openSoundName);
            }
        }

        _rotationTween =
            doorPivot
                .DOLocalRotate(
                    _openRotation,
                    openDuration
                )
                .SetEase(openEase)
                .SetLink(
                    doorPivot.gameObject,
                    LinkBehaviour.KillOnDestroy
                );

        OnDoorStateChanged?.Invoke(true);
    }

    private void CloseDoor()
    {
        _rotationTween?.Kill();

        IsOpen = false;

        if (!string.IsNullOrEmpty(closeSoundName) && AudioManager.Instance != null)
        {
            if (closeSoundDelay > 0f)
            {
                DOVirtual.DelayedCall(closeSoundDelay, () =>
                {
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.Play(closeSoundName);
                }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                AudioManager.Instance.Play(closeSoundName);
            }
        }

        _rotationTween =
            doorPivot
                .DOLocalRotate(
                    _closedRotation,
                    openDuration
                )
                .SetEase(closeEase)
                .SetLink(
                    doorPivot.gameObject,
                    LinkBehaviour.KillOnDestroy
                );

        OnDoorStateChanged?.Invoke(false);
    }

    private void OnDisable()
    {
        _rotationTween?.Kill();
        _rotationTween = null;
    }

    private void OnDestroy()
    {
        _rotationTween?.Kill();
    }
}
