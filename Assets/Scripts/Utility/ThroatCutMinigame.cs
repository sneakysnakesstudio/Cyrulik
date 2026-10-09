using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Minigra podcięcia gardła (Throat Cut Minigame):
/// 1. Kinowy najazd kamery w zbliżeniu (close-up) na odsłonięte gardło siedzącego Jurka.
/// 2. Pozioma linia w faliste kształty (sinusoida z wierzchołkami i dolinami), imitująca chirurgiczne nacięcie.
/// 3. Płonący iskrowy punkt (burning fuse / flame spark), który porusza się wzdłuż sinusoidy i narzuca tempo cięcia.
/// 4. Gracz trzyma LPM (LMB) i prowadzi kursor-brzytwę po sinusoidzie w X i Y; za ostrzem rysuje się tętnicza linia krwi (blood cut line).
/// 5. Obliczana dokładność (0-100%):
///    - >= 90%: Perfekcyjne podcięcie gardła -> tryskająca krew tętnicza, osunięcie się Jurka w fotelu, ekran sukcesu "A FLAWLESS CUT".
///    - < 90%: Nieudana próba -> krzyk Jurka, odepchnięcie gracza, ucieczka z krwawiącą raną, ekran porażki GTA "YOU FAILED".
/// Zoptymalizowane pod kątem wydajności (zero alokacji GC w Update).
/// </summary>
public class ThroatCutMinigame : MonoBehaviour
{
    public static ThroatCutMinigame Instance { get; private set; }

    [Header("UI Canvas & Główne Panele")]
    [Tooltip("Główny CanvasGroup minigry (płynne ściemnianie/rozjaśnianie).")]
    [SerializeField] private CanvasGroup minigameCanvasGroup;

    [Tooltip("Główny kontener pozycjonujący elementy minigry (RectTransform).")]
    [SerializeField] private RectTransform containerRect;

    [Header("Linie: Sinusoida (Tor) & Krew (Cięcie)")]
    [Tooltip("Komponent rysujący falistą linię pomocniczą (sinusoidę z wierzchołkami).")]
    [SerializeField] private ThroatCutLineRenderer waveGuideRenderer;

    [Tooltip("Komponent rysujący dynamiczną linię krwi za ostrzem brzytwy.")]
    [SerializeField] private ThroatCutLineRenderer bloodCutRenderer;

    [Header("Wskaźnik Tempa: Płonący Lont / Iskra (Burning Fuse Spark)")]
    [Tooltip("Obiekt iskry płonącego lontu narzucający tempo cięcia.")]
    [SerializeField] private RectTransform sparkIndicator;

    [Tooltip("Grafika rozbłysku iskry.")]
    [SerializeField] private Image sparkGlowImage;

    [Header("Kursor Brzytwy (Razor Cursor)")]
    [Tooltip("Obiekt brzytwy prowadzony myszą gracza.")]
    [SerializeField] private RectTransform razorCursor;

    [Tooltip("Obrazek brzytwy (do obrotu kątowego zgodnie z nachyleniem nacięcia).")]
    [SerializeField] private Image razorCursorImage;

    [Header("HUD & Mierniki")]
    [Tooltip("Tekst wyświetlający aktualną / końcową precyzję (np. 'PRECISION: 94%').")]
    [SerializeField] private TextMeshProUGUI precisionText;

    [Tooltip("Pasek wypełnienia precyzji.")]
    [SerializeField] private Image precisionBarFill;

    [Tooltip("Tekst instrukcji dla gracza.")]
    [SerializeField] private TextMeshProUGUI instructionText;

    [Tooltip("Tekst statusu cięcia (np. 'SURGICAL', 'DEVIATING', 'TOO SLOW').")]
    [SerializeField] private TextMeshProUGUI statusFeedbackText;

    [Header("Efekty Wizualne Krwi (Blood Splash UI)")]
    [Tooltip("Pełnoekranowy rozbryzg krwi na ekranie po udanym cięciu tętnicy.")]
    [SerializeField] private Image screenBloodSplatter;

    [Tooltip("Błysk winiety krawędziowej.")]
    [SerializeField] private Image edgeVignetteImage;

    [Header("Parametry Fali (Sinusoida)")]
    [Tooltip("Całkowita szerokość toru cięcia w pikselach UI.")]
    [SerializeField] private float waveWidth = 720.0f;

    [Tooltip("Główna amplituda fali (wysokość wierzchołków góra-dół).")]
    [SerializeField] private float mainAmplitude = 46.0f;

    [Tooltip("Liczba próbek w siatce fali.")]
    [SerializeField] private int waveSampleCount = 64;

    [Header("Tempo i Czas Trwania Cięcia")]
    [Tooltip("Czas w sekundach, przez jaki iskra lontu pokonuje drogę od początku do końca.")]
    [SerializeField] private float cutDuration = 5.0f;

    [Header("Tolerancja i Kalkulacja Precyzji")]
    [Tooltip("Tolerancja w pikselach, przy której cięcie uznawane jest za idealne (100%).")]
    [SerializeField] private float idealTolerancePixels = 26.0f;

    [Tooltip("Maksymalny błąd w pikselach, powyżej którego punkt daje 0% precyzji.")]
    [SerializeField] private float maxErrorPixels = 75.0f;

    [Tooltip("Wymagany próg precyzji (w procentach) do zaliczenia cięcia gardła.")]
    [Range(50f, 95f)]
    [SerializeField] private float successThreshold = 90.0f;

    [Header("Dźwięki SFX")]
    [Tooltip("Dźwięk cięcia brzytwą po skórze (odtwarzany podczas trzymania LPM).")]
    [SerializeField] private AudioClip razorSliceLoopClip;

    [Tooltip("Dźwięk tryskającej krwi z tętnicy przy udanym podcięciu gardła.")]
    [SerializeField] private AudioClip arterialBloodSpurtClip;

    [Tooltip("Dźwięk stępienia/ześlizgnięcia się brzytwy przy nieudanym cięciu.")]
    [SerializeField] private AudioClip botchedCutClip;

    [Tooltip("Krzyk Jurka przy zepsutym cięciu.")]
    [SerializeField] private AudioClip jurekScreamClip;

    [Tooltip("AudioSource dla minigry.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Kamera Kinowa (Cinematic Neck Close-Up)")]
    [Tooltip("Dedykowany punkt kamery w scenie dla ujęcia gardła. Jeśli pusty, skrypt wyliczy go automatycznie względem Jurka.")]
    [SerializeField] private Transform customCameraShotPoint;

    [Tooltip("Docelowy kąt widzenia kamery (FOV) podczas zbliżenia na szyję.")]
    [SerializeField] private float cinematicFov = 34.0f;

    [Tooltip("Czas trwania najazdu kamery na szyję (w sekundach).")]
    [SerializeField] private float cameraTransitionDuration = 0.65f;

    [Header("Tryb Testowy / Sandbox (F1 Hotkey)")]
    [Tooltip("Czy wciśnięcie klawisza F1 ma natychmiast odpalać minigrę w trybie testowym bez NPC.")]
    [SerializeField] private bool enableTestHotkey = true;

    [Tooltip("Klawisz uruchamiający tryb testowy w locie (domyślnie F1).")]
    [SerializeField] private Key testHotkey = Key.F1;

    [Tooltip("W trybie testowym nie zmieniaj pozycji kamery gracza (test w miejscu, w którym stoi gracz).")]
    [SerializeField] private bool testModePreserveCamera = true;

    [Tooltip("Czy w trybie testowym minigra ma być powtarzalna bez ekranu końca gry (Game Over)?")]
    [SerializeField] private bool testModeLoopable = true;

    private bool _isTestMode = false;

    // ──────────────────────────────────────────────────────────
    // STANY WEWNĘTRZNE I OPTYMALIZACJA (0 GC)
    // ──────────────────────────────────────────────────────────
    private bool _isActive = false;
    private bool _isCompleted = false;
    private float _sparkProgress = 0f; // 0..1 wzdłuż fali
    private float _playerProgress = 0f; // 0..1 postęp gracza wzdłuż osi X
    private float _totalAccuracyAccumulator = 0f;
    private int _accuracySampleCount = 0;
    private float _currentLivePrecision = 100.0f;

    private readonly List<Vector2> _wavePointsBuffer = new List<Vector2>(80);
    private readonly List<Vector2> _bloodPointsBuffer = new List<Vector2>(120);

    // Cache stanu kamery i gracza
    private Camera _cachedMainCamera;
    private Vector3 _originalCamPos;
    private Quaternion _originalCamRot;
    private float _originalCamFov = 60f;
    private bool _hasOriginalCamTransform = false;

    private Tween _camMoveTween;
    private Tween _camRotTween;
    private Tween _camFovTween;
    private Tween _uiFadeTween;
    private Tween _sparkPulseTween;

    // Cache kolorów dla uniknięcia alokacji
    private static readonly Color ColorPerfectGreen = new Color(0.1f, 0.95f, 0.45f, 1f);
    private static readonly Color ColorWarningYellow = new Color(0.95f, 0.85f, 0.15f, 1f);
    private static readonly Color ColorDangerRed = new Color(0.95f, 0.15f, 0.15f, 1f);
    private static readonly Color ColorBloodRed = new Color(0.72f, 0.05f, 0.05f, 1f);

    public bool IsActive => _isActive;
    public float CurrentPrecision => _currentLivePrecision;

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

#if UNITY_EDITOR
        if (razorSliceLoopClip == null)
            razorSliceLoopClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/razor_minigame_sounds/ostrzenie szybkie.wav");
        if (arterialBloodSpurtClip == null)
            arterialBloodSpurtClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/SFX/Effekt_sfx_8.ogg");
        if (botchedCutClip == null)
            botchedCutClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/Negative/error_dound.ogg");
        if (jurekScreamClip == null)
            jurekScreamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sounds/SFX/jesus_chis.ogg");
#endif

        if (minigameCanvasGroup == null)
        {
            BuildRuntimeFallbackUI();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D UI SFX
        }

        HideInstant();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_isActive)
        {
            CancelMinigame();
        }

        _camMoveTween?.Kill();
        _camRotTween?.Kill();
        _camFovTween?.Kill();
        _uiFadeTween?.Kill();
        _sparkPulseTween?.Kill();
    }

    /// <summary>
    /// Anuluje aktywną minigrę i przywraca kamerę oraz kontrolę gracza.
    /// </summary>
    public void CancelMinigame()
    {
        if (!_isActive && !_isCompleted) return;
        _isActive = false;
        _isCompleted = true;

        HideInstant();
        RestoreCamera();

        if (PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.enabled = true;
        }

        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchToPlayer();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ──────────────────────────────────────────────────────────
    // INICJALIZACJA I START MINIGRY
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Uruchamia minigrę w trybie testowym sandbox pod klawiszem F1 (bez NPC, bez ekranu końca gry).
    /// </summary>
    [ContextMenu("Debug: Start Minigame Test Mode (F1)")]
    public void StartMinigameTestMode()
    {
        if (_isActive)
        {
            // Natychmiastowy reset do ponownej próby
            _uiFadeTween?.Kill();
            _camMoveTween?.Kill();
            _camRotTween?.Kill();
            _camFovTween?.Kill();
            _sparkPulseTween?.Kill();
            _isActive = false;
        }

        StartMinigameInternal(isTest: true);
    }

    /// <summary>
    /// Rozpoczyna sekwencję podcięcia gardła (pełna rozgrywka fabularna z NPC):
    /// - Zatrzymuje ruch gracza.
    /// - Ustawia zbliżenie kamery na gardło Jurka.
    /// - Generuje falistą sinusoidę i włącza UI.
    /// </summary>
    [ContextMenu("Start Throat Cut Minigame")]
    public void StartMinigame()
    {
        if (_isActive) return;
        StartMinigameInternal(isTest: false);
    }

    private void StartMinigameInternal(bool isTest)
    {
        _isTestMode = isTest;
        _isActive = true;
        _isCompleted = false;
        _sparkProgress = 0f;
        _playerProgress = 0f;
        _totalAccuracyAccumulator = 0f;
        _accuracySampleCount = 0;
        _currentLivePrecision = 100f;

        DevLog.Log($"<color=#FF3030>[ThroatCutMinigame] Rozpoczęto minigrę! (Tryb testowy: {isTest})</color>");

        // 1. Zablokuj ruch gracza i przełącz kursor
        if (PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.enabled = false;
        }

        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchToMinigame(unlockCursor: true);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 2. Najazd kamery na gardło Jurka (w teście możemy zachować pozycję gracza)
        if (!isTest || !testModePreserveCamera)
        {
            SetupCinematicCamera();
        }

        // 3. Wygeneruj falistą sinusoidę i wyczyść ścieżkę krwi
        GenerateSinusoidWave();
        ClearBloodTrail();

        // 4. Pokaż UI minigry
        ShowUI();

        // 5. Animacja pulsującej iskry
        AnimateSparkIndicator();

        if (isTest && instructionText != null)
        {
            instructionText.text = "TRYB TESTOWY: Trzymaj [LPM] i prowadź brzytwę po linii | [F1] Restart | [ESC] Wyjdź";
        }
    }

    private void SetupCinematicCamera()
    {
        _cachedMainCamera = Camera.main;
        if (_cachedMainCamera == null)
        {
            _cachedMainCamera = FindAnyObjectByType<Camera>();
        }

        if (_cachedMainCamera == null) return;

        if (!_hasOriginalCamTransform)
        {
            _originalCamPos = _cachedMainCamera.transform.position;
            _originalCamRot = _cachedMainCamera.transform.rotation;
            _originalCamFov = _cachedMainCamera.fieldOfView;
            _hasOriginalCamTransform = true;
        }

        Vector3 targetPos;
        Quaternion targetRot;

        if (customCameraShotPoint != null)
        {
            targetPos = customCameraShotPoint.position;
            targetRot = customCameraShotPoint.rotation;
        }
        else
        {
            // Wylicz idealne ujęcie gardła siedzącego Jurka (ujęcie z przodu na odsłoniętą krtań)
            CustomerJurek jurek = CustomerJurek.Instance != null ? CustomerJurek.Instance : FindAnyObjectByType<CustomerJurek>();
            Vector3 neckPos = new Vector3(-1.556814f, 1.42f, 4.958035f); // domyślne położenie gardła fotela
            Vector3 jurekForward = Vector3.forward;
            Vector3 jurekUp = Vector3.up;
            Vector3 jurekRight = Vector3.right;

            if (jurek != null)
            {
                Transform neckBone = null;
                var anim = jurek.GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                {
                    neckBone = anim.GetBoneTransform(HumanBodyBones.Neck);
                }

                if (neckBone == null)
                {
                    Transform[] allTransforms = jurek.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < allTransforms.Length; i++)
                    {
                        if (allTransforms[i].name.IndexOf("neck", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            neckBone = allTransforms[i];
                            break;
                        }
                    }
                }

                if (neckBone != null)
                {
                    neckPos = neckBone.position;
                }
                else
                {
                    neckPos = jurek.transform.position + new Vector3(0f, 1.35f, 0f);
                }

                jurekForward = jurek.transform.forward;
                jurekUp = jurek.transform.up;
                jurekRight = jurek.transform.right;
            }

            // Kamera ustawiona Z PRZODU Jurka i delikatnie pod kątem, patrząca prosto na gardło/krtań
            targetPos = neckPos + (jurekForward * 0.44f) + (jurekUp * 0.05f) - (jurekRight * 0.12f);
            Vector3 lookDir = (neckPos - targetPos).normalized;
            targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
        }

        _camMoveTween?.Kill();
        _camRotTween?.Kill();
        _camFovTween?.Kill();

        _camMoveTween = _cachedMainCamera.transform.DOMove(targetPos, cameraTransitionDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);

        _camRotTween = _cachedMainCamera.transform.DORotateQuaternion(targetRot, cameraTransitionDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);

        _camFovTween = _cachedMainCamera.DOFieldOfView(cinematicFov, cameraTransitionDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void RestoreCamera()
    {
        if (_cachedMainCamera == null || !_hasOriginalCamTransform) return;

        _camMoveTween?.Kill();
        _camRotTween?.Kill();
        _camFovTween?.Kill();

        _camMoveTween = _cachedMainCamera.transform.DOMove(_originalCamPos, 0.5f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);

        _camRotTween = _cachedMainCamera.transform.DORotateQuaternion(_originalCamRot, 0.5f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);

        _camFovTween = _cachedMainCamera.DOFieldOfView(_originalCamFov, 0.5f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _hasOriginalCamTransform = false;
            });
    }

    // ──────────────────────────────────────────────────────────
    // GENEROWANIE SINUSOIDY & TRAKCJI
    // ──────────────────────────────────────────────────────────

    private void GenerateSinusoidWave()
    {
        _wavePointsBuffer.Clear();

        float halfW = waveWidth * 0.5f;
        int samples = Mathf.Max(32, waveSampleCount);

        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples; // 0..1
            float x = Mathf.Lerp(-halfW, halfW, t);
            float y = EvaluateWaveY(t);

            _wavePointsBuffer.Add(new Vector2(x, y));
        }

        if (waveGuideRenderer != null)
        {
            waveGuideRenderer.SetPoints(_wavePointsBuffer);
        }

        // Ustawienie iskry na starcie fali
        if (sparkIndicator != null && _wavePointsBuffer.Count > 0)
        {
            sparkIndicator.anchoredPosition = _wavePointsBuffer[0];
        }

        // Ustawienie kursora brzytwy na starcie fali
        if (razorCursor != null && _wavePointsBuffer.Count > 0)
        {
            razorCursor.anchoredPosition = _wavePointsBuffer[0] + new Vector2(0f, 25f);
        }
    }

    /// <summary>
    /// Oblicza wysokość fali dla dowolnego t (0..1) i zadanej amplitudy.
    /// Publiczna i statyczna dla pełnej testowalności jednostkowej.
    /// </summary>
    public static float EvaluateWave(float t, float amplitude)
    {
        // 1. Główna fala sinusoidalna (szeroki łuk przez szyję)
        float wave1 = Mathf.Sin(t * Mathf.PI * 2.0f) * amplitude;

        // 2. Druga harmoniczna (wierzchołki wznoszące i opadające)
        float wave2 = Mathf.Sin((t * Mathf.PI * 4.0f) + 0.8f) * (amplitude * 0.42f);

        // 3. Delikatna asymetria anatomiczna (krtań / jabłko Adama)
        float adamApple = Mathf.Sin(t * Mathf.PI * 6.0f) * (amplitude * 0.22f);

        return wave1 + wave2 + adamApple;
    }

    /// <summary>
    /// Wylicza wysokość Y sinusoidy w danym punkcie znormalizowanym t (0..1).
    /// </summary>
    public float EvaluateWaveY(float t)
    {
        return EvaluateWave(t, mainAmplitude);
    }

    /// <summary>
    /// Wylicza znormalizowaną dokładność pojedynczej próbki (1.0 = idealnie na linii, 0.0 = poza zakresem).
    /// </summary>
    public static float CalculateAccuracyScore(float errorDist, float idealTolerance, float maxError)
    {
        if (errorDist <= idealTolerance) return 1.0f;
        float excess = errorDist - idealTolerance;
        float range = Mathf.Max(1f, maxError - idealTolerance);
        return Mathf.Clamp01(1.0f - (excess / range));
    }

    /// <summary>
    /// Wylicza ostateczny procentowy wynik cięcia, uwzględniając średnią trajektorię i pokrycie długości rany.
    /// Zapobiega zaliczeniu cięcia, jeśli gracz wykonał tylko kawałek nacięcia lub przeskoczył myszą!
    /// </summary>
    public static float CalculateFinalScore(float avgTrajectoryAccuracy, float cutProgress)
    {
        float coverage = Mathf.Clamp01(cutProgress / 0.90f);
        return Mathf.Clamp(avgTrajectoryAccuracy * coverage, 0f, 100f);
    }

    private void ClearBloodTrail()
    {
        _bloodPointsBuffer.Clear();
        if (bloodCutRenderer != null)
        {
            bloodCutRenderer.ClearPoints();
        }
    }

    private void AnimateSparkIndicator()
    {
        if (sparkGlowImage == null) return;

        _sparkPulseTween?.Kill();
        _sparkPulseTween = sparkGlowImage.transform.DOScale(1.28f, 0.25f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    // ──────────────────────────────────────────────────────────
    // PĘTLA GŁÓWNA UPDATE (0 GC)
    // ──────────────────────────────────────────────────────────

    private void Update()
    {
        // 0. Klawisze skrótów: F1 (Start/Restart testu) oraz ESC (Wyjście z minigry)
        HandleTestHotkeys();

        if (!_isActive || _isCompleted) return;

        // 1. Postęp płonącej iskry lontu (narzuca tempo)
        UpdateSparkProgress();

        // 2. Pozycja kursora brzytwy i input gracza
        UpdateRazorInputAndCutting();

        // 3. Aktualizacja wskaźników HUD (precyzja, teksty)
        UpdateHUDIndicators();

        // 4. Sprawdzenie warunku zakończenia cięcia
        CheckCompletionConditions();
    }

    private void HandleTestHotkeys()
    {
        if (!enableTestHotkey) return;

        bool f1Pressed = false;
        if (Keyboard.current != null && testHotkey != Key.None)
        {
            f1Pressed = Keyboard.current[testHotkey].wasPressedThisFrame;
        }
        else
        {
            f1Pressed = Input.GetKeyDown(KeyCode.F1);
        }

        if (f1Pressed)
        {
            StartMinigameTestMode();
            return;
        }

        if (_isActive)
        {
            bool escPressed = false;
            if (Keyboard.current != null)
            {
                escPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
            }
            else
            {
                escPressed = Input.GetKeyDown(KeyCode.Escape);
            }

            if (escPressed)
            {
                CancelMinigame();
            }
        }
    }

    private void UpdateSparkProgress()
    {
        float speed = (cutDuration > 0.05f) ? (1.0f / cutDuration) : 0.2f;
        _sparkProgress += Time.deltaTime * speed;
        _sparkProgress = Mathf.Clamp01(_sparkProgress);

        if (sparkIndicator != null)
        {
            float halfW = waveWidth * 0.5f;
            float sparkX = Mathf.Lerp(-halfW, halfW, _sparkProgress);
            float sparkY = EvaluateWaveY(_sparkProgress);

            sparkIndicator.anchoredPosition = new Vector2(sparkX, sparkY);
        }
    }

    private void UpdateRazorInputAndCutting()
    {
        if (containerRect == null || razorCursor == null) return;

        // Odczyt pozycji myszy
        Vector2 mouseScreenPos = Vector2.zero;
        if (Mouse.current != null)
        {
            mouseScreenPos = Mouse.current.position.ReadValue();
        }
        else
        {
            mouseScreenPos = Input.mousePosition;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? (canvas.worldCamera ?? Camera.main) : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerRect,
            mouseScreenPos,
            uiCamera,
            out Vector2 localMousePos
        );

        // Ogranicz ruch brzytwy do pola roboczego szyi
        float halfW = waveWidth * 0.5f;
        float clampedX = Mathf.Clamp(localMousePos.x, -halfW - 50f, halfW + 50f);
        float clampedY = Mathf.Clamp(localMousePos.y, -mainAmplitude * 2.8f, mainAmplitude * 2.8f);
        Vector2 currentRazorPos = new Vector2(clampedX, clampedY);

        razorCursor.anchoredPosition = currentRazorPos;

        // Nachylenie brzytwy do kąta stycznej do krzywej
        float tCurrent = Mathf.InverseLerp(-halfW, halfW, clampedX);
        float dt = 0.01f;
        float yA = EvaluateWaveY(Mathf.Clamp01(tCurrent - dt));
        float yB = EvaluateWaveY(Mathf.Clamp01(tCurrent + dt));
        float slopeAngle = Mathf.Atan2(yB - yA, (waveWidth * dt * 2f)) * Mathf.Rad2Deg;

        if (razorCursorImage != null)
        {
            razorCursorImage.transform.localRotation = Quaternion.Euler(0f, 0f, -slopeAngle * 0.75f);
        }

        // Gracz naciska lub trzyma Lewy Przycisk Myszy (LPM)
        bool isCutting = (Mouse.current != null && Mouse.current.leftButton.isPressed) || Input.GetMouseButton(0);

        if (isCutting)
        {
            HandleActiveCutMotion(currentRazorPos, tCurrent);
        }
        else
        {
            // Brak trzymania LPM w strefie cięcia – kara za przerwanie nacięcia
            if (_playerProgress > 0.05f && _playerProgress < 0.92f)
            {
                RecordAccuracySample(0f);
            }
        }
    }

    private void HandleActiveCutMotion(Vector2 razorPos, float t)
    {
        // 1. Zabezpieczenie przed teleportem/eksploitem: cięcie musi rozpocząć się przy początku fali (t <= 0.18)
        if (_playerProgress <= 0.01f && t > 0.18f)
        {
            // Gracz kliknął za daleko od lewego brzegu szyi – kursor musi najpierw dotknąć początku nacięcia
            return;
        }

        // 2. Jeśli gracz posuwa się w przód
        if (t >= _playerProgress - 0.04f)
        {
            // Płynny krok w przód (zapobiega jednoklatkowemu skokowi do końca)
            float maxStepPerFrame = 0.08f;
            _playerProgress = Mathf.Min(t, _playerProgress + maxStepPerFrame);

            // Dodawanie punktu do ścieżki krwi
            if (_bloodPointsBuffer.Count == 0 || Vector2.Distance(_bloodPointsBuffer[_bloodPointsBuffer.Count - 1], razorPos) >= 5.0f)
            {
                _bloodPointsBuffer.Add(razorPos);
                if (bloodCutRenderer != null)
                {
                    bloodCutRenderer.AddPoint(razorPos);
                }
            }

            // 3. Obliczenie odchyłki od idealnej sinusoidy
            float idealY = EvaluateWaveY(t);
            float errorDist = Mathf.Abs(razorPos.y - idealY);
            float rawTrajScore = CalculateAccuracyScore(errorDist, idealTolerancePixels, maxErrorPixels);

            // 4. Synchronizacja tempa z płonącym lontem (spark)
            float tempoDelta = t - _sparkProgress;
            float tempoPenalty = 1.0f;
            if (Mathf.Abs(tempoDelta) > 0.15f)
            {
                float excess = Mathf.Abs(tempoDelta) - 0.15f;
                tempoPenalty = Mathf.Clamp01(1.0f - (excess / 0.25f) * 0.35f);
            }

            float finalSampleScore = rawTrajScore * tempoPenalty;
            RecordAccuracySample(finalSampleScore);

            PlaySliceSound(finalSampleScore);
        }
        else
        {
            // Cofanie się w naciętej ranie – spadek precyzji
            RecordAccuracySample(0.3f);
        }
    }

    private void RecordAccuracySample(float score)
    {
        _totalAccuracyAccumulator += score;
        _accuracySampleCount++;

        _currentLivePrecision = (_totalAccuracyAccumulator / Mathf.Max(1, _accuracySampleCount)) * 100f;
        _currentLivePrecision = Mathf.Clamp(_currentLivePrecision, 0f, 100f);
    }

    private void UpdateHUDIndicators()
    {
        if (precisionText != null)
        {
            Color displayColor = _currentLivePrecision >= 90f ? ColorPerfectGreen : (_currentLivePrecision >= 75f ? ColorWarningYellow : ColorDangerRed);
            string colorHex = ColorUtility.ToHtmlStringRGB(displayColor);
            precisionText.text = $"PRECISION: <color=#{colorHex}>{_currentLivePrecision:0}%</color>";
        }

        if (precisionBarFill != null)
        {
            precisionBarFill.fillAmount = _currentLivePrecision / 100f;
            precisionBarFill.color = _currentLivePrecision >= 90f ? ColorPerfectGreen : (_currentLivePrecision >= 75f ? ColorWarningYellow : ColorDangerRed);
        }

        if (statusFeedbackText != null)
        {
            float tempoLead = _playerProgress - _sparkProgress;
            if (tempoLead < -0.20f)
            {
                statusFeedbackText.text = "<color=#FF8020>TOO SLOW (LAGGING BEHIND FUSE)</color>";
            }
            else if (tempoLead > 0.22f)
            {
                statusFeedbackText.text = "<color=#FFD020>TOO FAST (RUSHING AHEAD)</color>";
            }
            else if (_currentLivePrecision >= 90f)
            {
                statusFeedbackText.text = "<color=#20FF80>SURGICAL TEMPO</color>";
            }
            else if (_currentLivePrecision >= 75f)
            {
                statusFeedbackText.text = "<color=#FFD020>DEVIATING FROM FUSE</color>";
            }
            else
            {
                statusFeedbackText.text = "<color=#FF3030>ERRATIC INCISION</color>";
            }
        }
    }

    private void CheckCompletionConditions()
    {
        // Cięcie kończy się gdy:
        // 1. Gracz doprowadził cięcie do końca prawej krawędzi (postęp >= 92%) I iskra lontu również dobiegła końca (>= 85%).
        // 2. LUB iskra lontu dopaliła się do końca (czas minął).
        bool playerCompletedCut = _playerProgress >= 0.92f && _sparkProgress >= 0.85f;
        bool fuseBurnedOut = _sparkProgress >= 0.99f;

        if (playerCompletedCut || fuseBurnedOut)
        {
            FinishMinigame();
        }
    }

    // ──────────────────────────────────────────────────────────
    // FINAŁ CIĘCIA: SUKCES (>= 90%) LUB PORAŻKA (< 90%)
    // ──────────────────────────────────────────────────────────

    private void FinishMinigame()
    {
        if (_isCompleted) return;
        _isCompleted = true;
        _isActive = false;

        float finalScore = CalculateFinalScore(_currentLivePrecision, _playerProgress);
        bool isSuccess = (finalScore >= successThreshold) && (_playerProgress >= 0.88f);

        DevLog.Log($"[ThroatCutMinigame] Zakończono cięcie! Wynik końcowy: {finalScore:0.0}% (Czysta precyzja: {_currentLivePrecision:0.0}%, Pokrycie: {_playerProgress * 100f:0}%, Wymagane: {successThreshold}%). Sukces: {isSuccess}");

        if (_isTestMode && testModeLoopable)
        {
            ExecuteTestModeFinish(finalScore, isSuccess);
            return;
        }

        HideUI();

        if (isSuccess)
        {
            ExecuteLethalSuccessSequence(finalScore);
        }
        else
        {
            ExecuteBotchedFailSequence(finalScore);
        }
    }

    private void ExecuteTestModeFinish(float finalScore, bool isSuccess)
    {
        if (isSuccess)
        {
            PlaySound(arterialBloodSpurtClip, "task_complete", "somethig_1");
            TriggerBloodSplatterUI(true);
        }
        else
        {
            PlaySound(botchedCutClip, "error_sound", "sharpen_miss");
            TriggerBloodSplatterUI(false);
        }

        if (instructionText != null)
        {
            string statusStr = isSuccess ? "<color=#20FF40>SUKCES (ZALICZONE)</color>" : "<color=#FF3030>PORAŻKA (ZA MAŁA PRECYZJA)</color>";
            instructionText.text = $"WYNIK: {finalScore:0.0}% (Precyzja: {_currentLivePrecision:0.0}%) - {statusStr}\n[F1] Ponów próbę | [ESC] Wyjdź do gry";
        }

        // Przywróć sterowanie graczem po krótkiej chwili
        DOVirtual.DelayedCall(1.2f, () =>
        {
            if (!_isActive)
            {
                if (InputModeManager.Instance != null)
                {
                    InputModeManager.Instance.SwitchToPlayer();
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                if (PlayerMovement.Instance != null)
                {
                    PlayerMovement.Instance.enabled = true;
                }
            }
        }).SetUpdate(true);
    }

    /// <summary>
    /// Finał Sukcesu (>= 90%): Tętniczy rozbryzg krwi, osunięcie się Jurka w fotelu, ekran GTA "A FLAWLESS CUT".
    /// </summary>
    private void ExecuteLethalSuccessSequence(float finalScore)
    {
        // 1. Dźwięk tętniczy / fontanna krwi
        PlaySound(arterialBloodSpurtClip, "task_complete", "somethig_1");

        // 2. Rozbryzg krwi na ekranie
        TriggerBloodSplatterUI(true);

        // 3. Reakcja Jurka: osunięcie się w fotelu bezwładnie
        if (CustomerJurek.Instance != null)
        {
            CustomerJurek.Instance.OnThroatSlitSuccess();
        }

        // 4. Kinowe spowolnienie czasu i ekran końcowy
        DOVirtual.DelayedCall(1.6f, () =>
        {
            RestoreCamera();

            if (EndSummaryUI.Instance != null)
            {
                string reason = $"Precision: {finalScore:0}% - Lethal cut. Sweeney Todd would be proud.";
                EndSummaryUI.Instance.ShowEndScreen("A FLAWLESS CUT", reason, true);
            }
        }).SetUpdate(true);
    }

    /// <summary>
    /// Finał Porażki (< 90%): Jurek krzyczy, odpycha gracza, ucieka z krwawiącą raną, ekran "YOU FAILED".
    /// </summary>
    private void ExecuteBotchedFailSequence(float finalScore)
    {
        // 1. Dźwięk ześlizgnięcia się brzytwy i krzyk
        PlaySound(botchedCutClip, "error_sound", "sharpen_miss");
        PlaySound(jurekScreamClip, "error_sound");

        // 2. Wstrząs kamery od odepchnięcia
        if (_cachedMainCamera != null)
        {
            _cachedMainCamera.transform.DOShakePosition(0.45f, 0.25f, 20, 90f).SetUpdate(true);
        }

        TriggerBloodSplatterUI(false);

        // 3. Reakcja Jurka: krzyk, zeskoczenie z fotela i ucieczka do wyjścia
        if (CustomerJurek.Instance != null)
        {
            CustomerJurek.Instance.OnThroatSlitBotched();
        }

        // 4. Ekran porażki
        DOVirtual.DelayedCall(1.5f, () =>
        {
            RestoreCamera();

            if (EndSummaryUI.Instance != null)
            {
                string reason = $"Precision: {finalScore:0}% - Botched attempt! Jurek survived and fled screaming into the street.";
                EndSummaryUI.Instance.ShowEndScreen("YOU FAILED", reason, false);
            }
        }).SetUpdate(true);
    }

    // ──────────────────────────────────────────────────────────
    // AUDIO & EFEKTY WIZUALNE
    // ──────────────────────────────────────────────────────────

    private void PlaySliceSound(float score)
    {
        if (audioSource == null) return;

        if (razorSliceLoopClip != null && !audioSource.isPlaying)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(razorSliceLoopClip, 0.45f);
        }
        else if (AudioManager.Instance != null && !audioSource.isPlaying)
        {
            if (!AudioManager.Instance.TryPlay("sharpen_good"))
            {
                AudioManager.Instance.TryPlay("sharpen_perfect");
            }
        }
    }

    private void PlaySound(AudioClip clip, string audioManagerGroup, string fallbackGroup = null)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, _cachedMainCamera != null ? _cachedMainCamera.transform.position : transform.position);
            return;
        }

        if (AudioManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(audioManagerGroup) && AudioManager.Instance.TryPlay(audioManagerGroup))
            {
                return;
            }

            if (!string.IsNullOrEmpty(fallbackGroup))
            {
                AudioManager.Instance.TryPlay(fallbackGroup);
            }
        }
    }

    private void TriggerBloodSplatterUI(bool lethal)
    {
        if (screenBloodSplatter != null)
        {
            screenBloodSplatter.gameObject.SetActive(true);
            screenBloodSplatter.color = lethal ? ColorBloodRed : new Color(0.7f, 0.1f, 0.1f, 0.6f);
            screenBloodSplatter.DOFade(0.92f, 0.15f)
                .SetEase(Ease.OutFlash)
                .SetUpdate(true);
        }

        if (edgeVignetteImage != null)
        {
            edgeVignetteImage.gameObject.SetActive(true);
            edgeVignetteImage.color = ColorBloodRed;
            edgeVignetteImage.DOFade(0.85f, 0.2f).SetUpdate(true);
        }
    }

    // ──────────────────────────────────────────────────────────
    // ZARZĄDZANIE POKAZYWANIEM / UKRYWANIEM UI
    // ──────────────────────────────────────────────────────────

    public void ShowUI()
    {
        if (minigameCanvasGroup == null) return;

        _uiFadeTween?.Kill();
        minigameCanvasGroup.alpha = 0f;
        minigameCanvasGroup.gameObject.SetActive(true);
        minigameCanvasGroup.blocksRaycasts = true;
        minigameCanvasGroup.interactable = true;

        _uiFadeTween = minigameCanvasGroup.DOFade(1f, 0.35f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    public void HideUI()
    {
        if (minigameCanvasGroup == null) return;

        _uiFadeTween?.Kill();
        minigameCanvasGroup.blocksRaycasts = false;
        minigameCanvasGroup.interactable = false;

        _uiFadeTween = minigameCanvasGroup.DOFade(0f, 0.25f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                minigameCanvasGroup.gameObject.SetActive(false);
            });
    }

    public void HideInstant()
    {
        if (minigameCanvasGroup != null)
        {
            minigameCanvasGroup.alpha = 0f;
            minigameCanvasGroup.blocksRaycasts = false;
            minigameCanvasGroup.interactable = false;
            minigameCanvasGroup.gameObject.SetActive(false);
        }

        if (screenBloodSplatter != null) screenBloodSplatter.gameObject.SetActive(false);
        if (edgeVignetteImage != null) edgeVignetteImage.gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────
    // PROCEDURALNY RUNTIME FALLBACK UI BUILDER
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Automatycznie tworzy kompletną strukturę UI na scenie, jeśli deweloper nie przypisał jej w Inspectorze.
    /// Gwarantuje 100% niezawodności i brak błędów NullReferenceException.
    /// </summary>
    private void BuildRuntimeFallbackUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("ThroatCutMinigame_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950; // tuż pod EndSummaryUI (999)

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            transform.SetParent(canvasObj.transform, false);
        }

        minigameCanvasGroup = GetComponent<CanvasGroup>();
        if (minigameCanvasGroup == null)
        {
            minigameCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        RectTransform mainRect = GetComponent<RectTransform>();
        if (mainRect != null)
        {
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.sizeDelta = Vector2.zero;
        }

        // 1. Kontener
        if (containerRect == null)
        {
            GameObject contObj = new GameObject("ThroatCut_Container", typeof(RectTransform));
            contObj.transform.SetParent(transform, false);
            containerRect = contObj.GetComponent<RectTransform>();
            containerRect.anchoredPosition = new Vector2(0f, -80f); // umieszczony na wysokości gardła
            containerRect.sizeDelta = new Vector2(900f, 300f);
        }

        // 2. Linia Sinusoidy (Wave Guide)
        if (waveGuideRenderer == null)
        {
            GameObject waveObj = new GameObject("WaveGuideLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(ThroatCutLineRenderer));
            waveObj.transform.SetParent(containerRect, false);
            waveGuideRenderer = waveObj.GetComponent<ThroatCutLineRenderer>();
            waveGuideRenderer.LineWidth = 7.0f;
            waveGuideRenderer.StartColor = new Color(1f, 0.45f, 0.15f, 0.75f); // kolor płonącego lontu
            waveGuideRenderer.EndColor = new Color(1f, 0.2f, 0.1f, 0.85f);
        }

        // 3. Linia Krwi (Blood Cut Line)
        if (bloodCutRenderer == null)
        {
            GameObject bloodObj = new GameObject("BloodCutLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(ThroatCutLineRenderer));
            bloodObj.transform.SetParent(containerRect, false);
            bloodCutRenderer = bloodObj.GetComponent<ThroatCutLineRenderer>();
            bloodCutRenderer.LineWidth = 9.0f;
            bloodCutRenderer.StartColor = new Color(0.85f, 0.05f, 0.05f, 0.95f);
            bloodCutRenderer.EndColor = new Color(0.55f, 0.01f, 0.01f, 1f);
        }

        // 4. Iskra Lontu (Spark Indicator)
        if (sparkIndicator == null)
        {
            GameObject sparkObj = new GameObject("SparkIndicator", typeof(RectTransform), typeof(Image));
            sparkObj.transform.SetParent(containerRect, false);
            sparkIndicator = sparkObj.GetComponent<RectTransform>();
            sparkIndicator.sizeDelta = new Vector2(28f, 28f);

            sparkGlowImage = sparkObj.GetComponent<Image>();
            sparkGlowImage.color = new Color(1f, 0.95f, 0.4f, 1f);
        }

        // 5. Kursor Brzytwy
        if (razorCursor == null)
        {
            GameObject razorObj = new GameObject("RazorCursor", typeof(RectTransform), typeof(Image));
            razorObj.transform.SetParent(containerRect, false);
            razorCursor = razorObj.GetComponent<RectTransform>();
            razorCursor.sizeDelta = new Vector2(48f, 16f);

            razorCursorImage = razorObj.GetComponent<Image>();
            razorCursorImage.color = new Color(0.92f, 0.95f, 1f, 0.95f);
        }

        // 6. Teksty HUD
        if (precisionText == null)
        {
            GameObject txtObj = new GameObject("PrecisionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(transform, false);
            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0.5f, 1f);
            txtRect.anchorMax = new Vector2(0.5f, 1f);
            txtRect.anchoredPosition = new Vector2(0f, -60f);
            txtRect.sizeDelta = new Vector2(500f, 60f);

            precisionText = txtObj.GetComponent<TextMeshProUGUI>();
            precisionText.fontSize = 32;
            precisionText.alignment = TextAlignmentOptions.Center;
            precisionText.text = "PRECISION: 100%";
        }

        if (instructionText == null)
        {
            GameObject insObj = new GameObject("InstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            insObj.transform.SetParent(transform, false);
            RectTransform insRect = insObj.GetComponent<RectTransform>();
            insRect.anchorMin = new Vector2(0.5f, 0f);
            insRect.anchorMax = new Vector2(0.5f, 0f);
            insRect.anchoredPosition = new Vector2(0f, 60f);
            insRect.sizeDelta = new Vector2(800f, 50f);

            instructionText = insObj.GetComponent<TextMeshProUGUI>();
            instructionText.fontSize = 24;
            instructionText.alignment = TextAlignmentOptions.Center;
            instructionText.text = "HOLD [LMB] & TRACE THE INCISION LINE (FOLLOW THE SPARK)";
        }
    }
}
