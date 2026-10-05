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
    [SerializeField] private bool spawnDustParticles = true;
    [SerializeField] private string dustParticleEffectId = "lamp_dust";
    [Tooltip("Lokalne przesunięcie pozycji cząsteczek pod źródłem światła (np. Y = -0.4 m pod żarówką).")]
    [SerializeField] private Vector3 dustLocalOffset = new Vector3(0f, -0.4f, 0f);
    [Tooltip("Mnożnik skali efektu cząsteczek kurzu.")]
    [SerializeField] private float dustScaleMultiplier = 1.0f;
    [Tooltip("Czy czyścić i usuwać cząsteczki kurzu natychmiast po zgaszeniu światła (brak lewitujących drobinek po ciemku).")]
    [SerializeField] private bool clearDustInstantlyOnTurnOff = true;

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

    [Header("Dying Bulb Pulse (Pulsowanie dogorywającej żarówki)")]
    [Tooltip("Czy światło po włączeniu ma ciągle delikatnie pulsować/falować, jakby żarówka była stara lub niedomagała?")]
    [SerializeField] private bool pulseBulb = false;

    [Tooltip("Szybkość pulsowania (częstotliwość mrugania/falowania).")]
    [SerializeField] private float pulseSpeed = 3.5f;

    [Tooltip("Minimalna jasność jako ułamek domyślnej intensywności (np. 0.75 = spadek do 75% jasności).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float minPulseMultiplier = 0.75f;

    [Tooltip("Maksymalna jasność jako ułamek domyślnej intensywności (np. 1.02 = skok do 102% jasności).")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float maxPulseMultiplier = 1.02f;

    [Tooltip("Czy dodawać sporadyczne mikro-iskrzenia / mrugnięcia żarówki?")]
    [SerializeField] private bool randomJitter = true;

    [Header("Switch Handle / Cord Animation (Pociągnięcie sznurka / klamka)")]
    [Tooltip("Czy włączyć animację pociągnięcia sznurka lub ruchu przełącznika.")]
    [SerializeField] private bool animateHandle = false;

    [Tooltip("Transform linki/sznurka/przełącznika. Jeśli puste, a animateHandle jest włączone, animuje ten obiekt (this.transform).")]
    [SerializeField] private Transform switchHandle;

    public enum HandleMotionMode
    {
        Slide,
        Rotate
    }

    public enum MovementAxis
    {
        X,
        Y,
        Z
    }

    [Tooltip("Slide: przesunięcie w dół (dla linki/sznurka). Rotate: obrót kątowy (dla włącznika/dźwigni).")]
    [SerializeField] private HandleMotionMode handleMotionMode = HandleMotionMode.Slide;

    [Tooltip("Oś ruchu (dla sznurka w dół zazwyczaj Y).")]
    [SerializeField] private MovementAxis handleAxis = MovementAxis.Y;

    [Tooltip("Czy dla trybu Slide pociągać linkę zawsze pionowo w dół ku ziemi (Vector3.down)? Eliminuje problem obróconych osi z Blendera.")]
    [SerializeField] private bool pullStraightDown = true;

    [Tooltip("Dystans pociągnięcia w metrach (np. 0.08m = 8 cm) lub kąt w stopniach (dla Rotate).")]
    [SerializeField] private float handleDistance = 0.08f;

    [Tooltip("Czas ruchu w dół przy pociągnięciu (w sekundach).")]
    [SerializeField] private float handlePressDuration = 0.12f;

    [Tooltip("Czas powrotu sznurka do góry na swoje miejsce.")]
    [SerializeField] private float handleReturnDuration = 0.18f;

    [SerializeField] private Ease handlePressEase = Ease.OutQuad;
    [SerializeField] private Ease handleReturnEase = Ease.OutBack;

#pragma warning disable CS0414
    [Tooltip("Opóźnienie przełączenia światła po pociągnięciu (zintegrowane automatycznie z ruchem w dół).")]
    [SerializeField] private float lightToggleDelay = 0.10f;
#pragma warning restore CS0414

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
    private float _pulseSeed;

    private float[] _defaultIntensities;

    private Tween _lightTween;
    private Sequence _flickerSequence;
    private Tween _handleTween;
    private Vector3 _handleRestPos;
    private Vector3 _handleRestRot;
    private bool _isPulling;

    private void Awake()
    {
        _pulseSeed = UnityEngine.Random.Range(0f, 1000f);

        if (animateHandle || switchHandle != null)
        {
            if (switchHandle == null)
                switchHandle = transform;

            _handleRestPos = switchHandle.localPosition;
            _handleRestRot = switchHandle.localEulerAngles;
        }

        if (targetLights == null || targetLights.Length == 0)
        {
            DevLog.LogWarning(
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

    private void Update()
    {
        if (_isOn && pulseBulb && _flickerSequence == null && _lightTween == null)
        {
            UpdateBulbPulse();
        }
    }

    private void UpdateBulbPulse()
    {
        if (targetLights == null || _defaultIntensities == null) return;

        float time = Time.time * pulseSpeed;
        float noise = Mathf.PerlinNoise(time, _pulseSeed);
        float sine = (Mathf.Sin(time * 1.5f) + 1f) * 0.5f;

        float combined = Mathf.Lerp(noise, sine, 0.35f);

        if (randomJitter && UnityEngine.Random.value < 0.06f)
        {
            combined *= UnityEngine.Random.Range(0.65f, 0.95f);
        }

        float finalMultiplier = Mathf.Lerp(minPulseMultiplier, maxPulseMultiplier, combined);
        SetLightMultiplier(finalMultiplier);
    }

    public void Interact()
    {
        if (_isOn && !canTurnOff)
            return;

        if (_isPulling)
            return;

        bool shouldAnimate = (animateHandle || switchHandle != null) && switchHandle != null;
        if (shouldAnimate)
        {
            AnimateHandleAndToggle();
        }
        else
        {
            PlayInteractionSound();
            if (_isOn) TurnOff(); else TurnOn();
        }
    }

    private void AnimateHandleAndToggle()
    {
        if (switchHandle == null)
            return;

        _handleTween?.Kill();

        // Gwarancja startu dokładnie z pozycji spoczynkowej
        switchHandle.localPosition = _handleRestPos;
        switchHandle.localEulerAngles = _handleRestRot;

        Sequence seq = DOTween.Sequence();
        _isPulling = true;

        if (handleMotionMode == HandleMotionMode.Slide)
        {
            Vector3 pressedPos;
            if (pullStraightDown)
            {
                float dist = Mathf.Abs(handleDistance);
                if (dist < 0.001f) dist = 0.05f;

                // Oblicz wektor przesunięcia w dół ku ziemi w lokalnym układzie rodzica
                Vector3 worldDown = Vector3.down * dist;
                Vector3 localOffset = switchHandle.parent != null
                    ? switchHandle.parent.InverseTransformVector(worldDown)
                    : worldDown;

                pressedPos = _handleRestPos + localOffset;
            }
            else
            {
                pressedPos = _handleRestPos;
                switch (handleAxis)
                {
                    case MovementAxis.X: pressedPos.x += handleDistance; break;
                    case MovementAxis.Y: pressedPos.y += handleDistance; break;
                    case MovementAxis.Z: pressedPos.z += handleDistance; break;
                }
            }

            // 1. Ruch sznurka w dół (gracz pociąga sznurek ku dołowi)
            seq.Append(switchHandle.DOLocalMove(pressedPos, handlePressDuration).SetEase(handlePressEase));

            // W najniższym punkcie pociągnięcia: dźwięk kliknięcia i przełączenie światła
            seq.AppendCallback(() =>
            {
                PlayInteractionSound();
                if (_isOn) TurnOff(); else TurnOn();
            });

            // 2. Ruch do góry (sznurek sprężyście wraca na swoje pierwotne miejsce jak przygwożdżony)
            seq.Append(switchHandle.DOLocalMove(_handleRestPos, handleReturnDuration).SetEase(handleReturnEase));

            // Gwarancja idealnego lądowania w pozycji wyjściowej i odblokowanie pociągania
            seq.OnComplete(() =>
            {
                if (switchHandle != null)
                {
                    switchHandle.localPosition = _handleRestPos;
                }
                _isPulling = false;
            });
        }
        else
        {
            Vector3 pressedRot = _handleRestRot;
            switch (handleAxis)
            {
                case MovementAxis.X: pressedRot.x += handleDistance; break;
                case MovementAxis.Y: pressedRot.y += handleDistance; break;
                case MovementAxis.Z: pressedRot.z += handleDistance; break;
            }

            // 1. Ruch kątowy włącznika
            seq.Append(switchHandle.DOLocalRotate(pressedRot, handlePressDuration).SetEase(handlePressEase));

            // W punkcie wciśnięcia: dźwięk i przełączenie światła
            seq.AppendCallback(() =>
            {
                PlayInteractionSound();
                if (_isOn) TurnOff(); else TurnOn();
            });

            // 2. Powrót
            seq.Append(switchHandle.DOLocalRotate(_handleRestRot, handleReturnDuration).SetEase(handleReturnEase));

            seq.OnComplete(() =>
            {
                if (switchHandle != null)
                {
                    switchHandle.localEulerAngles = _handleRestRot;
                }
                _isPulling = false;
            });
        }

        seq.SetLink(switchHandle.gameObject, LinkBehaviour.KillOnDestroy);
        _handleTween = seq;
    }

    private void PlayInteractionSound()
    {
        if (string.IsNullOrWhiteSpace(interactionSound))
            return;

        AudioManager.Instance?.Play(interactionSound);
    }

    public void TurnOn()
    {
        KillLightTweens();

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
                    ParticleManager.Instance.AttachLoopingEffect(dustParticleEffectId, light.transform, dustLocalOffset, $"lamp_dust_{light.GetEntityId()}", null, dustScaleMultiplier);
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
        KillLightTweens();

        bool wasOn = _isOn;
        _isOn = false;

        if (spawnDustParticles && ParticleManager.Instance != null && targetLights != null)
        {
            foreach (var light in targetLights)
            {
                if (light != null)
                {
                    ParticleManager.Instance.DetachLoopingEffect(light.transform, $"lamp_dust_{light.GetEntityId()}", clearDustInstantlyOnTurnOff);
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

    private void KillLightTweens()
    {
        _lightTween?.Kill();
        _flickerSequence?.Kill();

        _lightTween = null;
        _flickerSequence = null;
    }

    private void KillTweens()
    {
        KillLightTweens();
        _handleTween?.Kill();

        _handleTween = null;
        _isPulling = false;
    }

    private void OnDisable()
    {
        KillTweens();

        if (targetLights != null && ParticleManager.Instance != null)
        {
            foreach (var light in targetLights)
            {
                if (light != null)
                {
                    ParticleManager.Instance.DetachLoopingEffect(light.transform, $"lamp_dust_{light.GetEntityId()}", true);
                }
            }
        }

        if (switchHandle != null && (animateHandle || switchHandle != transform || _handleRestPos != Vector3.zero))
        {
            switchHandle.localPosition = _handleRestPos;
            switchHandle.localEulerAngles = _handleRestRot;
        }
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}