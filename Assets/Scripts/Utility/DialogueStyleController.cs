using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Statyczny kontroler aplikujący jedną z 5 wersji graficznych ramek do okien dialogowych i chmurek myśli.
/// Działa w edytorze (zapis stanu) oraz w trybie Play Mode w czasie rzeczywistym.
/// </summary>
public static class DialogueStyleController
{
    /// <summary>
    /// Zmienia styl w całej scenie dla myśli i dialogu klienta.
    /// </summary>
    public static void ApplyGlobalStyle(DialogueFrameStyle style)
    {
        InnerDialogueUI inner = InnerDialogueUI.Instance ?? Object.FindAnyObjectByType<InnerDialogueUI>(FindObjectsInactive.Include);
        if (inner != null)
        {
            ApplyStyleToInnerThought(inner, style);
        }

        ClientDialogueUI client = ClientDialogueUI.Instance ?? Object.FindAnyObjectByType<ClientDialogueUI>(FindObjectsInactive.Include);
        if (client != null)
        {
            ApplyStyleToClientDialogue(client, style);
        }

        DevLog.Log($"<color=#70FF70>[DialogueStyleController] Zastosowano styl globalny: {style}</color>");
    }

    /// <summary>
    /// Aplikuje styl do chmurki myśli (InnerThought_Bubble).
    /// </summary>
    public static void ApplyStyleToInnerThought(InnerDialogueUI innerUI, DialogueFrameStyle style)
    {
        if (innerUI == null) return;

        DialogueStyleTheme theme = DialogueStyleTheme.GetTheme(style);
        Sprite frameSprite = LoadFrameSprite(theme.frameSpriteName);
        Sprite bgSprite = LoadBgSprite(theme.bgSpriteName);

        // 1. Tło i Ramka
        Transform bgTrans = innerUI.transform.Find("Bubble_Background");
        if (bgTrans != null)
        {
            Image bgImg = bgTrans.GetComponent<Image>();
            if (bgImg != null)
            {
                if (bgSprite != null)
                {
                    bgImg.sprite = bgSprite;
                    bgImg.type = Image.Type.Sliced;
                }
                bgImg.color = theme.bgColor;
            }

            Transform borderTrans = bgTrans.Find("Bubble_Border");
            if (borderTrans != null)
            {
                Image borderImg = borderTrans.GetComponent<Image>();
                if (borderImg != null)
                {
                    if (frameSprite != null)
                    {
                        borderImg.sprite = frameSprite;
                        borderImg.type = Image.Type.Sliced;
                    }
                    borderImg.color = theme.frameColor;
                }
            }
        }

        // 2. Kropelki myśli (Thought_Dot)
        Image[] dots = innerUI.GetComponentsInChildren<Image>(true);
        foreach (var img in dots)
        {
            if (img.gameObject.name.StartsWith("Thought_Dot"))
            {
                img.color = theme.thoughtDotColor;
            }
        }

        // 3. Wskaźnik [E] + ▼
        Transform promptTrans = innerUI.transform.Find("Continue_Prompt");
        if (promptTrans != null)
        {
            ApplyPromptStyle(promptTrans, theme, bgSprite, frameSprite);
        }

        // 4. Tekst myśli (TextMeshProUGUI)
        TextMeshProUGUI tmp = innerUI.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null && tmp.gameObject.name.Contains("Thought_Text"))
        {
            tmp.color = theme.thoughtTextColor;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(innerUI.gameObject);
        }
#endif
    }

    /// <summary>
    /// Aplikuje styl do prostokątnego okna dialogowego klienta (ClientDialogue_Box).
    /// </summary>
    public static void ApplyStyleToClientDialogue(ClientDialogueUI clientUI, DialogueFrameStyle style)
    {
        if (clientUI == null) return;

        DialogueStyleTheme theme = DialogueStyleTheme.GetTheme(style);
        Sprite frameSprite = LoadFrameSprite(theme.frameSpriteName);
        Sprite bgSprite = LoadBgSprite(theme.bgSpriteName);

        // 1. Tło i Ramka
        Transform bgTrans = clientUI.transform.Find("Box_Background");
        if (bgTrans != null)
        {
            Image bgImg = bgTrans.GetComponent<Image>();
            if (bgImg != null)
            {
                if (bgSprite != null)
                {
                    bgImg.sprite = bgSprite;
                    bgImg.type = Image.Type.Sliced;
                }
                bgImg.color = theme.bgColor;
            }

            Transform borderTrans = bgTrans.Find("Box_Border");
            if (borderTrans != null)
            {
                Image borderImg = borderTrans.GetComponent<Image>();
                if (borderImg != null)
                {
                    if (frameSprite != null)
                    {
                        borderImg.sprite = frameSprite;
                        borderImg.type = Image.Type.Sliced;
                    }
                    borderImg.color = theme.frameColor;
                }
            }
        }

        // 2. Tabliczka Mówcy (Speaker_Badge)
        Transform badgeTrans = clientUI.transform.Find("Speaker_Badge");
        if (badgeTrans != null)
        {
            Image badgeImg = badgeTrans.GetComponent<Image>();
            if (badgeImg != null)
            {
                if (bgSprite != null)
                {
                    badgeImg.sprite = bgSprite;
                    badgeImg.type = Image.Type.Sliced;
                }
                badgeImg.color = theme.speakerBadgeBgColor;
            }

            TextMeshProUGUI speakerTmp = badgeTrans.GetComponentInChildren<TextMeshProUGUI>(true);
            if (speakerTmp != null)
            {
                speakerTmp.color = theme.speakerBadgeTextColor;
            }
        }

        // 3. Wskaźnik [E] + ▼
        Transform promptTrans = clientUI.transform.Find("Continue_Prompt");
        if (promptTrans != null)
        {
            ApplyPromptStyle(promptTrans, theme, bgSprite, frameSprite);
        }

        // 4. Główny tekst wypowiedzi
        TextMeshProUGUI clientTmp = clientUI.GetComponentInChildren<TextMeshProUGUI>(true);
        if (clientTmp != null && clientTmp.gameObject.name.Contains("Client_Text"))
        {
            clientTmp.color = theme.dialogueTextColor;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(clientUI.gameObject);
        }
#endif
    }

    private static void ApplyPromptStyle(Transform promptTrans, DialogueStyleTheme theme, Sprite bgSprite, Sprite frameSprite)
    {
        Transform keyBadge = promptTrans.Find("Key_Badge");
        if (keyBadge != null)
        {
            Image keyImg = keyBadge.GetComponent<Image>();
            if (keyImg != null)
            {
                if (bgSprite != null)
                {
                    keyImg.sprite = bgSprite;
                    keyImg.type = Image.Type.Sliced;
                }
                keyImg.color = theme.keyBadgeBgColor;
            }

            TextMeshProUGUI keyTmp = keyBadge.GetComponentInChildren<TextMeshProUGUI>(true);
            if (keyTmp != null)
            {
                keyTmp.color = theme.keyBadgeTextColor;
            }
        }

        Transform arrowTrans = promptTrans.Find("Arrow_Icon") ?? promptTrans.Find("Prompt_Arrow");
        if (arrowTrans != null)
        {
            TextMeshProUGUI arrowTmp = arrowTrans.GetComponent<TextMeshProUGUI>();
            if (arrowTmp != null)
            {
                arrowTmp.color = theme.arrowColor;
            }
        }
    }

    private static Sprite LoadFrameSprite(string spriteName)
    {
        DialogueStyleDatabase db = DialogueStyleDatabase.Instance;
        if (db != null)
        {
            for (int i = 0; i < db.styles.Length; i++)
            {
                if (db.styles[i] != null && db.styles[i].frameSprite != null && db.styles[i].frameSprite.name == spriteName)
                    return db.styles[i].frameSprite;
            }
        }

#if UNITY_EDITOR
        string path = $"Assets/Art/UI_DialogueFrames/{spriteName}.png";
        Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
#endif
        return Resources.Load<Sprite>(spriteName);
    }

    private static Sprite LoadBgSprite(string spriteName)
    {
        DialogueStyleDatabase db = DialogueStyleDatabase.Instance;
        if (db != null)
        {
            for (int i = 0; i < db.styles.Length; i++)
            {
                if (db.styles[i] != null && db.styles[i].bgSprite != null && db.styles[i].bgSprite.name == spriteName)
                    return db.styles[i].bgSprite;
            }
        }

#if UNITY_EDITOR
        string path = $"Assets/Art/UI_DialogueFrames/{spriteName}.png";
        Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
#endif
        return Resources.Load<Sprite>(spriteName);
    }
}
