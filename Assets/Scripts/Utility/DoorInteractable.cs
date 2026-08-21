using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class DoorInteractable : MonoBehaviour, IConditionalInteractable
{
    public event Action<bool> OnDoorStateChanged;

    public enum MotionMode
    {
        Rotate,
        Slide
    }

    public enum MovementAxis
    {
        X,
        Y,
        Z
    }

    [Header("Interaction")]
    [SerializeField] private string interactionName = "Door";

    [Header("Availability")]
    [Tooltip("Zaznacz tylko na drzwiach/szufladach, które mają być zablokowane na początku.")]
    [SerializeField] private bool lockedAtStart = false;

    [Header("References")]
    [SerializeField] private Transform doorPivot;

    [EnumButtons]
    [Header("Motion Mode")]
    [Tooltip("Rotate: obrót kątowy (np. drzwi). Slide: przesunięcie pozycji (np. szuflady).")]
    [SerializeField] private MotionMode motionMode = MotionMode.Rotate;

    [EnumButtons]
    [Header("Axis")]
    [FormerlySerializedAs("rotationAxis")]
    [SerializeField] private MovementAxis axis = MovementAxis.Y;

    [Header("Rotation Settings (Gdy Rotate)")]
    [Tooltip("Kąt otwarcia w stopniach.")]
    [SerializeField] private float openAngle = 90f;

    [Header("Slide Settings (Gdy Slide / Szuflada)")]
    [Tooltip("Dystans wysunięcia w jednostkach lokalnych (np. -0.35 na osi X w lewo).")]
    [SerializeField] private float slideDistance = -0.35f;

    [Header("Timing & Animation")]
    [SerializeField] private float openDuration = 0.6f;

    [SerializeField] private Ease openEase = Ease.OutQuad;
    [SerializeField] private Ease closeEase = Ease.InOutQuad;

    [Header("Audio")]
    [SerializeField] private string openSoundName = "";
    [SerializeField] private float openSoundDelay = 0f;
    [SerializeField] private string closeSoundName = "";
    [SerializeField] private float closeSoundDelay = 0f;

    public string InteractionName => interactionName;

    public bool IsOpen { get; private set; }

    public bool CanInteract => _isUnlocked;

    private bool _isUnlocked;

    private Vector3 _closedRotation;
    private Vector3 _openRotation;

    private Vector3 _closedPosition;
    private Vector3 _openPosition;

    private Tween _motionTween;

    private void Awake()
    {
        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        _isUnlocked = !lockedAtStart;

        // Zapamiętujemy pozycję i rotację zamkniętą
        _closedRotation = doorPivot.localEulerAngles;
        _openRotation = _closedRotation;

        _closedPosition = doorPivot.localPosition;
        _openPosition = _closedPosition;

        // Obliczamy wartości otwarte w zależności od osi
        switch (axis)
        {
            case MovementAxis.X:
                _openRotation.x += openAngle;
                _openPosition.x += slideDistance;
                break;

            case MovementAxis.Y:
                _openRotation.y += openAngle;
                _openPosition.y += slideDistance;
                break;

            case MovementAxis.Z:
                _openRotation.z += openAngle;
                _openPosition.z += slideDistance;
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
        _motionTween?.Kill();

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

        if (motionMode == MotionMode.Rotate)
        {
            _motionTween = doorPivot
                .DOLocalRotate(_openRotation, openDuration)
                .SetEase(openEase)
                .SetLink(doorPivot.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            _motionTween = doorPivot
                .DOLocalMove(_openPosition, openDuration)
                .SetEase(openEase)
                .SetLink(doorPivot.gameObject, LinkBehaviour.KillOnDestroy);
        }

        OnDoorStateChanged?.Invoke(true);
    }

    private void CloseDoor()
    {
        _motionTween?.Kill();

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

        if (motionMode == MotionMode.Rotate)
        {
            _motionTween = doorPivot
                .DOLocalRotate(_closedRotation, openDuration)
                .SetEase(closeEase)
                .SetLink(doorPivot.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            _motionTween = doorPivot
                .DOLocalMove(_closedPosition, openDuration)
                .SetEase(closeEase)
                .SetLink(doorPivot.gameObject, LinkBehaviour.KillOnDestroy);
        }

        OnDoorStateChanged?.Invoke(false);
    }

    private void OnDisable()
    {
        _motionTween?.Kill();
        _motionTween = null;
    }

    private void OnDestroy()
    {
        _motionTween?.Kill();
    }
}
