using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Uniwersalny i w 100% odporny na błędy ScreenFader (płynne ściemnianie/rozjaśnianie ekranu i zmiana scen).
/// Działa niezawodnie w Edytorze i w skompilowanym Buildzie.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader _instance;
    public static ScreenFader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ScreenFader>();
                if (_instance == null)
                {
                    CreateRuntimeFader();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Fade Settings")]
    [SerializeField] private float defaultFadeDuration = 0.4f;

    [Header("Startup")]
    [SerializeField] private bool fadeInOnStart = true;

    private Tween _fadeTween;
    private bool _isTransitioning = false;
    private Coroutine _currentRoutine;

    public bool IsTransitioning => _isTransitioning;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
    }
#endif

    private void Awake()
    {
        if (_instance != null && _instance != this && _instance.gameObject != null)
        {
            // Jeśli już istnieje instancja z DontDestroyOnLoad, usuń duplikat
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureRootDontDestroyOnLoad();
        EnsureCanvasGroup();

        if (canvasGroup != null)
        {
            if (fadeInOnStart)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }
    }

    private void Start()
    {
        if (fadeInOnStart && canvasGroup != null && canvasGroup.alpha > 0.01f)
        {
            FadeIn(defaultFadeDuration);
        }
    }

    private void EnsureRootDontDestroyOnLoad()
    {
        Transform rootTransform = transform.root;
        if (rootTransform != null)
        {
            DontDestroyOnLoad(rootTransform.gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }

        // Jeśli canvasGroup jest na innym roocie, również go zachowaj
        if (canvasGroup != null && canvasGroup.transform.root != rootTransform)
        {
            DontDestroyOnLoad(canvasGroup.transform.root.gameObject);
        }
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInChildren<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            // Znajdź ScreenFader_Canvas w scenie
            var foundCanvas = GameObject.Find("ScreenFader_Canvas");
            if (foundCanvas != null)
            {
                canvasGroup = foundCanvas.GetComponentInChildren<CanvasGroup>();
            }
        }

        if (canvasGroup == null)
        {
            // Stwórz dynamiczny canvas nakładki jeśli żaden nie istnieje
            GameObject faderCanvasGo = new GameObject("ScreenFader_Canvas_Dynamic", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = faderCanvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Najwyższy sorting order

            var scaler = faderCanvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject blackImgGo = new GameObject("BlackOverlay", typeof(RectTransform), typeof(Image));
            blackImgGo.transform.SetParent(faderCanvasGo.transform, false);
            var rect = blackImgGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var img = blackImgGo.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;

            canvasGroup = faderCanvasGo.GetComponent<CanvasGroup>();
            DontDestroyOnLoad(faderCanvasGo);
        }
    }

    private static void CreateRuntimeFader()
    {
        GameObject faderGo = new GameObject("ScreenFader_Runtime", typeof(ScreenFader));
        _instance = faderGo.GetComponent<ScreenFader>();
        _instance.EnsureCanvasGroup();
        DontDestroyOnLoad(faderGo);
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
        EnsureCanvasGroup();
        KillCurrentTween();

        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            return;
        }

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

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
        EnsureCanvasGroup();
        KillCurrentTween();

        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            return;
        }

        _fadeTween = canvasGroup
            .DOFade(0f, duration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (canvasGroup != null)
                {
                    canvasGroup.blocksRaycasts = false;
                    canvasGroup.interactable = false;
                }
                onComplete?.Invoke();
            });
    }

    // -------------------------------------------------------
    // GENERIC TRANSITION
    // -------------------------------------------------------

    public void Transition(Action middleAction, float duration = -1f)
    {
        float dur = duration > 0 ? duration : defaultFadeDuration;
        if (_currentRoutine != null) StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(TransitionRoutine(middleAction, dur));
    }

    private IEnumerator TransitionRoutine(Action middleAction, float duration)
    {
        _isTransitioning = true;
        bool fadeFinished = false;

        FadeOut(duration, () => fadeFinished = true);

        float timeout = duration + 1.0f;
        while (!fadeFinished && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        middleAction?.Invoke();

        yield return null;

        fadeFinished = false;
        FadeIn(duration, () =>
        {
            fadeFinished = true;
            _isTransitioning = false;
        });

        timeout = duration + 1.0f;
        while (!fadeFinished && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        _isTransitioning = false;
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
        // Anuluj poprzednią procedurę jeśli trwała, aby wymusić nowe przejście
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
        }

        _currentRoutine = StartCoroutine(LoadSceneRoutine(sceneName, duration > 0 ? duration : defaultFadeDuration));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, float duration)
    {
        _isTransitioning = true;
        Time.timeScale = 1f;

        bool fadeFinished = false;
        FadeOut(duration, () => fadeFinished = true);

        // Oczekiwanie na zakończenie zaciemniania z zabezpieczeniem czasowym
        float timeout = duration + 1.5f;
        while (!fadeFinished && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Upewnij się, że ekran jest całkowicie czarny przed przełączeniem sceny
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        // Ładowanie sceny asynchronicznie
        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScreenFader] Błąd podczas ładowania asynchronicznego sceny '{sceneName}': {ex.Message}");
        }

        if (operation != null)
        {
            while (!operation.isDone)
            {
                yield return null;
            }
        }
        else
        {
            // Fallback: próba załadowania synchronicznego
            Debug.LogWarning($"[ScreenFader] Próba załadowania synchronicznego sceny '{sceneName}'...");
            try
            {
                SceneManager.LoadScene(sceneName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScreenFader] Krytyczny błąd: nie można załadować sceny '{sceneName}': {ex.Message}");
            }
        }

        // Odczekaj jedną klatkę po załadowaniu nowej sceny, aby obiekty w Awake/Start zdążyły się zainicjalizować
        yield return null;

        // Przywróć czas na wypadek gdyby gra była zapauzowana
        Time.timeScale = 1f;

        // Płynne rozjaśnienie ekranu w nowej scenie
        fadeFinished = false;
        FadeIn(duration, () =>
        {
            fadeFinished = true;
            _isTransitioning = false;
        });

        timeout = duration + 1.5f;
        while (!fadeFinished && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        _isTransitioning = false;
        _currentRoutine = null;
    }

    private void KillCurrentTween()
    {
        if (_fadeTween != null && _fadeTween.IsActive())
        {
            _fadeTween.Kill();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        KillCurrentTween();
    }
}