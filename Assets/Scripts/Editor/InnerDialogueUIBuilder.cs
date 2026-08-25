#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InnerDialogueUIBuilder
{
    [MenuItem("Tools/Cyrulik/Create Full Dialogue System (Thoughts + Client)", false, 1)]
    [MenuItem("GameObject/UI/Cyrulik - Full Dialogue System", false, 10)]
    public static void CreateFullDialogueSystem()
    {
        Canvas canvas = GetOrCreateCanvas();

        // 1. Stwórz Chmurkę Myśli
        GameObject thoughtBubble = CreateThoughtBubble(canvas.gameObject);

        // 2. Stwórz Prostokątną Belkę Klienta
        GameObject clientBox = CreateClientDialogueBox(canvas.gameObject);

        // 3. Stwórz DialogueManager
        GameObject managerGo = new GameObject("DialogueManager", typeof(DialogueManager));
        DialogueManager dm = managerGo.GetComponent<DialogueManager>();

        SerializedObject so = new SerializedObject(dm);
        so.FindProperty("innerThoughtsUI").objectReferenceValue = thoughtBubble.GetComponent<InnerDialogueUI>();
        so.FindProperty("clientDialogueUI").objectReferenceValue = clientBox.GetComponent<ClientDialogueUI>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(managerGo, "Create Dialogue Manager");
        Selection.activeGameObject = managerGo;

        Debug.Log("[DialogueUIBuilder] Pomyślnie utworzono pełny system dialogów (Chmurka Myśli + Dialog Klienta + DialogueManager)!");
    }

    [MenuItem("Tools/Cyrulik/Create Thought Bubble (Inner Thoughts)", false, 2)]
    public static void CreateThoughtBubbleMenu()
    {
        Canvas canvas = GetOrCreateCanvas();
        GameObject thought = CreateThoughtBubble(canvas.gameObject);
        Selection.activeGameObject = thought;
    }

    [MenuItem("Tools/Cyrulik/Create Client Dialogue Box (Rectangular)", false, 3)]
    public static void CreateClientDialogueBoxMenu()
    {
        Canvas canvas = GetOrCreateCanvas();
        GameObject clientBox = CreateClientDialogueBox(canvas.gameObject);
        Selection.activeGameObject = clientBox;
    }

    private static Canvas GetOrCreateCanvas()
    {
        var found = GameObject.Find("Dialogue_Canvas");
        if (found != null && found.GetComponent<Canvas>() != null)
        {
            return found.GetComponent<Canvas>();
        }

        GameObject canvasGo = new GameObject("Dialogue_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Dialogue Canvas");
        return canvas;
    }

    // ──────────────────────────────────────────────────────────
    // 1. CHMURKA MYŚLI (THOUGHT BUBBLE)
    // ──────────────────────────────────────────────────────────
    private static GameObject CreateThoughtBubble(GameObject parentCanvas)
    {
        GameObject thoughtRoot = new GameObject("InnerThought_Bubble", typeof(RectTransform), typeof(CanvasGroup), typeof(InnerDialogueUI));
        thoughtRoot.transform.SetParent(parentCanvas.transform, false);

        RectTransform rootRect = thoughtRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0, 95);
        rootRect.sizeDelta = new Vector2(740, 120);

        CanvasGroup rootCanvasGroup = thoughtRoot.GetComponent<CanvasGroup>();
        InnerDialogueUI dialogueUI = thoughtRoot.GetComponent<InnerDialogueUI>();

        // Tło chmurki (zaokrąglone, stonowany vintage szary/błękit)
        GameObject bgGo = new GameObject("Bubble_Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(thoughtRoot.transform, false);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.color = new Color(0.10f, 0.12f, 0.14f, 0.92f);

        // Obrys / ramka chmurki
        GameObject outlineGo = new GameObject("Bubble_Border", typeof(RectTransform), typeof(Image));
        outlineGo.transform.SetParent(bgGo.transform, false);
        RectTransform outlineRect = outlineGo.GetComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.sizeDelta = Vector2.zero;
        Image outlineImage = outlineGo.GetComponent<Image>();
        outlineImage.color = new Color(0.45f, 0.55f, 0.65f, 0.35f);

        // Kropelki myśli
        CreateThoughtDot(thoughtRoot.transform, new Vector2(-40, -14), 16);
        CreateThoughtDot(thoughtRoot.transform, new Vector2(-60, -28), 10);
        CreateThoughtDot(thoughtRoot.transform, new Vector2(-75, -38), 6);

        // Pole tekstowe
        GameObject textGo = new GameObject("Thought_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(thoughtRoot.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(28, 14);
        textRect.offsetMax = new Vector2(-90, -14);

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "<i>I should get dressed first...</i>";
        tmp.fontSize = 23;
        tmp.fontStyle = FontStyles.Italic;
        tmp.color = new Color(0.88f, 0.92f, 0.95f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        // Prompt [E] + ▼
        (CanvasGroup promptGroup, RectTransform arrowRect, TextMeshProUGUI keyTmp, TextMeshProUGUI arrowTmp) =
            CreateContinuePrompt(thoughtRoot.transform, new Vector2(-20, 0));

        // Powiąż referencje
        SerializedObject so = new SerializedObject(dialogueUI);
        so.FindProperty("dialogueText").objectReferenceValue = tmp;
        so.FindProperty("dialogueCanvasGroup").objectReferenceValue = rootCanvasGroup;
        so.FindProperty("continuePromptGroup").objectReferenceValue = promptGroup;
        so.FindProperty("arrowTransform").objectReferenceValue = arrowRect;
        so.FindProperty("promptKeyText").objectReferenceValue = keyTmp;
        so.FindProperty("promptArrowText").objectReferenceValue = arrowTmp;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(thoughtRoot, "Create Thought Bubble");
        return thoughtRoot;
    }

    private static void CreateThoughtDot(Transform parent, Vector2 pos, float size)
    {
        GameObject dot = new GameObject("Thought_Dot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(parent, false);
        RectTransform rt = dot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        Image img = dot.GetComponent<Image>();
        img.color = new Color(0.12f, 0.14f, 0.16f, 0.85f);
    }

    // ──────────────────────────────────────────────────────────
    // 2. PROSTOKĄTNY DIALOG KLIENTA (RECTANGULAR CLIENT DIALOGUE)
    // ──────────────────────────────────────────────────────────
    private static GameObject CreateClientDialogueBox(GameObject parentCanvas)
    {
        GameObject clientRoot = new GameObject("ClientDialogue_Box", typeof(RectTransform), typeof(CanvasGroup), typeof(ClientDialogueUI));
        clientRoot.transform.SetParent(parentCanvas.transform, false);

        RectTransform rootRect = clientRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0, 50);
        rootRect.sizeDelta = new Vector2(920, 160);

        CanvasGroup rootCanvasGroup = clientRoot.GetComponent<CanvasGroup>();
        ClientDialogueUI clientUI = clientRoot.GetComponent<ClientDialogueUI>();

        // Tło prostokątne
        GameObject bgGo = new GameObject("Box_Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(clientRoot.transform, false);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.color = new Color(0.06f, 0.06f, 0.06f, 0.94f);

        // Obrys ramki
        GameObject borderGo = new GameObject("Box_Border", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(bgGo.transform, false);
        RectTransform borderRect = borderGo.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        Image borderImage = borderGo.GetComponent<Image>();
        borderImage.color = new Color(0.6f, 0.5f, 0.35f, 0.45f);

        // Tabliczka Imienia Mówcy (Speaker Badge) w lewym górnym rogu
        GameObject speakerBadge = new GameObject("Speaker_Badge", typeof(RectTransform), typeof(Image));
        speakerBadge.transform.SetParent(clientRoot.transform, false);
        RectTransform badgeRect = speakerBadge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 0f);
        badgeRect.anchoredPosition = new Vector2(24, -4);
        badgeRect.sizeDelta = new Vector2(160, 34);

        Image badgeImg = speakerBadge.GetComponent<Image>();
        badgeImg.color = new Color(0.20f, 0.16f, 0.12f, 0.95f);

        // Tekst Imienia
        GameObject speakerTextGo = new GameObject("Speaker_Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        speakerTextGo.transform.SetParent(speakerBadge.transform, false);
        RectTransform speakerTextRect = speakerTextGo.GetComponent<RectTransform>();
        speakerTextRect.anchorMin = Vector2.zero;
        speakerTextRect.anchorMax = Vector2.one;
        speakerTextRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI speakerTmp = speakerTextGo.GetComponent<TextMeshProUGUI>();
        speakerTmp.text = "Klient";
        speakerTmp.fontSize = 20;
        speakerTmp.fontStyle = FontStyles.Bold;
        speakerTmp.color = new Color(0.95f, 0.82f, 0.55f, 1f);
        speakerTmp.alignment = TextAlignmentOptions.Center;

        // Pole tekstowe wypowiedzi klienta
        GameObject textGo = new GameObject("Client_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(clientRoot.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(28, 20);
        textRect.offsetMax = new Vector2(-100, -32);

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "Dzień dobry, poproszę golenie na gładko.";
        tmp.fontSize = 24;
        tmp.color = new Color(0.94f, 0.94f, 0.94f, 1f);
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        // Prompt [E] + ▼
        (CanvasGroup promptGroup, RectTransform arrowRect, TextMeshProUGUI keyTmp, TextMeshProUGUI arrowTmp) =
            CreateContinuePrompt(clientRoot.transform, new Vector2(-24, 0));

        // Powiąż referencje
        SerializedObject so = new SerializedObject(clientUI);
        so.FindProperty("dialogueText").objectReferenceValue = tmp;
        so.FindProperty("speakerNameText").objectReferenceValue = speakerTmp;
        so.FindProperty("speakerBadgeContainer").objectReferenceValue = speakerBadge;
        so.FindProperty("dialogueCanvasGroup").objectReferenceValue = rootCanvasGroup;
        so.FindProperty("continuePromptGroup").objectReferenceValue = promptGroup;
        so.FindProperty("arrowTransform").objectReferenceValue = arrowRect;
        so.FindProperty("promptKeyText").objectReferenceValue = keyTmp;
        so.FindProperty("promptArrowText").objectReferenceValue = arrowTmp;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(clientRoot, "Create Client Dialogue Box");
        return clientRoot;
    }

    // ──────────────────────────────────────────────────────────
    // POMOCNIK: PROMPT [E] + STRZAŁKA
    // ──────────────────────────────────────────────────────────
    private static (CanvasGroup, RectTransform, TextMeshProUGUI, TextMeshProUGUI) CreateContinuePrompt(Transform parent, Vector2 pos)
    {
        GameObject promptGo = new GameObject("Continue_Prompt", typeof(RectTransform), typeof(CanvasGroup));
        promptGo.transform.SetParent(parent, false);

        RectTransform promptRect = promptGo.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(1f, 0.5f);
        promptRect.anchorMax = new Vector2(1f, 0.5f);
        promptRect.pivot = new Vector2(1f, 0.5f);
        promptRect.anchoredPosition = pos;
        promptRect.sizeDelta = new Vector2(50, 80);

        CanvasGroup promptCanvasGroup = promptGo.GetComponent<CanvasGroup>();

        // Klawisz [E]
        GameObject keyBgGo = new GameObject("Key_Badge", typeof(RectTransform), typeof(Image));
        keyBgGo.transform.SetParent(promptGo.transform, false);
        RectTransform keyBgRect = keyBgGo.GetComponent<RectTransform>();
        keyBgRect.anchorMin = new Vector2(0.5f, 0.5f);
        keyBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        keyBgRect.pivot = new Vector2(0.5f, 0.5f);
        keyBgRect.anchoredPosition = new Vector2(0, 14);
        keyBgRect.sizeDelta = new Vector2(34, 34);

        Image keyBgImage = keyBgGo.GetComponent<Image>();
        keyBgImage.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);

        GameObject keyTextGo = new GameObject("Key_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        keyTextGo.transform.SetParent(keyBgGo.transform, false);
        RectTransform keyTextRect = keyTextGo.GetComponent<RectTransform>();
        keyTextRect.anchorMin = Vector2.zero;
        keyTextRect.anchorMax = Vector2.one;
        keyTextRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI keyTmp = keyTextGo.GetComponent<TextMeshProUGUI>();
        keyTmp.text = "E";
        keyTmp.fontSize = 20;
        keyTmp.fontStyle = FontStyles.Bold;
        keyTmp.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        keyTmp.alignment = TextAlignmentOptions.Center;

        // Strzałka ▼
        GameObject arrowGo = new GameObject("Arrow_Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrowGo.transform.SetParent(promptGo.transform, false);
        RectTransform arrowRect = arrowGo.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(0, -18);
        arrowRect.sizeDelta = new Vector2(30, 24);

        TextMeshProUGUI arrowTmp = arrowGo.GetComponent<TextMeshProUGUI>();
        arrowTmp.text = "▼";
        arrowTmp.fontSize = 18;
        arrowTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        arrowTmp.alignment = TextAlignmentOptions.Center;

        return (promptCanvasGroup, arrowRect, keyTmp, arrowTmp);
    }
}
#endif
