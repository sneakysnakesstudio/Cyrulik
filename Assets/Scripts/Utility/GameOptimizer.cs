using UnityEngine;

/// <summary>
/// Główny optymalizator gry — zarządza ustawieniami wydajności graficznej:
/// VSync, limit FPS, poziom jakości grafiki, anti-aliasing i włączanie lustra.
/// Wczytuje i zapisuje ustawienia do PlayerPrefs.
/// Udostępnia statyczne metody wywoływalne z dowolnego ekranu ustawień.
/// </summary>
public class GameOptimizer : MonoBehaviour
{
    // Klucze PlayerPrefs
    private const string KEY_VSYNC        = "Cyrulik_VSync";
    private const string KEY_FPS          = "Cyrulik_TargetFPS";
    private const string KEY_QUALITY      = "Cyrulik_QualityLevel";
    private const string KEY_MIRROR       = "Cyrulik_MirrorEnabled";
    private const string KEY_AA           = "Cyrulik_AntiAliasing";

    /// <summary>Wywoływane po zmianie dowolnego ustawienia graficznego.</summary>
    public static event System.Action OnSettingsChanged;

    [Tooltip("Domyślny poziom jakości dla słabszych maszyn: 0=Low, 1=Medium, 2=High.")]
    [SerializeField] private int defaultQualityLevel = 1;

    private static GameOptimizer _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoRunOptimizer()
    {
        if (_instance != null) return;

        GameObject optimizerObject = new GameObject("GameOptimizer_Auto");
        optimizerObject.AddComponent<GameOptimizer>();
        DontDestroyOnLoad(optimizerObject);
        Debug.Log("[GameOptimizer] Optymalizator uruchomiony — wczytywanie ustawień.");
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        ApplyAllSavedSettings();
    }

    private void ApplyAllSavedSettings()
    {
        // VSync
        int vsync = PlayerPrefs.GetInt(KEY_VSYNC, 1);
        QualitySettings.vSyncCount = vsync;

        // Target FPS
        int fps = PlayerPrefs.GetInt(KEY_FPS, 60);
        Application.targetFrameRate = fps;

        // Quality Level
        int defaultQual = defaultQualityLevel >= 0 ? defaultQualityLevel : 1;
        int quality = PlayerPrefs.GetInt(KEY_QUALITY, defaultQual);
        if (quality >= 0 && quality < QualitySettings.names.Length)
            QualitySettings.SetQualityLevel(quality, true);

        // Anti-aliasing (na poziomie QualitySettings)
        int aa = PlayerPrefs.GetInt(KEY_AA, 0);
        QualitySettings.antiAliasing = aa;

        Debug.Log($"[GameOptimizer] Ustawienia: VSync={vsync}, FPS={fps}, Quality={quality}, AA={aa}");
    }

    // ──────────────────────────────────────────────────────
    // PUBLICZNE STATYCZNE METODY do wywołania z UI
    // ──────────────────────────────────────────────────────

    /// <summary>Ustawia VSync. 0 = wyłączony, 1 = włączony.</summary>
    public static void SetVSync(int count)
    {
        count = Mathf.Clamp(count, 0, 4);
        QualitySettings.vSyncCount = count;
        PlayerPrefs.SetInt(KEY_VSYNC, count);
        PlayerPrefs.Save();
        Debug.Log($"[GameOptimizer] VSync = {count}");
        OnSettingsChanged?.Invoke();
    }

    /// <summary>Ustawia limit klatek na sekundę. -1 = bez limitu, 30/60/120 = typowe wartości.</summary>
    public static void SetTargetFPS(int fps)
    {
        Application.targetFrameRate = fps;
        PlayerPrefs.SetInt(KEY_FPS, fps);
        PlayerPrefs.Save();
        Debug.Log($"[GameOptimizer] Target FPS = {fps}");
        OnSettingsChanged?.Invoke();
    }

    /// <summary>Ustawia poziom jakości grafiki. 0=Low, 1=Medium, 2=High.</summary>
    public static void SetQualityLevel(int level)
    {
        if (level >= 0 && level < QualitySettings.names.Length)
        {
            QualitySettings.SetQualityLevel(level, true);
            PlayerPrefs.SetInt(KEY_QUALITY, level);
            PlayerPrefs.Save();
            Debug.Log($"[GameOptimizer] Quality Level = {QualitySettings.names[level]}");
            OnSettingsChanged?.Invoke();
        }
    }

    /// <summary>Włącza lub wyłącza renderowanie lustra przez MirrorOptimizer w scenie.</summary>
    public static void SetMirrorEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(KEY_MIRROR, enabled ? 1 : 0);
        PlayerPrefs.Save();

        // Znajdź MirrorOptimizer w scenie i zastosuj
        MirrorOptimizer[] mirrors = UnityEngine.Object.FindObjectsByType<MirrorOptimizer>(FindObjectsInactive.Include);
        foreach (var m in mirrors)
        {
            if (m != null)
                m.SetMirrorEnabled(enabled);
        }

        // Fallback: znajdź PlanarMirror bezpośrednio
        PlanarMirror[] directMirrors = UnityEngine.Object.FindObjectsByType<PlanarMirror>(FindObjectsInactive.Include);
        foreach (var m in directMirrors)
        {
            if (m != null)
                m.SetEnabled(enabled);
        }

        Debug.Log($"[GameOptimizer] Lustro: {(enabled ? "WŁĄCZONE" : "WYŁĄCZONE")}");
        OnSettingsChanged?.Invoke();
    }

    /// <summary>Ustawia poziom anti-aliasingu. 0=None, 2=2xMSAA, 4=4xMSAA, 8=8xMSAA.</summary>
    public static void SetAntiAliasing(int samples)
    {
        QualitySettings.antiAliasing = samples;
        PlayerPrefs.SetInt(KEY_AA, samples);
        PlayerPrefs.Save();
        Debug.Log($"[GameOptimizer] Anti-Aliasing = {samples}x");
        OnSettingsChanged?.Invoke();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
        OnSettingsChanged = null;
    }
#endif
}
