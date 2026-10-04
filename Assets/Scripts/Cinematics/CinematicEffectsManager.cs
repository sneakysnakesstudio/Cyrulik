using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ustawienia kinowego najazdu fizycznego (Dolly Push-in z gracza na przedmiot i z powrotem do gracza).
/// </summary>
[System.Serializable]
public class CinematicDollySettings
{
    [Tooltip("Czy kamera ma fizycznie przemieścić się z pozycji gracza pod sam przedmiot i wrócić?")]
    public bool usePhysicalPush = true;

    [Tooltip("Odległość zatrzymania kamery przed badanym przedmiotem (w metrach).")]
    public float targetDistance = 0.42f;

    [Tooltip("Dodatkowy offset pozycji kamery względem celu (np. lekko niżej/wyżej).")]
    public Vector3 targetOffset = new Vector3(0f, -0.05f, 0f);

    [Tooltip("Kąt widzenia (FOV) kamery podczas przebywania przed obiektem.")]
    public float targetFov = 35f;

    [Tooltip("Czas płynnego dolotu kamery z gracza do przedmiotu (w sekundach).")]
    public float approachDuration = 0.85f;

    [Tooltip("Czas skupienia / przebywania tuż przed przedmiotem (w sekundach).")]
    public float holdDuration = 2.0f;

    [Tooltip("Czas płynnego powrotu kamery z przedmiotu do pozycji gracza (w sekundach).")]
    public float returnDuration = 0.75f;

    [Tooltip("Krzywa przejścia dolotu (approach ease).")]
    public Ease approachEase = Ease.OutCubic;

    [Tooltip("Krzywa przejścia powrotu (return ease).")]
    public Ease returnEase = Ease.InOutQuad;

    [Tooltip("Czy zamrozić ruch gracza podczas trwania sekwencji ujęcia.")]
    public bool lockPlayerMovement = true;
}

/// <summary>
/// Główny menedżer efektów kinowych (Cinema Transitions, Edge Flash, Divine Grace / Poświata Anielska, Obuch, Camera Dolly Zoom).
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

    [Header("Domyślne Parametry Dolly Zoom")]
    [SerializeField] private CinematicDollySettings defaultDollySettings = new CinematicDollySettings();

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

    // Cache kamery i gracza
    private Camera _mainCamera;
    private Unity.Cinemachine.CinemachineBrain _cinemachineBrain;
    private float _defaultFov = 60f;
    private Tween _fovTween;
    private Tween _moveTween;
    private Tween _rotTween;
    private Coroutine _dollyRoutine;

    private bool _isDollyActive = false;
    public bool IsDollyActive => _isDollyActive;

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
        _moveTween?.Kill();
        _rotTween?.Kill();
    }

    private void CacheCamera()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _defaultFov = _mainCamera.fieldOfView;
                _cinemachineBrain = _mainCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
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

        Transform existing = transform.Find("CinematicEdgeCanvas");
        if (existing != null)
        {
            _edgeCanvasGroup = existing.GetComponent<CanvasGroup>();
            _edgeFlashImage = existing.GetComponentInChildren<Image>();
            if (_edgeCanvasGroup != null && _edgeFlashImage != null) return;
        }

        GameObject canvasGo = new GameObject("CinematicEdgeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = CanvasLayerManager.LAYER_CROSSHAIR_HUD + 1;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _edgeCanvasGroup = canvasGo.GetComponent<CanvasGroup>();
        _edgeCanvasGroup.alpha = 0f;
        _edgeCanvasGroup.blocksRaycasts = false;
        _edgeCanvasGroup.interactable = false;

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
    // 2. ANIELSKA POŚWIATA I ŁASKA (DIVINE GRACE)
    // ──────────────────────────────────────────────────────────

    public void TriggerDivineGrace(
        float duration = 2.5f,
        float intensity = 1.0f,
        Color? glowColor = null,
        AudioClip customClip = null,
        string audioGroupName = "Croos_audio_sfx")
    {
        Color divineColor = glowColor ?? defaultDivineGlowColor;
        TriggerEdgeFlash(divineColor, duration, 0.85f * intensity, 1);

        if (HeadBobbing.Instance != null)
        {
            HeadBobbing.Instance.TriggerConcussion(duration, 0.35f * intensity);
        }

        if (Crosshair.Instance != null)
        {
            Crosshair.Instance.PlayConcussionShake(duration * 0.45f, 4.5f * intensity, 12);
        }

        PlayHeavenlyAudio(customClip, audioGroupName, 0.95f * intensity);
    }

    // ──────────────────────────────────────────────────────────
    // 3. FIZYCZNY NAJAZD / DOLLY ZOOM Z GRACZA DO PRZEDMIOTU I POWRÓT
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Wykonuje płynny fizyczny najazd kamery z pozycji gracza pod sam badany przedmiot,
    /// trzyma ujęcie, po czym płynnie wraca z powrotem do gracza.
    /// </summary>
    public void PlayDollyZoom(Transform target, CinematicDollySettings settings = null, Action onHoldStart = null, Action onComplete = null)
    {
        CacheCamera();
        if (_mainCamera == null || target == null)
        {
            onHoldStart?.Invoke();
            onComplete?.Invoke();
            return;
        }

        if (_dollyRoutine != null)
        {
            StopCoroutine(_dollyRoutine);
        }

        CinematicDollySettings activeSettings = settings ?? defaultDollySettings;
        _dollyRoutine = StartCoroutine(DollyZoomRoutine(target, activeSettings, onHoldStart, onComplete));
    }

    private IEnumerator DollyZoomRoutine(Transform target, CinematicDollySettings s, Action onHoldStart, Action onComplete)
    {
        _isDollyActive = true;
        CacheCamera();

        Transform camTransform = _mainCamera.transform;
        Vector3 startCamPos = camTransform.position;
        Quaternion startCamRot = camTransform.rotation;
        float startFov = _mainCamera.fieldOfView;

        // 1. Zablokuj gracza i cinemachine na czas trwania najazdu
        if (s.lockPlayerMovement && PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.enabled = false;
        }

        if (_cinemachineBrain != null)
        {
            _cinemachineBrain.enabled = false;
        }

        if (HeadBobbing.Instance != null)
        {
            HeadBobbing.Instance.enabled = false;
        }

        // 2. Oblicz punkt docelowy przed przedmiotem
        Vector3 directionToPlayer = (startCamPos - target.position).normalized;
        if (directionToPlayer.sqrMagnitude < 0.001f)
        {
            directionToPlayer = target.forward;
        }

        Vector3 targetPos = target.position + (directionToPlayer * s.targetDistance) + s.targetOffset;
        Quaternion targetRot = Quaternion.LookRotation(target.position - targetPos, Vector3.up);

        // 3. FAZA 1: Najazd z gracza na rzecz (Approach)
        _moveTween?.Kill();
        _rotTween?.Kill();
        _fovTween?.Kill();

        if (s.usePhysicalPush)
        {
            _moveTween = camTransform.DOMove(targetPos, s.approachDuration).SetEase(s.approachEase);
            _rotTween = camTransform.DORotateQuaternion(targetRot, s.approachDuration).SetEase(s.approachEase);
        }

        _fovTween = _mainCamera.DOFieldOfView(s.targetFov, s.approachDuration).SetEase(s.approachEase);

        yield return new WaitForSeconds(s.approachDuration);

        // 4. FAZA 2: Skupienie na przedmiocie (Hold)
        onHoldStart?.Invoke();
        yield return new WaitForSeconds(s.holdDuration);

        // 5. FAZA 3: Powrót do gracza (Return)
        // Pobierz aktualną pozycję bazową głowy gracza
        Vector3 returnPos = startCamPos;
        Quaternion returnRot = startCamRot;

        if (PlayerMovement.Instance != null)
        {
            returnPos = startCamPos;
            returnRot = startCamRot;
        }

        _moveTween?.Kill();
        _rotTween?.Kill();
        _fovTween?.Kill();

        if (s.usePhysicalPush)
        {
            _moveTween = camTransform.DOMove(returnPos, s.returnDuration).SetEase(s.returnEase);
            _rotTween = camTransform.DORotateQuaternion(returnRot, s.returnDuration).SetEase(s.returnEase);
        }

        _fovTween = _mainCamera.DOFieldOfView(startFov, s.returnDuration).SetEase(s.returnEase);

        yield return new WaitForSeconds(s.returnDuration);

        // 6. FAZA 4: Odblokowanie gracza i przywrócenie kontroli
        if (_cinemachineBrain != null)
        {
            _cinemachineBrain.enabled = true;
        }

        if (HeadBobbing.Instance != null)
        {
            HeadBobbing.Instance.enabled = true;
        }

        if (s.lockPlayerMovement && PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.enabled = true;
        }

        _isDollyActive = false;
        _dollyRoutine = null;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Klasyczny prosty focus kamery (FOV zoom).
    /// </summary>
    public void FocusCameraOn(Transform target, float holdDuration = 1.6f, float zoomFov = -1f, Action onComplete = null)
    {
        var settings = new CinematicDollySettings
        {
            usePhysicalPush = false,
            targetFov = (zoomFov > 0f) ? zoomFov : defaultDollySettings.targetFov,
            holdDuration = holdDuration,
            lockPlayerMovement = false
        };

        PlayDollyZoom(target, settings, null, onComplete);
    }

    public void ResetCameraFocus()
    {
        if (_dollyRoutine != null)
        {
            StopCoroutine(_dollyRoutine);
            _dollyRoutine = null;
        }

        _moveTween?.Kill();
        _rotTween?.Kill();
        _fovTween?.Kill();

        if (_cinemachineBrain != null) _cinemachineBrain.enabled = true;
        if (HeadBobbing.Instance != null) HeadBobbing.Instance.enabled = true;
        if (PlayerMovement.Instance != null) PlayerMovement.Instance.enabled = true;

        CacheCamera();
        if (_mainCamera != null)
        {
            _mainCamera.fieldOfView = _defaultFov;
        }
        _isDollyActive = false;
    }

    // ──────────────────────────────────────────────────────────
    // 4. ODTWARZANIE DŹWIĘKÓW
    // ──────────────────────────────────────────────────────────

    public void PlayHeavenlyAudio(AudioClip customClip = null, string audioGroup = "Croos_audio_sfx", float volume = 1f)
    {
        if (AudioManager.Instance != null)
        {
            string primaryGroup = !string.IsNullOrEmpty(audioGroup) ? audioGroup : "Croos_audio_sfx";
            if (AudioManager.Instance.TryPlay(primaryGroup))
            {
                return;
            }

            string fallbackGroup = primaryGroup.Equals("Croos_audio_sfx", StringComparison.OrdinalIgnoreCase) ? "Cross_audio_sfx" : "Croos_audio_sfx";
            if (AudioManager.Instance.TryPlay(fallbackGroup))
            {
                return;
            }
        }

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

    public static AudioClip GenerateProceduralHeavenlyChordClip()
    {
        const int sampleRate = 44100;
        const float duration = 3.0f;
        int sampleCount = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        float[] chordFreqs = { 261.63f, 329.63f, 392.00f, 523.25f, 1046.5f };
        float[] weights = { 0.35f, 0.28f, 0.25f, 0.20f, 0.12f };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;

            float attack = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            float decay = Mathf.Exp(-t * 0.95f);
            float envelope = attack * decay;

            float vibrato = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 4.5f * t);

            float totalWave = 0f;
            for (int f = 0; f < chordFreqs.Length; f++)
            {
                float freq = chordFreqs[f] * vibrato;
                totalWave += Mathf.Sin(2f * Mathf.PI * freq * t) * weights[f];
            }

            float chimeEnv = Mathf.Exp(-t * 3.5f) * Mathf.SmoothStep(0f, 1f, t * 40f);
            float chime = Mathf.Sin(2f * Mathf.PI * 2093f * t) * chimeEnv * 0.15f;

            samples[i] = Mathf.Clamp((totalWave * envelope) + chime, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("SFX_Procedural_Heavenly_Divine_Chord", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
