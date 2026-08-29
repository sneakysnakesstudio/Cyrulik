using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Sekwencja grozy zegara ściennego o godzinie 17:00:
/// 1. O 17:00:00 zegar ścienny w salonie zaczyna głośno, echem odliczać sekundy (głośne TIK... TAK...).
/// 2. Buduje 7 sekund psychologicznego napięcia.
/// 3. O 17:00:07 rozbrzmiewa dzwonek nad drzwiami wejściowymi i rozpoczyna się sekwencja wejścia Jurka.
/// </summary>
public class WallClockSequence : MonoBehaviour
{
    public static WallClockSequence Instance { get; private set; }

    [Header("Harmonogram Czasowy")]
    [SerializeField] private int suspenseHour = 17;
    [SerializeField] private int suspenseMinute = 0;
    [SerializeField] private int suspenseSecond = 0;

    [Tooltip("Czas trwania głośnego tykania w sekundach (domyślnie 7s).")]
    [SerializeField] private float suspenseDuration = 7.0f;

    [Header("Audio")]
    [Tooltip("Dźwięk głośnego, złowrogiego tykania zegara ściennego.")]
    [SerializeField] private string loudTickSound = "clock_tick";
    [SerializeField] private AudioClip customLoudTickClip;
    [Range(0f, 1f)]
    [SerializeField] private float loudVolume = 1.0f;

    [Header("Elementy Wizualne Zegara")]
    [Tooltip("Opcjonalne wahadło zegara (obraca się w pętli).")]
    [SerializeField] private Transform pendulumTransform;
    [SerializeField] private float pendulumAngle = 12f;
    [SerializeField] private float pendulumSpeed = 1f;

    [Tooltip("Wskazówka godzinowa.")]
    [SerializeField] private Transform hourHand;
    [Tooltip("Wskazówka minutowa.")]
    [SerializeField] private Transform minuteHand;
    [Tooltip("Wskazówka sekundowa.")]
    [SerializeField] private Transform secondHand;

    private AudioSource _audioSource;
    private bool _sequenceTriggered = false;
    private bool _arrivalTriggered = false;
    private float _suspenseTimer = 0f;
    private int _lastSecond = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f; // Pełny dźwięk przestrzenny 3D ze ściany
            _audioSource.minDistance = 1f;
            _audioSource.maxDistance = 15f;
            _audioSource.playOnAwake = false;
        }

        StartPendulumAnimation();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void StartPendulumAnimation()
    {
        if (pendulumTransform == null) return;

        pendulumTransform.localRotation = Quaternion.Euler(0f, 0f, -pendulumAngle);
        pendulumTransform.DOLocalRotate(new Vector3(0f, 0f, pendulumAngle), pendulumSpeed)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void Update()
    {
        UpdateClockHands();

        if (GameTimeController.Instance == null) return;

        // 1. Sprawdź, czy nadeszła godzina 17:00:00
        if (!_sequenceTriggered)
        {
            if (GameTimeController.Instance.HasTimeReached(suspenseHour, suspenseMinute, suspenseSecond))
            {
                TriggerSuspenseSequence();
            }
        }

        // 2. Obsługa 7 sekund napięcia
        if (_sequenceTriggered && !_arrivalTriggered)
        {
            _suspenseTimer += Time.deltaTime;

            // Głośne tykanie co każdą sekundę
            int curSec = GameTimeController.Instance.Second;
            if (curSec != _lastSecond)
            {
                _lastSecond = curSec;
                PlayLoudTick();
            }

            if (_suspenseTimer >= suspenseDuration)
            {
                _arrivalTriggered = true;
                TriggerCustomerArrival();
            }
        }
    }

    private void UpdateClockHands()
    {
        if (GameTimeController.Instance == null) return;

        int h = GameTimeController.Instance.Hour;
        int m = GameTimeController.Instance.Minute;
        int s = GameTimeController.Instance.Second;

        if (hourHand != null)
        {
            float hAngle = (h % 12 + m / 60f) * 30f;
            hourHand.localRotation = Quaternion.Euler(0f, 0f, -hAngle);
        }

        if (minuteHand != null)
        {
            float mAngle = (m + s / 60f) * 6f;
            minuteHand.localRotation = Quaternion.Euler(0f, 0f, -mAngle);
        }

        if (secondHand != null)
        {
            float sAngle = s * 6f;
            secondHand.localRotation = Quaternion.Euler(0f, 0f, -sAngle);
        }
    }

    /// <summary>
    /// Rozpoczyna sekwencję 7 sekund głośnego tykania o 17:00:00.
    /// </summary>
    public void TriggerSuspenseSequence()
    {
        _sequenceTriggered = true;
        _suspenseTimer = 0f;

        Debug.Log("<color=#FF4444>[WallClockSequence] 17:00:00! Zegar ścienny zaczyna złowrogo tykać... 7 sekund do nadejścia klienta!</color>");

        PlayLoudTick();
    }

    private void PlayLoudTick()
    {
        if (_audioSource != null)
        {
            if (customLoudTickClip != null)
            {
                _audioSource.PlayOneShot(customLoudTickClip, loudVolume);
            }
            else if (AudioManager.Instance != null && !string.IsNullOrEmpty(loudTickSound))
            {
                AudioManager.Instance.Play(loudTickSound);
            }
        }
    }

    /// <summary>
    /// Wywoływane po 7 sekundach (17:00:07) – uruchamia nadejście Jurka.
    /// </summary>
    private void TriggerCustomerArrival()
    {
        Debug.Log("<color=#70FF70>[WallClockSequence] 17:00:07! Rozbrzmiewa dzwonek drzwi – Jurek przybył do salonu!</color>");

        if (CustomerJurek.Instance != null && !CustomerJurek.Instance.HasArrived)
        {
            CustomerJurek.Instance.SetVisualsActive(true);
            CustomerJurek.Instance.TriggerArrival();
        }
    }
}
