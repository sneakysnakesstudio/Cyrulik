using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Automatyczny menedżer warstw Canvasów (Sorting Orders).
/// Rozwiązuje problem zasłaniania UI (dialogów, celownika, minigier) przez pełnoekranowy efekt RenderTexture / Dithering.
/// Działa automatycznie przy starcie sceny i przy każdym przeładowaniu sceny zarówno w Edytorze, jak i w Buildzie.
/// </summary>
public static class CanvasLayerManager
{
    public const int LAYER_BACKGROUND_RENDER = 0;   // Pełnoekranowy 3D World Render / Dithering
    public const int LAYER_CROSSHAIR_HUD     = 10;  // Celownik i HUD gracza
    public const int LAYER_MINIGAME          = 20;  // Minigra ostrzenia brzytwy
    public const int LAYER_DIALOGUES         = 30;  // Dialogi NPC i myśli wewnętrzne
    public const int LAYER_PAUSE_MENU        = 50;  // Menu pauzy (ESC)
    public const int LAYER_TASK_FEEDBACK     = 95;  // Powiadomienia o zadaniach
    public const int LAYER_END_SUMMARY       = 999; // Ekran końcowy
    public const int LAYER_SCREEN_FADER      = 9999;// Płynne ściemnianie ekranu (fade transitions)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OrganizeCanvases();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        OrganizeCanvases();
    }

    /// <summary>
    /// Główna metoda porządkująca wszystkie Canvasy w scenie.
    /// </summary>
    public static void OrganizeCanvases()
    {
        // 1. Wyizoluj obiekt 'Render' (RawImage z efektem ditheringu) na osobny Canvas w tle (Layer 0)
        SetupBackgroundRenderCanvas();

        // 2. Skonfiguruj CrossHair_Canvas (Layer 10)
        SetupCanvas("CrossHair_Canvas", LAYER_CROSSHAIR_HUD);

        // 3. Skonfiguruj Minigame_Razor_Canvas (Layer 20)
        SetupCanvas("Minigame_Razor_Canvas", LAYER_MINIGAME);

        // 4. Skonfiguruj DialogueCanvas (Layer 30)
        SetupDialogueCanvas();

        // 5. Skonfiguruj PauseMenu_Canvas (Layer 50)
        SetupCanvas("PauseMenu_Canvas", LAYER_PAUSE_MENU);
    }

    private static void SetupBackgroundRenderCanvas()
    {
        // Znajdź obiekt 'Render'
        GameObject renderGo = GameObject.Find("Render");
        if (renderGo == null)
        {
            // Może być dzieckiem wyłączonego obiektu
            var allRawImages = Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var raw in allRawImages)
            {
                if (raw.gameObject.name == "Render" || (raw.texture != null && raw.texture.name.Contains("RenderingTexture")))
                {
                    renderGo = raw.gameObject;
                    break;
                }
            }
        }

        if (renderGo != null)
        {
            if (renderGo.TryGetComponent<RawImage>(out var rawImg))
            {
                rawImg.raycastTarget = false; // Nie blokuj kliknięć myszy
            }

            // Sprawdź rodzica - jeśli jest pod PauseMenu_Canvas lub innym Canvasem UI, przenieś na osobny Background_Render_Canvas
            Transform parent = renderGo.transform.parent;
            bool needsReparent = (parent == null) || (parent.name.Contains("Pause") || parent.name.Contains("Dialogue") || parent.name.Contains("CrossHair"));

            if (needsReparent)
            {
                GameObject bgCanvasGo = GameObject.Find("Background_Render_Canvas");
                if (bgCanvasGo == null)
                {
                    bgCanvasGo = new GameObject("Background_Render_Canvas", typeof(Canvas), typeof(CanvasScaler));
                }

                Canvas bgCanvas = bgCanvasGo.GetComponent<Canvas>();
                bgCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                bgCanvas.overrideSorting = true;
                bgCanvas.sortingOrder = LAYER_BACKGROUND_RENDER;

                CanvasScaler scaler = bgCanvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                renderGo.transform.SetParent(bgCanvasGo.transform, false);

                var rt = renderGo.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = Vector2.zero;
                    rt.localScale = Vector3.one;
                }
            }
            else
            {
                // Jeśli już jest na osobnym obiekcie, upewnij się, że jego Canvas ma layer 0
                Canvas c = renderGo.GetComponentInParent<Canvas>();
                if (c != null && !c.name.Contains("Pause"))
                {
                    c.overrideSorting = true;
                    c.sortingOrder = LAYER_BACKGROUND_RENDER;
                }
            }
        }
    }

    private static void SetupDialogueCanvas()
    {
        var canvasGo = GameObject.Find("DialogueCanvas");
        if (canvasGo != null)
        {
            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = LAYER_DIALOGUES;
            }

            // Upewnij się, że stary czarny fader (BlackImage) nie zasłania ekranu
            Transform blackImg = canvasGo.transform.Find("BlackImage");
            if (blackImg != null && blackImg.gameObject.activeSelf)
            {
                blackImg.gameObject.SetActive(false);
            }
        }
    }

    private static void SetupCanvas(string canvasName, int sortingOrder)
    {
        var canvasGo = GameObject.Find(canvasName);
        if (canvasGo != null)
        {
            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = sortingOrder;
            }
        }
    }
}
