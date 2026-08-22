using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;

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

    [Header("First Doors (Tutorial Gate)")]
    [Tooltip("Zaznacz na drzwiach wyjściowych z pokoju — wymaga ubrania się przed wyjściem.")]
    [SerializeField] private bool firstDoors = false;

    [Tooltip("ID taska w PreparationStateManager wymaganego do otwarcia (domyślnie 'dressed_up').")]
    [SerializeField] private string requiredTaskId = "dressed_up";

    [Tooltip("Wewnętrzny dialog gracza przy próbie wyjścia bez ubrania.")]
    [SerializeField] private string blockedMessage = "I should get dressed first...";

    [Tooltip("Wywoływane gdy gracz próbuje otworzyć drzwi bez spełnienia warunku firstDoors.")]
    [SerializeField] private UnityEvent<string> OnDoorBlocked;

    [Header("References")]
    [SerializeField] private Transform doorPivot;

    [Header("Door Handle (Opcjonalnie)")]
    [Tooltip("Osobny Transform klamki (np. dziecko skrzydła drzwi). Pozostaw puste jeśli drzwi/szuflada nie ma osobnej klamki.")]
    [SerializeField] private Transform doorHandle;

    [EnumButtons]
    [Tooltip("Oś obrotu klamki.")]
    [SerializeField] private MovementAxis handleAxis = MovementAxis.Z;

    [Tooltip("Kąt naciśnięcia klamki w stopniach (np. 35 lub -35).")]
    [SerializeField] private float handleAngle = 35f;

    [Tooltip("Czas obrotu klamki w dół przy naciskaniu.")]
    [SerializeField] private float handlePressDuration = 0.12f;

    [Tooltip("Czas powrotu klamki do pozycji poziomej/spoczynkowej.")]
    [SerializeField] private float handleReturnDuration = 0.18f;

    [SerializeField] private Ease handlePressEase = Ease.OutQuad;
    [SerializeField] private Ease handleReturnEase = Ease.OutQuad;

    [Tooltip("Opóźnienie rozpoczęcia ruchu drzwi po naciśnięciu klamki (np. 0.05s).")]
    [SerializeField] private float doorOpenDelayAfterHandle = 0.05f;

    [Header("Handle Audio")]
    [SerializeField] private string handleSoundName = "";
    [SerializeField] private float handleSoundDelay = 0f;

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

    public bool CanInteract
    {
        get
        {
            if (!_isUnlocked)
                return false;

            if (firstDoors && !IsRequiredTaskDone())
                return false;

            return true;
        }
    }

    private bool _isUnlocked;

    private Vector3 _closedRotation;
    private Vector3 _openRotation;

    private Vector3 _closedPosition;
    private Vector3 _openPosition;

    private Tween _motionTween;
    private Tween _handleTween;

    private Vector3 _handleRestRotation;
    private Vector3 _handlePressedRotation;

    private void Awake()
    {
        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        _isUnlocked = !lockedAtStart;

        // Zapamiętujemy pozycję i rotację zamkniętą drzwi
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

        // Zapamiętujemy kąty klamki
        if (doorHandle != null)
        {
            _handleRestRotation = doorHandle.localEulerAngles;
            _handlePressedRotation = _handleRestRotation;

            switch (handleAxis)
            {
                case MovementAxis.X:
                    _handlePressedRotation.x += handleAngle;
                    break;

                case MovementAxis.Y:
                    _handlePressedRotation.y += handleAngle;
                    break;

                case MovementAxis.Z:
                    _handlePressedRotation.z += handleAngle;
                    break;
            }
        }
    }

    public void Interact()
    {
        if (!CanInteract)
        {
            string msg = BlockedMessage;
            if (InnerDialogueUI.Instance != null && !string.IsNullOrEmpty(msg))
            {
                InnerDialogueUI.Instance.ShowMessage(msg);
            }
            return;
        }

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

    public string BlockedMessage
    {
        get
        {
            // Dialog wewnętrzny wyświetla się TYLKO dla drzwi z First Doors, gdy gracz jeszcze się nie ubrał
            if (firstDoors && !IsRequiredTaskDone())
            {
                AnimateHandleBlocked();
                OnDoorBlocked?.Invoke(blockedMessage);
                return blockedMessage;
            }

            // Dla zwykłych zamkniętych szuflad/drzwi (Locked At Start):
            // szarpnij klamką jeśli jest, ale NIE pokazuj dialogu myśli (zwróć null)
            if (!_isUnlocked)
            {
                AnimateHandleBlocked();
                return null;
            }

            return null;
        }
    }

    private bool IsRequiredTaskDone()
    {
        if (PreparationStateManager.Instance == null)
            return false;

        return PreparationStateManager.Instance.IsTaskCompleted(requiredTaskId);
    }

    private void OpenDoor()
    {
        _motionTween?.Kill();

        IsOpen = true;

        if (doorHandle != null)
        {
            AnimateHandlePress();
        }

        if (doorHandle != null && doorOpenDelayAfterHandle > 0f)
        {
            DOVirtual.DelayedCall(doorOpenDelayAfterHandle, StartOpenMotion)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            StartOpenMotion();
        }

        OnDoorStateChanged?.Invoke(true);
    }

    private void StartOpenMotion()
    {
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
    }

    private void AnimateHandlePress()
    {
        if (doorHandle == null)
            return;

        _handleTween?.Kill();

        if (!string.IsNullOrEmpty(handleSoundName) && AudioManager.Instance != null)
        {
            if (handleSoundDelay > 0f)
            {
                DOVirtual.DelayedCall(handleSoundDelay, () =>
                {
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.Play(handleSoundName);
                }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                AudioManager.Instance.Play(handleSoundName);
            }
        }

        Sequence handleSeq = DOTween.Sequence();
        handleSeq.Append(
            doorHandle.DOLocalRotate(_handlePressedRotation, handlePressDuration)
                .SetEase(handlePressEase)
        );
        handleSeq.Append(
            doorHandle.DOLocalRotate(_handleRestRotation, handleReturnDuration)
                .SetEase(handleReturnEase)
        );
        handleSeq.SetLink(doorHandle.gameObject, LinkBehaviour.KillOnDestroy);

        _handleTween = handleSeq;
    }

    private void AnimateHandleBlocked()
    {
        if (doorHandle == null)
            return;

        _handleTween?.Kill();

        if (!string.IsNullOrEmpty(handleSoundName) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(handleSoundName);
        }

        // Subtelne szarpnięcie klamką przy zablokowanych drzwiach (np. 35% kąta i powrót)
        Vector3 jiggleRotation = _handleRestRotation;
        float jiggleAngle = handleAngle * 0.35f;

        switch (handleAxis)
        {
            case MovementAxis.X:
                jiggleRotation.x += jiggleAngle;
                break;
            case MovementAxis.Y:
                jiggleRotation.y += jiggleAngle;
                break;
            case MovementAxis.Z:
                jiggleRotation.z += jiggleAngle;
                break;
        }

        Sequence blockedSeq = DOTween.Sequence();
        blockedSeq.Append(
            doorHandle.DOLocalRotate(jiggleRotation, 0.06f)
                .SetEase(Ease.OutQuad)
        );
        blockedSeq.Append(
            doorHandle.DOLocalRotate(_handleRestRotation, 0.08f)
                .SetEase(Ease.InQuad)
        );
        blockedSeq.SetLink(doorHandle.gameObject, LinkBehaviour.KillOnDestroy);

        _handleTween = blockedSeq;
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
        _handleTween?.Kill();
        _handleTween = null;
    }

    private void OnDestroy()
    {
        _motionTween?.Kill();
        _handleTween?.Kill();
    }
}
