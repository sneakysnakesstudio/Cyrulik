using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Panel deweloperski (Dev Tools / Debug Overlay) do testowania gry.
/// Otwierany w grze klawiszem [~] (Tylda / Backquote) lub [F1].
/// Zoptymalizowany pod buildy (solidne tekstury GUI, brak emoji powodujących [□]).
/// </summary>
public class DebugOverlay : MonoBehaviour
{
    public static DebugOverlay Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Czy panel deweloperski ma być dostępny w skompilowanym buildzie gry.")]
    [SerializeField] private bool allowInBuild = true;

    [Header("Toggle Hotkeys")]
    [Tooltip("Klawisz otwierający/zamykający panel deweloperski (domyślnie Tylda ~).")]
    [SerializeField] private Key primaryToggleKey = Key.Backquote; // tylda ~
    [SerializeField] private Key secondaryToggleKey = Key.F1;

    [Tooltip("Klawisz przesuwający czas gry na przyjście Jurka (17:01:30).")]
    [SerializeField] private Key jurekSpawnTimeKey = Key.F3;

    [Tooltip("Klawisz przypinający listę questów po prawej stronie ekranu.")]
    [SerializeField] private Key pinQuestKey = Key.F4;

    [Header("UI State")]
    [SerializeField] private bool showOverlay = false;
    [SerializeField] private bool showCompactHud = false;
    [SerializeField] private bool pinQuestTracker = false;

#if UNITY_EDITOR
    [MenuItem("Tools/Cyrulik/Add Dev Debug Overlay to Scene", false, 50)]
    public static void AddDebugOverlayToScene()
    {
        DebugOverlay existing = FindAnyObjectByType<DebugOverlay>();
        if (existing == null)
        {
            GameObject go = new GameObject("DebugOverlay", typeof(DebugOverlay));
            Undo.RegisterCreatedObjectUndo(go, "Create Debug Overlay");
            Selection.activeGameObject = go;
            Debug.Log("[DebugOverlay] Pomyślnie dodano DebugOverlay do sceny! Otwieranie klawiszem [~] lub [F1].");
        }
        else
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[DebugOverlay] DebugOverlay już istnieje w scenie.");
        }
    }

    [MenuItem("Tools/Cyrulik/Fix & Optimize All UI Canvas Scalers", false, 40)]
    public static void FixAllSceneCanvasScalersMenu()
    {
        FixAllSceneCanvasScalers();
    }
#endif

    public static void FixAllSceneCanvasScalers()
    {
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        int fixedCount = 0;

        foreach (Canvas canvas in allCanvases)
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;

            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            }

            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            fixedCount++;
        }

        Debug.Log($"[CanvasScaler] Zoptymalizowano i dopasowano skalowanie ekranu (1920x1080) dla {fixedCount} Canvasów w scenie!");
    }

    public enum DebugTab
    {
        Quests,
        NPC,
        TowelAndStove,
        Inventory,
        Teleport,
        Time,
        Atmosphere,
        Razor,
        Dialogues,
        Cheats
    }

    private DebugTab _currentTab = DebugTab.Quests;

    // Referencje do menedżerów w scenie
    private GameManager _gameManager;
    private GameTimeController _timeController;
    private PreparationStateManager _questManager;
    private PlayerMovement _playerMovement;
    private PlayerHands _playerHands;
    private CharacterController _characterController;
    private Transform _playerTransform;
    private StoveController _stoveController;
    private RadioInteractable _radio;
    private CustomerJurek _customerJurek;

    // Zmienne pomocnicze
    private bool _superSpeedActive = false;
    private float _originalWalkSpeed = 5f;
    private float _originalSprintSpeed = 6f;
    private float _currentFps = 60f;
    private float _fpsAccumulator = 0f;
    private int _fpsFrames = 0;
    private float _fpsTimeLeft = 0.5f;

    // Custom checkpoint
    private Vector3 _customCheckpointPos;
    private Quaternion _customCheckpointRot;
    private bool _hasCustomCheckpoint = false;

    // Tekstury tła i krawędzi
    private Texture2D _darkBgTex;
    private Texture2D _panelBgTex;
    private Texture2D _tabActiveBgTex;
    private Texture2D _tabInactiveBgTex;
    private Texture2D _btnBgTex;
    private Texture2D _btnHoverBgTex;
    private Texture2D _successBgTex;
    private Texture2D _dangerBgTex;
    private Texture2D _questDoneBgTex;
    private Texture2D _questPendingBgTex;
    private Texture2D _goldBorderTex;

    // Style GUI
    private GUIStyle _boxStyle;
    private GUIStyle _panelBoxStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    private GUIStyle _tabActiveStyle;
    private GUIStyle _tabInactiveStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _successButtonStyle;
    private GUIStyle _dangerButtonStyle;
    private GUIStyle _statusLabelStyle;
    private GUIStyle _questCardCompletedStyle;
    private GUIStyle _questCardPendingStyle;
    private Vector2 _scrollPos;
    private Vector2 _questScrollPos;
    private bool _stylesInitialized = false;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void OnValidate()
    {
        if (jurekSpawnTimeKey == Key.F2) jurekSpawnTimeKey = Key.F3;
        if (pinQuestKey == Key.F2 || pinQuestKey == Key.F3) pinQuestKey = Key.F4;
    }

    private void Awake()
    {
        // Automatyczna migracja ze starych danych w scenie (uwolnienie klawisza F2)
        if (jurekSpawnTimeKey == Key.F2) jurekSpawnTimeKey = Key.F3;
        if (pinQuestKey == Key.F2 || pinQuestKey == Key.F3) pinQuestKey = Key.F4;

        if (!allowInBuild)
        {
#if !UNITY_EDITOR
            Destroy(gameObject);
            return;
#endif
        }

        if (Instance != null && Instance != this && Instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        // Sprawdź wciśnięcie klawiszy
        if (Keyboard.current != null)
        {
            if (Keyboard.current[primaryToggleKey].wasPressedThisFrame ||
                Keyboard.current[secondaryToggleKey].wasPressedThisFrame)
            {
                ToggleOverlay();
            }

            // Klawisz F3 (Nadejście Jurka)
            if (Keyboard.current[Key.F3].wasPressedThisFrame ||
                (jurekSpawnTimeKey != Key.F2 && Keyboard.current[jurekSpawnTimeKey].wasPressedThisFrame))
            {
                FastForwardToJurekSpawn();
            }

            if (pinQuestKey != Key.F2 && pinQuestKey != Key.F3 && Keyboard.current[pinQuestKey].wasPressedThisFrame)
            {
                pinQuestTracker = !pinQuestTracker;
            }
        }

        // Licznik FPS
        _fpsTimeLeft -= Time.unscaledDeltaTime;
        _fpsAccumulator += 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        _fpsFrames++;

        if (_fpsTimeLeft <= 0f)
        {
            _currentFps = _fpsAccumulator / _fpsFrames;
            _fpsTimeLeft = 0.5f;
            _fpsAccumulator = 0f;
            _fpsFrames = 0;
        }
    }

    public void ToggleOverlay()
    {
        showOverlay = !showOverlay;
        ResolveReferences();

        if (showOverlay)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (InnerDialogueUI.Instance == null || !InnerDialogueUI.Instance.IsDialogueActive)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    /// <summary>
    /// Przesuwa czas gry do momentu tuż przed pojawieniem się Jurka (17:01:30),
    /// a jeśli Jurek już był przywołany, resetuje go, aby sekwencja odegrała się na nowo.
    /// </summary>
    public void FastForwardToJurekSpawn()
    {
        if (_timeController == null || _customerJurek == null)
        {
            ResolveReferences();
        }

        // Jeśli Jurek już przybył lub uciekł, zresetuj go do stanu startowego
        if (_customerJurek != null && (_customerJurek.HasArrived || _customerJurek.HasLeft))
        {
            _customerJurek.ResetCustomerState();
        }

        if (_timeController != null)
        {
            _timeController.SetTime(17, 1, 30);
            Debug.Log("<color=#70FF70>[DebugOverlay] [F3] Przesunięto czas gry na 17:01:30! Jurek pojawi się za 3 sekundy (o 17:01:33).</color>");
        }
        else if (_customerJurek != null)
        {
            _customerJurek.TriggerArrival();
            Debug.Log("<color=#70FF70>[DebugOverlay] [F3] Brak zegara – wywołano natychmiastowe przyjście Jurka!</color>");
        }
    }

    private void ResolveReferences()
    {
        if (_gameManager == null) _gameManager = FindAnyObjectByType<GameManager>();
        if (_timeController == null) _timeController = FindAnyObjectByType<GameTimeController>();
        if (_questManager == null) _questManager = PreparationStateManager.Instance != null ? PreparationStateManager.Instance : FindAnyObjectByType<PreparationStateManager>();
        if (_playerMovement == null) _playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (_playerHands == null) _playerHands = FindAnyObjectByType<PlayerHands>();
        if (_stoveController == null) _stoveController = FindAnyObjectByType<StoveController>();
        if (_radio == null) _radio = FindAnyObjectByType<RadioInteractable>();
        if (_customerJurek == null) _customerJurek = FindAnyObjectByType<CustomerJurek>(FindObjectsInactive.Include);

        if (_playerMovement != null)
        {
            _characterController = _playerMovement.GetComponent<CharacterController>();
            _playerTransform = _playerMovement.transform;
        }
    }

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _darkBgTex = MakeTex(16, 16, new Color(0.08f, 0.08f, 0.10f, 0.97f));
        _panelBgTex = MakeTex(16, 16, new Color(0.14f, 0.14f, 0.16f, 0.95f));
        _tabActiveBgTex = MakeTex(16, 16, new Color(0.88f, 0.70f, 0.28f, 1f));
        _tabInactiveBgTex = MakeTex(16, 16, new Color(0.20f, 0.20f, 0.24f, 0.95f));
        _btnBgTex = MakeTex(16, 16, new Color(0.25f, 0.25f, 0.30f, 1f));
        _btnHoverBgTex = MakeTex(16, 16, new Color(0.35f, 0.35f, 0.42f, 1f));
        _successBgTex = MakeTex(16, 16, new Color(0.18f, 0.58f, 0.28f, 1f));
        _dangerBgTex = MakeTex(16, 16, new Color(0.72f, 0.22f, 0.22f, 1f));
        _questDoneBgTex = MakeTex(16, 16, new Color(0.12f, 0.38f, 0.18f, 0.95f));
        _questPendingBgTex = MakeTex(16, 16, new Color(0.24f, 0.20f, 0.16f, 0.95f));
        _goldBorderTex = MakeTex(16, 16, new Color(0.92f, 0.78f, 0.38f, 1f));

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(12, 12, 12, 12),
            normal = { background = _darkBgTex }
        };

        _panelBoxStyle = new GUIStyle(GUI.skin.box)
        {
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(12, 12, 12, 12),
            normal = { background = _panelBgTex }
        };

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.96f, 0.85f, 0.45f, 1f) },
            alignment = TextAnchor.MiddleLeft
        };

        _subHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 1f) }
        };

        _tabActiveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(0, 0, 0, 0),
            normal = { background = _tabActiveBgTex, textColor = Color.black },
            hover = { background = _tabActiveBgTex, textColor = Color.black },
            active = { background = _tabActiveBgTex, textColor = Color.black }
        };

        _tabInactiveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(0, 0, 0, 0),
            normal = { background = _tabInactiveBgTex, textColor = new Color(0.85f, 0.85f, 0.85f, 1f) },
            hover = { background = _btnHoverBgTex, textColor = Color.white },
            active = { background = _tabActiveBgTex, textColor = Color.black }
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(8, 8, 4, 4),
            normal = { background = _btnBgTex, textColor = Color.white },
            hover = { background = _btnHoverBgTex, textColor = Color.white },
            active = { background = _tabActiveBgTex, textColor = Color.black }
        };

        _successButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(8, 8, 4, 4),
            normal = { background = _successBgTex, textColor = Color.white },
            hover = { background = _successBgTex, textColor = Color.white },
            active = { background = _btnHoverBgTex, textColor = Color.white }
        };

        _dangerButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(8, 8, 4, 4),
            normal = { background = _dangerBgTex, textColor = Color.white },
            hover = { background = _dangerBgTex, textColor = Color.white },
            active = { background = _btnHoverBgTex, textColor = Color.white }
        };

        _statusLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.90f, 0.90f, 0.90f, 1f) }
        };

        _questCardCompletedStyle = new GUIStyle(GUI.skin.box)
        {
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(10, 10, 8, 8),
            margin = new RectOffset(0, 0, 3, 3),
            normal = { background = _questDoneBgTex }
        };

        _questCardPendingStyle = new GUIStyle(GUI.skin.box)
        {
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(10, 10, 8, 8),
            margin = new RectOffset(0, 0, 3, 3),
            normal = { background = _questPendingBgTex }
        };

        _stylesInitialized = true;
    }

    private const float ReferenceHeight = 1080f;

    private void OnGUI()
    {
        InitStyles();

        // 1. Skalowanie całego GUI na podstawie wysokości ekranu (Auto UI Scaling)
        float scale = Mathf.Clamp(Screen.height / ReferenceHeight, 0.65f, 2.5f);
        Matrix4x4 origMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        float scaledWidth = Screen.width / scale;
        float scaledHeight = Screen.height / scale;

        // 2. Pasek statusu
        if (showCompactHud && !showOverlay)
        {
            DrawCompactHud();
        }

        // 3. Przypięta lista zadań (F2)
        if (pinQuestTracker && !showOverlay)
        {
            DrawPinnedQuestTracker(scaledWidth, scaledHeight);
        }

        // 4. Główny panel
        if (showOverlay)
        {
            DrawMainOverlayWindow(scaledWidth, scaledHeight);
        }

        GUI.matrix = origMatrix;
    }

    private void DrawCompactHud()
    {
        string timeStr = _timeController != null ? $"{_timeController.Hour:00}:{_timeController.Minute:00}" : "--:--";
        int completed = 0;
        int total = 0;
        if (_questManager != null && _questManager.Tasks != null)
        {
            total = _questManager.Tasks.Count;
            foreach (var t in _questManager.Tasks) if (t != null && t.isCompleted) completed++;
        }

        string hudText = $"[~] DEV TOOLS | Czas: {timeStr} | Zadania: {completed}/{total} | FPS: {_currentFps:0.0}";

        Rect hudRect = new Rect(12, 12, 380, 30);
        GUI.DrawTexture(hudRect, _darkBgTex);
        GUI.DrawTexture(new Rect(12, 12, 380, 2), _goldBorderTex);

        if (GUI.Button(hudRect, hudText, _buttonStyle))
        {
            ToggleOverlay();
        }
    }

    private void DrawMainOverlayWindow(float screenW, float screenH)
    {
        float width = Mathf.Min(screenW * 0.90f, 960f);
        float height = Mathf.Min(screenH * 0.88f, 680f);
        float x = (screenW - width) * 0.5f;
        float y = (screenH - height) * 0.5f;

        // Solid background and border lines
        Rect fullWindowRect = new Rect(x, y, width, height);
        GUI.DrawTexture(fullWindowRect, _darkBgTex);
        GUI.DrawTexture(new Rect(x, y, width, 3), _goldBorderTex);
        GUI.DrawTexture(new Rect(x, y + height - 3, width, 3), _goldBorderTex);

        GUILayout.BeginArea(new Rect(x + 12, y + 10, width - 24, height - 20));

        // Header
        GUILayout.BeginHorizontal();
        GUILayout.Label("[~] CYRULIK — DEV TOOLS & DEBUG OVERLAY", _headerStyle);
        if (GUILayout.Button("[X] ZAMKNIJ [~]", _dangerButtonStyle, GUILayout.Width(120), GUILayout.Height(30)))
        {
            ToggleOverlay();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Zakładki menu
        GUILayout.BeginHorizontal();
        DrawTabButton(DebugTab.Quests, "Zadania");
        DrawTabButton(DebugTab.NPC, "NPC (Jurek)");
        DrawTabButton(DebugTab.TowelAndStove, "Piec / Recznik");
        DrawTabButton(DebugTab.Inventory, "Ekwipunek");
        DrawTabButton(DebugTab.Teleport, "Teleport");
        DrawTabButton(DebugTab.Time, "Czas");
        DrawTabButton(DebugTab.Atmosphere, "Klimat");
        DrawTabButton(DebugTab.Razor, "Brzytwa");
        DrawTabButton(DebugTab.Dialogues, "Dialogi");
        DrawTabButton(DebugTab.Cheats, "Cheaty");
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Zawartość aktywnej zakładki w panelu
        _scrollPos = GUILayout.BeginScrollView(_scrollPos, _panelBoxStyle);

        switch (_currentTab)
        {
            case DebugTab.Quests: DrawQuestsTab(); break;
            case DebugTab.NPC: DrawNPCTab(); break;
            case DebugTab.TowelAndStove: DrawTowelAndStoveTab(); break;
            case DebugTab.Inventory: DrawInventoryTab(); break;
            case DebugTab.Teleport: DrawTeleportTab(); break;
            case DebugTab.Time: DrawTimeTab(); break;
            case DebugTab.Atmosphere: DrawAtmosphereTab(); break;
            case DebugTab.Razor: DrawRazorTab(); break;
            case DebugTab.Dialogues: DrawDialoguesTab(); break;
            case DebugTab.Cheats: DrawCheatsTab(); break;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(6);

        // Pasek dolny
        GUILayout.BeginHorizontal();
        GUILayout.Label($"FPS: {_currentFps:0.0} | TimeScale: {Time.timeScale:0.0}x | [F3] Czas Jurka (17:01:30) | [F4] Zadania", _statusLabelStyle);
        if (GUILayout.Button("[R] Odswiez referencje", _buttonStyle, GUILayout.Width(170), GUILayout.Height(24)))
        {
            ResolveReferences();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void DrawTabButton(DebugTab tab, string title)
    {
        bool isActive = _currentTab == tab;
        if (GUILayout.Button(title, isActive ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(30)))
        {
            _currentTab = tab;
        }
    }

    // ──────────────────────────────────────────────────────────
    // 1. ZAKŁADKA ZADAŃ (QUESTS)
    // ──────────────────────────────────────────────────────────
    private void DrawQuestsTab()
    {
        GUILayout.Label("[ZARZADZANIE ZADANIAMI - PreparationStateManager]", _subHeaderStyle);
        GUILayout.Space(6);

        if (_questManager == null)
        {
            GUILayout.Label("[!] Brak PreparationStateManager w scenie!", _statusLabelStyle);
            return;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[+] ZALICZ WSZYSTKIE ZADANIA", _successButtonStyle, GUILayout.Height(34)))
        {
            if (_questManager.Tasks != null)
            {
                foreach (var t in _questManager.Tasks)
                {
                    if (t != null) _questManager.SetTaskState(t.taskId, true);
                }
            }
        }
        if (GUILayout.Button("[-] RESETUJ WSZYSTKIE ZADANIA", _dangerButtonStyle, GUILayout.Height(34)))
        {
            if (_questManager.Tasks != null)
            {
                foreach (var t in _questManager.Tasks)
                {
                    if (t != null) _questManager.SetTaskState(t.taskId, false);
                }
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (_questManager.Tasks != null)
        {
            foreach (var task in _questManager.Tasks)
            {
                if (task == null) continue;

                GUIStyle cardStyle = task.isCompleted ? _questCardCompletedStyle : _questCardPendingStyle;
                GUILayout.BeginHorizontal(cardStyle);

                string statusText = task.isCompleted ? "[ZROBIONE]" : "[W TOKU]";
                GUILayout.Label($"<b>{task.displayName}</b>\n<size=11><color=#C0C0C0>ID: {task.taskId}</color></size>", _statusLabelStyle, GUILayout.Width(360));
                GUILayout.Label(statusText, _statusLabelStyle, GUILayout.Width(110));

                if (task.isCompleted)
                {
                    if (GUILayout.Button("Cofnij", _dangerButtonStyle, GUILayout.Width(80), GUILayout.Height(28)))
                    {
                        _questManager.SetTaskState(task.taskId, false);
                    }
                }
                else
                {
                    if (GUILayout.Button("Zalicz", _successButtonStyle, GUILayout.Width(80), GUILayout.Height(28)))
                    {
                        _questManager.SetTaskState(task.taskId, true);
                    }
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    // 2. ZAKŁADKA NPC (JUREK)
    // ──────────────────────────────────────────────────────────
    private void DrawNPCTab()
    {
        GUILayout.Label("[ZARZADZANIE KLIENTEM - NPC JUREK]", _subHeaderStyle);
        GUILayout.Space(6);

        if (_customerJurek == null) ResolveReferences();

        if (_customerJurek == null)
        {
            GUILayout.BeginVertical(_panelBoxStyle);
            GUILayout.Label("[!] Nie znaleziono komponentu CustomerJurek w aktywnej scenie.", _statusLabelStyle);
            if (GUILayout.Button("[R] Wyszukaj ponownie w scenie", _buttonStyle, GUILayout.Height(30)))
            {
                ResolveReferences();
            }
            GUILayout.EndVertical();
            return;
        }

        // Status Card
        GUILayout.BeginVertical(_panelBoxStyle);
        GUILayout.Label($"Klient: <b>Jurek</b>", _headerStyle);
        GUILayout.Space(2);
        GUILayout.Label($"Status Przybycia (HasArrived): {(_customerJurek.HasArrived ? "<color=#70FF70>[TAK] Przybyl do salonu</color>" : "<color=#FFC040>[NIE] Czeka na wywolanie</color>")}", _statusLabelStyle);
        GUILayout.Label($"Status Marszu (IsWalking): {(_customerJurek.IsWalking ? "<color=#70D0FF>[TAK] W ruchu</color>" : "[NIE] Stoi w miejscu")}", _statusLabelStyle);
        GUILayout.Label($"Czekanie na gracza (Patience): {(_customerJurek.IsWaitingForPlayer ? $"<color=#FFFF00>[CZEKA] Pozostalo {_customerJurek.PatienceRemaining:0.0}s</color>" : "[NIE]")}", _statusLabelStyle);
        GUILayout.Label($"Przy fotelu (HasReachedChair): {(_customerJurek.HasReachedChair ? "<color=#70FF70>[TAK] Gotowy na stanowisku fryzjerskim</color>" : "[NIE]")}", _statusLabelStyle);
        GUILayout.Label($"Status Ucieczki (HasLeft): {(_customerJurek.HasLeft ? "<color=#FF5050>[TAK] Uciekl przed mysza</color>" : "[NIE] Obecny / Brak ucieczki")}", _statusLabelStyle);
        GUILayout.Label($"Interakcja z graczem (CanInteract): {(_customerJurek.CanInteract ? "<color=#70FF70>[TAK] Gotowy do rozmowy (wcisnij [E])</color>" : "[NIE]")}", _statusLabelStyle);
        GUILayout.EndVertical();

        GUILayout.Space(8);

        // Sekcja natychmiastowych przywołań
        GUILayout.Label("Szybkie przywolanie Jurka:", _subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[>] WYWOLAJ PRZYBYCIE (Auto -> Schody -> Drzwi -> Waiting Point)", _successButtonStyle, GUILayout.Height(36)))
        {
            _customerJurek.TriggerArrival();
        }
        if (GUILayout.Button("[>>] TELEPORTUJ DO WAITING POINTU", _tabActiveStyle, GUILayout.Height(36)))
        {
            _customerJurek.ForceSpawnInsideSalon();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[!] POSTAW PRZED DRZWIAMI (Szczyt schodow)", _buttonStyle, GUILayout.Height(32)))
        {
            _customerJurek.ForceSpawnAtDoor();
        }
        if (GUILayout.Button("[X] RESETUJ JURKA (Stan poczatkowy)", _dangerButtonStyle, GUILayout.Height(32)))
        {
            _customerJurek.ResetCustomerState();
        }
        GUILayout.EndHorizontal();

        if (_customerJurek.CanInteract || _customerJurek.IsWaitingForPlayer)
        {
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[E] ROZPOCZNIJ ROZMOWE (Symuluj interakcje [E])", _successButtonStyle, GUILayout.Height(32)))
            {
                _customerJurek.Interact();
            }
            if (GUILayout.Button("[>>] POSLIJ DO FOTELA (Pomin dialog)", _buttonStyle, GUILayout.Height(32)))
            {
                _customerJurek.WalkToBarberChair();
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // Drzwi i dzwonek
        GUILayout.Label("Interakcja z drzwiami i dzwonkiem wejsciowym:", _subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Zadzwon dzwonkiem w drzwiach", _buttonStyle, GUILayout.Height(30)))
        {
            _customerJurek.PlayDoorBellManual();
        }
        if (GUILayout.Button("Zapukaj do drzwi", _buttonStyle, GUILayout.Height(30)))
        {
            _customerJurek.PlayKnockManual();
        }
        DoorInteractable door = FindAnyObjectByType<DoorInteractable>();
        if (door != null)
        {
            if (GUILayout.Button(door.IsOpen ? "Zamknij Drzwi" : "Otworz Drzwi", _buttonStyle, GUILayout.Height(30)))
            {
                if (door.IsOpen) door.CloseDoor(); else { door.Unlock(); door.OpenDoor(); }
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Dialog i reakcje
        GUILayout.Label("Dialog i interakcja:", _subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Uruchom powitanie / dialog", _buttonStyle, GUILayout.Height(32)))
        {
            _customerJurek.Interact();
        }
        if (GUILayout.Button("Wymus paniczna ucieczke (Mysz)", _dangerButtonStyle, GUILayout.Height(32)))
        {
            _customerJurek.TriggerMouseScareAndLeave();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Ustawienie godziny w grze (17:01:33)
        if (_timeController != null)
        {
            GUILayout.Label("Synchronizacja czasu gry z przybyciem Jurka (17:01:33):", _subHeaderStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ustaw 17:01:30 (3s przed wejsciem)", _buttonStyle, GUILayout.Height(30)))
            {
                _timeController.SetTime(17, 1, 30);
            }
            if (GUILayout.Button("Ustaw 17:01:33 (Dokladny czas wejscia)", _buttonStyle, GUILayout.Height(30)))
            {
                _timeController.SetTime(17, 1, 33);
            }
            GUILayout.EndHorizontal();
        }
    }

    // ──────────────────────────────────────────────────────────
    // 2. ZAKŁADKA PIEC & GORĄCY RĘCZNIK
    // ──────────────────────────────────────────────────────────
    private void DrawTowelAndStoveTab()
    {
        GUILayout.Label("[PRZYGOTOWANIE GORACEGO RECZNIKA]", _subHeaderStyle);
        GUILayout.Space(6);

        if (_stoveController == null)
        {
            GUILayout.Label("[!] Brak StoveController w scenie!", _statusLabelStyle);
        }
        else
        {
            GUILayout.BeginVertical(_panelBoxStyle);
            if (_stoveController.StoveDoor != null)
            {
                GUILayout.Label($"Drzwiczki pieca: {(_stoveController.IsDoorOpen ? "[TAK] OTWARTE" : "[NIE] ZAMKNIĘTE")}", _statusLabelStyle);
            }
            GUILayout.Label($"Ogień w piecu: {(_stoveController.IsLit ? "[TAK] ROZPALONY" : "[NIE] WYGASZONY")}", _statusLabelStyle);
            GUILayout.Label($"Garnek na piecu: {(_stoveController.HasPot ? "[TAK] POSTAWIONY" : "[NIE] BRAK")}", _statusLabelStyle);
            GUILayout.Label($"Woda gotuje się: {(_stoveController.IsBoiling ? "[TAK] WRZACA" : "[NIE] ZIMNA")}", _statusLabelStyle);
            GUILayout.Label($"Ręcznik włożony: {(_stoveController.HasTowel ? "[TAK] WLOZONY" : "[NIE] BRAK")}", _statusLabelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(8);

            GUILayout.Label("Szybkie akcje pieca:", _subHeaderStyle);
            GUILayout.BeginHorizontal();
            if (_stoveController.StoveDoor != null)
            {
                if (GUILayout.Button(_stoveController.IsDoorOpen ? "Zamknij drzwiczki" : "Otwórz drzwiczki", _buttonStyle, GUILayout.Height(30)))
                {
                    _stoveController.ToggleDoor();
                }
            }
            if (GUILayout.Button("Rozpal ogien w piecu", _buttonStyle, GUILayout.Height(30)))
            {
                _stoveController.LightFire();
            }
            if (GUILayout.Button("Postaw garnek z woda", _buttonStyle, GUILayout.Height(30)))
            {
                _stoveController.PlacePot(true);
            }
            if (GUILayout.Button("Zagotuj wode natychmiast", _successButtonStyle, GUILayout.Height(30)))
            {
                _stoveController.InstantBoil();
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(12);

        GUILayout.Label("Spawnowanie przedmiotów do rąk gracza:", _subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Pusty garnek", _buttonStyle, GUILayout.Height(30))) GiveItemToPlayer("pot");
        if (GUILayout.Button("Garnek z woda", _buttonStyle, GUILayout.Height(30))) GiveItemToPlayer("pot_water");
        if (GUILayout.Button("Czysty recznik", _buttonStyle, GUILayout.Height(30))) GiveItemToPlayer("towel");
        if (GUILayout.Button("Goracy recznik", _successButtonStyle, GUILayout.Height(30))) GiveItemToPlayer("hot_towel");
        GUILayout.EndHorizontal();
    }

    // ──────────────────────────────────────────────────────────
    // 3. ZAKŁADKA EKWIPUNEK
    // ──────────────────────────────────────────────────────────
    private void DrawInventoryTab()
    {
        GUILayout.Label("[TRZYMANY PRZEDMIOT - PlayerHands]", _subHeaderStyle);
        GUILayout.Space(6);

        if (_playerHands != null)
        {
            string held = "--- PUSTE DLONIE ---";
            if (_playerHands.HeldItem != null)
            {
                var pickupComp = _playerHands.HeldItem.GetComponentInChildren<PickupItem>();
                held = pickupComp != null ? pickupComp.ItemId : _playerHands.HeldItem.name;
            }

            GUILayout.BeginVertical(_panelBoxStyle);
            GUILayout.Label($"Aktualnie w rekach: <b>{held}</b>", _headerStyle);
            if (_playerHands.HeldItem != null)
            {
                if (GUILayout.Button("[X] Upusc trzymany przedmiot", _dangerButtonStyle, GUILayout.Height(28)))
                {
                    _playerHands.DropHeldItem();
                }
            }
            GUILayout.EndVertical();
        }
        else
        {
            GUILayout.Label("[!] Brak PlayerHands na graczu!", _statusLabelStyle);
        }

        GUILayout.Space(10);
        GUILayout.Label("Szybkie dawanie przedmiotow:", _subHeaderStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Brzytwa (razor)", _buttonStyle, GUILayout.Height(30))) GiveItemToPlayer("razor");
        if (GUILayout.Button("Martwa mysz (dead_mouse)", _buttonStyle, GUILayout.Height(30))) GiveItemToPlayer("dead_mouse");
        if (GUILayout.Button("Zapalki (matchbox)", _buttonStyle, GUILayout.Height(30))) GiveItemToPlayer("matchbox");
        if (GUILayout.Button("Walizka (suitcase)", _buttonStyle, GUILayout.Height(30))) GiveItemToPlayer("suitcase");
        GUILayout.EndHorizontal();
    }

    // ──────────────────────────────────────────────────────────
    // 4. ZAKŁADKA TELEPORTACJI
    // ──────────────────────────────────────────────────────────
    private void DrawTeleportTab()
    {
        GUILayout.Label("[TELEPORTACJA DO MIEJSC W SALONIE]", _subHeaderStyle);
        GUILayout.Space(6);

        if (_gameManager != null)
        {
            var spawn1Field = typeof(GameManager).GetField("spawnPosition1", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spawn2Field = typeof(GameManager).GetField("spawnPosition2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spawn3Field = typeof(GameManager).GetField("spawnPosition3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Transform spawn1 = spawn1Field?.GetValue(_gameManager) as Transform;
            Transform spawn2 = spawn2Field?.GetValue(_gameManager) as Transform;
            Transform spawn3 = spawn3Field?.GetValue(_gameManager) as Transform;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sypialnia (Spawn 1)", _buttonStyle, GUILayout.Height(34)))
            {
                if (spawn1 != null) TeleportPlayer(spawn1.position, spawn1.rotation);
            }
            if (GUILayout.Button("Fotel fryzjerski (Spawn 2)", _buttonStyle, GUILayout.Height(34)))
            {
                if (spawn2 != null) TeleportPlayer(spawn2.position, spawn2.rotation);
            }
            if (GUILayout.Button("Drzwi / Korytarz (Spawn 3)", _buttonStyle, GUILayout.Height(34)))
            {
                if (spawn3 != null) TeleportPlayer(spawn3.position, spawn3.rotation);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(12);

        // Checkpoint
        GUILayout.BeginVertical(_panelBoxStyle);
        GUILayout.Label("Tymczasowy punkt kontrolny (Checkpoint):", _statusLabelStyle);
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[S] Zapisz tu Checkpoint", _buttonStyle, GUILayout.Height(30)))
        {
            if (_playerTransform != null)
            {
                _customCheckpointPos = _playerTransform.position;
                _customCheckpointRot = _playerTransform.rotation;
                _hasCustomCheckpoint = true;
            }
        }

        GUI.enabled = _hasCustomCheckpoint;
        if (GUILayout.Button("[>] Wroc do Checkpointu", _successButtonStyle, GUILayout.Height(30)))
        {
            TeleportPlayer(_customCheckpointPos, _customCheckpointRot);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (_hasCustomCheckpoint)
        {
            GUILayout.Label($"Zapisana pozycja: {_customCheckpointPos:F1}", _statusLabelStyle);
        }
        GUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────
    // 5. ZAKŁADKA CZASU (GAME CLOCK)
    // ──────────────────────────────────────────────────────────
    private void DrawTimeTab()
    {
        GUILayout.Label("[KONTROLA ZEGARA GRY - GameTimeController]", _subHeaderStyle);
        GUILayout.Space(6);

        if (_timeController != null)
        {
            string openingStatus = _timeController.OpeningTimeReached ? "[OTWARTE] Po godzinie otwarcia" : "[ZAMKNIETE] Przed otwarciem";

            GUILayout.BeginVertical(_panelBoxStyle);
            GUILayout.Label($"Aktualny czas gry: {_timeController.Hour:00}:{_timeController.Minute:00}:{_timeController.Second:00}", _headerStyle);
            GUILayout.Label($"Status salonu: {openingStatus}", _statusLabelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(8);

            GUILayout.Label("Ustaw konkretna godzine:", _statusLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("16:57:00 (Start)", _buttonStyle, GUILayout.Height(30))) _timeController.SetTime(16, 57, 0);
            if (GUILayout.Button("16:59:45 (Zaraz otwarcie)", _buttonStyle, GUILayout.Height(30))) _timeController.SetTime(16, 59, 45);
            if (GUILayout.Button("17:00:00 (Otwarcie)", _buttonStyle, GUILayout.Height(30))) _timeController.SetTime(17, 0, 0);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<< -1 Minuta", _buttonStyle))
            {
                int newMin = _timeController.Minute - 1;
                int newHour = newMin < 0 ? (_timeController.Hour - 1 + 24) % 24 : _timeController.Hour;
                _timeController.SetTime(newHour, (newMin + 60) % 60, _timeController.Second);
            }
            if (GUILayout.Button(">> +1 Minuta", _buttonStyle))
            {
                int newMin = _timeController.Minute + 1;
                int newHour = (_timeController.Hour + newMin / 60) % 24;
                _timeController.SetTime(newHour, newMin % 60, _timeController.Second);
            }
            if (GUILayout.Button(">> +5 Minut", _buttonStyle))
            {
                int newMin = _timeController.Minute + 5;
                int newHour = (_timeController.Hour + newMin / 60) % 24;
                _timeController.SetTime(newHour, newMin % 60, _timeController.Second);
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("[!] Brak GameTimeController w scenie.", _statusLabelStyle);
        }
    }

    // ──────────────────────────────────────────────────────────
    // 6. ZAKŁADKA KLIMATU (RADIO & ŚWIATŁA)
    // ──────────────────────────────────────────────────────────
    private void DrawAtmosphereTab()
    {
        GUILayout.Label("[KLIMAT SALONU - proper_atmosphere]", _subHeaderStyle);
        GUILayout.Space(6);

        if (_radio != null)
        {
            GUILayout.BeginVertical(_panelBoxStyle);
            GUILayout.Label($"Radio: {(_radio.IsOn ? "[WLACZONE] Gra muzyka" : "[WYLACZONE] Cisza")}", _headerStyle);
            if (GUILayout.Button(_radio.IsOn ? "Wylacz Radio" : "Wlacz Radio", _buttonStyle, GUILayout.Height(32)))
            {
                _radio.ToggleRadio();
            }
            GUILayout.EndVertical();
        }

        GUILayout.Space(10);
        GUILayout.Label("Przelaczniki swiatla w scenie:", _subHeaderStyle);
        if (GUILayout.Button("Wlacz wszystkie lampy w salonie", _buttonStyle, GUILayout.Height(32)))
        {
            LampSwitch[] lamps = FindObjectsByType<LampSwitch>(FindObjectsInactive.Include);
            foreach (var lamp in lamps)
            {
                if (lamp != null && !lamp.IsOn) lamp.Interact();
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    // 7. ZAKŁADKA OSTRZENIA BRZYTWY
    // ──────────────────────────────────────────────────────────
    private void DrawRazorTab()
    {
        GUILayout.Label("[MINIGRA OSTRZENIA BRZYTWY - razor_sharpened]", _subHeaderStyle);
        GUILayout.Space(6);

        RazorMinigame minigame = FindAnyObjectByType<RazorMinigame>();
        if (minigame != null)
        {
            GUILayout.BeginVertical(_panelBoxStyle);
            GUILayout.Label($"Aktualna ostrosc: {minigame.CurrentSharpness:0.0}%", _headerStyle);
            GUILayout.Label($"Status: {minigame.CurrentStateName} | Zaliczone: {(minigame.IsCompleted ? "[TAK]" : "[NIE]")}", _statusLabelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[+] Zaostrz brzytwe (100%)", _successButtonStyle, GUILayout.Height(34)))
            {
                minigame.ForceCompleteMinigame(100f);
            }
            if (GUILayout.Button("[>] Wymus start minigry", _buttonStyle, GUILayout.Height(34)))
            {
                minigame.ForceStartMinigame();
            }
            if (GUILayout.Button("[-] Resetuj minigre", _dangerButtonStyle, GUILayout.Height(34)))
            {
                minigame.ResetMinigameState();
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("[!] Brak RazorMinigame w scenie.", _statusLabelStyle);
        }
    }

    // ──────────────────────────────────────────────────────────
    // 8. ZAKŁADKA DIALOGÓW
    // ──────────────────────────────────────────────────────────
    private void DrawDialoguesTab()
    {
        GUILayout.Label("[SYSTEM DIALOGOW I MYSLI]", _subHeaderStyle);
        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[>>] Pomin aktywny dialog", _dangerButtonStyle, GUILayout.Height(32)))
        {
            if (InnerDialogueUI.Instance != null && InnerDialogueUI.Instance.IsDialogueActive)
            {
                InnerDialogueUI.Instance.HideAllInstant();
            }
            if (ClientDialogueUI.Instance != null && ClientDialogueUI.Instance.IsDialogueActive)
            {
                ClientDialogueUI.Instance.HideAllInstant();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("Testuj monolog wewnetrzny:", _statusLabelStyle);
        if (GUILayout.Button("Test: \"The razor is sharp enough now.\"", _buttonStyle, GUILayout.Height(30)))
        {
            if (InnerDialogueUI.Instance != null)
            {
                InnerDialogueUI.Instance.ShowMessage("The razor is sharp enough now. Time to get ready.");
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    // 9. ZAKŁADKA CHEATÓW & PRĘDKOŚCI
    // ──────────────────────────────────────────────────────────
    private void DrawCheatsTab()
    {
        GUILayout.Label("[MODYFIKACJE ROZGRYWKI & CHEATY]", _subHeaderStyle);
        GUILayout.Space(6);

        GUILayout.BeginVertical(_panelBoxStyle);
        GUILayout.Label($"Predkosc gracza (Super Speed): {(_superSpeedActive ? "[WLACZONE] (18 m/s)" : "[NORMALNA]")}", _statusLabelStyle);
        if (GUILayout.Button(_superSpeedActive ? "Wylacz Super Speed" : "Wlacz Super Speed (3x)", _buttonStyle, GUILayout.Height(32)))
        {
            ToggleSuperSpeed();
        }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.Label($"Predkosc uplywu czasu (TimeScale): {Time.timeScale:0.0}x", _statusLabelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0.5x (SlowMo)", _buttonStyle)) Time.timeScale = 0.5f;
        if (GUILayout.Button("1.0x (Normalny)", _buttonStyle)) Time.timeScale = 1.0f;
        if (GUILayout.Button("2.0x (Szybko)", _buttonStyle)) Time.timeScale = 2.0f;
        if (GUILayout.Button("5.0x (Super Szybko)", _successButtonStyle)) Time.timeScale = 5.0f;
        GUILayout.EndHorizontal();

        GUILayout.Space(12);

        GUILayout.Label("Optymalizacja UI i Skalowania:", _subHeaderStyle);
        if (GUILayout.Button("[+] Zoptymalizuj Canvasy w scenie (ScaleWithScreenSize 1920x1080)", _buttonStyle, GUILayout.Height(32)))
        {
            FixAllSceneCanvasScalers();
        }
    }

    private void DrawPinnedQuestTracker(float screenW, float screenH)
    {
        float width = 320f;
        float height = Mathf.Min(screenH * 0.45f, 380f);
        float x = screenW - width - 15f;
        float y = 15f;

        Rect trackerRect = new Rect(x, y, width, height);
        GUI.DrawTexture(trackerRect, _darkBgTex);
        GUI.DrawTexture(new Rect(x, y, width, 2), _goldBorderTex);

        GUILayout.BeginArea(new Rect(x + 10, y + 10, width - 20, height - 20));
        GUILayout.Label("[ZADANIA - F4]", _subHeaderStyle);
        GUILayout.Space(4);

        _questScrollPos = GUILayout.BeginScrollView(_questScrollPos);

        if (_questManager != null && _questManager.Tasks != null)
        {
            foreach (var task in _questManager.Tasks)
            {
                if (task == null) continue;
                string icon = task.isCompleted ? "[OK]" : "[..]";
                Color color = task.isCompleted ? new Color(0.45f, 0.95f, 0.5f) : new Color(0.95f, 0.80f, 0.45f);
                GUIStyle qStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = task.isCompleted ? FontStyle.Normal : FontStyle.Bold,
                    normal = { textColor = color }
                };
                GUILayout.Label($"{icon} {task.displayName}", qStyle);
            }
        }
        else
        {
            GUILayout.Label("Brak danych zadan.", _statusLabelStyle);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ──────────────────────────────────────────────────────────
    // POMOCNICZE METODY
    // ──────────────────────────────────────────────────────────

    private void TeleportPlayer(Vector3 targetPos, Quaternion targetRot)
    {
        if (_playerMovement == null) ResolveReferences();
        if (_playerMovement == null) return;

        if (_characterController != null) _characterController.enabled = false;
        _playerTransform.position = targetPos;
        _playerTransform.rotation = targetRot;
        if (_characterController != null) _characterController.enabled = true;

        Debug.Log($"[DebugOverlay] Przeteleportowano gracza na pozycje: {targetPos}");
    }

    private void GiveItemToPlayer(string itemId)
    {
        if (_playerHands == null) ResolveReferences();
        if (_playerHands == null) return;

        PickupItem[] allItems = FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
        foreach (var item in allItems)
        {
            if (item != null && item.ItemId == itemId)
            {
                _playerHands.TryHold(item.gameObject);
                Debug.Log($"[DebugOverlay] Dano graczowi przedmiot: {itemId}");
                return;
            }
        }

        Debug.LogWarning($"[DebugOverlay] Nie znaleziono w scenie przedmiotu o ItemId: {itemId}");
    }

    private void ToggleSuperSpeed()
    {
        if (_playerMovement == null) ResolveReferences();
        if (_playerMovement == null) return;

        var walkField = typeof(PlayerMovement).GetField("walkSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var sprintField = typeof(PlayerMovement).GetField("sprintSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (!_superSpeedActive)
        {
            if (walkField != null) _originalWalkSpeed = (float)walkField.GetValue(_playerMovement);
            if (sprintField != null) _originalSprintSpeed = (float)sprintField.GetValue(_playerMovement);

            walkField?.SetValue(_playerMovement, 14f);
            sprintField?.SetValue(_playerMovement, 20f);
            _superSpeedActive = true;
        }
        else
        {
            walkField?.SetValue(_playerMovement, _originalWalkSpeed);
            sprintField?.SetValue(_playerMovement, _originalSprintSpeed);
            _superSpeedActive = false;
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.SetPixels(pix);
        result.Apply();
        result.hideFlags = HideFlags.DontSave;
        return result;
    }
}
