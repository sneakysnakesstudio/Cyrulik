using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Wyświetla wewnętrzny dialog/myśli gracza w stylu maszyny do pisania (typewriter effect),
/// z dźwiękiem przy literkach, ramką, wskaźnikiem [E] + strzałka w dół po prawej stronie,
/// pauzą głównego zegara gry na czas wyświetlania oraz wymogiem interakcji (E) aby przejść dalej.
/// </summary>
public class InnerDialogueUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private GameTimeController gameTimeController;
    [SerializeField] private InputActionReference interactAction;

    [Header("UI - Tekst i Okno")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Tooltip("CanvasGroup całego okna dialogu/ramki. Jeśli puste, spróbuje pobrać z tego obiektu.")]
    [SerializeField] private CanvasGroup dialogueCanvasGroup;

    [Tooltip("Opcjonalny obiekt Canvas — wyłączany gdy dialog jest nieaktywny.")]
    [SerializeField] private Canvas dialogueCanvas;

    [Tooltip("Opcjonalny obiekt nadrzędny/tło/kontener do aktywacji/deaktywacji.")]
    [SerializeField] private GameObject dialogueContainer;

    [Tooltip("Opcjonalny CanvasGroup głównego HUDu (np. celownik, zegar), który ma się wygasić w trakcie myśli gracza.")]
    [SerializeField] private CanvasGroup hudToHide;

    [Header("UI - Prompt [E] + Strzałka")]
    [Tooltip("CanvasGroup wskaźnika [E] + strzałka po prawej stronie.")]
    [SerializeField] private CanvasGroup continuePromptGroup;

    [Tooltip("RectTransform strzałki pod literą E (do animacji pulsowania/podskakiwania).")]
    [SerializeField] private RectTransform arrowTransform;

    [Tooltip("Tekst klawisza (domyślnie 'E').")]
    [SerializeField] private TextMeshProUGUI promptKeyText;

    [Tooltip("Tekst lub symbol strzałki (domyślnie '▼').")]
    [SerializeField] private TextMeshProUGUI promptArrowText;

    [Tooltip("Amplituda podskakiwania strzałki w pikselach.")]
    [SerializeField] private float arrowBobDistance = 6f;

    [Tooltip("Czas jednego cyklu podskakiwania strzałki.")]
    [SerializeField] private float arrowBobDuration = 0.45f;

    [Header("Interakcja i Sterowanie")]
    [Tooltip("Czy gracz musi nacisnąć E, aby przejść dalej. Jeśli true, okno czeka na gracza.")]
    [SerializeField] private bool requireInteractionToClose = true;

    [Tooltip("Czy pierwsze naciśnięcie E w trakcie pisania ma natychmiast dokończyć pisanie tekstu.")]
    [SerializeField] private bool allowSkipTypewriter = true;

    [Tooltip("Czas wyświetlania, jeśli requireInteractionToClose jest odznaczone.")]
    [SerializeField] private float autoCloseDuration = 3.5f;

    [Tooltip("Czy zablokować ruch gracza na czas czytania myśli.")]
    [SerializeField] private bool lockPlayerMovement = false;

    [Header("Game Timer Control")]
    [Tooltip("Czy zatrzymać zegar gry (GameTimeController) na czas pisania i czytania dialogu.")]
    [SerializeField] private bool pauseGameTimer = true;

    [Header("Typewriter Timing (Maszyna do pisania)")]
    [Tooltip("Czas w sekundach między kolejnymi znakami (np. 0.035s = ~28 znaków na sekundę).")]
    [SerializeField] private float charDelay = 0.035f;

    [Tooltip("Dodatkowa pauza w sekundach przy znakach przestankowych (. , ! ? : ;).")]
    [SerializeField] private float punctuationExtraDelay = 0.12f;

    [Tooltip("Czas płynnego pojawiania się ramki dialogu.")]
    [SerializeField] private float fadeInDuration = 0.2f;

    [Tooltip("Czas płynnego zanikania całego okna dialogu.")]
    [SerializeField] private float fadeOutDuration = 0.35f;

    [Header("Style")]
    [Tooltip("Kolor tekstu wewnętrznego dialogu.")]
    [SerializeField] private Color textColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    [Tooltip("Czy tekst ma być kursywą (myśli wewnętrzne gracza).")]
    [SerializeField] private bool useItalic = true;

    [Tooltip("Wyrównanie tekstu wewnątrz okna (domyślnie MidlineLeft – wyśrodkowane pionowo, wyrównane do lewej).")]
    [SerializeField] private TextAlignmentOptions textAlignment = TextAlignmentOptions.MidlineLeft;

    public enum TypewriterAudioMode
    {
        ContinuousLoop,     // Klip zapętlony (np. nagrane 'DU DU DU DU DU'), gra w trakcie pisania tekstu i natychmiast wyłącza się po skończeniu
        PerCharacter        // Pojedynczy klik przy każdej literce
    }

    [Header("Typewriter Audio")]
    [Tooltip("ContinuousLoop: Klip zapętlony (np. nagrane 'DU DU DU DU DU'), gra w trakcie pisania tekstu i wyłącza się natychmiast po skończeniu.\nPerCharacter: Pojedynczy dźwięk przy każdej literce.")]
    [SerializeField] private TypewriterAudioMode audioMode = TypewriterAudioMode.ContinuousLoop;

    [Tooltip("Dźwięk maszyny / gadania (AudioClip). W trybie ContinuousLoop leci w pętli dopóki litery się pojawiają i wyłącza się na końcu.")]
    [SerializeField] private AudioClip charAudioClip;

    [Tooltip("Nazwa dźwięku/grupy w AudioManager (używana jeśli charAudioClip jest pusty).")]
    [SerializeField] private string charSoundGroup = "";

    [Tooltip("Dedykowany AudioSource (jeśli pusty, stworzy automatycznie w Awake).")]
    [SerializeField] private AudioSource audioSource;

    [Range(0f, 1f)]
    [SerializeField] private float charSoundVolume = 0.5f;

    [Range(0.5f, 2f)]
    [SerializeField] private float minPitch = 0.95f;

    [Range(0.5f, 2f)]
    [SerializeField] private float maxPitch = 1.05f;

    [Tooltip("Odtwarzaj dźwięk co N liter (tylko dla trybu PerCharacter).")]
    [SerializeField] private int soundFrequency = 1;

    [Tooltip("Czy pomijać spacje przy odtwarzaniu dźwięku (tylko PerCharacter).")]
    [SerializeField] private bool playSoundOnlyOnNonWhitespace = true;

    [Header("Advance Audio (Dźwięk Przejścia Dalej)")]
    [Tooltip("Dźwięk odtwarzany w momencie wciśnięcia [E] i zamknięcia/zatwierdzenia dialogu.")]
    [SerializeField] private string advanceSoundGroup = "";

    public static InnerDialogueUI Instance { get; private set; }
    public bool IsDialogueActive => _isDialogueActive;

    private Coroutine _typewriterCoroutine;
    private Tween _fadeTween;
    private Tween _promptFadeTween;
    private Tween _arrowBobTween;

    private Vector2 _arrowOriginalAnchoredPos;
    private bool _isTimerPausedByDialogue = false;
    private bool _isDialogueActive = false;
    private bool _isTyping = false;
    private bool _skipRequested = false;
    private bool _continuePressed = false;
    private float _dialogueStartTime = -100f;

    // Cache WaitForSeconds — unikamy new() co klatkę w typewriterze (GC pressure)
    private WaitForSeconds _waitChar;
    private WaitForSeconds _waitContinueGuard;
    private float _cachedCharDelay = -1f;

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

        // ZAWSZE upewnij się, że dialogueCanvasGroup to CanvasGroup na TYM obiekcie (InnerThought_Bubble)
        var ownCg = GetComponent<CanvasGroup>();
        if (ownCg == null)
            ownCg = gameObject.AddComponent<CanvasGroup>();
        dialogueCanvasGroup = ownCg;
        dialogueCanvasGroup.ignoreParentGroups = true;

        if (dialogueCanvas == null)
            dialogueCanvas = GetComponentInParent<Canvas>(true);

        // Jeśli na samym DialogueCanvas jest CanvasGroup, wymuś alpha = 1, bo ten komponent blokuje cały Canvas
        if (dialogueCanvas != null)
        {
            CanvasGroup rootCg = dialogueCanvas.GetComponent<CanvasGroup>();
            if (rootCg != null)
            {
                rootCg.alpha = 1f;
                rootCg.interactable = true;
                rootCg.blocksRaycasts = true;
                rootCg.ignoreParentGroups = true;
            }
        }

        if (dialogueContainer == null)
            dialogueContainer = gameObject;

        if (dialogueText == null)
            dialogueText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (gameTimeController == null)
        {
            gameTimeController = GameTimeController.Instance ?? FindAnyObjectByType<GameTimeController>();
        }

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

        if (audioSource == null && charAudioClip != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        if (arrowTransform != null)
        {
            _arrowOriginalAnchoredPos = arrowTransform.anchoredPosition;
        }

        if (promptKeyText != null && string.IsNullOrEmpty(promptKeyText.text))
        {
            promptKeyText.text = "E";
        }

        if (promptArrowText != null && string.IsNullOrEmpty(promptArrowText.text))
        {
            promptArrowText.text = "▼";
        }

        HideAllInstant();
    }

    private void OnEnable()
    {
        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

        if (playerMovement != null)
        {
            playerMovement.OnInteractionBlocked += HandleBlocked;
        }

        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractInput;
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.OnInteractionBlocked -= HandleBlocked;
        }

        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteractInput;
        }

        StopAllAnimations();
        ResumeTimerIfNeeded();
        UnlockPlayerIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        StopAllAnimations();
        ResumeTimerIfNeeded();
        UnlockPlayerIfNeeded();
    }

    private void Update()
    {
        if (!_isDialogueActive) return;

        // Bezpośredni odczyt wejścia klawiszy (E, Spacja, Enter, Kliknięcie myszą, Gamepad)
        // Działa ZAWSZE, nawet po przełączeniu Action Map na UI
        bool advanceInput = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
            {
                advanceInput = true;
            }
        }

        if (!advanceInput && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            advanceInput = true;
        }

        if (!advanceInput && Gamepad.current != null &&
            (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame))
        {
            advanceInput = true;
        }

        if (advanceInput)
        {
            TriggerInputAdvance();
        }
    }

    private void OnInteractInput(InputAction.CallbackContext context)
    {
        if (!_isDialogueActive) return;
        TriggerInputAdvance();
    }

    private void TriggerInputAdvance()
    {
        // Ignorujemy naciśnięcie klawisza, które dopiero co uruchomiło dialog (okres ochronny 0.15s)
        if (Time.unscaledTime - _dialogueStartTime < 0.15f)
            return;

        if (_isTyping && allowSkipTypewriter)
        {
            _skipRequested = true;
            StopTypewriterAudio();
            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
            }
        }
        else if (!_isTyping)
        {
            _continuePressed = true;
        }
    }

    private void HandleBlocked(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            ShowMessage(message);
        }
    }

    /// <summary>
    /// Wyświetla dialog maszynopisem, pokazuje ramkę z [E] + strzałka, pauzuje czas gry i czeka na interakcję.
    /// </summary>
    public void ShowMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (dialogueText == null)
            dialogueText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (dialogueText == null)
        {
            Debug.LogWarning("[InnerDialogueUI] Brak komponentu dialogueText!");
            return;
        }

        Debug.Log($"[InnerDialogueUI] Showing message: \"{message}\"");

        // Aktywuj Canvas i GameObject PRZED wywołaniem StartCoroutine
        if (dialogueCanvas != null)
            dialogueCanvas.enabled = true;

        if (transform.parent != null && !transform.parent.gameObject.activeSelf)
            transform.parent.gameObject.SetActive(true);

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (dialogueContainer != null && !dialogueContainer.activeSelf)
            dialogueContainer.SetActive(true);

        StopAllAnimations();

        _typewriterCoroutine = StartCoroutine(DialogueFlowRoutine(message));
    }

    private IEnumerator DialogueFlowRoutine(string message)
    {
        _dialogueStartTime = Time.unscaledTime;
        _isDialogueActive = true;
        _isTyping = true;
        _skipRequested = false;
        _continuePressed = false;

        // 1. Zablokuj gracza i zapauzuj czas
        PauseTimerIfNeeded();
        LockPlayerIfNeeded();

        // 2. Aktywuj kontenery i Canvas
        if (dialogueContainer != null)
            dialogueContainer.SetActive(true);

        if (dialogueCanvas != null)
            dialogueCanvas.enabled = true;

        if (continuePromptGroup != null)
        {
            continuePromptGroup.alpha = 0f;
        }

        if (hudToHide != null)
        {
            hudToHide.DOFade(0f, fadeInDuration).SetLink(hudToHide.gameObject, LinkBehaviour.KillOnDestroy);
        }

        // 3. Przygotuj tekst i formatowanie
        dialogueText.alignment = textAlignment;
        dialogueText.color = textColor;
        dialogueText.text = useItalic ? $"<i>{message}</i>" : message;
        dialogueText.ForceMeshUpdate();

        int totalVisibleChars = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = 0;

        // Jeśli pod DialogueCanvas jest stary BlackImage (czarny pełnoekranowy fader), wyłącz go, aby nie zasłaniał widoku gry!
        if (transform.parent != null)
        {
            Transform blackImg = transform.parent.Find("BlackImage");
            if (blackImg != null)
            {
                blackImg.gameObject.SetActive(false);
            }
        }

        // 4. Płynne pojawienie się całego okna/ramki dialogu
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.ignoreParentGroups = true;
            dialogueCanvasGroup.alpha = 1f;
            dialogueCanvasGroup.blocksRaycasts = true;
            dialogueCanvasGroup.interactable = true;
        }

        // Resetujemy flagę skipa i odświeżamy czas po zakończeniu animacji pojawiania się okna
        _skipRequested = false;
        _dialogueStartTime = Time.unscaledTime;

        // 5. Maszynowe pojawianie się liter (Typewriter Effect)
        int soundCounter = 0;

        // Start ciągłego dźwięku maszynopisu / gadania (np. "DU DU DU DU DU")
        StartTypewriterAudio();

        for (int i = 1; i <= totalVisibleChars; i++)
        {
            if (_skipRequested)
            {
                dialogueText.maxVisibleCharacters = totalVisibleChars;
                StopTypewriterAudio();
                break;
            }

            dialogueText.maxVisibleCharacters = i;
            char currentChar = dialogueText.textInfo.characterInfo[i - 1].character;

            // Dźwięk przy literce (tylko w trybie pojedynczych kliknięć PerCharacter)
            if (audioMode == TypewriterAudioMode.PerCharacter)
            {
                if (!char.IsWhiteSpace(currentChar) || !playSoundOnlyOnNonWhitespace)
                {
                    soundCounter++;
                    if (soundCounter % soundFrequency == 0)
                    {
                        PlayTypewriterSound();
                    }
                }
            }

            // Pauza między literami + dłuższa pauza przy interpunkcji
            float delay = charDelay;
            if (IsPunctuation(currentChar))
            {
                delay += punctuationExtraDelay;
            }

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
        }

        // Zawsze zatrzymaj audio po ukończeniu pisania tekstu lub pominięciu!
        StopTypewriterAudio();
        _isTyping = false;
        dialogueText.maxVisibleCharacters = totalVisibleChars;

        // 6. Pokaż i animuj wskaźnik [E] + strzałka w dół
        if (requireInteractionToClose)
        {
            ShowContinuePrompt();
        }

        // 7. Czekanie na interakcję gracza (E) lub timeout
        if (requireInteractionToClose)
        {
            // Małe opóźnienie ochronne (0.15s), żeby to samo wciśnięcie E nie zamknęło natychmiast okna
            yield return new WaitForSecondsRealtime(0.12f);
            _continuePressed = false;

            while (!_continuePressed)
            {
                yield return null;
            }
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < autoCloseDuration && !_continuePressed)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // 8. Dźwięk potwierdzenia / zamknięcia
        if (!string.IsNullOrEmpty(advanceSoundGroup) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(advanceSoundGroup);
        }

        // 9. Płynne zanikanie całego okna
        HideContinuePrompt();

        if (dialogueCanvasGroup != null)
        {
            _fadeTween = dialogueCanvasGroup
                .DOFade(0f, fadeOutDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetLink(dialogueCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy);

            yield return _fadeTween.WaitForCompletion();
        }

        // 10. Schowaj i wyłącz całe UI
        HideAllInstant();

        // 11. Przywróć HUD, ruch gracza i zegar gry
        if (hudToHide != null)
        {
            hudToHide.DOFade(1f, fadeOutDuration).SetUpdate(true).SetLink(hudToHide.gameObject, LinkBehaviour.KillOnDestroy);
        }

        UnlockPlayerIfNeeded();
        ResumeTimerIfNeeded();

        _isDialogueActive = false;
        _typewriterCoroutine = null;
    }

    private void ShowContinuePrompt()
    {
        if (continuePromptGroup != null)
        {
            _promptFadeTween?.Kill();
            _promptFadeTween = continuePromptGroup
                .DOFade(1f, 0.2f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(continuePromptGroup.gameObject, LinkBehaviour.KillOnDestroy);
        }

        if (arrowTransform != null)
        {
            _arrowBobTween?.Kill();
            arrowTransform.anchoredPosition = _arrowOriginalAnchoredPos;

            // Animacja podskakiwania strzałki w dół i w górę
            _arrowBobTween = arrowTransform
                .DOAnchorPosY(_arrowOriginalAnchoredPos.y - arrowBobDistance, arrowBobDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(arrowTransform.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private void HideContinuePrompt()
    {
        _promptFadeTween?.Kill();
        _arrowBobTween?.Kill();

        if (continuePromptGroup != null)
        {
            continuePromptGroup.alpha = 0f;
        }

        if (arrowTransform != null)
        {
            arrowTransform.anchoredPosition = _arrowOriginalAnchoredPos;
        }
    }

    private void StartTypewriterAudio()
    {
        if (audioMode == TypewriterAudioMode.ContinuousLoop)
        {
            if (charAudioClip != null)
            {
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                }

                audioSource.clip = charAudioClip;
                audioSource.loop = true;
                audioSource.volume = charSoundVolume;
                audioSource.pitch = 1f;
                audioSource.Play();
            }
            else if (!string.IsNullOrEmpty(charSoundGroup) && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(charSoundGroup);
            }
        }
    }

    private void StopTypewriterAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void PlayTypewriterSound()
    {
        if (charAudioClip != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(charAudioClip, charSoundVolume);
            return;
        }

        if (!string.IsNullOrEmpty(charSoundGroup) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(charSoundGroup);
        }
    }

    private bool IsPunctuation(char c)
    {
        return c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == ';' || c == '…' || c == '-';
    }

    private void PauseTimerIfNeeded()
    {
        if (!pauseGameTimer) return;

        if (gameTimeController == null)
        {
            gameTimeController = GameTimeController.Instance ?? FindAnyObjectByType<GameTimeController>();
        }

        if (gameTimeController != null)
        {
            gameTimeController.Pause();
            _isTimerPausedByDialogue = true;
        }
    }

    private void ResumeTimerIfNeeded()
    {
        if (!_isTimerPausedByDialogue) return;

        if (gameTimeController == null)
        {
            gameTimeController = GameTimeController.Instance ?? FindAnyObjectByType<GameTimeController>();
        }

        if (gameTimeController != null)
        {
            gameTimeController.Resume();
            _isTimerPausedByDialogue = false;
        }
    }

    private void LockPlayerIfNeeded()
    {
        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchToUI(unlockCursor: false);
        }

        if (lockPlayerMovement && playerMovement != null)
        {
            playerMovement.enabled = false;
        }
    }

    private void UnlockPlayerIfNeeded()
    {
        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchToPlayer();
        }

        if (lockPlayerMovement && playerMovement != null)
        {
            playerMovement.enabled = true;
        }
    }

    public void HideAllInstant()
    {
        StopAllAnimations();
        StopTypewriterAudio();
        HideContinuePrompt();

        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.alpha = 0f;
            dialogueCanvasGroup.blocksRaycasts = false;
            dialogueCanvasGroup.interactable = false;
        }

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = 0;
        }

        UnlockPlayerIfNeeded();
        ResumeTimerIfNeeded();
        _isDialogueActive = false;
    }

    private void StopAllAnimations()
    {
        StopTypewriterAudio();

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        _fadeTween?.Kill();
        _fadeTween = null;
        _isTyping = false;

        HideContinuePrompt();
    }
}
