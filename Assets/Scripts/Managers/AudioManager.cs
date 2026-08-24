using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("References")]
    [SerializeField] private AudioDatabaseSO database;
    [SerializeField] private AudioSource sfxSource;

    [Header("Background Music (Main Theme)")]
    [Tooltip("Dedykowany AudioSource dla muzyki w tle (jeśli pusty, skrypt stworzy go automatycznie).")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Główny motyw muzyczny / utwór lecący w tle (Main Theme).")]
    [SerializeField] private AudioClip mainThemeClip;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.5f;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private bool loopMusic = true;
    [Tooltip("Czas płynnego rozkręcania muzyki na starcie lub przy zmianie utworu (w sekundach).")]
    [SerializeField] private float musicFadeDuration = 1.5f;

    [Header("Ambient Sound")]
    [Tooltip("Dedykowany AudioSource dla ambientu (jeśli pusty, skrypt stworzy go automatycznie).")]
    [SerializeField] private AudioSource ambientSource;
    [Tooltip("Dźwięk otoczenia w tle (np. szum pokoju, wiatr, wentylator).")]
    [SerializeField] private AudioClip ambientClip;
    [Range(0f, 1f)]
    [SerializeField] private float ambientVolume = 0.5f;
    [SerializeField] private bool playAmbientOnStart = true;
    [SerializeField] private bool loopAmbient = true;
    [Tooltip("Czas płynnego rozkręcania ambientu (w sekundach).")]
    [SerializeField] private float ambientFadeDuration = 1.5f;

    [Header("Radio Ducking & Transitions")]
    [Tooltip("Głośność muzyki w tle, gdy gra radio (0 = całkowite płynne wyciszenie muzyki).")]
    [Range(0f, 1f)]
    [SerializeField] private float radioDuckingMusicVolume = 0f;
    [Tooltip("Czy dźwięk ambientu również ma się ściszać, gdy włączone jest radio?")]
    [SerializeField] private bool duckAmbientWithRadio = false;
    [Tooltip("Głośność ambientu w trakcie grania radia (jeśli duckAmbientWithRadio jest zaznaczone).")]
    [Range(0f, 1f)]
    [SerializeField] private float radioDuckingAmbientVolume = 0.15f;
    [Tooltip("Domyślny czas płynnego przejścia wyciszenia/powrotu przy włączaniu radia.")]
    [SerializeField] private float defaultRadioFadeDuration = 1.0f;

    [Header("SFX Audio Pool")]
    [SerializeField] private int initialPoolSize = 8;

    private readonly List<AudioSource> _sourcePool = new List<AudioSource>();
    private Tween _musicFadeTween;
    private Tween _ambientFadeTween;
    private bool _isRadioPlaying = false;

    // Właściwości publiczne do odczytu stanu
    public bool IsRadioPlaying => _isRadioPlaying;
    public float MusicVolume => musicVolume;
    public float AmbientVolume => ambientVolume;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupMusicSource();
        SetupAmbientSource();
        CreateAudioPool();
    }

    private void Start()
    {
        if (playMusicOnStart && mainThemeClip != null)
        {
            PlayMusic(mainThemeClip, musicFadeDuration, loopMusic);
        }

        if (playAmbientOnStart && ambientClip != null)
        {
            PlayAmbient(ambientClip, ambientFadeDuration, loopAmbient);
        }
    }

    private void SetupMusicSource()
    {
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("Music Source");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = loopMusic;
        musicSource.spatialBlend = 0f; // Dźwięk 2D dla muzyki w tle
        musicSource.volume = 0f;
    }

    private void SetupAmbientSource()
    {
        if (ambientSource == null)
        {
            GameObject ambientObj = new GameObject("Ambient Source");
            ambientObj.transform.SetParent(transform);
            ambientSource = ambientObj.AddComponent<AudioSource>();
        }

        ambientSource.playOnAwake = false;
        ambientSource.loop = loopAmbient;
        ambientSource.spatialBlend = 0f; // Dźwięk 2D dla tła otoczenia
        ambientSource.volume = 0f;
    }

    #region Music (Main Theme) Controls

    /// <summary>
    /// Uruchamia lub zmienia muzykę w tle z płynnym wejściem (fade in).
    /// </summary>
    public void PlayMusic(AudioClip clip = null, float fadeDuration = -1f, bool loop = true)
    {
        if (clip == null)
            clip = mainThemeClip;

        if (clip == null || musicSource == null)
            return;

        float duration = fadeDuration >= 0f ? fadeDuration : musicFadeDuration;
        float targetVolume = _isRadioPlaying ? (musicVolume * radioDuckingMusicVolume) : musicVolume;

        _musicFadeTween?.Kill();

        if (musicSource.clip != clip || !musicSource.isPlaying)
        {
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = 0f;
            musicSource.Play();
        }

        if (duration > 0f)
        {
            _musicFadeTween = musicSource
                .DOFade(targetVolume, duration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            musicSource.volume = targetVolume;
        }
    }

    /// <summary>
    /// Płynnie wycisza i zatrzymuje muzykę w tle.
    /// </summary>
    public void StopMusic(float fadeDuration = -1f)
    {
        if (musicSource == null || !musicSource.isPlaying)
            return;

        float duration = fadeDuration >= 0f ? fadeDuration : musicFadeDuration;

        _musicFadeTween?.Kill();

        if (duration > 0f)
        {
            _musicFadeTween = musicSource
                .DOFade(0f, duration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (musicSource != null)
                        musicSource.Stop();
                });
        }
        else
        {
            musicSource.volume = 0f;
            musicSource.Stop();
        }
    }

    /// <summary>
    /// Płynnie wycisza i pauzuje muzykę.
    /// </summary>
    public void PauseMusic(float fadeDuration = -1f)
    {
        if (musicSource == null || !musicSource.isPlaying)
            return;

        float duration = fadeDuration >= 0f ? fadeDuration : 0.5f;

        _musicFadeTween?.Kill();

        if (duration > 0f)
        {
            _musicFadeTween = musicSource
                .DOFade(0f, duration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (musicSource != null)
                        musicSource.Pause();
                });
        }
        else
        {
            musicSource.volume = 0f;
            musicSource.Pause();
        }
    }

    /// <summary>
    /// Wznawia zapauzowaną muzykę z płynnym wejściem.
    /// </summary>
    public void ResumeMusic(float fadeDuration = -1f)
    {
        if (musicSource == null)
            return;

        float duration = fadeDuration >= 0f ? fadeDuration : 0.5f;
        float targetVolume = _isRadioPlaying ? (musicVolume * radioDuckingMusicVolume) : musicVolume;

        _musicFadeTween?.Kill();
        musicSource.UnPause();

        if (duration > 0f)
        {
            _musicFadeTween = musicSource
                .DOFade(targetVolume, duration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            musicSource.volume = targetVolume;
        }
    }

    /// <summary>
    /// Zmienia bazowy poziom głośności muzyki.
    /// </summary>
    public void SetMusicVolume(float volume, bool instant = false)
    {
        musicVolume = Mathf.Clamp01(volume);
        float targetVolume = _isRadioPlaying ? (musicVolume * radioDuckingMusicVolume) : musicVolume;

        if (musicSource != null && musicSource.isPlaying)
        {
            if (instant)
            {
                _musicFadeTween?.Kill();
                musicSource.volume = targetVolume;
            }
            else
            {
                _musicFadeTween?.Kill();
                _musicFadeTween = musicSource
                    .DOFade(targetVolume, 0.3f)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
        }
    }

    #endregion

    #region Ambient Sound Controls

    /// <summary>
    /// Uruchamia lub zmienia dźwięk otoczenia (ambient) z płynnym wejściem.
    /// </summary>
    public void PlayAmbient(AudioClip clip = null, float fadeDuration = -1f, bool loop = true)
    {
        if (clip == null)
            clip = ambientClip;

        if (clip == null || ambientSource == null)
            return;

        float duration = fadeDuration >= 0f ? fadeDuration : ambientFadeDuration;
        float targetVolume = (_isRadioPlaying && duckAmbientWithRadio)
            ? (ambientVolume * radioDuckingAmbientVolume)
            : ambientVolume;

        _ambientFadeTween?.Kill();

        if (ambientSource.clip != clip || !ambientSource.isPlaying)
        {
            ambientSource.clip = clip;
            ambientSource.loop = loop;
            ambientSource.volume = 0f;
            ambientSource.Play();
        }

        if (duration > 0f)
        {
            _ambientFadeTween = ambientSource
                .DOFade(targetVolume, duration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
        else
        {
            ambientSource.volume = targetVolume;
        }
    }

    /// <summary>
    /// Płynnie wycisza i zatrzymuje ambient.
    /// </summary>
    public void StopAmbient(float fadeDuration = -1f)
    {
        if (ambientSource == null || !ambientSource.isPlaying)
            return;

        float duration = fadeDuration >= 0f ? fadeDuration : ambientFadeDuration;

        _ambientFadeTween?.Kill();

        if (duration > 0f)
        {
            _ambientFadeTween = ambientSource
                .DOFade(0f, duration)
                .SetEase(Ease.InOutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (ambientSource != null)
                        ambientSource.Stop();
                });
        }
        else
        {
            ambientSource.volume = 0f;
            ambientSource.Stop();
        }
    }

    /// <summary>
    /// Zmienia bazowy poziom głośności ambientu.
    /// </summary>
    public void SetAmbientVolume(float volume, bool instant = false)
    {
        ambientVolume = Mathf.Clamp01(volume);
        float targetVolume = (_isRadioPlaying && duckAmbientWithRadio)
            ? (ambientVolume * radioDuckingAmbientVolume)
            : ambientVolume;

        if (ambientSource != null && ambientSource.isPlaying)
        {
            if (instant)
            {
                _ambientFadeTween?.Kill();
                ambientSource.volume = targetVolume;
            }
            else
            {
                _ambientFadeTween?.Kill();
                _ambientFadeTween = ambientSource
                    .DOFade(targetVolume, 0.3f)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
        }
    }

    #endregion

    #region Radio Ducking System

    /// <summary>
    /// Wywoływane przez RadioInteractable lub inne źródło muzyki.
    /// Płynnie wycisza muzykę w tle (i opcjonalnie ambient) gdy radio gra,
    /// oraz płynnie przywraca pierwotną głośność po wyłączeniu radia.
    /// </summary>
    /// <param name="isRadioPlaying">Czy radio gra muzykę?</param>
    /// <param name="customFadeDuration">Opcjonalny czas płynnego przejścia (jeśli ujemny, używa defaultRadioFadeDuration).</param>
    public void SetRadioActive(bool isRadioPlaying, float customFadeDuration = -1f)
    {
        _isRadioPlaying = isRadioPlaying;
        float duration = customFadeDuration >= 0f ? customFadeDuration : defaultRadioFadeDuration;

        // 1. Wyciszanie / powrót muzyki w tle (Main Theme)
        if (musicSource != null && musicSource.isPlaying)
        {
            float targetMusicVolume = _isRadioPlaying
                ? (musicVolume * radioDuckingMusicVolume)
                : musicVolume;

            _musicFadeTween?.Kill();

            if (duration > 0f)
            {
                _musicFadeTween = musicSource
                    .DOFade(targetMusicVolume, duration)
                    .SetEase(Ease.InOutQuad)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                musicSource.volume = targetMusicVolume;
            }
        }

        // 2. Wyciszanie / powrót ambientu (jeśli duckAmbientWithRadio jest włączone)
        if (ambientSource != null && ambientSource.isPlaying)
        {
            float targetAmbientVolume = (_isRadioPlaying && duckAmbientWithRadio)
                ? (ambientVolume * radioDuckingAmbientVolume)
                : ambientVolume;

            _ambientFadeTween?.Kill();

            if (duration > 0f)
            {
                _ambientFadeTween = ambientSource
                    .DOFade(targetAmbientVolume, duration)
                    .SetEase(Ease.InOutQuad)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            else
            {
                ambientSource.volume = targetAmbientVolume;
            }
        }
    }

    #endregion

    #region SFX Pool System

    private void CreateAudioPool()
    {
        if (sfxSource == null)
            return;

        _sourcePool.Clear();
        _sourcePool.Add(sfxSource);

        for (int i = 1; i < initialPoolSize; i++)
        {
            CreatePooledSource();
        }
    }

    private AudioSource CreatePooledSource()
    {
        GameObject sourceObject = new GameObject($"SFX Source {_sourcePool.Count + 1}");
        sourceObject.transform.SetParent(transform);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        CopyAudioSourceSettings(sfxSource, source);

        _sourcePool.Add(source);
        return source;
    }

    private void CopyAudioSourceSettings(AudioSource from, AudioSource to)
    {
        if (from == null || to == null)
            return;

        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.mute = from.mute;
        to.bypassEffects = from.bypassEffects;
        to.bypassListenerEffects = from.bypassListenerEffects;
        to.bypassReverbZones = from.bypassReverbZones;
        to.priority = from.priority;
        to.volume = from.volume;
        to.pitch = 1f;
        to.panStereo = from.panStereo;
        to.spatialBlend = 0f; // Dźwięki systemowe 2D
        to.reverbZoneMix = from.reverbZoneMix;
        to.dopplerLevel = from.dopplerLevel;
        to.spread = from.spread;
        to.rolloffMode = from.rolloffMode;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
        to.playOnAwake = false;
        to.loop = false;
    }

    private AudioSource GetAvailableSource()
    {
        foreach (AudioSource source in _sourcePool)
        {
            if (source == null)
                continue;

            if (!source.isPlaying)
                return source;
        }

        return CreatePooledSource();
    }

    public void Play(string groupName)
    {
        if (database == null)
        {
            Debug.LogWarning("AudioManager: AudioDatabaseSO is not assigned!", this);
            return;
        }

        if (sfxSource == null)
        {
            Debug.LogWarning("AudioManager: SFX AudioSource is not assigned!", this);
            return;
        }

        AudioClipData data = database.Get(groupName);

        if (data == null)
        {
            Debug.LogWarning($"AudioManager: Audio group '{groupName}' not found!", this);
            return;
        }

        AudioClip clip = data.GetRandomClip();

        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: Audio group '{groupName}' has no clips!", this);
            return;
        }

        AudioSource source = GetAvailableSource();
        source.pitch = data.GetRandomPitch();
        float volume = data.GetRandomVolume();

        source.PlayOneShot(clip, volume);
    }

    #endregion

    private void OnDisable()
    {
        _musicFadeTween?.Kill();
        _ambientFadeTween?.Kill();
    }

    private void OnDestroy()
    {
        _musicFadeTween?.Kill();
        _ambientFadeTween?.Kill();
    }
}