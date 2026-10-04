using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Główny interfejs kinowej inspekcji przedmiotów (Item Showcase / Item Reveal) w stylu Resident Evil / Silent Hill.
/// Prezentuje:
/// 1. Tytuł przedmiotu na górze ekranu (np. SCISSORS, STRAIGHT RAZOR).
/// 2. Płynnie obracający się model 3D na środku ekranu (poprzez ItemShowcaseStudio i RenderTexture).
/// 3. Klimatyczny komentarz / myśl fryzjera na dole ekranu (wraz z efektem maszyny do pisania).
/// 4. Prompt [E] do zabrania lub przejścia dalej.
/// </summary>
public class ItemShowcaseUI : MonoBehaviour
{
    public static ItemShowcaseUI Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Główny CanvasGroup całego okna inspekcji.")]
    [SerializeField] private CanvasGroup mainCanvasGroup;

    [Tooltip("Półprzezroczyste przyciemnienie tła gry.")]
    [SerializeField] private CanvasGroup backgroundDimmer;

    [Tooltip("Tekst nagłówka / nazwy przedmiotu na górze ekranu.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Podgląd 3D (RawImage) połączony ze studiem RenderTexture.")]
    [SerializeField] private RawImage modelViewport;

    [Tooltip("Tekst komentarza / myśli fryzjera na dole ekranu.")]
    [SerializeField] private TextMeshProUGUI commentText;

    [Tooltip("Wskaźnik przejścia dalej [E] na dole ekranu.")]
    [SerializeField] private CanvasGroup continuePromptGroup;

    [Tooltip("Tekst akcji przy klawiszu (np. 'Take', 'Examine', 'Continue').")]
    [SerializeField] private TextMeshProUGUI promptActionText;

    [Header("Audio")]
    [Tooltip("Nazwa dźwięku przy otwarciu widoku w AudioManager (np. 'item_pickup' lub uniwersalny chime).")]
    [SerializeField] private string openSound = "cloth_pickup";

    [Tooltip("Nazwa dźwięku przy zamknięciu / zatwierdzeniu [E].")]
    [SerializeField] private string closeSound = "";

    [Header("Typewriter Settings")]
    [Tooltip("Czas w sekundach między kolejnymi znakami komentarza.")]
    [SerializeField] private float charDelay = 0.025f;

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    public bool IsShowcaseActive => _isOpen;

    private bool _isOpen = false;
    private bool _isTyping = false;
    private bool _skipTypingRequested = false;
    private bool _continuePressed = false;
    private float _openTime = -100f;
    private Action _onCloseCallback;
    private Coroutine _typewriterCoroutine;
    private Tween _fadeTween;

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

        if (mainCanvasGroup == null)
            mainCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        HideInstant();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!_isOpen) return;

        // 1. Obsługa wejścia gracza do przewijania / zamykania (E, Spacja, Enter, LPM, Gamepad)
        bool advanceInput = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.escapeKey.wasPressedThisFrame)
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

        // Okres ochronny 0.15s, by to samo wciśnięcie E nie zamknęło natychmiast okna
        if (advanceInput && (Time.unscaledTime - _openTime > 0.15f))
        {
            if (_isTyping)
            {
                _skipTypingRequested = true;
            }
            else
            {
                _continuePressed = true;
            }
        }

        // 2. Ręczne obracanie modelu myszką (jeśli gracz przeciąga myszką)
        if (Mouse.current != null && ItemShowcaseStudio.Instance != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            if (Mouse.current.rightButton.isPressed || Mouse.current.leftButton.isPressed)
            {
                ItemShowcaseStudio.Instance.AddManualRotation(delta);
            }
        }
    }

    /// <summary>
    /// Otwiera widok inspekcji dla podanego przedmiotu.
    /// </summary>
    /// <param name="title">Nazwa przedmiotu na górze (np. 'SCISSORS')</param>
    /// <param name="comment">Klimatyczny opis / myśl na dole</param>
    /// <param name="modelSource">GameObject z meshem (lub prefab)</param>
    /// <param name="modelScale">Mnożnik skali podglądu</param>
    /// <param name="customRotation">Początkowa rotacja</param>
    /// <param name="actionPrompt">Napis przy [E] (np. 'Take' lub 'Continue')</param>
    /// <param name="onClose">Callback wywoływany po zamknięciu (np. podniesienie do rąk)</param>
    public void Show(
        string title,
        string comment,
        GameObject modelSource,
        float modelScale = 1f,
        Vector3 customRotation = default,
        string actionPrompt = "Take",
        Action onClose = null)
    {
        _onCloseCallback = onClose;
        _isOpen = true;
        _openTime = Time.unscaledTime;
        _continuePressed = false;
        _skipTypingRequested = false;

        // 1. Zablokuj gracza i zapauzuj czas gry
        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchToUI(unlockCursor: false);
        }

        if (GameTimeController.Instance != null)
        {
            GameTimeController.Instance.Pause();
        }

        // 2. Skonfiguruj widok 3D w studio
        if (ItemShowcaseStudio.Instance != null)
        {
            ItemShowcaseStudio.Instance.DisplayItem(modelSource, modelScale, customRotation);

            if (modelViewport != null)
            {
                modelViewport.texture = ItemShowcaseStudio.Instance.GetRenderTexture();
            }
        }

        // 3. Ustaw teksty
        if (titleText != null)
        {
            titleText.text = title.ToUpperInvariant();
        }

        if (promptActionText != null)
        {
            promptActionText.text = actionPrompt;
        }

        // 4. Dźwięk otwarcia
        if (!string.IsNullOrEmpty(openSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(openSound);
        }

        // 5. Animacja wejścia UI
        gameObject.SetActive(true);
        _fadeTween?.Kill();

        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.blocksRaycasts = true;
            mainCanvasGroup.interactable = true;
            _fadeTween = mainCanvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
        }

        if (backgroundDimmer != null)
        {
            backgroundDimmer.alpha = 0f;
            backgroundDimmer.DOFade(1f, fadeInDuration).SetUpdate(true);
        }

        // 6. Maszynopis komentarza
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
        }
        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(comment));
    }

    private IEnumerator TypewriterRoutine(string message)
    {
        _isTyping = true;

        if (continuePromptGroup != null)
        {
            continuePromptGroup.alpha = 0f;
        }

        if (commentText != null)
        {
            commentText.text = $"<i>\"{message}\"</i>";
            commentText.ForceMeshUpdate();

            int totalChars = commentText.textInfo.characterCount;
            commentText.maxVisibleCharacters = 0;

            for (int i = 1; i <= totalChars; i++)
            {
                if (_skipTypingRequested)
                {
                    commentText.maxVisibleCharacters = totalChars;
                    break;
                }

                commentText.maxVisibleCharacters = i;

                if (charDelay > 0f)
                {
                    yield return new WaitForSecondsRealtime(charDelay);
                }
            }

            commentText.maxVisibleCharacters = totalChars;
        }

        _isTyping = false;

        // Pokaż prompt [E]
        if (continuePromptGroup != null)
        {
            continuePromptGroup.DOFade(1f, 0.2f).SetUpdate(true);
        }

        // Czekaj na wciśnięcie E / LPM
        yield return new WaitForSecondsRealtime(0.1f);
        _continuePressed = false;

        while (!_continuePressed)
        {
            yield return null;
        }

        Close();
    }

    /// <summary>
    /// Zamyka okno inspekcji i przywraca grę.
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        // Dźwięk zamknięcia
        if (!string.IsNullOrEmpty(closeSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(closeSound);
        }

        // Animacja wygaszania
        _fadeTween?.Kill();
        if (mainCanvasGroup != null)
        {
            _fadeTween = mainCanvasGroup.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    HideInstant();
                    FinishClose();
                });
        }
        else
        {
            HideInstant();
            FinishClose();
        }
    }

    private void FinishClose()
    {
        if (ItemShowcaseStudio.Instance != null)
        {
            ItemShowcaseStudio.Instance.DeactivateStudio();
        }

        // Przywróć sterowanie i czas
        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchToPlayer();
        }

        if (GameTimeController.Instance != null)
        {
            GameTimeController.Instance.Resume();
        }

        // Wywołaj callback zamknięcia (np. podniesienie do rąk gracza)
        Action cb = _onCloseCallback;
        _onCloseCallback = null;
        cb?.Invoke();
    }

    public void HideInstant()
    {
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.blocksRaycasts = false;
            mainCanvasGroup.interactable = false;
        }

        if (continuePromptGroup != null)
        {
            continuePromptGroup.alpha = 0f;
        }

        if (backgroundDimmer != null)
        {
            backgroundDimmer.alpha = 0f;
        }

        if (commentText != null)
        {
            commentText.text = string.Empty;
        }

        if (titleText != null)
        {
            titleText.text = string.Empty;
        }

        _isOpen = false;
        _isTyping = false;
    }
}
