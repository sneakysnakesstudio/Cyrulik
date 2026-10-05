using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Pomocnik Git do automatycznego commitowania zmian w projekcie Cyrulik.
/// Uruchamia się automatycznie wraz z Unity ([InitializeOnLoad]).
/// Sprawdza w tle (co określony czas, domyślnie co 60 minut), czy nastąpiły większe zmiany w repozytorium.
/// Jeśli wykryje zmiany przekraczające zadany próg, automatycznie zapisuje assety i tworzy commit.
/// </summary>
[InitializeOnLoad]
public class GitCommitAssistant : EditorWindow
{
    // Klucze EditorPrefs
    private const string PREF_ENABLED = "Cyrulik_Git_Enabled";
    private const string PREF_AUTO_COMMIT = "Cyrulik_Git_AutoCommit";
    private const string PREF_INTERVAL_MIN = "Cyrulik_Git_IntervalMinutes";
    private const string PREF_MIN_FILES = "Cyrulik_Git_MinFilesThreshold";
    private const string PREF_AUTO_PUSH = "Cyrulik_Git_AutoPush";
    private const string PREF_SHOW_NOTIF = "Cyrulik_Git_ShowNotification";
    private const string PREF_LAST_CHECK_TICKS = "Cyrulik_Git_LastCheckTicks";

    // Konfiguracja domyślna
    public static bool IsEnabled = true;
    public static bool AutoCommitEnabled = true;
    public static int IntervalMinutes = 60;
    public static int MinFilesThreshold = 1;
    public static bool AutoPushEnabled = false;
    public static bool ShowNotifications = true;

    private static DateTime _lastCheckTime = DateTime.MinValue;
    private static bool _isChecking = false;
    private static string _cachedBranch = "";
    private static readonly List<string> _cachedModifiedFiles = new List<string>();
    private static readonly List<string> _cachedUntrackedFiles = new List<string>();
    private static readonly List<string> _cachedDeletedFiles = new List<string>();
    private static DateTime _lastStatusQuery = DateTime.MinValue;

    // UI state
    private string _manualCommitMessage = "";
    private Vector2 _scrollPosition;
    private bool _showFileListFoldout = true;
    private bool _showSettingsFoldout = true;

    static GitCommitAssistant()
    {
        LoadPreferences();
        EditorApplication.update -= BackgroundUpdate;
        EditorApplication.update += BackgroundUpdate;

        // Pierwsze ciche sprawdzenie gałęzi po załadowaniu edytora
        EditorApplication.delayCall += () =>
        {
            _cachedBranch = QueryCurrentBranch();
            Debug.Log($"<color=#70C0FF><b>[Git Commit Helper]</b></color> Uruchomiony pomyślnie. Branch: <b>{_cachedBranch}</b>, interwał: {IntervalMinutes} min, AutoCommit: {(AutoCommitEnabled ? "WŁ" : "WYŁ")}.");
        };
    }

    [MenuItem("Tools/Cyrulik/💾 Git Commit Helper", false, 1)]
    public static void OpenWindow()
    {
        var window = GetWindow<GitCommitAssistant>("Git Helper", true);
        window.minSize = new Vector2(380, 520);
        window.Show();
        RefreshStatusNow();
    }

    private static void LoadPreferences()
    {
        IsEnabled = EditorPrefs.GetBool(PREF_ENABLED, true);
        AutoCommitEnabled = EditorPrefs.GetBool(PREF_AUTO_COMMIT, true);
        IntervalMinutes = EditorPrefs.GetInt(PREF_INTERVAL_MIN, 60);
        MinFilesThreshold = EditorPrefs.GetInt(PREF_MIN_FILES, 1);
        AutoPushEnabled = EditorPrefs.GetBool(PREF_AUTO_PUSH, false);
        ShowNotifications = EditorPrefs.GetBool(PREF_SHOW_NOTIF, true);

        string ticksStr = EditorPrefs.GetString(PREF_LAST_CHECK_TICKS, "");
        if (long.TryParse(ticksStr, out long ticks) && ticks > 0)
        {
            _lastCheckTime = new DateTime(ticks, DateTimeKind.Utc);
        }
        else
        {
            _lastCheckTime = DateTime.UtcNow;
            SaveLastCheckTime();
        }
    }

    private static void SavePreferences()
    {
        EditorPrefs.SetBool(PREF_ENABLED, IsEnabled);
        EditorPrefs.SetBool(PREF_AUTO_COMMIT, AutoCommitEnabled);
        EditorPrefs.SetInt(PREF_INTERVAL_MIN, IntervalMinutes);
        EditorPrefs.SetInt(PREF_MIN_FILES, MinFilesThreshold);
        EditorPrefs.SetBool(PREF_AUTO_PUSH, AutoPushEnabled);
        EditorPrefs.SetBool(PREF_SHOW_NOTIF, ShowNotifications);
    }

    private static void SaveLastCheckTime()
    {
        EditorPrefs.SetString(PREF_LAST_CHECK_TICKS, _lastCheckTime.Ticks.ToString());
    }

    private static void BackgroundUpdate()
    {
        if (!IsEnabled) return;
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (_isChecking) return;

        double elapsedMinutes = (DateTime.UtcNow - _lastCheckTime).TotalMinutes;
        if (elapsedMinutes >= IntervalMinutes)
        {
            _lastCheckTime = DateTime.UtcNow;
            SaveLastCheckTime();
            PerformPeriodicCheck();
        }
    }

    private static void PerformPeriodicCheck()
    {
        _isChecking = true;
        try
        {
            RefreshStatusNow();

            int totalChanges = _cachedModifiedFiles.Count + _cachedUntrackedFiles.Count + _cachedDeletedFiles.Count;
            if (totalChanges >= MinFilesThreshold)
            {
                if (AutoCommitEnabled)
                {
                    string commitMsg = GenerateAutoCommitMessage(totalChanges);
                    bool success = ExecuteCommit(commitMsg);
                    if (success)
                    {
                        string notificationText = $"Git: Zapisano auto-commit ({totalChanges} zmian)!";
                        if (ShowNotifications)
                        {
                            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(notificationText));
                        }
                        Debug.Log($"<color=#55FF55><b>[Git Commit Helper]</b></color> Utworzono automatyczny commit: <i>\"{commitMsg}\"</i>.");

                        if (AutoPushEnabled)
                        {
                            ExecutePush();
                        }
                    }
                }
                else
                {
                    string alertMsg = $"Git Helper: Masz {totalChanges} niezacommitowanych zmian od ponad {IntervalMinutes} minut!";
                    if (ShowNotifications)
                    {
                        SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(alertMsg));
                    }
                    Debug.LogWarning($"<color=#FFAA00><b>[Git Commit Helper]</b></color> {alertMsg} Otwórz 'Tools -> Cyrulik -> Git Commit Helper' aby zacommitować.");
                }
            }
        }
        finally
        {
            _isChecking = false;
        }
    }

    public static void RefreshStatusNow()
    {
        _cachedBranch = QueryCurrentBranch();
        _cachedModifiedFiles.Clear();
        _cachedUntrackedFiles.Clear();
        _cachedDeletedFiles.Clear();

        if (RunGit("status --porcelain", out string output, out _))
        {
            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.Length < 3) continue;
                    string status = line.Substring(0, 2);
                    string filePath = line.Substring(3).Trim(' ', '\"');

                    if (status.Contains("?") || status.Contains("A"))
                        _cachedUntrackedFiles.Add(filePath);
                    else if (status.Contains("D"))
                        _cachedDeletedFiles.Add(filePath);
                    else
                        _cachedModifiedFiles.Add(filePath);
                }
            }
        }

        _lastStatusQuery = DateTime.UtcNow;
    }

    private static string QueryCurrentBranch()
    {
        if (RunGit("rev-parse --abbrev-ref HEAD", out string output, out _))
        {
            return output.Trim();
        }
        return "nieznany";
    }

    private static string GenerateAutoCommitMessage(int totalCount)
    {
        string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        var keyAreas = new HashSet<string>();

        void CollectAreas(List<string> list)
        {
            foreach (var file in list)
            {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".unity"))
                    keyAreas.Add(fileName);
                else if (file.Contains("Scripts/"))
                    keyAreas.Add("Scripts");
                else if (file.Contains("Prefabs/"))
                    keyAreas.Add("Prefabs");
                else if (file.Contains("Settings/"))
                    keyAreas.Add("Settings");
                else
                {
                    string dir = Path.GetDirectoryName(file)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(dir))
                    {
                        string[] parts = dir.Split('/');
                        keyAreas.Add(parts.Length > 1 ? parts[1] : parts[0]);
                    }
                }
            }
        }

        CollectAreas(_cachedModifiedFiles);
        CollectAreas(_cachedUntrackedFiles);
        CollectAreas(_cachedDeletedFiles);

        string areasStr = keyAreas.Count > 0 ? string.Join(", ", keyAreas) : "zmiany w projekcie";
        return $"Auto-backup [{timestamp}] - {totalCount} zmodyfikowanych ({areasStr})";
    }

    public static bool ExecuteCommit(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Debug.LogError("[Git Commit Helper] Wiadomość commita nie może być pusta!");
            return false;
        }

        // Zapisz assety w edytorze przed commitem
        AssetDatabase.SaveAssets();

        // 1. git add -A
        if (!RunGit("add -A", out _, out string addError))
        {
            Debug.LogError($"[Git Commit Helper] Błąd przy git add: {addError}");
            return false;
        }

        // 2. git commit -m "..."
        // Zabezpiecz cudzysłowy w wiadomości
        string escapedMsg = message.Replace("\"", "\\\"");
        if (!RunGit($"commit -m \"{escapedMsg}\"", out string commitOutput, out string commitError))
        {
            // Może brak zmian po git add
            if (commitOutput.Contains("nothing to commit") || commitError.Contains("nothing to commit"))
            {
                Debug.Log("[Git Commit Helper] Brak zmian do zatwierdzenia.");
                RefreshStatusNow();
                return false;
            }

            Debug.LogError($"[Git Commit Helper] Błąd przy git commit: {commitError}");
            return false;
        }

        RefreshStatusNow();
        return true;
    }

    public static bool ExecutePush()
    {
        Debug.Log("<color=#70C0FF><b>[Git Commit Helper]</b></color> Wysyłanie zmian na serwer (git push)...");
        if (RunGit("push", out string output, out string error))
        {
            Debug.Log($"<color=#55FF55><b>[Git Commit Helper]</b></color> Git push zakończony sukcesem:\n{output}");
            return true;
        }
        else
        {
            Debug.LogError($"[Git Commit Helper] Błąd podczas git push:\n{error}");
            return false;
        }
    }

    private static bool RunGit(string arguments, out string output, out string error)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = projectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    output = "";
                    error = "Nie udało się uruchomić procesu git.";
                    return false;
                }

                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit(15000);
                return process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            output = "";
            error = ex.Message;
            return false;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        // Header
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("Cyrulik — Git Commit Helper", EditorStyles.boldLabel);

            string statusText = IsEnabled
                ? (AutoCommitEnabled ? "Aktywny (Auto-Commit włączony)" : "Aktywny (Tylko przypomnienia)")
                : "Wyłączony";
            Color statusColor = IsEnabled ? new Color(0.3f, 1f, 0.4f) : new Color(0.8f, 0.8f, 0.8f);

            var prevColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label($"Status: {statusText}", EditorStyles.miniBoldLabel);
            GUI.color = prevColor;

            GUILayout.Label($"Aktualna gałąź: {_cachedBranch}", EditorStyles.label);

            if (IsEnabled)
            {
                double remainingSeconds = (IntervalMinutes * 60) - (DateTime.UtcNow - _lastCheckTime).TotalSeconds;
                if (remainingSeconds < 0) remainingSeconds = 0;
                TimeSpan remaining = TimeSpan.FromSeconds(remainingSeconds);
                string remainingStr = remaining.Hours > 0
                    ? $"{remaining.Hours}h {remaining.Minutes}m {remaining.Seconds}s"
                    : $"{remaining.Minutes}m {remaining.Seconds}s";

                GUILayout.Label($"Kolejne auto-sprawdzenie za: {remainingStr}", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space(4);

        // Przyciski akcji
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Odśwież status", GUILayout.Height(26)))
            {
                RefreshStatusNow();
            }

            if (GUILayout.Button("Sprawdź i zcommituj teraz", GUILayout.Height(26)))
            {
                PerformPeriodicCheck();
            }

            if (GUILayout.Button("Push", GUILayout.Width(60), GUILayout.Height(26)))
            {
                ExecutePush();
            }
        }

        EditorGUILayout.Space(6);

        // Sekcja ręcznego commitowania
        int totalChanges = _cachedModifiedFiles.Count + _cachedUntrackedFiles.Count + _cachedDeletedFiles.Count;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label($"Niezacommitowane zmiany: {totalChanges}", EditorStyles.boldLabel);

            _manualCommitMessage = EditorGUILayout.TextField("Wiadomość commita:", _manualCommitMessage);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = totalChanges > 0;
                if (GUILayout.Button("Zapisz assety i zcommituj", GUILayout.Height(28)))
                {
                    string msg = string.IsNullOrWhiteSpace(_manualCommitMessage)
                        ? GenerateAutoCommitMessage(totalChanges)
                        : _manualCommitMessage;

                    if (ExecuteCommit(msg))
                    {
                        _manualCommitMessage = "";
                        GUIUtility.keyboardControl = 0;
                    }
                }
                GUI.enabled = true;
            }
        }

        EditorGUILayout.Space(4);

        // Lista zmienionych plików
        _showFileListFoldout = EditorGUILayout.Foldout(_showFileListFoldout, $"Zmienione pliki ({totalChanges})", true);
        if (_showFileListFoldout)
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(140));
            if (totalChanges == 0)
            {
                GUILayout.Label("Czysto! Brak niezatwierdzonych zmian w repozytorium.", EditorStyles.miniLabel);
            }
            else
            {
                DrawFileListCategory("Zmodyfikowane", _cachedModifiedFiles, new Color(0.4f, 0.8f, 1f));
                DrawFileListCategory("Nowe / Nieśledzone", _cachedUntrackedFiles, new Color(0.4f, 1f, 0.4f));
                DrawFileListCategory("Usunięte", _cachedDeletedFiles, new Color(1f, 0.4f, 0.4f));
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(6);

        // Ustawienia
        _showSettingsFoldout = EditorGUILayout.Foldout(_showSettingsFoldout, "Ustawienia asystenta", true);
        if (_showSettingsFoldout)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();

                IsEnabled = EditorGUILayout.Toggle("Włącz asystenta", IsEnabled);
                AutoCommitEnabled = EditorGUILayout.Toggle("Automatyczny commit", AutoCommitEnabled);
                IntervalMinutes = EditorGUILayout.IntSlider("Interwał (minuty)", IntervalMinutes, 5, 240);
                MinFilesThreshold = EditorGUILayout.IntSlider("Min. liczba zmian", MinFilesThreshold, 1, 20);
                AutoPushEnabled = EditorGUILayout.Toggle("Auto-Push na serwer", AutoPushEnabled);
                ShowNotifications = EditorGUILayout.Toggle("Powiadomienia w SceneView", ShowNotifications);

                if (EditorGUI.EndChangeCheck())
                {
                    SavePreferences();
                }
            }
        }

        // Repaint dla płynnego zegara
        if (IsEnabled && (DateTime.UtcNow - _lastStatusQuery).TotalSeconds > 1)
        {
            Repaint();
        }
    }

    private void DrawFileListCategory(string title, List<string> files, Color labelColor)
    {
        if (files == null || files.Count == 0) return;

        var prevColor = GUI.color;
        GUI.color = labelColor;
        GUILayout.Label($"• {title} ({files.Count}):", EditorStyles.miniBoldLabel);
        GUI.color = prevColor;

        foreach (var file in files)
        {
            GUILayout.Label($"   {file}", EditorStyles.miniLabel);
        }
    }
}
