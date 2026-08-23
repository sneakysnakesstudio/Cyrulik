using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Nazwa sceny głównej z rozgrywką.")]
    [SerializeField] private string gameSceneName = "MainScene";

    [Header("Panels")]
    [SerializeField] private CanvasGroup mainButtonsPanel;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private CanvasGroup creditsPanel;

    [Header("Version Display")]
    [Tooltip("Tekst wersji wyświetlany w rogu.")]
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
    [SerializeField] private float fadeDuration = 0.25f;

    private void Awake()
    {
        // Upewniamy się, że kursor myszy jest widoczny i odblokowany w menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Jeśli jakikolwiek nadrzędny CanvasGroup został zablokowany przez ScreenFader, odblokowujemy go
        CanvasGroup[] parentCgs = GetComponentsInParent<CanvasGroup>(true);
        foreach (var cg in parentCgs)
        {
            if (cg != null && cg != settingsPanel && cg != creditsPanel)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        if (versionLabel != null)
        {
            versionLabel.text = versionString;
        }

        InitPanels();
        InitSettings();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void InitPanels()
    {
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

    // ---------------------------------------------------------
    // BUTTON ACTIONS
    // ---------------------------------------------------------

    public void StartGame()
    {
        PlayClickSound();

        // Jeśli w scenie jest ScreenFader, używamy płynnego przejścia
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.LoadScene(gameSceneName);
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    public void OpenSettings()
    {
        PlayClickSound();
        SwitchPanel(mainButtonsPanel, settingsPanel);
    }

    public void CloseSettings()
    {
        PlayClickSound();
        SwitchPanel(settingsPanel, mainButtonsPanel);
    }

    public void OpenCredits()
    {
        PlayClickSound();
        SwitchPanel(mainButtonsPanel, creditsPanel);
    }

    public void CloseCredits()
    {
        PlayClickSound();
        SwitchPanel(creditsPanel, mainButtonsPanel);
    }

    public void QuitGame()
    {
        PlayClickSound();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
    // HELPERS & ANIMATIONS
    // ---------------------------------------------------------

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