using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Główny menedżer efektów kinowych (Cinema Transitions, Edge Flash, Divine Grace / Poświata Anielska, Obuch, Camera Focus).
/// Działa automatycznie jako Singleton i zarządza nakładkami winiety, ruchem kamery oraz dźwiękiem.
/// </summary>
public class CinematicEffectsManager : MonoBehaviour
{
    private static CinematicEffectsManager _instance;
    public static CinematicEffectsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<CinematicEffectsManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CinematicEffectsManager_Dynamic", typeof(CinematicEffectsManager));
                    _instance = go.GetComponent<CinematicEffectsManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Edge Flash & Divine Glow Settings")]
    [Tooltip("Kolor domyślnego błysku winiety (np. czerwony/obuch).")]
    [SerializeField] private Color defaultFlashColor = new Color(0.65f, 0.08f, 0.08f, 0.85f);
    [Tooltip("Kolor anielskiej, świętej poświaty (złoto-biały).")]
    [SerializeField] private Color defaultDivineGlowColor = new Color(1.0f, 0.94f, 0.72f, 0.85f);

    [Header("Camera Focus & Zoom Settings")]
    [Tooltip("Domyślny kąt widzenia (FOV) podczas zbliżenia kinowego.")]
    [SerializeField] private float cinematicFov = 38f;
    [Tooltip("Czas płynnego najazdu/dojścia kamery (zoom in).")]
    [SerializeField] private float focusInDuration = 0.75f;
    [Tooltip("Czas płynnego powrotu kamery (zoom out).")]
    [SerializeField] private float focusOutDuration = 0.6f;

    [Header("Audio Settings")]
    [Tooltip("Dedykowany AudioSource dla efektów kinowych.")]
    [SerializeField] private AudioSource cinemaAudioSource;
    [Tooltip("Opcjonalny własny AudioClip uderzenia obuchem / szoku.")]
    [SerializeField] private AudioClip customConcussionClip;
    [Tooltip("Opcjonalny własny AudioClip anielskiego blasku / chóru.")]
    [SerializeField] private AudioClip customHeavenlyClip;
    [Tooltip("Nazwa grupy audio w AudioManager (opcjonalnie).")]
    [SerializeField] private string concussionAudioGroup = "";

    // Elementy interfejsu winiety
    private CanvasGroup _edgeCanvasGroup;
    private Image _edgeFlashImage;
    private Tween _edgeFlashTween;

    // Cache kamery
    private Camera _mainCamera;
    private float _defaultFov = 60f;
    private Tween _fovTween;
    private Coroutine _focusSequenceRoutine;

    // Wygenerowane proceduralne klipy audio
    private static AudioClip _proceduralConcussionClip;
    private static AudioClip _proceduralHeavenlyClip;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
        _proceduralConcussionClip = null;
        _proceduralHeavenlyClip = null;
    }
#endif

    private void Awake()
    {
        if (_instance != null && _instance != this && _instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        SetupAudioSource();
        EnsureEdgeFlashOverlay();
        CacheCamera();
    }

    private void Start()
    {
        CacheCamera();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        _edgeFlashTween?.Kill();
        _fovTween?.Kill();
    }

    private void CacheCamera()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _defaultFov = _mainCamera.fieldOfView;
            }
        }
    }

    private void SetupAudioSource()
    {
        if (cinemaAudioSource == null)
        {
            cinemaAudioSource = GetComponent<AudioSource>();
            if (cinemaAudioSource == null)
            {
                cinemaAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        cinemaAudioSource.playOnAwake = false;
        cinemaAudioSource.loop = false;
        cinemaAudioSource.spatialBlend = 0f; // 2D Full Stereo
        cinemaAudioSource.volume = 1f;
    }

    private void EnsureEdgeFlashOverlay()
    {
        if (_edgeCanvasGroup != null && _edgeFlashImage != null) return;

        // Szukamy istniejącego obiektu w scenie
        Transform existing = transform.Find("CinematicEdgeCanvas");
        if (existing != null)
        {
            _edgeCanvasGroup = existing.GetComponent<CanvasGroup>();
            _edgeFlashImage = existing.GetComponentInChildren<Image>();
            if (_edgeCanvasGroup != null && _edgeFlashImage != null) return;
        }

        // Tworzymy dynamiczny Canvas dla efektów kinowych
        GameObject canvasGo = new GameObject("CinematicEdgeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = CanvasLayerManager.LAYER_CROSSHAIR_HUD + 1; // Tuż nad celownikiem

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _edgeCanvasGroup = canvasGo.GetComponent<CanvasGroup>();
        _edgeCanvasGroup.alpha = 0f;
        _edgeCanvasGroup.blocksRaycasts = false;
        _edgeCanvasGroup.interactable = false;

        // Generujemy obiekt winiety (Edge Vignette Image)
        GameObject imgGo = new GameObject("EdgeFlashImage", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        _edgeFlashImage = imgGo.GetComponent<Image>();
        _edgeFlashImage.raycastTarget = false;
        _edgeFlashImage.sprite = GenerateVignetteSprite();
        _edgeFlashImage.type = Image.Type.Simple;
        _edgeFlashImage.color = defaultDivineGlowColor;
    }

    /// <summary>
    /// Generuje proceduralną teksturę miękkiej winiety krawędziowej.
    /// </summary>
    private Sprite GenerateVignetteSprite()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "Procedural_Cinematic_Vignette";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.72f;

        Color[] colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float normDist = Mathf.Clamp01(dist / maxDist);

                // Miękkie przejście — środek przezroczysty, brzegi nasycone
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Pow(normDist, 2.2f));
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ──────────────────────────────────────────────────────────
    // 1. FLASH NA BOKACH EKRANU (EDGE FLASH / VIGNETTE PULSE)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Odpala kinowy puls winiety na krawędziach ekranu.
    /// </summary>
    public void TriggerEdgeFlash(Color? flashColor = null, float duration = 1.2f, float peakAlpha = 0.85f, int pulseCount = 2)
    {
        EnsureEdgeFlashOverlay();
        if (_edgeCanvasGroup == null || _edgeFlashImage == null) return;

        Color targetColor = flashColor ?? defaultFlashColor;
        _edgeFlashImage.color = targetColor;

        _edgeFlashTween?.Kill();
        _edgeCanvasGroup.alpha = 0f;

        Sequence seq = DOTween.Sequence();

        float singlePulseDuration = duration / Mathf.Max(1, pulseCount);
        float attackTime = singlePulseDuration * 0.25f;
        float decayTime = singlePulseDuration * 0.75f;

        for (int i = 0; i < pulseCount; i++)
        {
            float currentPeak = (i == 0) ? peakAlpha : peakAlpha * 0.7f;
            seq.Append(_edgeCanvasGroup.DOFade(currentPeak, attackTime).SetEase(Ease.OutQuad));
            seq.Append(_edgeCanvasGroup.DOFade(0f, decayTime).SetEase(Ease.InSine));
        }

        seq.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _edgeFlashTween = seq;
    }

    // ──────────────────────────────────────────────────────────
    // 2. ANIELSKA POŚWIATA I ŁASKA (DIVINE GRACE / HEAVENLY GLOW)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Wyzwala anielską / psychodeliczną poświatę (złoto-białą na bokach), subtelny mistyczny shimmer i dźwięk z playlisty "Croos_audio_sfx".
    /// Wprowadza w transowo-modlitewny stan skupienia.
    /// </summary>
    public void TriggerDivineGrace(
        float duration = 2.5f,
        float intensity = 1.0f,
        Color? glowColor = null,
        AudioClip customClip = null,
        string audioGroupName = "Croos_audio_sfx")
    {
        // 1. Złoto-biała święta poświata na bokach z subtelnym, psychodelicznym tętnieniem
        Color divineColor = glowColor ?? defaultDivineGlowColor;
        TriggerEdgeFlash(divineColor, duration, 0.85f * intensity, 1);

        // 2. Subtelne, medytacyjno-psychodeliczne kołysanie głowy
        if (HeadBobbing.Instance != null)
        {
            HeadBobbing.Instance.TriggerConcussion(duration, 0.35f * intensity);
        }

        // 3. Delikatny shimmer celownika
        if (Crosshair.Instance != null)
        {
            Crosshair.Instance.PlayConcussionShake(duration * 0.45f, 4.5f * intensity, 12);
        }

        // 4. Dźwięk z playlisty Croos_audio_sfx lub anielski akord
        PlayHeavenlyAudio(customClip, audioGroupName, 0.95f * intensity);
    }

    // ──────────────────────────────────────────────────────────
    // 3. OBUCH / CONCUSSION SHOCK (GIBANIE KAMERY + KROPKA + AUDIO)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Wyzwala pełny efekt uderzenia obuchem / wstrząsu.
    /// </summary>
    public void TriggerConcussionShock(
        float duration = 3.5f,
        float intensity = 1.0f,
        Color? flashColor = null,
        AudioClip customClip = null,
        string audioGroup = null)
    {
        if (Crosshair.Instance != null)
        {
            Crosshair.Instance.PlayConcussionShake(duration * 0.5f, 15f * intensity, 25);
        }

        if (HeadBobbing.Instance != null)
        {
            HeadBobbing.Instance.TriggerConcussion(duration, intensity);
        }

        TriggerEdgeFlash(flashColor ?? defaultFlashColor, duration * 0.6f, 0.85f * intensity, 2);

        PlayConcussionAudio(customClip, audioGroup, 1f * intensity);
    }

    // ──────────────────────────────────────────────────────────
    // 4. PŁYNNE PRZEJŚCIE / FOCUS KAMERY DO PRZEDMIOTU
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Płynnie przybliża kamerę do badanego przedmiotu (zoom FOV) ze skupieniem uwagi.
    /// </summary>
    public void FocusCameraOn(Transform target, float holdDuration = 1.6f, float zoomFov = -1f, Action onComplete = null)
    {
        CacheCamera();
        if (_mainCamera == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (_focusSequenceRoutine != null)
        {
            StopCoroutine(_focusSequenceRoutine);
        }

        float targetFov = (zoomFov > 0f) ? zoomFov : cinematicFov;
        _focusSequenceRoutine = StartCoroutine(FocusRoutine(target, holdDuration, targetFov, onComplete));
    }

    private IEnumerator FocusRoutine(Transform target, float holdDuration, float targetFov, Action onComplete)
    {
        CacheCamera();
        _fovTween?.Kill();

        if (_mainCamera != null)
        {
            _fovTween = _mainCamera
                .DOFieldOfView(targetFov, focusInDuration)
                .SetEase(Ease.OutCubic);
        }

        yield return new WaitForSeconds(focusInDuration + holdDuration);

        if (_mainCamera != null)
        {
            _fovTween = _mainCamera
                .DOFieldOfView(_defaultFov, focusOutDuration)
                .SetEase(Ease.InOutSine);
        }

        yield return new WaitForSeconds(focusOutDuration);

        _focusSequenceRoutine = null;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Natychmiastowo przerywa przybliżenie kamery i przywraca normalny FOV.
    /// </summary>
    public void ResetCameraFocus()
    {
        if (_focusSequenceRoutine != null)
        {
            StopCoroutine(_focusSequenceRoutine);
            _focusSequenceRoutine = null;
        }

        CacheCamera();
        if (_mainCamera != null)
        {
            _fovTween?.Kill();
            _fovTween = _mainCamera
                .DOFieldOfView(_defaultFov, 0.4f)
                .SetEase(Ease.OutQuad);
        }
    }

    // ──────────────────────────────────────────────────────────
    // 5. ODTWARZANIE DŹWIĘKÓW (HEAVENLY CHORD & OBUCH)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Odtwarza anielski, czysty dźwięk akordu (Heavenly Choir / Chime).
    /// <summary>
    /// Odtwarza anielski / mistyczny dźwięk z bazy AudioManager (np. playlista 'Croos_audio_sfx') lub custom / proceduralny akord.
    /// </summary>
    public void PlayHeavenlyAudio(AudioClip customClip = null, string audioGroup = "Croos_audio_sfx", float volume = 1f)
    {
        // 1. Sprawdź najpierw AudioManager dla podanej grupy lub jej wariantów ("Croos_audio_sfx" / "Cross_audio_sfx")
        if (AudioManager.Instance != null)
        {
            string primaryGroup = !string.IsNullOrEmpty(audioGroup) ? audioGroup : "Croos_audio_sfx";
            if (AudioManager.Instance.TryPlay(primaryGroup))
            {
                return;
            }

            // Fallback na alternatywną pisownię jeśli pierwsza nie została znaleziona
            string fallbackGroup = primaryGroup.Equals("Croos_audio_sfx", StringComparison.OrdinalIgnoreCase) ? "Cross_audio_sfx" : "Croos_audio_sfx";
            if (AudioManager.Instance.TryPlay(fallbackGroup))
            {
                return;
            }
        }

        // 2. Jeśli podano klip w parametrze lub Inspectorze
        AudioClip clipToPlay = customClip ?? customHeavenlyClip;
        if (clipToPlay == null)
        {
            if (_proceduralHeavenlyClip == null)
            {
                _proceduralHeavenlyClip = GenerateProceduralHeavenlyChordClip();
            }
            clipToPlay = _proceduralHeavenlyClip;
        }

        if (cinemaAudioSource != null && clipToPlay != null)
        {
            cinemaAudioSource.PlayOneShot(clipToPlay, Mathf.Clamp01(volume));
        }
    }

    public void PlayConcussionAudio(AudioClip customClip = null, string audioGroup = null, float volume = 1f)
    {
        string grp = !string.IsNullOrEmpty(audioGroup) ? audioGroup : concussionAudioGroup;
        if (!string.IsNullOrEmpty(grp) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(grp);
            return;
        }

        AudioClip clipToPlay = customClip ?? customConcussionClip;
        if (clipToPlay == null)
        {
            if (_proceduralConcussionClip == null)
            {
                _proceduralConcussionClip = GenerateProceduralConcussionClip();
            }
            clipToPlay = _proceduralConcussionClip;
        }

        if (cinemaAudioSource != null && clipToPlay != null)
        {
            cinemaAudioSource.PlayOneShot(clipToPlay, Mathf.Clamp01(volume));
        }
    }

    /// <summary>
    /// Generuje proceduralny, wzniosły, anielski akord organowo-chóralny z dzwonkiem (Divine Chord).
    /// </summary>
    public static AudioClip GenerateProceduralHeavenlyChordClip()
    {
        const int sampleRate = 44100;
        const float duration = 3.0f;
        int sampleCount = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Częstotliwości akordu F-dur / C-dur (261.6Hz C4, 329.6Hz E4, 392.0Hz G4, 523.2Hz C5, 1046.5Hz C6 shimmer)
        float[] chordFreqs = { 261.63f, 329.63f, 392.00f, 523.25f, 1046.5f };
        float[] weights = { 0.35f, 0.28f, 0.25f, 0.20f, 0.12f };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;

            // Płynna obwiednia: miękki atak (0.35s) i długie wybrzmiewanie
            float attack = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            float decay = Mathf.Exp(-t * 0.95f);
            float envelope = attack * decay;

            // Chóralny shimmer (lekkie vibrato 4.5 Hz)
            float vibrato = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 4.5f * t);

            float totalWave = 0f;
            for (int f = 0; f < chordFreqs.Length; f++)
            {
                float freq = chordFreqs[f] * vibrato;
                totalWave += Mathf.Sin(2f * Mathf.PI * freq * t) * weights[f];
            }

            // Dodaj delikatny kryształowy dzwoneczek (high crystal chime)
            float chimeEnv = Mathf.Exp(-t * 3.5f) * Mathf.SmoothStep(0f, 1f, t * 40f);
            float chime = Mathf.Sin(2f * Mathf.PI * 2093f * t) * chimeEnv * 0.15f;

            samples[i] = Mathf.Clamp((totalWave * envelope) + chime, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("SFX_Procedural_Heavenly_Divine_Chord", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip GenerateProceduralConcussionClip()
    {
        const int sampleRate = 44100;
        const float duration = 2.4f;
        int sampleCount = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;

            float bassFreq = Mathf.Lerp(60f, 32f, t * 1.5f);
            float bassEnv = Mathf.Exp(-t * 4.5f);
            float bassWave = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * bassEnv * 0.75f;

            float tinnitusEnv = Mathf.Exp(-t * 1.2f) * Mathf.SmoothStep(0f, 1f, t * 20f);
            float tinnitusWave = Mathf.Sin(2f * Mathf.PI * 3250f * t) * (tinnitusEnv * 0.22f);

            float noiseEnv = Mathf.Exp(-t * 18f);
            float noise = (UnityEngine.Random.value * 2f - 1f) * noiseEnv * 0.25f;

            samples[i] = Mathf.Clamp(bassWave + tinnitusWave + noise, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("SFX_Procedural_Concussion_Obuch", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
