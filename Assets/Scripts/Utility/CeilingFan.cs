using UnityEngine;

public class CeilingFan : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("References")]
    [SerializeField] private Transform fanPivot;
    [SerializeField] private LampSwitch lampSwitch;

    [Header("Rotation")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;

    [Tooltip("Prędkość wentylatora po włączeniu światła.")]
    [SerializeField] private float fanSpeed = 220f;

    [Tooltip("Jak szybko wentylator się rozpędza.")]
    [SerializeField] private float acceleration = 60f;

    [Tooltip("Jak szybko wentylator zwalnia po wyłączeniu.")]
    [SerializeField] private float deceleration = 40f;

    [Header("Audio")]
    [SerializeField] private AudioSource fanAudioSource;
    [SerializeField] private AudioClip fanLoopClip;

    [Tooltip("Pitch wentylatora podczas pracy.")]
    [SerializeField] private float runningPitch = 1f;

    [Tooltip("Jak szybko pitch dochodzi do docelowej wartości.")]
    [SerializeField] private float pitchChangeSpeed = 1f;

    private float _currentSpeed;
    private float _targetSpeed;

    private void Awake()
    {
        // Na początku wentylator stoi.
        _currentSpeed = 0f;
        _targetSpeed = 0f;

        SetupAudio();
    }

    private void OnEnable()
    {
        if (lampSwitch != null)
        {
            lampSwitch.OnLightStateChanged += HandleLightStateChanged;
        }
    }

    private void OnDisable()
    {
        if (lampSwitch != null)
        {
            lampSwitch.OnLightStateChanged -= HandleLightStateChanged;
        }
    }

    private void Update()
    {
        UpdateSpeed();
        RotateFan();
        UpdateAudio();
    }

    private void HandleLightStateChanged(bool isLightOn)
    {
        if (isLightOn)
        {
            // Światło ON -> wentylator zaczyna się rozpędzać.
            _targetSpeed = fanSpeed;

            StartFanAudio();
        }
        else
        {
            // Światło OFF -> wentylator zwalnia do zera.
            _targetSpeed = 0f;

            StopFanAudio();
        }
    }

    private void UpdateSpeed()
    {
        float changeRate = _currentSpeed < _targetSpeed
            ? acceleration
            : deceleration;

        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed,
            _targetSpeed,
            changeRate * Time.deltaTime
        );
    }

    private void RotateFan()
    {
        if (fanPivot == null)
            return;

        if (_currentSpeed <= 0f)
            return;

        Vector3 axis = rotationAxis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            RotationAxis.Z => Vector3.forward,
            _ => Vector3.up
        };

        fanPivot.Rotate(
            axis,
            _currentSpeed * Time.deltaTime,
            Space.Self
        );
    }

    private void SetupAudio()
    {
        if (fanAudioSource == null)
            return;

        fanAudioSource.loop = true;
        fanAudioSource.playOnAwake = false;
        fanAudioSource.clip = fanLoopClip;
        fanAudioSource.pitch = runningPitch;

        // Na wszelki wypadek zatrzymujemy audio na starcie.
        fanAudioSource.Stop();
    }

    private void StartFanAudio()
    {
        if (fanAudioSource == null || fanLoopClip == null)
            return;

        if (!fanAudioSource.isPlaying)
        {
            fanAudioSource.pitch = runningPitch;
            fanAudioSource.Play();
        }
    }

    private void StopFanAudio()
    {
        if (fanAudioSource == null)
            return;

        fanAudioSource.Stop();
    }

    private void UpdateAudio()
    {
        if (fanAudioSource == null || !fanAudioSource.isPlaying)
            return;

        fanAudioSource.pitch = Mathf.MoveTowards(
            fanAudioSource.pitch,
            runningPitch,
            pitchChangeSpeed * Time.deltaTime
        );
    }
}