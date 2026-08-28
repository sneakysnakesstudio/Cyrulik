using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zarządza sekwencją startową (Intro):
/// 1. Czarny ekran z datą i zegarem tykającym w czasie.
/// 2. Elegancki panel Tutorialu i Klawiszologii (Controls / How to Play).
/// 3. Płynne rozjaśnienie do widoku z oczu gracza (odblokowanie sterowania).
/// </summary>
public class IntroSequence : MonoBehaviour
{
    [Header("Gracz (Do zablokowania)")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHands playerHands;

    [Header("UI Intro")]
    [Tooltip("CanvasGroup z przypisanym czarnym tłem oraz tekstem czasu.")]
    [SerializeField] private CanvasGroup introCanvasGroup;
    
    [Tooltip("Panel zegara/czasu, który po intrze zostaje wyłączony.")]
    [SerializeField] private GameObject clockUIToHide;

    [Header("UI Tutorial / Sterowanie (Controls)")]
    [Tooltip("CanvasGroup ekranu z tutorialem i klawiszologią. Jeśli puste, zostanie wygenerowane automatycznie.")]
    [SerializeField] private CanvasGroup controlsCanvasGroup;

    [Tooltip("Obraz / Grafika karty tutorialu ze sterowaniem (np. Assets/Art/Tutorial_Controls.jpg).")]
    [SerializeField] private Sprite tutorialCardImageSprite;

    [Tooltip("Ile sekund ma być wyświetlany ekran tutorialu przed rozjaśnieniem gry.")]
    [SerializeField] private float waitOnControlsScreen = 8f;

    [Tooltip("Czy gracz może nacisnąć dowolny klawisz (Spacja, Enter, E, LPM itp.), aby pominąć tutorial i zacząć grę?")]
    [SerializeField] private bool allowSkipControlsWithKey = true;

    [Tooltip("Czas trwania wejścia/wyjścia (fade) dla ekranu tutorialu.")]
    [SerializeField] private float controlsFadeDuration = 0.45f;

    [Header("Timings")]
    [Tooltip("Ile sekund gracz patrzy na czarny ekran z uciekającym czasem.")]
    [SerializeField] private float waitOnBlackScreen = 4.5f;
    
    [Tooltip("Jak długo trwa przejście (fade) z czarnego ekranu do widoku z oczu gracza.")]
    [SerializeField] private float fadeDuration = 2.0f;

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

        EnsureControlsTutorialUI();

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
        // 1. Zablokuj gracza na start
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerHands != null) playerHands.enabled = false;

        // 2. Aktywuj panel zegara/napisów
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

        // Opcjonalne uruchomienie dźwięku zegara
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

        // 3. Czekaj na czarnym ekranie z tykającym czasem
        yield return new WaitForSeconds(waitOnBlackScreen);

        // 4. Ukryj panel zegara
        if (clockUIToHide != null)
        {
            clockUIToHide.SetActive(false);
        }

        // 5. Wyłącz cykanie jeśli wymagane
        if (stopTickingAfterIntro && GameTimeController.Instance != null)
        {
            GameTimeController.Instance.SetTickingEnabled(false);
        }

        // 6. FAZA TUTORIALU I KLAWISZOLOGII
        if (controlsCanvasGroup != null)
        {
            controlsCanvasGroup.gameObject.SetActive(true);

            // Fade in tutorial
            float fadeElapsed = 0f;
            while (fadeElapsed < controlsFadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                controlsCanvasGroup.alpha = Mathf.Lerp(0f, 1f, fadeElapsed / controlsFadeDuration);
                yield return null;
            }
            controlsCanvasGroup.alpha = 1f;
            controlsCanvasGroup.blocksRaycasts = true;

            // Czekamy na upływ czasu LUB naciśnięcie dowolnego klawisza
            float controlsTimer = 0f;
            while (controlsTimer < waitOnControlsScreen)
            {
                controlsTimer += Time.deltaTime;

                if (allowSkipControlsWithKey && controlsTimer > 0.3f)
                {
                    if (WasAnyKeyPressed())
                    {
                        break;
                    }
                }

                yield return null;
            }

            // Fade out tutorial
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

    private bool WasAnyKeyPressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.anyKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                return true;
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Buduje elegancki ekran Tutorialu i Klawiszologii, jeśli nie został przypisany w Inspektorze.
    /// </summary>
    private void EnsureControlsTutorialUI()
    {
        if (controlsCanvasGroup != null) return;

        // 1. Sprawdź, czy w scenie istnieje już obiekt Controls_Panel
        var found = GameObject.Find("Controls_Panel");
        if (found != null && found.TryGetComponent<CanvasGroup>(out var cg))
        {
            controlsCanvasGroup = cg;
            return;
        }

        // 2. Utwórz Canvas dla Tutorialu
        GameObject canvasGo = new GameObject("Tutorial_Controls_Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // 3. Panel tła
        GameObject panelGo = new GameObject("Controls_Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.04f, 0.04f, 0.94f);

        controlsCanvasGroup = panelGo.GetComponent<CanvasGroup>();
        controlsCanvasGroup.alpha = 0f;
        controlsCanvasGroup.interactable = false;
        controlsCanvasGroup.blocksRaycasts = false;

        // 4. Sprawdź i załaduj grafikę karty tutorialu
        if (tutorialCardImageSprite == null)
        {
            string imagePath = System.IO.Path.Combine(Application.dataPath, "Art", "Tutorial_Controls.jpg");
            if (System.IO.File.Exists(imagePath))
            {
                try
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(imagePath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(fileData))
                    {
                        tutorialCardImageSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[IntroSequence] Nie udało się załadować grafiki tutorialu: {ex.Message}");
                }
            }
        }

        if (tutorialCardImageSprite != null)
        {
            GameObject cardGo = new GameObject("Tutorial_Card_Image", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(panelGo.transform, false);

            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(1320f, 742f);
            cardRect.anchoredPosition = new Vector2(0f, 20f);

            var cardImg = cardGo.GetComponent<Image>();
            cardImg.sprite = tutorialCardImageSprite;
            cardImg.preserveAspect = true;

            // Podpowiedź na dole
            GameObject hintGo = new GameObject("Hint_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(panelGo.transform, false);
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0.05f);
            hintRect.anchorMax = new Vector2(0.5f, 0.05f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.sizeDelta = new Vector2(900f, 50f);
            hintRect.anchoredPosition = Vector2.zero;

            var hintTmp = hintGo.GetComponent<TextMeshProUGUI>();
            hintTmp.text = "Naciśnij [ SPACJĘ / DOWOLNY KLAWISZ ], aby rozpocząć grę...";
            hintTmp.fontSize = 24f;
            hintTmp.fontStyle = FontStyles.Italic;
            hintTmp.alignment = TextAlignmentOptions.Center;
            hintTmp.color = new Color(0.9f, 0.85f, 0.75f, 0.95f);

            return;
        }

        // 5. Fallback: Główna ramka kontenera z tekstem
        GameObject boxGo = new GameObject("Tutorial_Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        boxGo.transform.SetParent(panelGo.transform, false);

        var boxRect = boxGo.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(960f, 680f);

        var boxImage = boxGo.GetComponent<Image>();
        boxImage.color = new Color(0.12f, 0.11f, 0.10f, 0.96f);

        var layout = boxGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(45, 45, 35, 35);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Tytuł: TUTORIAL & STEROWANIE
        CreateText(boxGo.transform, "TUTORIAL & STEROWANIE", 38f, FontStyles.Bold, new Color(0.96f, 0.82f, 0.51f, 1f), 50f);

        CreateDivider(boxGo.transform);

        // Wiersze klawiszy
        CreateRow(boxGo.transform, "W  S  A  D", "Poruszanie się / Chodzenie");
        CreateRow(boxGo.transform, "MYSZKA", "Rozglądanie się");
        CreateRow(boxGo.transform, "E   /   LPM", "Interakcja (Podnieś / Użyj / Otwórz)");
        CreateRow(boxGo.transform, "G", "Upuszczenie trzymanego przedmiotu");
        CreateRow(boxGo.transform, "SHIFT", "Bieg (Sprint)");
        CreateRow(boxGo.transform, "ESC", "Menu pauzy");

        CreateDivider(boxGo.transform);

        // Wskazówki rozgrywki (Barber Guide)
        CreateText(boxGo.transform, "CEL: Przygotuj salon przed przyjściem klienta (rozpal piec, zagotuj wodę, przygotuj ręczniki i pułapkę na myszy).", 20f, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f, 0.95f), 45f);

        // Podpowiedź na dole
        CreateText(boxGo.transform, "Naciśnij [ SPACJĘ / DOWOLNY KLAWISZ ], aby rozpocząć...", 22f, FontStyles.Italic, new Color(0.72f, 0.72f, 0.72f, 0.85f), 35f);
    }

    private static void CreateRow(Transform parent, string key, string desc)
    {
        GameObject rowGo = new GameObject("Row_" + key, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(parent, false);

        var hLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 20f;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = true;

        rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(860f, 40f);

        // Klawisz
        GameObject keyGo = new GameObject("Key", typeof(RectTransform), typeof(TextMeshProUGUI));
        keyGo.transform.SetParent(rowGo.transform, false);
        var keyTmp = keyGo.GetComponent<TextMeshProUGUI>();
        keyTmp.text = $"[ {key} ]";
        keyTmp.fontSize = 24f;
        keyTmp.fontStyle = FontStyles.Bold;
        keyTmp.alignment = TextAlignmentOptions.Left;
        keyTmp.color = new Color(0.96f, 0.82f, 0.51f, 1f);
        keyGo.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 40f);

        // Opis
        GameObject descGo = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGo.transform.SetParent(rowGo.transform, false);
        var descTmp = descGo.GetComponent<TextMeshProUGUI>();
        descTmp.text = desc;
        descTmp.fontSize = 22f;
        descTmp.alignment = TextAlignmentOptions.Left;
        descTmp.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        descGo.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 40f);
    }

    private static void CreateText(Transform parent, string text, float size, FontStyles style, Color color, float height)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(860f, height);
    }

    private static void CreateDivider(Transform parent)
    {
        GameObject divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divGo.transform.SetParent(parent, false);
        var divImg = divGo.GetComponent<Image>();
        divImg.color = new Color(0.35f, 0.32f, 0.28f, 0.7f);
        divGo.GetComponent<RectTransform>().sizeDelta = new Vector2(860f, 2f);
    }
}
