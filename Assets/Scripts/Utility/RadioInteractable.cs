using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Interaktywne radio w klimacie retro horroru.
/// Gracz podchodzi, klika E, a radio płynnie włącza/wyłącza sączącą się w przestrzeni 3D muzykę.
/// </summary>
public class RadioInteractable : MonoBehaviour, IConditionalInteractable
{
    public event Action<bool> OnRadioStateChanged;

    [Header("Interaction Prompts (English)")]
    [SerializeField] private string promptTurnOn = "Turn on radio";
    [SerializeField] private string promptTurnOff = "Turn off radio";

    [Header("Radio Audio Setup")]
    [Tooltip("Dedykowany AudioSource radia (jeśli pusty, skrypt pobierze z tego obiektu).")]
    [SerializeField] private AudioSource radioAudioSource;

    [Tooltip("Główny utwór muzyczny / audycja radiowa.")]
    [SerializeField] private AudioClip radioMusicClip;

    [Tooltip("Opcjonalna playlista wielu utworów (jeśli radioMusicClip jest pusty).")]
    [SerializeField] private AudioClip[] playlist;

    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.75f;

    [Tooltip("Czas płynnego pogłaśniania i wyciszania muzyki (w sekundach).")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Switch SFX")]
    [Tooltip("Dźwięk kliknięcia przełącznika z bazy AudioManager (np. small_lamp).")]
    [SerializeField] private string switchSoundName = "small_lamp";

    [Header("Visuals (Optional)")]
    [Tooltip("Opcjonalna dioda / światełko radia (włącza się razem z radiem).")]
    [SerializeField] private Light powerIndicatorLight;

    [Tooltip("Opcjonalny Renderer ze świecącą skalą radia / emisją.")]
    [SerializeField] private Renderer dialRenderer;
    [SerializeField] private int materialIndex = 0;
    [SerializeField] private Color emissionOnColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private Color emissionOffColor = Color.black;

    [Header("Task & State")]
    [Tooltip("Stan początkowy radia po załadowaniu sceny.")]
    [SerializeField] private bool isOnAtStart = false;

    [Tooltip("Opcjonalne ID zadania do PreparationStateManager.")]
    [SerializeField] private string taskId = "radio_turned_on";

    public string InteractionName => _isOn ? promptTurnOff : promptTurnOn;

    public bool CanInteract => true;

    public bool IsOn => _isOn;

    private bool _isOn;
    private Tween _fadeTween;
    private Material _dialMaterial;
    private int _playlistIndex;

    private void Awake()
    {
        SetupAudioSource();

        if (dialRenderer != null && materialIndex < dialRenderer.materials.Length)
        {
            _dialMaterial = dialRenderer.materials[materialIndex];
        }

        _isOn = isOnAtStart;
        ApplyStateInstant(_isOn);
    }

    private void SetupAudioSource()
    {
        if (radioAudioSource == null)
        {
            radioAudioSource = GetComponent<AudioSource>();
            if (radioAudioSource == null)
            {
                radioAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Konfigurujemy AudioSource pod dźwięk przestrzenny 3D w pokoju
        radioAudioSource.playOnAwake = false;
        radioAudioSource.loop = true;
        radioAudioSource.spatialBlend = 1.0f; // 100% 3D dźwięk ze źródła radia
        radioAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        radioAudioSource.minDistance = 1.2f;
        radioAudioSource.maxDistance = 12.0f;
    }

    public void Interact()
    {
        ToggleRadio();
    }

    public void ToggleRadio()
    {
        SetRadioState(!_isOn);
    }

    public void SetRadioState(bool turnOn)
    {
        if (_isOn == turnOn) return;

        _isOn = turnOn;

        // Kliknięcie przełącznika
        if (!string.IsNullOrEmpty(switchSoundName) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(switchSoundName);
        }

        _fadeTween?.Kill();

        if (_isOn)
        {
            StartPlayingMusic();
        }
        else
        {
            StopPlayingMusic();
        }

        // Płynne wyciszenie / przywrócenie muzyki w tle (Main Theme i Ambient)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetRadioActive(_isOn, fadeDuration);
        }

        UpdateVisuals();

        // Powiadomienie menedżera zadań
        if (!string.IsNullOrEmpty(taskId))
        {
            PreparationStateManager.Instance?.SetTaskState(taskId, _isOn);
        }

        OnRadioStateChanged?.Invoke(_isOn);
    }

    private void StartPlayingMusic()
    {
        if (radioAudioSource == null) return;

        // Dobierz utwór
        AudioClip clipToPlay = radioMusicClip;
        if (clipToPlay == null && playlist != null && playlist.Length > 0)
        {
            clipToPlay = playlist[_playlistIndex % playlist.Length];
            _playlistIndex++;
        }

        if (clipToPlay != null)
        {
            if (radioAudioSource.clip != clipToPlay || !radioAudioSource.isPlaying)
            {
                radioAudioSource.clip = clipToPlay;
                radioAudioSource.time = 0f;
                radioAudioSource.volume = 0f;
                radioAudioSource.Play();
            }

            _fadeTween = radioAudioSource
                .DOFade(maxVolume, fadeDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void StopPlayingMusic()
    {
        if (radioAudioSource == null || !radioAudioSource.isPlaying) return;

        _fadeTween = radioAudioSource
            .DOFade(0f, fadeDuration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                if (!_isOn && radioAudioSource != null)
                {
                    radioAudioSource.Stop();
                }
            });
    }

    private void UpdateVisuals()
    {
        if (powerIndicatorLight != null)
        {
            powerIndicatorLight.enabled = _isOn;
        }

        if (_dialMaterial != null)
        {
            Color targetColor = _isOn ? emissionOnColor : emissionOffColor;
            _dialMaterial.SetColor("_EmissionColor", targetColor);
            if (_isOn)
                _dialMaterial.EnableKeyword("_EMISSION");
            else
                _dialMaterial.DisableKeyword("_EMISSION");
        }
    }

    private void ApplyStateInstant(bool state)
    {
        _isOn = state;

        if (radioAudioSource != null)
        {
            if (_isOn)
            {
                AudioClip clipToPlay = radioMusicClip;
                if (clipToPlay == null && playlist != null && playlist.Length > 0)
                {
                    clipToPlay = playlist[0];
                }

                if (clipToPlay != null)
                {
                    radioAudioSource.clip = clipToPlay;
                    radioAudioSource.volume = maxVolume;
                    radioAudioSource.Play();
                }
            }
            else
            {
                radioAudioSource.volume = 0f;
                radioAudioSource.Stop();
            }
        }

        UpdateVisuals();

        if (AudioManager.Instance != null && _isOn)
        {
            AudioManager.Instance.SetRadioActive(true, 0f);
        }
    }

    private void OnDisable()
    {
        _fadeTween?.Kill();
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }
}