using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Prostokątne okno dialogowe przeznaczone do rozmów z postaciami / klientem (NPC Dialogue).
/// Zawiera tabliczkę z imieniem mówcy (Speaker Name), prostokątną ramkę dialogową,
/// maszynopisanie, obsługę dźwięków głosu/liter, oraz przewijanie kwestii klawiszem [E].
/// </summary>
public class ClientDialogueUI : MonoBehaviour
{
    public static ClientDialogueUI Instance { get; private set; }

    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("Imię lub rola mówcy (np. 'Klient', 'Cyrulik', 'Nieznajomy').")]
        public string speakerName = "Klient";

        [TextArea(2, 5)]
        [Tooltip("Treść wypowiedzi.")]
        public string text = "";

        [Tooltip("Opcjonalna nazwa dźwięku / głosu w AudioManager dla tej kwestii.")]
        public string voiceSoundGroup = "";

        public DialogueLine(string speaker, string content, string sound = "")
        {
            speakerName = speaker;
            text = content;
            voiceSoundGroup = sound;
        }
    }

    [Header("UI - Elementy Prostokątnego Okna")]
    [SerializeField] private CanvasGroup dialogueCanvasGroup;
    [SerializeField] private Canvas dialogueCanvas;
    [SerializeField] private GameObject dialogueContainer;
    [SerializeField] private CanvasGroup hudToHide;

    [Header("UI - Pola Tekstowe")]
    [Tooltip("Pole tekstowe z imieniem mówcy (tabliczka).")]
    [SerializeField] private TextMeshProUGUI speakerNameText;

    [Tooltip("Obiekt tabliczki imienia (do ukrycia, jeśli brak imienia).")]
    [SerializeField] private GameObject speakerBadgeContainer;

    [Tooltip("Główne pole tekstowe wypowiedzi klienta/mówcy.")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Tooltip("Wyrównanie tekstu dialogu klienta (domyślnie TopLeft / MidlineLeft).")]
    [SerializeField] private TextAlignmentOptions textAlignment = TextAlignmentOptions.TopLeft;

    [Header("UI - Prompt [E] + Strzałka")]
    [SerializeField] private CanvasGroup continuePromptGroup;
    [SerializeField] private RectTransform arrowTransform;
    [SerializeField] private TextMeshProUGUI promptKeyText;
    [SerializeField] private TextMeshProUGUI promptArrowText;
    [SerializeField] private float arrowBobDistance = 6f;
    [SerializeField] private float arrowBobDuration = 0.45f;

    [Header("Interakcja")]
    [Tooltip("Czy pierwsze naciśnięcie E w trakcie pisania ma natychmiast odsłonić całą kwestię.")]
    [SerializeField] private bool allowSkipTypewriter = true;

    [Header("Game Timer Control")]
    [Tooltip("Czy zatrzymać zegar gry podczas rozmowy z klientem.")]
    [SerializeField] private bool pauseGameTimer = true;

    [Header("Timing")]
    [SerializeField] private float charDelay = 0.03f;
    [SerializeField] private float punctuationExtraDelay = 0.1f;
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    public enum TypewriterAudioMode
    {
        ContinuousLoop,     // Klip zapętlony (np. nagrane 'DU DU DU DU DU'), gra w trakcie pisania tekstu i natychmiast wyłącza się po skończeniu
        PerCharacter        // Pojedynczy klik przy każdej literce
    }

    [Header("Audio")]
    [Tooltip("ContinuousLoop: Klip zapętlony (np. nagrane 'DU DU DU DU DU'), gra w trakcie pisania tekstu i wyłącza się natychmiast po skończeniu.\nPerCharacter: Pojedynczy dźwięk przy każdej literce.")]
    [SerializeField] private TypewriterAudioMode audioMode = TypewriterAudioMode.ContinuousLoop;

    [Tooltip("Dźwięk maszyny / gadania (AudioClip). W trybie ContinuousLoop leci w pętli dopóki litery się pojawiają i wyłącza się na końcu.")]
    [SerializeField] private AudioClip charAudioClip;

    [Tooltip("Nazwa dźwięku/grupy w AudioManager (używana jeśli charAudioClip jest pusty).")]
    [SerializeField] private string defaultCharSoundGroup = "";

    [Tooltip("Dedykowany AudioSource (jeśli pusty, stworzy automatycznie w Awake).")]
    [SerializeField] private AudioSource audioSource;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.5f;

    [Range(0.5f, 2f)]
    [SerializeField] private float minPitch = 0.95f;

    [Range(0.5f, 2f)]
    [SerializeField] private float maxPitch = 1.05f;

    [Header("Advance Audio (Dźwięk Przejścia Dalej)")]
    [Tooltip("Dźwięk odtwarzany przy wciśnięciu [E] i przejściu do kolejnej kwestii / zamknięciu dialogu.")]
    [SerializeField] private string advanceSoundGroup = "";

    public bool IsDialogueActive => _isDialogueActive;
    public event Action OnDialogueStarted;
    public event Action OnDialogueFinished;

    private Coroutine _dialogueCoroutine;
    private Tween _fadeTween;
    private Tween _promptFadeTween;
    private Tween _arrowBobTween;

    private Vector2 _arrowOriginalAnchoredPos;
    private bool _isDialogueActive = false;
    private bool _isTyping = false;
    private bool _skipRequested = false;
    private bool _continuePressed = false;
    private bool _isTimerPaused = false;
    private float _dialogueStartTime = -100f;
    private string _currentVoiceGroup = "";

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

        // ZAWSZE upewnij się, że dialogueCanvasGroup to CanvasGroup na TYM obiekcie (ClientDialogue_Box), a NIE na rodzicu DialogueCanvas!
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

        if (speakerBadgeContainer == null)
        {
            Transform badge = transform.Find("Speaker_Badge");
            if (badge != null) speakerBadgeContainer = badge.gameObject;
        }

        if (speakerNameText == null && speakerBadgeContainer != null)
            speakerNameText = speakerBadgeContainer.GetComponentInChildren<TextMeshProUGUI>(true);

        if (continuePromptGroup == null)
        {
            Transform cp = transform.Find("Continue_Prompt");
            if (cp != null) continuePromptGroup = cp.GetComponent<CanvasGroup>();
        }

        if (arrowTransform == null && continuePromptGroup != null)
        {
            Transform arrow = continuePromptGroup.transform.Find("Prompt_Arrow");
            if (arrow == null) arrow = continuePromptGroup.transform.Find("Arrow_Icon");
            if (arrow != null) arrowTransform = arrow.GetComponent<RectTransform>();
        }

        if (arrowTransform != null)
            _arrowOriginalAnchoredPos = arrowTransform.anchoredPosition;

        if (audioSource == null && charAudioClip != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        HideAllInstant();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        StopAllCoroutines();
        _fadeTween?.Kill();
        ResumeTimerIfNeeded();
        InputModeManager.Instance?.SwitchToPlayer();
    }

    private void Update()
    {
        if (!_isDialogueActive) return;

        bool pressed = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
            {
                pressed = true;
            }
        }

        if (!pressed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressed = true;
        }

        if (!pressed && Gamepad.current != null &&
            (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame))
        {
            pressed = true;
        }

        if (pressed)
        {
            TriggerInputAdvance();
        }
    }

    private void TriggerInputAdvance()
    {
        // Ignoruj naciśnięcie klawisza, które dopiero co uruchomiło dialog/kwestię
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

    /// <summary>
    /// Wyświetla pojedynczą wypowiedź klienta / postaci.
    /// </summary>
    public void ShowLine(string speaker, string text, Action onComplete = null)
    {
        List<DialogueLine> lines = new List<DialogueLine>
        {
            new DialogueLine(speaker, text)
        };

        StartDialogue(lines, onComplete);
    }

    /// <summary>
    /// Rozpoczyna sekwencję dialogową (wiele kolejnych kwestii).
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines, Action onComplete = null)
    {
        if (dialogueText == null)
            dialogueText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (lines == null || lines.Count == 0 || dialogueText == null)
        {
            Debug.LogWarning("[ClientDialogueUI] Brak kwestii dialogowych lub komponentu dialogueText!");
            onComplete?.Invoke();
            return;
        }

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
        _dialogueCoroutine = StartCoroutine(DialogueSequenceRoutine(lines, onComplete));
    }

    private IEnumerator DialogueSequenceRoutine(List<DialogueLine> lines, Action onComplete)
    {
        _isDialogueActive = true;
        OnDialogueStarted?.Invoke();

        // 1. Zmień schemat sterowania na UI i zapauzuj czas
        InputModeManager.Instance?.SwitchToUI(unlockCursor: false);
        PauseTimerIfNeeded();

        // 2. Aktywuj okno
        if (dialogueCanvas != null)
            dialogueCanvas.enabled = true;

        // Jeśli pod DialogueCanvas jest stary BlackImage (czarny pełnoekranowy fader), wyłącz go, aby nie zasłaniał widoku gry!
        if (transform.parent != null)
        {
            Transform blackImg = transform.parent.Find("BlackImage");
            if (blackImg != null)
            {
                blackImg.gameObject.SetActive(false);
            }
        }

        if (dialogueContainer != null && dialogueContainer != gameObject)
            dialogueContainer.SetActive(true);

        gameObject.SetActive(true);

        if (hudToHide != null)
            hudToHide.DOFade(0f, fadeInDuration).SetUpdate(true).SetLink(hudToHide.gameObject, LinkBehaviour.KillOnDestroy);

        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.ignoreParentGroups = true;
            dialogueCanvasGroup.alpha = 1f;
            dialogueCanvasGroup.blocksRaycasts = true;
            dialogueCanvasGroup.interactable = true;
        }

        // 3. Pętla kolejnych kwestii dialogu
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            DialogueLine currentLine = lines[lineIndex];
            _currentVoiceGroup = !string.IsNullOrEmpty(currentLine.voiceSoundGroup) ? currentLine.voiceSoundGroup : defaultCharSoundGroup;

            // Ustaw imię mówcy
            if (speakerNameText != null)
            {
                speakerNameText.text = currentLine.speakerName;
                if (speakerBadgeContainer != null)
                {
                    speakerBadgeContainer.SetActive(!string.IsNullOrEmpty(currentLine.speakerName));
                }
            }

            // Przygotuj tekst
            dialogueText.alignment = textAlignment;
            dialogueText.text = currentLine.text;
            dialogueText.ForceMeshUpdate();
            int totalChars = dialogueText.textInfo.characterCount;
            dialogueText.maxVisibleCharacters = 0;

            HideContinuePrompt();
            _isTyping = true;
            _skipRequested = false;
            _continuePressed = false;
            _dialogueStartTime = Time.unscaledTime;

            // Maszynopisanie
            int soundCounter = 0;
            StartTypewriterAudio();

            for (int i = 1; i <= totalChars; i++)
            {
                if (_skipRequested)
                {
                    dialogueText.maxVisibleCharacters = totalChars;
                    StopTypewriterAudio();
                    break;
                }

                dialogueText.maxVisibleCharacters = i;
                char c = dialogueText.textInfo.characterInfo[i - 1].character;

                if (audioMode == TypewriterAudioMode.PerCharacter)
                {
                    if (!char.IsWhiteSpace(c))
                    {
                        soundCounter++;
                        if (soundCounter % 1 == 0)
                        {
                            PlayCharSound();
                        }
                    }
                }

                float delay = charDelay;
                if (IsPunctuation(c)) delay += punctuationExtraDelay;

                if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            }

            StopTypewriterAudio();
            _isTyping = false;
            dialogueText.maxVisibleCharacters = totalChars;

            // Pokaż prompt [E]
            ShowContinuePrompt();

            // Czekaj na wciśnięcie E
            yield return new WaitForSecondsRealtime(0.12f);
            _continuePressed = false;

            while (!_continuePressed)
            {
                yield return null;
            }

            if (!string.IsNullOrEmpty(advanceSoundGroup) && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(advanceSoundGroup);
            }
        }

        // 4. Koniec dialogu - schowaj okno
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

        HideAllInstant();

        if (hudToHide != null)
            hudToHide.DOFade(1f, fadeOutDuration).SetUpdate(true).SetLink(hudToHide.gameObject, LinkBehaviour.KillOnDestroy);

        ResumeTimerIfNeeded();
        InputModeManager.Instance?.SwitchToPlayer();

        _isDialogueActive = false;
        _dialogueCoroutine = null;

        OnDialogueFinished?.Invoke();
        onComplete?.Invoke();
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
            continuePromptGroup.alpha = 0f;

        if (arrowTransform != null)
            arrowTransform.anchoredPosition = _arrowOriginalAnchoredPos;
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
                audioSource.volume = soundVolume;
                audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
                audioSource.Play();
            }
            else
            {
                string sound = !string.IsNullOrEmpty(_currentVoiceGroup) ? _currentVoiceGroup : defaultCharSoundGroup;
                if (!string.IsNullOrEmpty(sound) && AudioManager.Instance != null)
                {
                    AudioManager.Instance.Play(sound);
                }
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

    private void PlayCharSound()
    {
        if (charAudioClip != null && audioSource != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(charAudioClip, soundVolume);
            return;
        }

        string sound = !string.IsNullOrEmpty(_currentVoiceGroup) ? _currentVoiceGroup : defaultCharSoundGroup;
        if (!string.IsNullOrEmpty(sound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(sound);
        }
    }

    private bool IsPunctuation(char c)
    {
        return c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == ';' || c == '…' || c == '-';
    }

    private void PauseTimerIfNeeded()
    {
        if (!pauseGameTimer) return;
        if (GameTimeController.Instance != null)
        {
            GameTimeController.Instance.Pause();
            _isTimerPaused = true;
        }
    }

    private void ResumeTimerIfNeeded()
    {
        if (!_isTimerPaused) return;
        if (GameTimeController.Instance != null)
        {
            GameTimeController.Instance.Resume();
            _isTimerPaused = false;
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

        InputModeManager.Instance?.SwitchToPlayer();
        ResumeTimerIfNeeded();
        _isDialogueActive = false;
    }

    private void StopAllAnimations()
    {
        StopTypewriterAudio();

        if (_dialogueCoroutine != null)
        {
            StopCoroutine(_dialogueCoroutine);
            _dialogueCoroutine = null;
        }

        _fadeTween?.Kill();
        _fadeTween = null;
        _isTyping = false;
        HideContinuePrompt();
    }
}
