using System;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class LampSwitch : MonoBehaviour, IConditionalInteractable
{
    public event Action<bool> OnLightStateChanged;
    
    [Header("Interaction")]
    [SerializeField] private string interactionName = "Light Switch";

    [Tooltip("Czy gracz może wyłączyć światło po jego włączeniu? Jeśli false (odznaczone), po zapaleniu światła nie można go ponownie zgasić.")]
    [SerializeField] private bool canTurnOff = true;

    [Header("Lights")]
    [SerializeField] private Light[] targetLights;

    [Header("State")]
    [SerializeField] private bool startOn = false;

    [Header("Audio")]
    [SerializeField] private string interactionSound = "small_lamp";

    [Header("Particle Dust Effects (Optional)")]
    [Tooltip("Czy włączać drobinki kurzu / poświatę (dust motes) w świetle lampy po jej włączeniu?")]
    [SerializeField] private bool spawnDustParticles = false;
    [SerializeField] private string dustParticleEffectId = "lamp_dust";

    [Header("Normal Animation")]
    [SerializeField] private float turnOnDuration = 0.1f;
    [SerializeField] private float turnOffDuration = 0.1f;

    [Header("Flickering")]
    [SerializeField] private bool flickering = false;

    [SerializeField] private int minFlickers = 2;
    [SerializeField] private int maxFlickers = 4;

    [SerializeField] private float minFlickerDuration = 0.03f;
    [SerializeField] private float maxFlickerDuration = 0.12f;

    [SerializeField]
    [Range(0f, 1f)]
    private float minFlickerIntensity = 0.05f;

    [SerializeField]
    [Range(0f, 1f)]
    private float maxFlickerIntensity = 0.6f;

    public string InteractionName => interactionName;
    public bool IsOn => _isOn;

    public bool CanInteract
    {
        get
        {
            if (_isOn && !canTurnOff)
                return false;

            return true;
        }
    }

    private bool _isOn;

    private float[] _defaultIntensities;

    private Tween _lightTween;
    private Sequence _flickerSequence;

    private void Awake()
    {
        if (targetLights == null || targetLights.Length == 0)
        {
            Debug.LogWarning(
                "LampSwitch: No lights assigned.",
                this
            );

            return;
        }

        _defaultIntensities =
            new float[targetLights.Length];

        for (int i = 0; i < targetLights.Length; i++)
        {
            Light light = targetLights[i];

            if (light == null)
                continue;

            _defaultIntensities[i] =
                light.intensity;
        }

        _isOn = startOn;

        if (startOn)
        {
            SetLightsEnabled(true);
            SetLightMultiplier(1f);
        }
        else
        {
            SetLightMultiplier(0f);
            SetLightsEnabled(false);
        }
    }

    public void Interact()
    {
        if (_isOn && !canTurnOff)
            return;

        PlayInteractionSound();

        if (_isOn)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }

    private void PlayInteractionSound()
    {
        if (string.IsNullOrWhiteSpace(interactionSound))
            return;

        AudioManager.Instance?.Play(interactionSound);
    }

    public void TurnOn()
    {
        KillTweens();

        bool wasOn = _isOn;
        _isOn = true;

        SetLightsEnabled(true);

        if (flickering)
        {
            StartFlickering();
        }
        else
        {
            NormalTurnOn();
        }

        if (spawnDustParticles && ParticleManager.Instance != null && targetLights != null)
        {
            foreach (var light in targetLights)
            {
                if (light != null)
                {
                    ParticleManager.Instance.AttachLoopingEffect(dustParticleEffectId, light.transform, Vector3.zero, $"lamp_dust_{light.GetEntityId()}");
                }
            }
        }

        if (!wasOn)
        {
            OnLightStateChanged?.Invoke(true);
        }
    }

    public void TurnOff()
    {
        KillTweens();

        bool wasOn = _isOn;
        _isOn = false;

        if (spawnDustParticles && ParticleManager.Instance != null && targetLights != null)
        {
            foreach (var light in targetLights)
            {
                if (light != null)
                {
                    ParticleManager.Instance.DetachLoopingEffect(light.transform, $"lamp_dust_{light.GetEntityId()}");
                }
            }
        }

        float currentMultiplier =
            GetCurrentMultiplier();

        _lightTween = DOVirtual
            .Float(
                currentMultiplier,
                0f,
                turnOffDuration,
                SetLightMultiplier
            )
            .SetEase(Ease.OutQuad)
            .SetLink(
                gameObject,
                LinkBehaviour.KillOnDestroy
            )
            .OnComplete(() =>
            {
                SetLightsEnabled(false);
            });

        if (wasOn)
        {
            OnLightStateChanged?.Invoke(false);
        }
    }

    private void NormalTurnOn()
    {
        float currentMultiplier =
            GetCurrentMultiplier();

        _lightTween = DOVirtual
            .Float(
                currentMultiplier,
                1f,
                turnOnDuration,
                SetLightMultiplier
            )
            .SetEase(Ease.OutQuad)
            .SetLink(
                gameObject,
                LinkBehaviour.KillOnDestroy
            );
    }

    private void StartFlickering()
    {
        SetLightMultiplier(0f);

        _flickerSequence =
            DOTween.Sequence();

        int flickerCount =
            Random.Range(
                minFlickers,
                maxFlickers + 1
            );

        for (int i = 0; i < flickerCount; i++)
        {
            float intensity =
                Random.Range(
                    minFlickerIntensity,
                    maxFlickerIntensity
                );

            float onDuration =
                Random.Range(
                    minFlickerDuration,
                    maxFlickerDuration
                );

            float offDuration =
                Random.Range(
                    minFlickerDuration,
                    maxFlickerDuration
                );

            _flickerSequence.AppendCallback(() =>
            {
                SetLightMultiplier(intensity);
            });

            _flickerSequence.AppendInterval(
                onDuration
            );

            _flickerSequence.AppendCallback(() =>
            {
                SetLightMultiplier(0f);
            });

            _flickerSequence.AppendInterval(
                offDuration
            );
        }

        _flickerSequence.Append(
            DOVirtual.Float(
                0f,
                1f,
                turnOnDuration,
                SetLightMultiplier
            )
        );

        _flickerSequence.SetLink(
            gameObject,
            LinkBehaviour.KillOnDestroy
        );
    }

    private void SetLightMultiplier(float multiplier)
    {
        if (_defaultIntensities == null)
            return;

        for (int i = 0; i < targetLights.Length; i++)
        {
            Light light = targetLights[i];

            if (light == null)
                continue;

            light.intensity =
                _defaultIntensities[i] * multiplier;
        }
    }

    private float GetCurrentMultiplier()
    {
        if (targetLights == null ||
            _defaultIntensities == null)
        {
            return 0f;
        }

        for (int i = 0; i < targetLights.Length; i++)
        {
            Light light = targetLights[i];

            if (light == null)
                continue;

            if (_defaultIntensities[i] <= 0f)
                continue;

            return light.intensity /
                   _defaultIntensities[i];
        }

        return 0f;
    }

    private void SetLightsEnabled(bool value)
    {
        if (targetLights == null)
            return;

        foreach (Light light in targetLights)
        {
            if (light == null)
                continue;

            light.enabled = value;
        }
    }

    private void KillTweens()
    {
        _lightTween?.Kill();
        _flickerSequence?.Kill();

        _lightTween = null;
        _flickerSequence = null;
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}