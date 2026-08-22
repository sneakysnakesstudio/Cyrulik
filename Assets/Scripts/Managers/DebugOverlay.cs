using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Profesjonalny panel debugowania (Dev Tools / Debug Overlay) z bocznym podglądem zadań (Quest Tracker).
/// Działa WYŁĄCZNIE w Edytorze Unity (#if UNITY_EDITOR). W finalnym buildzie gry jest całkowicie wycięty.
/// 
/// Skróty klawiszowe:
/// [F1] lub [~] (Tylda) -> Otwiera/Zamyka główny panel deweloperski
/// [F2]                  -> Przypina/Odpina stałą listę zadań po prawej stronie ekranu
/// </summary>
public class DebugOverlay : MonoBehaviour
{
#if !UNITY_EDITOR
    // W buildzie gry skrypt jest całkowicie nieaktywny
    private void Awake() => Destroy(this);
#else

    public static DebugOverlay Instance { get; private set; }

    [Header("Toggle Hotkeys")]
    [Tooltip("Klawisz otwierający/zamykający główny panel deweloperski.")]
    [SerializeField] private Key toggleKey = Key.F1;
    [SerializeField] private Key alternateToggleKey = Key.Backquote; // tylda ~

    [Tooltip("Klawisz przypinający listę questów po prawej stronie ekranu.")]
    [SerializeField] private Key pinQuestKey = Key.F2;

    [Header("UI Styling")]
    [SerializeField] private bool showOverlay = false;
    [SerializeField] private bool showCompactHud = true;
    [SerializeField] private bool pinQuestTracker = false;

    [MenuItem("Tools/Cyrulik/Add Dev Debug Overlay to Scene", false, 50)]
    public static void AddDebugOverlayToScene()
    {
        DebugOverlay existing = FindAnyObjectByType<DebugOverlay>();
        if (existing == null)
        {
            GameObject go = new GameObject("DebugOverlay", typeof(DebugOverlay));
            Undo.RegisterCreatedObjectUndo(go, "Create Debug Overlay");
            Selection.activeGameObject = go;
            Debug.Log("[DebugOverlay] Pomyślnie dodano DebugOverlay do sceny! Otwieranie w grze klawiszem [F1] lub [~], Questy [F2].");
        }
        else
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[DebugOverlay] DebugOverlay już istnieje w scenie.");
        }
    }

    // Zakładki w menu debugowania
    private enum DebugTab
    {
        Teleport,
        Time,
        Razor,
        Quests,
        Player,
        Dialogues,
        Performance
    }

    private DebugTab _currentTab = DebugTab.Teleport;

    // Referencje do menedżerów
    private GameManager _gameManager;
    private GameTimeController _timeController;
    private PreparationStateManager _questManager;
    private PlayerMovement _playerMovement;
    private CharacterController _characterController;
    private Transform _playerTransform;

    // Zmienne debugowe
    private bool _superSpeedActive = false;
    private float _originalWalkSpeed = 5f;
    private float _originalSprintSpeed = 6f;
    private float _fpsAccumulator = 0f;
    private int _fpsFrames = 0;
    private float _currentFps = 60f;
    private float _fpsTimeLeft = 0.5f;

    // Custom checkpoint
    private Vector3 _customCheckpointPos;
    private Quaternion _customCheckpointRot;
    private bool _hasCustomCheckpoint = false;

    // GUI Style
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        // Sprawdź wciśnięcie klawisza F1 lub Tyldy ~ (Główny panel)
        if (Keyboard.current != null)
        {
            if (Keyboard.current[toggleKey].wasPressedThisFrame ||
                Keyboard.current[alternateToggleKey].wasPressedThisFrame)
            {
                ToggleOverlay();
            }

            // Klawisz F2 (Przypięcie questów po prawej)
            if (Keyboard.current[pinQuestKey].wasPressedThisFrame)
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

        if (showOverlay)
        {
            ResolveReferences();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (InputModeManager.Instance != null && InputModeManager.Instance.CurrentScheme == InputModeManager.ControlScheme.Player)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void ResolveReferences()
    {
        if (_gameManager == null) _gameManager = FindAnyObjectByType<GameManager>();
        if (_timeController == null) _timeController = GameTimeController.Instance ?? FindAnyObjectByType<GameTimeController>();
        if (_questManager == null) _questManager = PreparationStateManager.Instance ?? FindAnyObjectByType<PreparationStateManager>();
        if (_playerMovement == null) _playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (_playerMovement != null)
        {
            _playerTransform = _playerMovement.transform;
            _characterController = _playerMovement.GetComponent<CharacterController>();
        }
    }

    private void InitStyles()
    {
        if (_boxStyle != null) return;

        // Tło głównego okna
        _boxStyle = new GUIStyle(GUI.skin.box);
        Texture2D bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.10f, 0.96f));
        bgTex.Apply();
        _boxStyle.normal.background = bgTex;
        _boxStyle.padding = new RectOffset(16, 16, 16, 16);

        // Tło bocznego panelu zadań
        _panelBoxStyle = new GUIStyle(GUI.skin.box);
        Texture2D panelBgTex = new Texture2D(1, 1);
        panelBgTex.SetPixel(0, 0, new Color(0.09f, 0.10f, 0.12f, 0.94f));
        panelBgTex.Apply();
        _panelBoxStyle.normal.background = panelBgTex;
        _panelBoxStyle.padding = new RectOffset(14, 14, 14, 14);

        // Nagłówek
        _headerStyle = new GUIStyle(GUI.skin.label);
        _headerStyle.fontSize = 17;
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.normal.textColor = new Color(0.98f, 0.78f, 0.35f, 1f); // Złoto

        // Podnagłówek
        _subHeaderStyle = new GUIStyle(GUI.skin.label);
        _subHeaderStyle.fontSize = 14;
        _subHeaderStyle.fontStyle = FontStyle.Bold;
        _subHeaderStyle.normal.textColor = new Color(0.4f, 0.8f, 1f, 1f);

        // Zakładki
        _tabActiveStyle = new GUIStyle(GUI.skin.button);
        _tabActiveStyle.fontSize = 13;
        _tabActiveStyle.fontStyle = FontStyle.Bold;
        _tabActiveStyle.normal.textColor = Color.white;

        _tabInactiveStyle = new GUIStyle(GUI.skin.button);
        _tabInactiveStyle.fontSize = 12;
        _tabInactiveStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        // Przyciski
        _buttonStyle = new GUIStyle(GUI.skin.button);
        _buttonStyle.fontSize = 13;
        _buttonStyle.margin = new RectOffset(4, 4, 4, 4);

        _successButtonStyle = new GUIStyle(GUI.skin.button);
        _successButtonStyle.fontSize = 13;
        _successButtonStyle.fontStyle = FontStyle.Bold;
        _successButtonStyle.normal.textColor = new Color(0.4f, 1f, 0.4f, 1f);

        _dangerButtonStyle = new GUIStyle(GUI.skin.button);
        _dangerButtonStyle.fontSize = 13;
        _dangerButtonStyle.fontStyle = FontStyle.Bold;
        _dangerButtonStyle.normal.textColor = new Color(1f, 0.4f, 0.4f, 1f);

        // Etykiety stanu
        _statusLabelStyle = new GUIStyle(GUI.skin.label);
        _statusLabelStyle.fontSize = 12;
        _statusLabelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        // Karta zadania ukończonego
        _questCardCompletedStyle = new GUIStyle(GUI.skin.box);
        Texture2D compTex = new Texture2D(1, 1);
        compTex.SetPixel(0, 0, new Color(0.08f, 0.18f, 0.10f, 0.85f));
        compTex.Apply();
        _questCardCompletedStyle.normal.background = compTex;
        _questCardCompletedStyle.padding = new RectOffset(10, 10, 8, 8);
        _questCardCompletedStyle.margin = new RectOffset(2, 2, 3, 3);

        // Karta zadania nieukończonego
        _questCardPendingStyle = new GUIStyle(GUI.skin.box);
        Texture2D pendTex = new Texture2D(1, 1);
        pendTex.SetPixel(0, 0, new Color(0.16f, 0.10f, 0.10f, 0.85f));
        pendTex.Apply();
        _questCardPendingStyle.normal.background = pendTex;
        _questCardPendingStyle.padding = new RectOffset(10, 10, 8, 8);
        _questCardPendingStyle.margin = new RectOffset(2, 2, 3, 3);
    }

    private void OnGUI()
    {
        InitStyles();

        // 1. Mały pasek na górze ekranu (zawsze widoczny, gdy zamknięte)
        if (!showOverlay)
        {
            if (showCompactHud)
            {
                DrawCompactHud();
            }

            // Jeśli przypięto questy (F2), rysuj panel zadań po prawej
            if (pinQuestTracker)
            {
                Rect pinnedRect = new Rect(Screen.width - 420, 20, 400, Mathf.Min(600, Screen.height - 40));
                DrawQuestsPanelArea(pinnedRect, isPinnedMode: true);
            }
            return;
        }

        // 2. GŁÓWNY PANEL DEV TOOLS (Lewa strona)
        float mainWidth = 520f;
        float mainHeight = 610f;
        Rect mainRect = new Rect(20, 20, mainWidth, mainHeight);

        GUILayout.BeginArea(mainRect, _boxStyle);

        // Nagłówek i przycisk X
        GUILayout.BeginHorizontal();
        GUILayout.Label("🛠️ CYRULIK DEV TOOLS", _headerStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✕ Zamknij [F1]", GUILayout.Width(100), GUILayout.Height(28)))
        {
            ToggleOverlay();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Zakładki menu
        GUILayout.BeginHorizontal();
        DrawTabButton(DebugTab.Teleport, "📍 Teleport");
        DrawTabButton(DebugTab.Time, "⏰ Czas");
        DrawTabButton(DebugTab.Razor, "🪒 Brzytwa");
        DrawTabButton(DebugTab.Quests, "📋 Zadania");
        DrawTabButton(DebugTab.Player, "🏃 Gracz");
        DrawTabButton(DebugTab.Dialogues, "💬 Dialogi");
        DrawTabButton(DebugTab.Performance, "📊 FPS");
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Zawartość aktywnej zakładki w ScrollView
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case DebugTab.Teleport:
                DrawTeleportTab();
                break;
            case DebugTab.Time:
                DrawTimeTab();
                break;
            case DebugTab.Razor:
                DrawRazorMinigameTab();
                break;
            case DebugTab.Quests:
                DrawQuestsTab();
                break;
            case DebugTab.Player:
                DrawPlayerTab();
                break;
            case DebugTab.Dialogues:
                DrawDialoguesTab();
                break;
            case DebugTab.Performance:
                DrawPerformanceTab();
                break;
        }

        GUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"FPS: {_currentFps:0.0} | Mono RAM: {GC.GetTotalMemory(false) / (1024 * 1024):0.0} MB", _statusLabelStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("🔄 Odśwież referencje", GUILayout.Width(150)))
        {
            ResolveReferences();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();

        // 3. STAŁY PANEL ZADAŃ PO PRAWEJ STRONIE (Right Side Quest Tracker)
        Rect questPanelRect = new Rect(555, 20, 440, mainHeight);
        DrawQuestsPanelArea(questPanelRect, isPinnedMode: false);
    }

    // ──────────────────────────────────────────────────────────
    // 📋 BOCZNY PANEL WSZYSTKICH ZADAŃ (QUEST TRACKER)
    // ──────────────────────────────────────────────────────────
    private void DrawQuestsPanelArea(Rect rect, bool isPinnedMode)
    {
        GUILayout.BeginArea(rect, _panelBoxStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("📋 STATUS ZADAŃ (QUESTS)", _headerStyle);
        GUILayout.FlexibleSpace();

        string pinLabel = pinQuestTracker ? "📌 Odpięty [F2]" : "📌 Przypnij [F2]";
        if (GUILayout.Button(pinLabel, _tabInactiveStyle, GUILayout.Width(105), GUILayout.Height(24)))
        {
            pinQuestTracker = !pinQuestTracker;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        if (_questManager != null)
        {
            var tasks = _questManager.Tasks;
            int totalTasks = tasks != null ? tasks.Count : 0;
            int completedCount = 0;

            if (tasks != null)
            {
                foreach (var t in tasks)
                {
                    if (t.isCompleted) completedCount++;
                }
            }

            float percent = totalTasks > 0 ? ((float)completedCount / totalTasks) * 100f : 0f;

            // Pasek postępu
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Postęp przygotowań: <b>{completedCount} / {totalTasks} ({percent:0}%)</b>", _subHeaderStyle);
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Lista wszystkich zadań
            _questScrollPos = GUILayout.BeginScrollView(_questScrollPos);

            if (tasks != null && tasks.Count > 0)
            {
                for (int i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    bool done = task.isCompleted;

                    GUILayout.BeginVertical(done ? _questCardCompletedStyle : _questCardPendingStyle);

                    GUILayout.BeginHorizontal();
                    string statusIcon = done ? "✅" : "⏳";
                    string statusText = done ? "<color=#55FF55><b>ZALICZONE</b></color>" : "<color=#FF6666>DO ZROBIENIA</color>";

                    GUILayout.Label($"{statusIcon} <b>{task.displayName}</b>", GUILayout.ExpandWidth(true));
                    GUILayout.Label(statusText, GUILayout.Width(100));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"<color=#888888>ID: {task.taskId}</color>", _statusLabelStyle);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(done ? "Odznacz ❌" : "Zalicz ✅", done ? _dangerButtonStyle : _successButtonStyle, GUILayout.Width(90), GUILayout.Height(22)))
                    {
                        _questManager.SetTaskState(task.taskId, !done);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                }
            }
            else
            {
                GUILayout.Label("Brak zarejestrowanych zadań w PreparationStateManager.", _statusLabelStyle);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);

            // Przyciski masowe
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("✅ Zalicz wszystkie", _successButtonStyle, GUILayout.Height(28)))
            {
                if (tasks != null)
                {
                    foreach (var t in tasks) _questManager.SetTaskState(t.taskId, true);
                }
            }

            if (GUILayout.Button("❌ Zresetuj wszystkie", _dangerButtonStyle, GUILayout.Height(28)))
            {
                if (tasks != null)
                {
                    foreach (var t in tasks) _questManager.SetTaskState(t.taskId, false);
                }
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("⚠️ Brak PreparationStateManager w scenie.", _statusLabelStyle);
            if (GUILayout.Button("🔄 Szukaj ponownie", _buttonStyle))
            {
                ResolveReferences();
            }
        }

        GUILayout.EndArea();
    }

    // ──────────────────────────────────────────────────────────
    // MINI PASEK NA GÓRZE EKRANU
    // ──────────────────────────────────────────────────────────
    private void DrawCompactHud()
    {
        string timeStr = _timeController != null
            ? $"{_timeController.Hour:00}:{_timeController.Minute:00}:{_timeController.Second:00}"
            : "--:--:--";

        int completed = 0;
        int total = 0;
        if (_questManager != null && _questManager.Tasks != null)
        {
            total = _questManager.Tasks.Count;
            foreach (var t in _questManager.Tasks) if (t.isCompleted) completed++;
        }

        string hudText = $"[F1] Dev Tools | 🕒 {timeStr} | 📋 {completed}/{total} Zadania | ⚡ {_currentFps:0.0} FPS";

        GUIStyle miniStyle = new GUIStyle(GUI.skin.box);
        miniStyle.fontSize = 12;
        miniStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);

        if (GUI.Button(new Rect(10, 10, 320, 26), hudText, miniStyle))
        {
            ToggleOverlay();
        }
    }

    private void DrawTabButton(DebugTab tab, string title)
    {
        bool isActive = _currentTab == tab;
        if (GUILayout.Button(title, isActive ? _tabActiveStyle : _tabInactiveStyle, GUILayout.Height(28)))
        {
            _currentTab = tab;
        }
    }

    // ──────────────────────────────────────────────────────────
    // 📍 1. ZAKŁADKA TELEPORTACJI
    // ──────────────────────────────────────────────────────────
    private void DrawTeleportTab()
    {
        GUILayout.Label("📍 Teleportacja do pozycji w scenie", _subHeaderStyle);
        GUILayout.Space(6);

        if (_gameManager != null)
        {
            var spawn1Field = typeof(GameManager).GetField("spawnPosition1", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spawn2Field = typeof(GameManager).GetField("spawnPosition2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spawn3Field = typeof(GameManager).GetField("spawnPosition3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Transform spawn1 = spawn1Field?.GetValue(_gameManager) as Transform;
            Transform spawn2 = spawn2Field?.GetValue(_gameManager) as Transform;
            Transform spawn3 = spawn3Field?.GetValue(_gameManager) as Transform;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Pozycje z GameManager:", _statusLabelStyle);
            GUILayout.Space(4);

            if (GUILayout.Button("🛏️ Pozycja 1 (Sypialnia / Spawn 1)", _buttonStyle, GUILayout.Height(34)))
            {
                if (spawn1 != null) TeleportPlayer(spawn1.position, spawn1.rotation);
                else Debug.LogWarning("[DebugOverlay] spawnPosition1 nie jest przypisany w GameManager!");
            }

            if (GUILayout.Button("💈 Pozycja 2 (Salon fryzjerski / Spawn 2)", _buttonStyle, GUILayout.Height(34)))
            {
                if (spawn2 != null) TeleportPlayer(spawn2.position, spawn2.rotation);
                else Debug.LogWarning("[DebugOverlay] spawnPosition2 nie jest przypisany w GameManager!");
            }

            if (GUILayout.Button("🚪 Pozycja 3 (Korytarz / Wyjście / Spawn 3)", _buttonStyle, GUILayout.Height(34)))
            {
                if (spawn3 != null) TeleportPlayer(spawn3.position, spawn3.rotation);
                else Debug.LogWarning("[DebugOverlay] spawnPosition3 nie jest przypisany w GameManager!");
            }
            GUILayout.EndVertical();
        }
        else
        {
            GUILayout.Label("⚠️ Brak GameManager w scenie.", _statusLabelStyle);
        }

        GUILayout.Space(10);

        // Własny punkt kontrolny (Custom Checkpoint)
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Tymczasowy Checkpoint:", _statusLabelStyle);
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("💾 Zapisz tu Checkpoint", _buttonStyle, GUILayout.Height(30)))
        {
            if (_playerTransform != null)
            {
                _customCheckpointPos = _playerTransform.position;
                _customCheckpointRot = _playerTransform.rotation;
                _hasCustomCheckpoint = true;
            }
        }

        GUI.enabled = _hasCustomCheckpoint;
        if (GUILayout.Button("⚡ Wróć do Checkpointu", _successButtonStyle, GUILayout.Height(30)))
        {
            TeleportPlayer(_customCheckpointPos, _customCheckpointRot);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (_hasCustomCheckpoint)
        {
            GUILayout.Label($"Zapisano: {_customCheckpointPos:F1}", _statusLabelStyle);
        }
        GUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────
    // ⏰ 2. ZAKŁADKA CZASU (GAME CLOCK)
    // ──────────────────────────────────────────────────────────
    private void DrawTimeTab()
    {
        GUILayout.Label("⏰ Kontrola zegara gry (GameTimeController)", _subHeaderStyle);
        GUILayout.Space(6);

        if (_timeController != null)
        {
            string openingStatus = _timeController.OpeningTimeReached ? "🟢 OTWARTE (Po godzinie otwarcia)" : "🔴 ZAMKNIĘTE (Przed otwarciem)";

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Aktualny czas gry: {_timeController.Hour:00}:{_timeController.Minute:00}:{_timeController.Second:00}", _headerStyle);
            GUILayout.Label($"Status otwarcia: {openingStatus}", _statusLabelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // Szybkie ustawianie godzin
            GUILayout.Label("Ustaw konkretną godzinę:", _statusLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("16:57:00 (Start)", _buttonStyle, GUILayout.Height(30))) _timeController.SetTime(16, 57, 0);
            if (GUILayout.Button("16:59:45 (Zaraz otwarcie)", _buttonStyle, GUILayout.Height(30))) _timeController.SetTime(16, 59, 45);
            if (GUILayout.Button("17:00:00 (Otwarcie)", _buttonStyle, GUILayout.Height(30))) _timeController.SetTime(17, 0, 0);
            if (GUILayout.Button("18:00:00 (+1h)", _buttonStyle, GUILayout.Height(30))) _timeController.SetTime(18, 0, 0);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Zmiana o kroki
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⏪ -1 Godzina", _buttonStyle)) _timeController.SetTime(Mathf.Max(0, _timeController.Hour - 1), _timeController.Minute, _timeController.Second);
            if (GUILayout.Button("⏩ +1 Godzina", _buttonStyle)) _timeController.SetTime((_timeController.Hour + 1) % 24, _timeController.Minute, _timeController.Second);
            if (GUILayout.Button("⏩ +15 Minut", _buttonStyle))
            {
                int newMin = _timeController.Minute + 15;
                int newHour = (_timeController.Hour + newMin / 60) % 24;
                _timeController.SetTime(newHour, newMin % 60, _timeController.Second);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Pauza i skala czasu gry
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⏸️ Pauza", _buttonStyle)) _timeController.Pause();
            if (GUILayout.Button("▶️ Wznów", _successButtonStyle)) _timeController.Resume();
            if (GUILayout.Button("⚡ x5 Szybciej", _buttonStyle)) Time.timeScale = Time.timeScale == 5f ? 1f : 5f;
            if (GUILayout.Button("⚡ x10 Szybciej", _buttonStyle)) Time.timeScale = Time.timeScale == 10f ? 1f : 10f;
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("⚠️ Brak GameTimeController w scenie.", _statusLabelStyle);
        }
    }

    // ──────────────────────────────────────────────────────────
    // 🪒 3. ZAKŁADKA MINIGRY OSTRZENIA BRZYTWY
    // ──────────────────────────────────────────────────────────
    private void DrawRazorMinigameTab()
    {
        GUILayout.Label("🪒 Kontrola Minigry Ostrzenia Brzytwy", _subHeaderStyle);
        GUILayout.Space(6);

        RazorMinigame razor = FindAnyObjectByType<RazorMinigame>();

        if (razor != null)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Stan minigry: <b>{razor.CurrentStateName}</b>", _statusLabelStyle);
            GUILayout.Label($"Czy ukończona: <b>{(razor.IsCompleted ? "<color=#55FF55>TAK (Naostrzona)</color>" : "<color=#FF6666>NIE (Wymaga naostrzenia)</color>")}</b>", _statusLabelStyle);
            GUILayout.Label($"Aktualna ostrość: <b>{razor.CurrentSharpness:0.0}%</b>", _statusLabelStyle);
            GUILayout.Label($"Wymóg żyletki w rękach: <b>{(razor.RequireBladeItem ? "TAK (Wymaga żyletki)" : "NIE (Bypass - wolny start)")}</b>", _statusLabelStyle);
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // Główne przyciski
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Uruchamianie minigry:", _statusLabelStyle);
            GUILayout.Space(4);

            if (GUILayout.Button("🚀 ODPAL MINIGRĘ NATYCHMIAST (Bypass żyletki)", _successButtonStyle, GUILayout.Height(38)))
            {
                showOverlay = false;
                razor.ForceStartMinigame();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("📍 Teleportuj przed stół do ostrzenia", _buttonStyle, GUILayout.Height(30)))
            {
                Vector3 standPos = razor.transform.position - razor.transform.forward * 0.9f;
                TeleportPlayer(standPos, razor.transform.rotation);
            }
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // Wymuszenie stanu / Obejścia
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Wymuszenie stanu / Cheaty:", _statusLabelStyle);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🏆 Zalicz PERFECT (100% ostrości)", _successButtonStyle, GUILayout.Height(32)))
            {
                razor.ForceCompleteMinigame(100f);
            }

            if (GUILayout.Button("🔄 Zresetuj stan (Ostrz od nowa)", _dangerButtonStyle, GUILayout.Height(32)))
            {
                razor.ResetMinigameState();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            bool newRequireBlade = GUILayout.Toggle(razor.RequireBladeItem, " Wymagaj przyniesienia żyletki w rękach gracza", GUILayout.Height(24));
            if (newRequireBlade != razor.RequireBladeItem)
            {
                razor.RequireBladeItem = newRequireBlade;
            }
            GUILayout.EndVertical();
        }
        else
        {
            GUILayout.Label("⚠️ Nie znaleziono obiektu RazorMinigame w aktualnej scenie.", _statusLabelStyle);
            if (GUILayout.Button("🔄 Szukaj ponownie", _buttonStyle))
            {
                ResolveReferences();
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    // 📋 3. ZAKŁADKA ZADAŃ (PREPARATION TASKS)
    // ──────────────────────────────────────────────────────────
    private void DrawQuestsTab()
    {
        GUILayout.Label("📋 Podgląd zadań i masowe akcje", _subHeaderStyle);
        GUILayout.Space(6);

        GUILayout.Label("Wszystkie zadania są również stale widoczne i klikalne w panelu po prawej stronie 👉", _statusLabelStyle);
        GUILayout.Space(8);

        if (_questManager != null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("✅ ZALICZ WSZYSTKIE ZADANIA", _successButtonStyle, GUILayout.Height(36)))
            {
                if (_questManager.Tasks != null)
                {
                    foreach (var t in _questManager.Tasks) _questManager.SetTaskState(t.taskId, true);
                }
            }

            if (GUILayout.Button("❌ ZRESETUJ WSZYSTKIE ZADANIA", _dangerButtonStyle, GUILayout.Height(36)))
            {
                if (_questManager.Tasks != null)
                {
                    foreach (var t in _questManager.Tasks) _questManager.SetTaskState(t.taskId, false);
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    // ──────────────────────────────────────────────────────────
    // 🏃 4. ZAKŁADKA GRACZA (PLAYER & MOVEMENT)
    // ──────────────────────────────────────────────────────────
    private void DrawPlayerTab()
    {
        GUILayout.Label("🏃 Parametry gracza i ułatwienia", _subHeaderStyle);
        GUILayout.Space(6);

        if (_playerMovement != null)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // Super prędkość do testów
            string speedButtonText = _superSpeedActive ? "⚡ WYŁĄCZ SUPER SPEED (x3)" : "🚀 WŁĄCZ SUPER SPEED (x3)";
            if (GUILayout.Button(speedButtonText, _superSpeedActive ? _dangerButtonStyle : _successButtonStyle, GUILayout.Height(36)))
            {
                ToggleSuperSpeed();
            }

            GUILayout.Space(6);

            // Przełączanie schematów InputModeManager
            if (InputModeManager.Instance != null)
            {
                GUILayout.Label($"Aktualny schemat Input: {InputModeManager.Instance.CurrentScheme}", _statusLabelStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Tryb Player", _buttonStyle)) InputModeManager.Instance.SwitchToPlayer();
                if (GUILayout.Button("Tryb UI", _buttonStyle)) InputModeManager.Instance.SwitchToUI(unlockCursor: true);
                if (GUILayout.Button("Tryb Minigra", _buttonStyle)) InputModeManager.Instance.SwitchToMinigame();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            // Kursor
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔓 Odblokuj Kursor", _buttonStyle)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (GUILayout.Button("🔒 Zablokuj Kursor", _buttonStyle)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }
        else
        {
            GUILayout.Label("⚠️ Brak PlayerMovement w scenie.", _statusLabelStyle);
        }
    }

    private void ToggleSuperSpeed()
    {
        if (_playerMovement == null) return;

        var walkField = typeof(PlayerMovement).GetField("walkSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var sprintField = typeof(PlayerMovement).GetField("sprintSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (!_superSpeedActive)
        {
            _originalWalkSpeed = (float)(walkField?.GetValue(_playerMovement) ?? 5f);
            _originalSprintSpeed = (float)(sprintField?.GetValue(_playerMovement) ?? 6f);

            walkField?.SetValue(_playerMovement, _originalWalkSpeed * 3f);
            sprintField?.SetValue(_playerMovement, _originalSprintSpeed * 3f);
            _superSpeedActive = true;
        }
        else
        {
            walkField?.SetValue(_playerMovement, _originalWalkSpeed);
            sprintField?.SetValue(_playerMovement, _originalSprintSpeed);
            _superSpeedActive = false;
        }
    }

    // ──────────────────────────────────────────────────────────
    // 💬 5. ZAKŁADKA TESTOWANIA DIALOGÓW I MINIGIER
    // ──────────────────────────────────────────────────────────
    private void DrawDialoguesTab()
    {
        GUILayout.Label("💬 Testowanie Dialogów i Minigier", _subHeaderStyle);
        GUILayout.Space(6);

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Myśli wewnętrzne (InnerDialogueUI):", _statusLabelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("💭 Myśl: 'Muszę się ubrać...'", _buttonStyle))
        {
            DialogueManager.Instance?.ShowThought("I should get dressed first...");
        }
        if (GUILayout.Button("💭 Myśl: 'Drzwi są zamknięte.'", _buttonStyle))
        {
            DialogueManager.Instance?.ShowThought("Drzwi są zamknięte na klucz.");
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(6);

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Dialog z klientem (ClientDialogueUI):", _statusLabelStyle);
        if (GUILayout.Button("👤 Kwestia klienta: 'Dzień dobry, poproszę golenie.'", _buttonStyle, GUILayout.Height(30)))
        {
            DialogueManager.Instance?.ShowClientLine("Klient", "Dzień dobry panie majstrze, poproszę golenie brzytwą na gładko.");
        }
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // Minigra Brzytwy
        RazorMinigame razor = FindAnyObjectByType<RazorMinigame>();
        if (razor != null)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Minigra Ostrzenia Brzytwy:", _statusLabelStyle);
            if (GUILayout.Button("🪒 Uruchom Minigrę Brzytwy", _successButtonStyle, GUILayout.Height(32)))
            {
                razor.Interact();
            }
            GUILayout.EndVertical();
        }
    }

    // ──────────────────────────────────────────────────────────
    // 📊 6. ZAKŁADKA WYDAJNOŚCI (PERFORMANCE & MEMORY)
    // ──────────────────────────────────────────────────────────
    private void DrawPerformanceTab()
    {
        GUILayout.Label("📊 Statystyki wydajności i pamięci", _subHeaderStyle);
        GUILayout.Space(6);

        long totalMemory = GC.GetTotalMemory(false) / (1024 * 1024);

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label($"Klatkaż (FPS): {_currentFps:0.0} FPS ({1000f / Mathf.Max(1f, _currentFps):0.0} ms)", _headerStyle);
        GUILayout.Label($"Alokacja pamięci zarządzanej (GC Heap): ~{totalMemory} MB", _statusLabelStyle);
        GUILayout.Label($"Limit FPS w grze: {Application.targetFrameRate} FPS", _statusLabelStyle);
        GUILayout.Label($"VSync Count: {QualitySettings.vSyncCount}", _statusLabelStyle);
        GUILayout.EndVertical();

        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🧹 Wymuś GC.Collect()", _buttonStyle, GUILayout.Height(30)))
        {
            GC.Collect();
            Resources.UnloadUnusedAssets();
            Debug.Log("[DebugOverlay] Wyczyszczono pamięć (GC.Collect + UnloadUnusedAssets).");
        }

        if (GUILayout.Button("🗑️ Wyczyść Konsolę", _buttonStyle, GUILayout.Height(30)))
        {
#if UNITY_EDITOR
            var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            var clearMethod = logEntries?.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearMethod?.Invoke(null, null);
#endif
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Limity FPS
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Ustaw 60 FPS", _buttonStyle)) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; }
        if (GUILayout.Button("Ustaw 120 FPS", _buttonStyle)) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120; }
        if (GUILayout.Button("Bez limitu (-1)", _buttonStyle)) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
        GUILayout.EndHorizontal();
    }

    private void TeleportPlayer(Vector3 targetPos, Quaternion targetRot)
    {
        if (_playerTransform == null)
        {
            ResolveReferences();
        }

        if (_playerTransform == null)
        {
            Debug.LogWarning("[DebugOverlay] Nie znaleziono gracza do teleportacji!");
            return;
        }

        if (_characterController != null)
            _characterController.enabled = false;

        _playerTransform.SetPositionAndRotation(targetPos, targetRot);

        if (_characterController != null)
            _characterController.enabled = true;

        Debug.Log($"[DebugOverlay] Przeteleportowano gracza do: {targetPos}");
    }

#endif
}
