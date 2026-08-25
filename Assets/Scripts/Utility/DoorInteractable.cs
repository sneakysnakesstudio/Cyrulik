using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
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

    [Header("Direct AudioClips (Przeciągnij bezpośrednio z Project)")]
    [Tooltip("Dźwięk szarpania zamkniętych/zablokowanych drzwi (AudioClip).")]
    [SerializeField] private AudioClip doorRattleClip;

    [Tooltip("Dźwięk otwierania drzwi (AudioClip).")]
    [SerializeField] private AudioClip openDoorClip;

    [Tooltip("Dźwięk zamykania drzwi (AudioClip).")]
    [SerializeField] private AudioClip closeDoorClip;

    [Tooltip("Dźwięk naciśnięcia klamki (AudioClip).")]
    [SerializeField] private AudioClip handlePressClip;

    [Header("Audio Mixer & Volume")]
    [Tooltip("Grupa wyjściowa miksera audio (np. SFX / Master) — pozwala sterować głośnością przez mikser gry.")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Range(0f, 1f)]
    [Tooltip("Poziom głośności dźwięków drzwi (0 = wyciszone, 1 = maksymalna głośność).")]
    [SerializeField] private float soundVolume = 0.75f;

    [Header("Audio (AudioManager Groups - Fallback)")]
    [SerializeField] private string openSoundName = "door_open";
    [SerializeField] private float openSoundDelay = 0f;
    [SerializeField] private string closeSoundName = "door_close";
    [SerializeField] private float closeSoundDelay = 0f;
    [SerializeField] private string handleSoundName = "door_handle_sound";
    [SerializeField] private float handleSoundDelay = 0f;
    [SerializeField] private string door_rattle_sound = "door_rattle_sound";

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
    private AudioSource _audioSource;

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

        SetupAudioSource();

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

    private void SetupAudioSource()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f; // Dźwięk przestrzenny 3D
        _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = 12f;

        if (outputMixerGroup != null)
        {
            _audioSource.outputAudioMixerGroup = outputMixerGroup;
        }
        else if (AudioManager.Instance != null && AudioManager.Instance.GetComponent<AudioSource>() != null)
        {
            var defaultSource = AudioManager.Instance.GetComponent<AudioSource>();
            if (defaultSource != null && defaultSource.outputAudioMixerGroup != null)
            {
                _audioSource.outputAudioMixerGroup = defaultSource.outputAudioMixerGroup;
            }
        }
    }

    public void Interact()
    {
        if (!CanInteract)
        {
            OnBlockedInteraction();
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
            OnBlockedInteraction();
            return GetBlockedDialogueMessage();
        }
    }

    private void OnBlockedInteraction()
    {
        PlayLockedSound();
        AnimateHandleBlocked();

        string msg = GetBlockedDialogueMessage();
        if (InnerDialogueUI.Instance != null && !string.IsNullOrEmpty(msg))
        {
            InnerDialogueUI.Instance.ShowMessage(msg);
        }
    }

    private string GetBlockedDialogueMessage()
    {
        // Dialog wewnętrzny wyświetla się TYLKO dla drzwi z First Doors, gdy gracz jeszcze się nie ubrał
        if (firstDoors && !IsRequiredTaskDone())
        {
            OnDoorBlocked?.Invoke(blockedMessage);
            return blockedMessage;
        }

        return null;
    }

    private bool IsRequiredTaskDone()
    {
        if (PreparationStateManager.Instance == null)
            return false;

        return PreparationStateManager.Instance.IsTaskCompleted(requiredTaskId);
    }

    public void OpenDoor()
    {
        if (IsOpen) return;
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
        PlaySound(openDoorClip, openSoundName, openSoundDelay, "door_open");

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

        PlaySound(handlePressClip, handleSoundName, handleSoundDelay, "door_handle_sound");

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

    private void PlayLockedSound()
    {
        // 1. Bezpośredni AudioClip przypisany w Inspectorze (najwyższy priorytet)
        if (doorRattleClip != null)
        {
            PlayDirectClip(doorRattleClip);
            return;
        }

        // 2. Pobranie z AudioManager
        string soundGroup = !string.IsNullOrWhiteSpace(door_rattle_sound) ? door_rattle_sound.Trim() : "door_rattle_sound";
        var audioManager = AudioManager.Instance != null ? AudioManager.Instance : FindAnyObjectByType<AudioManager>();
        if (audioManager != null)
        {
            audioManager.Play(soundGroup);
            return;
        }

        if (!string.IsNullOrEmpty(handleSoundName) && audioManager != null)
        {
            audioManager.Play(handleSoundName);
        }
    }

    private void PlaySound(AudioClip directClip, string soundGroupName, float delay, string fallbackGroup)
    {
        if (directClip != null)
        {
            if (delay > 0f)
            {
                DOVirtual.DelayedCall(delay, () => PlayDirectClip(directClip))
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                PlayDirectClip(directClip);
            }
            return;
        }

        string group = !string.IsNullOrEmpty(soundGroupName) ? soundGroupName : fallbackGroup;
        var audioMgr = AudioManager.Instance != null ? AudioManager.Instance : FindAnyObjectByType<AudioManager>();

        if (audioMgr != null)
        {
            if (delay > 0f)
            {
                DOVirtual.DelayedCall(delay, () =>
                {
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.Play(group);
                }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                audioMgr.Play(group);
            }
        }
    }

    private void PlayDirectClip(AudioClip clip)
    {
        if (clip == null) return;

        if (_audioSource != null)
        {
            if (outputMixerGroup != null && _audioSource.outputAudioMixerGroup != outputMixerGroup)
            {
                _audioSource.outputAudioMixerGroup = outputMixerGroup;
            }

            _audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(clip, soundVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, soundVolume);
        }
    }

    private void AnimateHandleBlocked()
    {
        if (doorHandle == null)
            return;

        _handleTween?.Kill();

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

    public void CloseDoor()
    {
        if (!IsOpen) return;
        _motionTween?.Kill();

        IsOpen = false;

        PlaySound(closeDoorClip, closeSoundName, closeSoundDelay, "door_close");

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
