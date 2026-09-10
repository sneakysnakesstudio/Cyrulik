using DG.Tweening;
using UnityEngine;

/// <summary>
/// Główny manager gry. Obsługuje spawn gracza, FPS limit i sekcję Debug
/// do szybkiego testowania poszczególnych systemów bez ręcznego grzebania w scenie.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // ENUMS
    // ─────────────────────────────────────────────────────────────────────────

    public enum FpsLimit
    {
        FPS60     = 60,
        FPS120    = 120,
        FPS144    = 144,
        Unlimited = -1
    }

    public enum PlayerSpawnPosition
    {
        Position1,
        Position2,
        Position3
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INSPECTOR – PERFORMANCE
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Performance")]
    [SerializeField] private FpsLimit fpsLimit = FpsLimit.FPS60;

    // ─────────────────────────────────────────────────────────────────────────
    // INSPECTOR – PLAYER SPAWN
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Player Spawn")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform spawnPosition1;
    [SerializeField] private Transform spawnPosition2;
    [SerializeField] private Transform spawnPosition3;
    [SerializeField] private PlayerSpawnPosition selectedSpawnPosition = PlayerSpawnPosition.Position1;

    // ─────────────────────────────────────────────────────────────────────────
    // INSPECTOR – DEBUG / QUICK START
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Debug / Quick Start – Jurek")]
    [Tooltip("Czy na starcie gry natychmiast wywołać spawn Jurka (pomija czekanie na 17:00 lub klikanie F3)?")]
    [SerializeField] private bool autoSpawnJurekOnStart = true;
    [Tooltip("Opóźnienie w sekundach przed wywołaniem przyjścia Jurka na starcie.")]
    [SerializeField] private float autoSpawnJurekDelay = 0.5f;

    [Header("Debug / Clocks – Zegarki")]
    [Tooltip("Wyłącza wszystkie zegarki (zegar ścienny + zegarek na ręce) od startu. Zatrzymuje też SFX tykania.")]
    [SerializeField] private bool disableClocksOnStart = false;

    [Tooltip("Wyłącza TYLKO dźwięk tykania zegara (clock_tick SFX) od startu. Zegarki dalej chodzą wizualnie.")]
    [SerializeField] private bool disableClockSfxOnStart = false;

    [Tooltip("Wyłącza zegarek na nadgarstku gracza od startu (klawisz Q nie działa, zegarek niewidoczny).")]
    [SerializeField] private bool disableWristwatchOnStart = false;

    [Header("Debug / Movement – Poruszanie")]
    [Tooltip("Wyłącza ruch gracza od startu (do testów cutscen itp.).")]
    [SerializeField] private bool disablePlayerMovementOnStart = false;

    [Tooltip("Wyłącza head bobbing od startu.")]
    [SerializeField] private bool disableHeadBobbingOnStart = false;

    [Header("Debug / Audio – SFX & Music")]
    [Tooltip("Wycisza WSZYSTKIE SFX od startu (AudioManager.sfxSource volume = 0).")]
    [SerializeField] private bool muteSfxOnStart = false;

    [Tooltip("Wycisza muzykę w tle od startu.")]
    [SerializeField] private bool muteMusicOnStart = false;

    [Tooltip("Wycisza ambient od startu.")]
    [SerializeField] private bool muteAmbientOnStart = false;

    // ─────────────────────────────────────────────────────────────────────────
    // REFERENCJE (auto-pobierane przez singletons)
    // ─────────────────────────────────────────────────────────────────────────

    // Cache'owane żeby nie szukać w Update
    private PlayerMovement  _playerMovement;
    private HeadBobbing     _headBobbing;
    private WristwatchController _wristwatch;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => Instance = null;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetFpsLimit(fpsLimit);
        SpawnPlayer();
    }

    private void Start()
    {
        // Cache referencji
        _playerMovement = PlayerMovement.Instance ?? FindAnyObjectByType<PlayerMovement>();
        _headBobbing    = FindAnyObjectByType<HeadBobbing>();
        _wristwatch     = WristwatchController.Instance ?? FindAnyObjectByType<WristwatchController>();

        ApplyDebugSettings();

        if (autoSpawnJurekOnStart)
        {
            if (autoSpawnJurekDelay > 0.01f)
                DOVirtual.DelayedCall(autoSpawnJurekDelay, SpawnJurekImmediately)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            else
                SpawnJurekImmediately();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRYWATNE
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyDebugSettings()
    {
        // ── Zegarki ─────────────────────────────────────────────────────────
        if (disableClocksOnStart)
        {
            SetClocksEnabled(false);
        }
        else
        {
            if (disableClockSfxOnStart)
                SetClockSfxEnabled(false);

            if (disableWristwatchOnStart)
                SetWristwatchEnabled(false);
        }

        // ── Ruch ────────────────────────────────────────────────────────────
        if (disablePlayerMovementOnStart && _playerMovement != null)
            _playerMovement.enabled = false;

        if (disableHeadBobbingOnStart && _headBobbing != null)
            _headBobbing.enabled = false;

        // ── Audio ────────────────────────────────────────────────────────────
        if (muteSfxOnStart)      SetSfxMuted(true);
        if (muteMusicOnStart)    SetMusicMuted(true);
        if (muteAmbientOnStart)  SetAmbientMuted(true);
    }

    private void SpawnJurekImmediately()
    {
        CustomerJurek jurek = CustomerJurek.Instance
            ?? FindAnyObjectByType<CustomerJurek>(FindObjectsInactive.Include);

        if (jurek != null)
        {
            jurek.TriggerArrival();
            Debug.Log("<color=#70FF70>[GameManager] [Debug] Auto-spawn Jurka wywołany!</color>");
        }
    }

    private void SpawnPlayer()
    {
        if (player == null) { Debug.LogWarning("GameManager: Nie przypisano gracza!", this); return; }

        Transform spawn = GetSelectedSpawnPosition();
        if (spawn == null) { Debug.LogWarning($"GameManager: Brak {selectedSpawnPosition}!", this); return; }

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(spawn.position, spawn.rotation);
        if (cc != null) cc.enabled = true;
    }

    private Transform GetSelectedSpawnPosition() => selectedSpawnPosition switch
    {
        PlayerSpawnPosition.Position1 => spawnPosition1,
        PlayerSpawnPosition.Position2 => spawnPosition2,
        PlayerSpawnPosition.Position3 => spawnPosition3,
        _                             => spawnPosition1
    };

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLICZNE API – zegarki
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Wyłącz/włącz WSZYSTKIE zegarki: zegar ścienny + SFX + zegarek gracza.</summary>
    public void SetClocksEnabled(bool enabled)
    {
        SetClockSfxEnabled(enabled);
        SetWristwatchEnabled(enabled);

        if (WallClockSequence.Instance != null)
            WallClockSequence.Instance.SetSequenceEnabled(enabled);
    }

    /// <summary>Wyłącz/włącz TYLKO dźwięk tykania co sekundę (GameTimeController).</summary>
    public void SetClockSfxEnabled(bool enabled)
    {
        if (GameTimeController.Instance != null)
            GameTimeController.Instance.SetTickingEnabled(enabled);
    }

    /// <summary>Wyłącz/włącz zegarek na nadgarstku gracza (klawisz Q).</summary>
    public void SetWristwatchEnabled(bool enabled)
    {
        _wristwatch ??= WristwatchController.Instance;
        if (_wristwatch != null)
            _wristwatch.enabled = enabled;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLICZNE API – ruch
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Wyłącz/włącz ruch gracza.</summary>
    public void SetPlayerMovementEnabled(bool enabled)
    {
        _playerMovement ??= PlayerMovement.Instance;
        if (_playerMovement != null)
            _playerMovement.enabled = enabled;
    }

    /// <summary>Wyłącz/włącz head bobbing.</summary>
    public void SetHeadBobbingEnabled(bool enabled)
    {
        if (_headBobbing != null)
            _headBobbing.enabled = enabled;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLICZNE API – audio
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Wycisz/odkryj SFX.</summary>
    public void SetSfxMuted(bool muted)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSfxMuted(muted);
    }

    /// <summary>Wycisz/odkryj muzykę.</summary>
    public void SetMusicMuted(bool muted)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicMuted(muted);
    }

    /// <summary>Wycisz/odkryj ambient.</summary>
    public void SetAmbientMuted(bool muted)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetAmbientMuted(muted);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLICZNE API – FPS
    // ─────────────────────────────────────────────────────────────────────────

    public void SetFpsLimit(FpsLimit newLimit)
    {
        fpsLimit = newLimit;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = (int)fpsLimit;
    }

    public void Set60Fps()  => SetFpsLimit(FpsLimit.FPS60);
    public void Set120Fps() => SetFpsLimit(FpsLimit.FPS120);
}