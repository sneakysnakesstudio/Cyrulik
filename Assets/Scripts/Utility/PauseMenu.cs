using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menedżer menu pauzy (In-Game Menu) wywoływanego klawiszem ESC podczas rozgrywki.
/// Stylistycznie identyczny z MainMenuScene, z dodatkowym przyciskiem RESTART w prawym górnym rogu.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [Header("Scene Settings")]
    [Tooltip("Nazwa sceny menu głównego.")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Canvas & Root")]
    [SerializeField] private Canvas pauseCanvas;
    [SerializeField] private CanvasGroup pauseCanvasGroup;

    [Header("Panels")]
    [SerializeField] private CanvasGroup mainButtonsPanel;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private CanvasGroup creditsPanel;

    [Header("Top-Right Restart Button")]
    [SerializeField] private Button restartButton;

    [Header("Version Display")]
    [Tooltip("Tekst wersji wyświetlany w prawym dolnym rogu.")]
    [SerializeField] private TMP_Text versionLabel;
    [SerializeField] private string versionString = "PROTOTYPE VERSION 0.0.3";

    [Header("Settings Controls")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private string volumeParameter = "MasterVolume";

    [Header("Audio SFX (Opcjonalnie)")]
    [SerializeField] private string buttonClickSound = "button_click";
    [SerializeField] private string buttonHoverSound = "button_hover";

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.22f;

    [Header("Keybindings")]
    [SerializeField] private Key togglePauseKey = Key.Escape;

    private bool _isPaused = false;
    private CanvasGroup _currentSubPanel = null;
    private Tween _fadeTween;
    private InputModeManager.ControlScheme _previousScheme = InputModeManager.ControlScheme.Player;

    public bool IsPaused => _isPaused;

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

        AutoResolveReferences();

        if (versionLabel != null)
        {
            versionLabel.text = versionString;
        }

        InitPanelsImmediate();
        InitSettings();
        HideMenuImmediate();
    }

    private void AutoResolveReferences()
    {
        if (pauseCanvas == null)
        {
            pauseCanvas = GetComponentInParent<Canvas>();
            if (pauseCanvas == null)
            {
                var canvasGo = GameObject.Find("PauseMenu_Canvas");
                if (canvasGo != null) pauseCanvas = canvasGo.GetComponent<Canvas>();
            }
        }

        if (pauseCanvas != null)
        {
            pauseCanvas.overrideSorting = true;
            if (pauseCanvas.sortingOrder < 50)
            {
                pauseCanvas.sortingOrder = 50;
            }
        }

        if (pauseCanvasGroup == null)
        {
            pauseCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (mainButtonsPanel == null)
        {
            var p = transform.Find("MainButtons_Panel");
            if (p != null) mainButtonsPanel = p.GetComponent<CanvasGroup>();
        }

        if (settingsPanel == null)
        {
            var p = transform.Find("Settings_Panel");
            if (p != null) settingsPanel = p.GetComponent<CanvasGroup>();
        }

        if (creditsPanel == null)
        {
            var p = transform.Find("Credits_Panel");
            if (p != null) creditsPanel = p.GetComponent<CanvasGroup>();
        }

        if (restartButton == null)
        {
            var rb = transform.Find("Restart_Button");
            if (rb != null) restartButton = rb.GetComponent<Button>();
        }

        if (versionLabel == null)
        {
            var vl = transform.Find("Version_Label");
            if (vl != null) versionLabel = vl.GetComponent<TMP_Text>();
        }
    }

    private void Start()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        _fadeTween?.Kill();

        // Jeśli niszczymy scenę / obiekt w trakcie pauzy, przywracamy normalny czas
        if (_isPaused)
        {
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        // Sprawdź wciśnięcie klawisza ESC w nowym Input Systemie
        bool escPressed = false;
        if (Keyboard.current != null)
        {
            escPressed = Keyboard.current[togglePauseKey].wasPressedThisFrame;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!escPressed && Input.GetKeyDown(KeyCode.Escape))
        {
            escPressed = true;
        }
#endif

        if (escPressed)
        {
            OnEscapePressed();
        }
    }

    public void OnEscapePressed()
    {
        if (_isPaused)
        {
            // Jeśli otwarty jest panel ustawień lub credits, ESC wraca do głównego panelu pauzy
            if (_currentSubPanel != null)
            {
                if (_currentSubPanel == settingsPanel)
                {
                    CloseSettings();
                }
                else if (_currentSubPanel == creditsPanel)
                {
                    CloseCredits();
                }
                else
                {
                    SwitchPanel(_currentSubPanel, mainButtonsPanel);
                    _currentSubPanel = null;
                }
            }
            else
            {
                // Jeśli jesteśmy w głównym menu pauzy, ESC wznawia grę
                ResumeGame();
            }
        }
        else
        {
            PauseGame();
        }
    }

    // ---------------------------------------------------------
    // PAUSE & RESUME
    // ---------------------------------------------------------

    public void PauseGame()
    {
        if (_isPaused) return;
        _isPaused = true;

        Time.timeScale = 0f;

        // Zapamiętaj poprzedni schemat sterowania i przełącz na UI
        if (InputModeManager.Instance != null)
        {
            _previousScheme = InputModeManager.Instance.CurrentScheme;
            InputModeManager.Instance.SwitchToUI(unlockCursor: true);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Upewnij się, że kursor jest odblokowany i widoczny
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowMenu();
    }

    public void ResumeGame()
    {
        if (!_isPaused) return;
        _isPaused = false;

        PlayClickSound();
        Time.timeScale = 1f;

        // Przywróć poprzedni schemat sterowania
        if (InputModeManager.Instance != null)
        {
            if (_previousScheme == InputModeManager.ControlScheme.Player)
            {
                InputModeManager.Instance.SwitchToPlayer();
            }
            else if (_previousScheme == InputModeManager.ControlScheme.Minigame)
            {
                InputModeManager.Instance.SwitchToMinigame(true);
            }
            else
            {
                InputModeManager.Instance.SetControlScheme(_previousScheme, false);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HideMenu();
    }

    public void RestartGame()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        _isPaused = false;

        string currentScene = SceneManager.GetActiveScene().name;

        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.LoadScene(currentScene);
        }
        else
        {
            SceneManager.LoadScene(currentScene);
        }
    }

    public void ReturnToMainMenu()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        _isPaused = false;

        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.LoadScene(mainMenuSceneName);
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public void QuitGame()
    {
        PlayClickSound();
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------
    // SUB-PANELS (SETTINGS & CREDITS)
    // ---------------------------------------------------------

    public void OpenSettings()
    {
        PlayClickSound();
        _currentSubPanel = settingsPanel;
        SwitchPanel(mainButtonsPanel, settingsPanel);
    }

    public void CloseSettings()
    {
        PlayClickSound();
        _currentSubPanel = null;
        SwitchPanel(settingsPanel, mainButtonsPanel);
    }

    public void OpenCredits()
    {
        PlayClickSound();
        _currentSubPanel = creditsPanel;
        SwitchPanel(mainButtonsPanel, creditsPanel);
    }

    public void CloseCredits()
    {
        PlayClickSound();
        _currentSubPanel = null;
        SwitchPanel(creditsPanel, mainButtonsPanel);
    }

    // ---------------------------------------------------------
    // SETTINGS LOGIC
    // ---------------------------------------------------------

    public void SetVolume(float volume)
    {
        PlayerPrefs.SetFloat("MasterVolume_Pref", volume);
        PlayerPrefs.Save();

        AudioListener.volume = volume;

        if (masterMixer != null && !string.IsNullOrEmpty(volumeParameter))
        {
            float dB = volume > 0.0001f ? Mathf.Log10(volume) * 20f : -80f;
            masterMixer.SetFloat(volumeParameter, dB);
        }
    }

    public void SetFullscreen(bool isFullscreen)
    {
        PlayClickSound();
        Screen.fullScreen = isFullscreen;
    }

    // ---------------------------------------------------------
    // UI ANIMATIONS & VISIBILITY
    // ---------------------------------------------------------

    private void ShowMenu()
    {
        if (pauseCanvas != null)
        {
            pauseCanvas.enabled = true;
        }

        InitPanelsImmediate();

        if (pauseCanvasGroup != null)
        {
            _fadeTween?.Kill();
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.interactable = true;
            pauseCanvasGroup.blocksRaycasts = true;

            _fadeTween = pauseCanvasGroup
                .DOFade(1f, fadeDuration)
                .SetUpdate(true);
        }
    }

    private void HideMenu()
    {
        if (pauseCanvasGroup != null)
        {
            _fadeTween?.Kill();
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;

            _fadeTween = pauseCanvasGroup
                .DOFade(0f, fadeDuration)
                .SetUpdate(true);
        }
    }

    private void HideMenuImmediate()
    {
        if (pauseCanvasGroup != null)
        {
            _fadeTween?.Kill();
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;
        }
    }

    private void InitPanelsImmediate()
    {
        _currentSubPanel = null;

        if (mainButtonsPanel != null)
        {
            mainButtonsPanel.alpha = 1f;
            mainButtonsPanel.blocksRaycasts = true;
            mainButtonsPanel.interactable = true;
            mainButtonsPanel.gameObject.SetActive(true);
        }

        if (settingsPanel != null)
        {
            settingsPanel.alpha = 0f;
            settingsPanel.blocksRaycasts = false;
            settingsPanel.interactable = false;
            settingsPanel.gameObject.SetActive(false);
        }

        if (creditsPanel != null)
        {
            creditsPanel.alpha = 0f;
            creditsPanel.blocksRaycasts = false;
            creditsPanel.interactable = false;
            creditsPanel.gameObject.SetActive(false);
        }
    }

    private void InitSettings()
    {
        if (volumeSlider != null)
        {
            float savedVol = PlayerPrefs.GetFloat("MasterVolume_Pref", 0.8f);
            volumeSlider.value = savedVol;
            volumeSlider.onValueChanged.AddListener(SetVolume);
            SetVolume(savedVol);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    private void SwitchPanel(CanvasGroup fromPanel, CanvasGroup toPanel)
    {
        if (fromPanel != null)
        {
            fromPanel.interactable = false;
            fromPanel.blocksRaycasts = false;
            fromPanel.DOFade(0f, fadeDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    fromPanel.gameObject.SetActive(false);
                });
        }

        if (toPanel != null)
        {
            toPanel.gameObject.SetActive(true);
            toPanel.alpha = 0f;
            toPanel.interactable = true;
            toPanel.blocksRaycasts = true;
            toPanel.DOFade(1f, fadeDuration).SetUpdate(true);
        }
    }

    public void PlayHoverSound()
    {
        if (!string.IsNullOrEmpty(buttonHoverSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(buttonHoverSound);
        }
    }

    public void PlayClickSound()
    {
        if (!string.IsNullOrEmpty(buttonClickSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(buttonClickSound);
        }
    }
}
