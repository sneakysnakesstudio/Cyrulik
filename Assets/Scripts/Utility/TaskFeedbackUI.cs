using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// System wizualno-dźwiękowego powiadomienia o wykonaniu zadania (Opcja 1: Bursztynowy Rozbłysk & Pieczęć).
/// - Bursztynowy/złoty rozbłysk winiety (Amber Flash)
/// - Stylowy retro baner z nazwą zadania wjeżdżający z góry ekranu
/// - Satysfakcjonujący dźwięk zaliczenia (AudioManager)
/// - Automatycznie nasłuchuje zdarzeń z PreparationStateManager
/// </summary>
public class TaskFeedbackUI : MonoBehaviour
{
    public static TaskFeedbackUI Instance { get; private set; }

    [Header("Bursztynowy Rozbłysk (Amber Flash)")]
    [Tooltip("CanvasGroup dla efektu rozbłysku/winiety.")]
    [SerializeField] private CanvasGroup flashCanvasGroup;

    [Tooltip("Obraz rozbłysku (winieta lub pełny kolor).")]
    [SerializeField] private Image flashImage;

    [Tooltip("Kolor rozbłysku (domyślnie ciepły złocisto-bursztynowy).")]
    [SerializeField] private Color flashColor = new Color(1f, 0.72f, 0.22f, 0.45f);

    [Tooltip("Czas rozjaśnienia rozbłysku w sekundach.")]
    [SerializeField] private float flashFadeInDuration = 0.12f;

    [Tooltip("Czas wygaszania rozbłysku w sekundach.")]
    [SerializeField] private float flashFadeOutDuration = 0.45f;

    [Header("Retro Baner Powiadomienia")]
    [Tooltip("Główny CanvasGroup banera notyfikacji.")]
    [SerializeField] private CanvasGroup bannerCanvasGroup;

    [Tooltip("RectTransform banera (do animacji wysuwania z góry).")]
    [SerializeField] private RectTransform bannerRectTransform;

    [Tooltip("Tekst nagłówka (np. '✓ ZADANIE ZALICZONE').")]
    [SerializeField] private TextMeshProUGUI headerText;

    [Tooltip("Tekst nazwy wykonanego zadania.")]
    [SerializeField] private TextMeshProUGUI taskNameText;

    [Tooltip("Domyślny tekst nagłówka.")]
    [SerializeField] private string defaultHeader = "✓ ZADANIE ZALICZONE";

    [Header("Animacja Banera")]
    [Tooltip("Wysokość początkowa (ukryta za górną krawędzią ekranu).")]
    [SerializeField] private float hiddenPosY = 90f;

    [Tooltip("Wysokość docelowa (widoczna na ekranie).")]
    [SerializeField] private float visiblePosY = -45f;

    [Tooltip("Czas wjeżdżania banera z góry.")]
    [SerializeField] private float bannerInDuration = 0.4f;

    [Tooltip("Czas pozostawania banera na ekranie.")]
    [SerializeField] private float bannerDisplayDuration = 2.8f;

    [Tooltip("Czas zanikania/chowania banera.")]
    [SerializeField] private float bannerOutDuration = 0.35f;

    [Header("Audio")]
    [Tooltip("Nazwa dźwięku/grupy w AudioManager.")]
    [SerializeField] private string soundTaskComplete = "task_complete";

    [Tooltip("Opcjonalny bezpośredni AudioClip jako fallback (jeśli brak w AudioManager).")]
    [SerializeField] private AudioClip customCompleteClip;
    [SerializeField] private AudioSource customAudioSource;

    private readonly Queue<string> _taskQueue = new Queue<string>();
    private bool _isShowingNotification = false;

    private Tween _flashTween;
    private Tween _bannerMoveTween;
    private Tween _bannerFadeTween;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Jeśli komponenty UI nie zostały przypisane w Inspectorze, wygenerujmy je automatycznie
        if (flashCanvasGroup == null || bannerCanvasGroup == null)
        {
            BuildDefaultUI();
        }

        HideInstant();
    }

    private void Start()
    {
        // Podpięcie pod zdarzenia managera zadań
        if (PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.OnTaskStateChanged += OnTaskStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.OnTaskStateChanged -= OnTaskStateChanged;
        }

        KillAllTweens();
    }

    private void OnTaskStateChanged(string taskId, bool isCompleted)
    {
        // Reagujemy tylko na ukończenie zadania (stan true)
        if (!isCompleted) return;

        string displayName = taskId;

        // Pobieramy ładną nazwę zadania z PreparationStateManager
        if (PreparationStateManager.Instance != null)
        {
            foreach (var task in PreparationStateManager.Instance.Tasks)
            {
                if (task != null && string.Equals(task.taskId, taskId, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(task.displayName))
                    {
                        displayName = task.displayName;
                    }
                    break;
                }
            }
        }

        ShowTaskSuccess(displayName);
    }

    /// <summary>
    /// Wyświetla efekt sukcesu i powiadomienie o ukończeniu zadania.
    /// </summary>
    public void ShowTaskSuccess(string taskDisplayName, string customHeader = null)
    {
        if (string.IsNullOrWhiteSpace(taskDisplayName)) return;

        // Zawsze natychmiast odpalamy rozbłysk i dźwięk
        TriggerAmberFlash();
        PlayCompletionAudio();

        // Dodajemy do kolejki banerów
        _taskQueue.Enqueue(taskDisplayName);

        if (!_isShowingNotification)
        {
            StartCoroutine(ProcessNotificationQueue(customHeader));
        }
    }

    private IEnumerator ProcessNotificationQueue(string customHeader = null)
    {
        _isShowingNotification = true;

        while (_taskQueue.Count > 0)
        {
            string taskName = _taskQueue.Dequeue();

            if (headerText != null)
                headerText.text = !string.IsNullOrEmpty(customHeader) ? customHeader : defaultHeader;

            if (taskNameText != null)
                taskNameText.text = taskName;

            // Animacja wejścia banera
            AnimateBannerIn();

            yield return new WaitForSeconds(bannerInDuration + bannerDisplayDuration);

            // Animacja wyjścia banera
            AnimateBannerOut();

            yield return new WaitForSeconds(bannerOutDuration + 0.1f);
        }

        _isShowingNotification = false;
    }

    private void TriggerAmberFlash()
    {
        if (flashCanvasGroup == null) return;

        _flashTween?.Kill();

        if (flashImage != null)
            flashImage.color = flashColor;

        flashCanvasGroup.alpha = 0f;

        Sequence flashSeq = DOTween.Sequence();
        flashSeq.Append(flashCanvasGroup.DOFade(1f, flashFadeInDuration).SetEase(Ease.OutQuad));
        flashSeq.Append(flashCanvasGroup.DOFade(0f, flashFadeOutDuration).SetEase(Ease.InQuad));
        flashSeq.SetLink(flashCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy);

        _flashTween = flashSeq;
    }

    private void AnimateBannerIn()
    {
        if (bannerCanvasGroup == null || bannerRectTransform == null) return;

        _bannerMoveTween?.Kill();
        _bannerFadeTween?.Kill();

        bannerRectTransform.anchoredPosition = new Vector2(bannerRectTransform.anchoredPosition.x, hiddenPosY);
        bannerCanvasGroup.alpha = 0f;

        _bannerMoveTween = bannerRectTransform
            .DOAnchorPosY(visiblePosY, bannerInDuration)
            .SetEase(Ease.OutBack)
            .SetLink(bannerRectTransform.gameObject, LinkBehaviour.KillOnDestroy);

        _bannerFadeTween = bannerCanvasGroup
            .DOFade(1f, bannerInDuration * 0.7f)
            .SetEase(Ease.OutQuad)
            .SetLink(bannerCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void AnimateBannerOut()
    {
        if (bannerCanvasGroup == null || bannerRectTransform == null) return;

        _bannerMoveTween?.Kill();
        _bannerFadeTween?.Kill();

        _bannerMoveTween = bannerRectTransform
            .DOAnchorPosY(hiddenPosY, bannerOutDuration)
            .SetEase(Ease.InQuad)
            .SetLink(bannerRectTransform.gameObject, LinkBehaviour.KillOnDestroy);

        _bannerFadeTween = bannerCanvasGroup
            .DOFade(0f, bannerOutDuration)
            .SetEase(Ease.InQuad)
            .SetLink(bannerCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void PlayCompletionAudio()
    {
        if (!string.IsNullOrEmpty(soundTaskComplete) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundTaskComplete);
            return;
        }

        if (customCompleteClip != null)
        {
            if (customAudioSource != null)
            {
                customAudioSource.PlayOneShot(customCompleteClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(customCompleteClip, Camera.main != null ? Camera.main.transform.position : transform.position);
            }
        }
    }

    private void HideInstant()
    {
        KillAllTweens();

        if (flashCanvasGroup != null)
        {
            flashCanvasGroup.alpha = 0f;
            flashCanvasGroup.blocksRaycasts = false;
        }

        if (bannerCanvasGroup != null)
        {
            bannerCanvasGroup.alpha = 0f;
            bannerCanvasGroup.blocksRaycasts = false;
        }

        if (bannerRectTransform != null)
        {
            bannerRectTransform.anchoredPosition = new Vector2(bannerRectTransform.anchoredPosition.x, hiddenPosY);
        }
    }

    private void KillAllTweens()
    {
        _flashTween?.Kill();
        _bannerMoveTween?.Kill();
        _bannerFadeTween?.Kill();
    }

    /// <summary>
    /// Automatycznie tworzy hierarchię UI w przypadku, gdy skrypt został dodany do pustego obiektu w scenie.
    /// </summary>
    private void BuildDefaultUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("TaskFeedback_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // 1. Flash Overlay
        GameObject flashGo = new GameObject("AmberFlash_Overlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        flashGo.transform.SetParent(canvas.transform, false);
        var flashRect = flashGo.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.sizeDelta = Vector2.zero;

        flashCanvasGroup = flashGo.GetComponent<CanvasGroup>();
        flashCanvasGroup.blocksRaycasts = false;
        flashCanvasGroup.alpha = 0f;

        flashImage = flashGo.GetComponent<Image>();
        flashImage.color = flashColor;
        flashImage.raycastTarget = false;

        // 2. Banner Container
        GameObject bannerGo = new GameObject("TaskBanner_Container", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        bannerGo.transform.SetParent(canvas.transform, false);
        bannerRectTransform = bannerGo.GetComponent<RectTransform>();
        bannerRectTransform.anchorMin = new Vector2(0.5f, 1f);
        bannerRectTransform.anchorMax = new Vector2(0.5f, 1f);
        bannerRectTransform.pivot = new Vector2(0.5f, 1f);
        bannerRectTransform.sizeDelta = new Vector2(560f, 74f);
        bannerRectTransform.anchoredPosition = new Vector2(0f, hiddenPosY);

        bannerCanvasGroup = bannerGo.GetComponent<CanvasGroup>();
        bannerCanvasGroup.blocksRaycasts = false;
        bannerCanvasGroup.alpha = 0f;

        var bannerBg = bannerGo.GetComponent<Image>();
        bannerBg.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);

        // Ramka złota (Outline/Border)
        var outline = bannerGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Header Text
        GameObject headerGo = new GameObject("Header_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerGo.transform.SetParent(bannerGo.transform, false);
        var headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 0.5f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.offsetMin = new Vector2(16f, 0f);
        headerRect.offsetMax = new Vector2(-16f, -6f);

        headerText = headerGo.GetComponent<TextMeshProUGUI>();
        headerText.text = defaultHeader;
        headerText.fontSize = 15f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = new Color(0.98f, 0.82f, 0.35f, 1f);
        headerText.alignment = TextAlignmentOptions.Center;

        // Task Name Text
        GameObject nameGo = new GameObject("TaskName_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(bannerGo.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.offsetMin = new Vector2(16f, 6f);
        nameRect.offsetMax = new Vector2(-16f, 0f);

        taskNameText = nameGo.GetComponent<TextMeshProUGUI>();
        taskNameText.text = "Zadanie wykonane";
        taskNameText.fontSize = 20f;
        taskNameText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        taskNameText.alignment = TextAlignmentOptions.Center;
    }
}
