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

    [Tooltip("Normalna prędkość wentylatora kiedy światło jest wyłączone.")]
    [SerializeField] private float normalSpeed = 80f;

    [Tooltip("Prędkość wentylatora po włączeniu światła.")]
    [SerializeField] private float boostedSpeed = 220f;

    [Tooltip("Jak szybko wentylator przyspiesza.")]
    [SerializeField] private float acceleration = 60f;

    [Tooltip("Jak szybko wentylator zwalnia.")]
    [SerializeField] private float deceleration = 40f;

    [Header("Audio")]
    [SerializeField] private AudioSource fanAudioSource;
    [SerializeField] private AudioClip fanLoopClip;

    [Tooltip("Pitch przy normalnej prędkości.")]
    [SerializeField] private float normalPitch = 0.85f;

    [Tooltip("Pitch przy szybkiej prędkości.")]
    [SerializeField] private float boostedPitch = 1.15f;

    [Tooltip("Jak szybko zmienia się pitch dźwięku.")]
    [SerializeField] private float pitchChangeSpeed = 1f;

    private float _currentSpeed;
    private float _targetSpeed;
    private float _targetPitch;

    private void Awake()
    {
        _currentSpeed = normalSpeed;
        _targetSpeed = normalSpeed;
        _targetPitch = normalPitch;

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

    private void Start()
    {
        StartFanAudio();
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
            _targetSpeed = boostedSpeed;
            _targetPitch = boostedPitch;
        }
        else
        {
            _targetSpeed = normalSpeed;
            _targetPitch = normalPitch;
        }
    }

    private void UpdateSpeed()
    {
        float speedChangeRate;

        if (_currentSpeed < _targetSpeed)
        {
            speedChangeRate = acceleration;
        }
        else
        {
            speedChangeRate = deceleration;
        }

        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed,
            _targetSpeed,
            speedChangeRate * Time.deltaTime
        );
    }

    private void RotateFan()
    {
        if (fanPivot == null)
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
        fanAudioSource.pitch = normalPitch;
    }

    private void StartFanAudio()
    {
        if (fanAudioSource == null || fanLoopClip == null)
            return;

        if (!fanAudioSource.isPlaying)
        {
            fanAudioSource.Play();
        }
    }

    private void UpdateAudio()
    {
        if (fanAudioSource == null)
            return;

        fanAudioSource.pitch = Mathf.MoveTowards(
            fanAudioSource.pitch,
            _targetPitch,
            pitchChangeSpeed * Time.deltaTime
        );
    }
}