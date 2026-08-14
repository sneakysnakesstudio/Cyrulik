using DG.Tweening;
using UnityEngine;

public class LightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DoorInteractable door;

    [SerializeField] private Light[] targetLights;

    [Header("Light Settings")]
    [SerializeField] private float targetIntensity = 2f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.1f;

    private Sequence _lightSequence;

    private void Awake()
    {
        if (targetLights == null || targetLights.Length == 0)
        {
            Debug.LogError(
                "LightController: Nie przypisano żadnych świateł!",
                this
            );

            return;
        }

        foreach (Light light in targetLights)
        {
            if (light == null)
                continue;

            light.intensity = 0f;
            light.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (door != null)
        {
            door.OnDoorStateChanged += HandleDoorStateChanged;
        }
    }

    private void OnDisable()
    {
        if (door != null)
        {
            door.OnDoorStateChanged -= HandleDoorStateChanged;
        }

        KillTween();
    }

    private void HandleDoorStateChanged(bool isOpen)
    {
        if (isOpen)
        {
            TurnOn();
        }
        else
        {
            TurnOff();
        }
    }

    public void TurnOn()
    {
        KillTween();

        _lightSequence = DOTween.Sequence();

        foreach (Light light in targetLights)
        {
            if (light == null)
                continue;

            light.enabled = true;

            Light currentLight = light;

            Tween tween = DOTween.To(
                    () => currentLight.intensity,
                    value => currentLight.intensity = value,
                    targetIntensity,
                    fadeInDuration
                )
                .SetEase(Ease.OutQuad);

            _lightSequence.Join(tween);
        }

        _lightSequence.SetLink(
            gameObject,
            LinkBehaviour.KillOnDestroy
        );
    }

    public void TurnOff()
    {
        KillTween();

        _lightSequence = DOTween.Sequence();

        foreach (Light light in targetLights)
        {
            if (light == null)
                continue;

            Light currentLight = light;

            Tween tween = DOTween.To(
                    () => currentLight.intensity,
                    value => currentLight.intensity = value,
                    0f,
                    fadeOutDuration
                )
                .SetEase(Ease.OutQuad);

            _lightSequence.Join(tween);
        }

        _lightSequence
            .OnComplete(() =>
            {
                foreach (Light light in targetLights)
                {
                    if (light == null)
                        continue;

                    light.enabled = false;
                }
            })
            .SetLink(
                gameObject,
                LinkBehaviour.KillOnDestroy
            );
    }

    private void KillTween()
    {
        _lightSequence?.Kill();
        _lightSequence = null;
    }

    private void OnDestroy()
    {
        KillTween();
    }
}