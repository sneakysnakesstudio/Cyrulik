using System.Collections;
using UnityEngine;

public class IntroSequence : MonoBehaviour
{
    [Header("Gracz (Do zablokowania)")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHands playerHands;

    [Header("UI Intro")]
    [Tooltip("CanvasGroup z przypisanym czarnym tłem oraz tekstem czasu.")]
    [SerializeField] private CanvasGroup introCanvasGroup;
    
    [Tooltip("Panel zegara/czasu, który po intrze chcesz wyłączyć, żeby nie zaśmiecał ekranu podczas gry.")]
    [SerializeField] private GameObject clockUIToHide;

    [Header("UI Klawiszologia / Sterowanie (Controls)")]
    [Tooltip("CanvasGroup ekranu z klawiszologią (WSAD, E/LPM, G).")]
    [SerializeField] private CanvasGroup controlsCanvasGroup;

    [Tooltip("Ile sekund ma być wyświetlany ekran klawiszologii przed rozjaśnieniem gry.")]
    [SerializeField] private float waitOnControlsScreen = 5f;

    [Tooltip("Czy gracz może nacisnąć dowolny klawisz (Spacja, Enter, E, LPM itp.), aby pominąć ekran klawiszologii?")]
    [SerializeField] private bool allowSkipControlsWithKey = true;

    [Tooltip("Czas trwania wejścia/wyjścia (fade) dla ekranu klawiszologii.")]
    [SerializeField] private float controlsFadeDuration = 0.4f;

    [Header("Timings")]
    [Tooltip("Ile sekund gracz ma patrzeć na czarny ekran z uciekającym czasem, zanim przejdzie do klawiszologii.")]
    [SerializeField] private float waitOnBlackScreen = 5f;
    
    [Tooltip("Jak długo trwa przejście (fade) z czarnego ekranu do widoku z oczu gracza.")]
    [SerializeField] private float fadeDuration = 2.5f;

    [Header("Dźwięk Zegara (Opcjonalnie)")]
    [Tooltip("Opcjonalny dźwięk zegara / tła odtwarzany podczas intra (AudioClip).")]
    [SerializeField] private AudioClip introClockClip;
    [Tooltip("Czy dźwięk ma być zapętlony w tle podczas czarnego ekranu i płynnie wyciszony przy rozjaśnianiu?")]
    [SerializeField] private bool loopClockAudio = false;
    [Range(0f, 1f)] [SerializeField] private float clockVolume = 0.8f;
    [Tooltip("Czy po zakończeniu intra wyłączyć cykanie w GameTimeController?")]
    [SerializeField] private bool stopTickingAfterIntro = true;

    private AudioSource _introAudioSource;

    private void Awake()
    {
        if (playerMovement == null) playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerHands == null) playerHands = FindAnyObjectByType<PlayerHands>();

        // Upewnij się, że panel sterowania jest początkowo niewidoczny
        if (controlsCanvasGroup != null)
        {
            controlsCanvasGroup.alpha = 0f;
            controlsCanvasGroup.blocksRaycasts = false;
            controlsCanvasGroup.interactable = false;
        }
    }

    private void Start()
    {
        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        // 1. Zablokuj gracza na start (żeby nie mógł chodzić ani wchodzić w interakcje w tle)
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerHands != null) playerHands.enabled = false;

        // 2. Aktywuj panel zegara/napisów i upewnij się, że wszystkie jego pod-obiekty (np. tekst TMP) są aktywne
        if (clockUIToHide != null)
        {
            clockUIToHide.SetActive(true);
            foreach (Transform child in clockUIToHide.transform)
            {
                child.gameObject.SetActive(true);
            }
        }

        // Czarny ekran jest 100% nieprzezroczysty na start
        if (introCanvasGroup != null)
        {
            introCanvasGroup.gameObject.SetActive(true);
            introCanvasGroup.alpha = 1f;
            introCanvasGroup.blocksRaycasts = true;
        }

        // Opcjonalne uruchomienie zapętlonego dźwięku zegara w intro
        if (introClockClip != null)
        {
            if (_introAudioSource == null)
            {
                _introAudioSource = GetComponent<AudioSource>();
                if (_introAudioSource == null)
                {
                    _introAudioSource = gameObject.AddComponent<AudioSource>();
                    _introAudioSource.playOnAwake = false;
                    _introAudioSource.spatialBlend = 0f;
                }
            }

            _introAudioSource.clip = introClockClip;
            _introAudioSource.loop = loopClockAudio;
            _introAudioSource.volume = clockVolume;
            _introAudioSource.Play();
        }

        // 3. Patrzymy na czarny ekran z tykającym czasem i datą
        yield return new WaitForSeconds(waitOnBlackScreen);

        // 4. Ukrywamy panel zegara
        if (clockUIToHide != null)
        {
            clockUIToHide.SetActive(false);
        }

        // 5. Wyłączamy cykanie w GameTimeController jeśli wymagane
        if (stopTickingAfterIntro && GameTimeController.Instance != null)
        {
            GameTimeController.Instance.SetTickingEnabled(false);
        }

        // 6. FAZA KLAWISZOLOGII / STEROWANIA
        if (controlsCanvasGroup != null)
        {
            controlsCanvasGroup.gameObject.SetActive(true);

            // Fade in controls
            float fadeElapsed = 0f;
            while (fadeElapsed < controlsFadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                controlsCanvasGroup.alpha = Mathf.Lerp(0f, 1f, fadeElapsed / controlsFadeDuration);
                yield return null;
            }
            controlsCanvasGroup.alpha = 1f;
            controlsCanvasGroup.blocksRaycasts = true;

            // Czekamy na upływ czasu LUB naciśnięcie dowolnego klawisza przez gracza
            float controlsTimer = 0f;
            while (controlsTimer < waitOnControlsScreen)
            {
                controlsTimer += Time.deltaTime;

                if (allowSkipControlsWithKey && controlsTimer > 0.4f)
                {
                    if (Input.anyKeyDown || (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame) || (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame))
                    {
                        break;
                    }
                }

                yield return null;
            }

            // Fade out controls
            fadeElapsed = 0f;
            while (fadeElapsed < controlsFadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                controlsCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / controlsFadeDuration);
                yield return null;
            }
            controlsCanvasGroup.alpha = 0f;
            controlsCanvasGroup.blocksRaycasts = false;
            controlsCanvasGroup.gameObject.SetActive(false);
        }

        // 7. Rozjaśnianie ekranu (fade do zera) oraz płynne wyciszanie dźwięku
        if (introCanvasGroup != null || _introAudioSource != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                if (introCanvasGroup != null)
                    introCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

                if (_introAudioSource != null && loopClockAudio)
                    _introAudioSource.volume = Mathf.Lerp(clockVolume, 0f, t);

                yield return null;
            }

            if (introCanvasGroup != null)
            {
                introCanvasGroup.alpha = 0f;
                introCanvasGroup.blocksRaycasts = false;
            }

            if (_introAudioSource != null && loopClockAudio)
            {
                _introAudioSource.Stop();
            }
        }

        // 8. Oddajemy kontrolę graczowi
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerHands != null) playerHands.enabled = true;
    }
}
