using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Fade Settings")]
    [SerializeField] private float defaultFadeDuration = 0.5f;

    [Header("Startup")]
    [SerializeField] private bool fadeInOnStart = true;

    private Tween _fadeTween;
    private bool _isTransitioning;

    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (canvasGroup == null)
        {
            Debug.LogError("[ScreenFader] CanvasGroup is not assigned!", this);
            return;
        }

        if (fadeInOnStart)
        {
            canvasGroup.alpha = 1f;
        }
        else
        {
            canvasGroup.alpha = 0f;
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void Start()
    {
        if (fadeInOnStart)
        {
            FadeIn();
        }
    }

    // -------------------------------------------------------
    // BASIC FADE
    // -------------------------------------------------------

    public void FadeOut(Action onComplete = null)
    {
        FadeOut(defaultFadeDuration, onComplete);
    }

    public void FadeOut(float duration, Action onComplete = null)
    {
        KillCurrentTween();

        canvasGroup.blocksRaycasts = true;

        _fadeTween = canvasGroup
            .DOFade(1f, duration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                onComplete?.Invoke();
            });
    }

    public void FadeIn(Action onComplete = null)
    {
        FadeIn(defaultFadeDuration, onComplete);
    }

    public void FadeIn(float duration, Action onComplete = null)
    {
        KillCurrentTween();

        _fadeTween = canvasGroup
            .DOFade(0f, duration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                canvasGroup.blocksRaycasts = false;

                onComplete?.Invoke();
            });
    }

    // -------------------------------------------------------
    // GENERIC TRANSITION
    // Fade Out -> Action -> Fade In
    // -------------------------------------------------------

    public void Transition(Action middleAction)
    {
        Transition(middleAction, defaultFadeDuration);
    }

    public void Transition(Action middleAction, float duration)
    {
        if (_isTransitioning)
            return;

        _isTransitioning = true;

        FadeOut(duration, () =>
        {
            middleAction?.Invoke();

            FadeIn(duration, () =>
            {
                _isTransitioning = false;
            });
        });
    }

    // -------------------------------------------------------
    // SCENE TRANSITION
    // -------------------------------------------------------

    public void LoadScene(string sceneName)
    {
        LoadScene(sceneName, defaultFadeDuration);
    }

    public void LoadScene(string sceneName, float duration)
    {
        if (_isTransitioning)
            return;

        StartCoroutine(LoadSceneRoutine(sceneName, duration));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, float duration)
    {
        _isTransitioning = true;

        bool fadeFinished = false;

        FadeOut(duration, () =>
        {
            fadeFinished = true;
        });

        while (!fadeFinished)
        {
            yield return null;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (!operation.isDone)
        {
            yield return null;
        }

        FadeIn(duration, () =>
        {
            _isTransitioning = false;
        });
    }

    // -------------------------------------------------------

    private void KillCurrentTween()
    {
        if (_fadeTween != null && _fadeTween.IsActive())
        {
            _fadeTween.Kill();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        KillCurrentTween();
    }
}