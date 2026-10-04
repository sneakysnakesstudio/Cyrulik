using UnityEngine;

/// <summary>
/// 5 Wersji graficznych obramowania i stylistyki dla okien dialogowych i chmurek myśli.
/// </summary>
public enum DialogueFrameStyle
{
    [InspectorName("1. Klasyczny Cyrulik (Mosiądz & Mahoń)")]
    BarberBrass = 0,

    [InspectorName("2. Retro Noir / PSX (Stal & Narożniki [ ])")]
    RetroNoir = 1,

    [InspectorName("3. Akta & Maszynopis (PRL Archiwum 1993)")]
    VintageLedger = 2,

    [InspectorName("4. Karmazynowy Mrok (Szlif Brzytwy & Krew)")]
    CrimsonRazor = 3,

    [InspectorName("5. Dymiony Aksamit (Beveled Glass Minimalizm)")]
    SmokedVelvet = 4
}

/// <summary>
/// Definicja parametrów wizualnych (kolory, sprite'y, typografia) dla danego stylu dialogu.
/// </summary>
[System.Serializable]
public class DialogueStyleTheme
{
    public string styleName;
    public string frameSpriteName;
    public string bgSpriteName;

    [Header("Kolory Ramy i Tła")]
    public Color frameColor = Color.white;
    public Color bgColor = Color.white;

    [Header("Tabliczka Mówcy (Speaker Badge)")]
    public Color speakerBadgeBgColor = new Color(0.20f, 0.16f, 0.12f, 0.95f);
    public Color speakerBadgeTextColor = new Color(0.95f, 0.82f, 0.55f, 1f);

    [Header("Wskaźnik [E] + ▼")]
    public Color keyBadgeBgColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
    public Color keyBadgeTextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    public Color arrowColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    [Header("Główny Tekst (TextMeshPro)")]
    public Color dialogueTextColor = new Color(0.94f, 0.94f, 0.94f, 1f);
    public Color thoughtTextColor = new Color(0.90f, 0.93f, 0.96f, 1f);

    [Header("Kropelki Myśli (Dla Chmurki)")]
    public Color thoughtDotColor = new Color(0.12f, 0.14f, 0.16f, 0.85f);

    public static DialogueStyleTheme GetTheme(DialogueFrameStyle style)
    {
        switch (style)
        {
            // ──────────────────────────────────────────────────────────
            // 1. KLASYCZNY CYRULIK (Mosiądz & Mahoń)
            // ──────────────────────────────────────────────────────────
            case DialogueFrameStyle.BarberBrass:
                return new DialogueStyleTheme
                {
                    styleName = "Klasyczny Cyrulik (Mosiądz & Mahoń)",
                    frameSpriteName = "Frame_BarberBrass",
                    bgSpriteName = "Bg_BarberBrass",
                    frameColor = new Color(1f, 0.95f, 0.85f, 1f),
                    bgColor = new Color(1f, 1f, 1f, 1f),
                    speakerBadgeBgColor = new Color(0.24f, 0.18f, 0.13f, 0.96f),
                    speakerBadgeTextColor = new Color(0.98f, 0.84f, 0.52f, 1f), // Ciepłe złoto
                    keyBadgeBgColor = new Color(0.26f, 0.20f, 0.14f, 0.95f),
                    keyBadgeTextColor = new Color(0.96f, 0.90f, 0.78f, 1f),
                    arrowColor = new Color(0.92f, 0.78f, 0.48f, 1f),
                    dialogueTextColor = new Color(0.95f, 0.92f, 0.87f, 1f), // Kość słoniowa
                    thoughtTextColor = new Color(0.92f, 0.90f, 0.85f, 1f),
                    thoughtDotColor = new Color(0.24f, 0.18f, 0.13f, 0.88f)
                };

            // ──────────────────────────────────────────────────────────
            // 2. RETRO NOIR / PSX (Stal & Narożniki [ ])
            // ──────────────────────────────────────────────────────────
            case DialogueFrameStyle.RetroNoir:
                return new DialogueStyleTheme
                {
                    styleName = "Retro Noir / PSX (Stal & Narożniki [ ])",
                    frameSpriteName = "Frame_RetroNoir",
                    bgSpriteName = "Bg_RetroNoir",
                    frameColor = new Color(0.90f, 0.95f, 1f, 1f),
                    bgColor = new Color(1f, 1f, 1f, 1f),
                    speakerBadgeBgColor = new Color(0.12f, 0.15f, 0.18f, 0.96f),
                    speakerBadgeTextColor = new Color(0.70f, 0.85f, 1.0f, 1f), // Chłodny stalowy błękit
                    keyBadgeBgColor = new Color(0.14f, 0.17f, 0.20f, 0.95f),
                    keyBadgeTextColor = new Color(0.90f, 0.95f, 1f, 1f),
                    arrowColor = new Color(0.75f, 0.88f, 1f, 1f),
                    dialogueTextColor = new Color(0.90f, 0.93f, 0.97f, 1f), // Chłodna biel/popiel
                    thoughtTextColor = new Color(0.85f, 0.90f, 0.95f, 1f),
                    thoughtDotColor = new Color(0.12f, 0.15f, 0.18f, 0.88f)
                };

            // ──────────────────────────────────────────────────────────
            // 3. AKTA & MASZYNOPIS (PRL Archiwum 1993)
            // ──────────────────────────────────────────────────────────
            case DialogueFrameStyle.VintageLedger:
                return new DialogueStyleTheme
                {
                    styleName = "Akta & Maszynopis (PRL Archiwum 1993)",
                    frameSpriteName = "Frame_VintageLedger",
                    bgSpriteName = "Bg_VintageLedger",
                    frameColor = new Color(0.95f, 0.92f, 0.85f, 1f),
                    bgColor = new Color(1f, 1f, 1f, 1f),
                    speakerBadgeBgColor = new Color(0.20f, 0.18f, 0.15f, 0.96f),
                    speakerBadgeTextColor = new Color(0.92f, 0.82f, 0.62f, 1f), // Wyblakły bursztyn
                    keyBadgeBgColor = new Color(0.22f, 0.20f, 0.17f, 0.95f),
                    keyBadgeTextColor = new Color(0.92f, 0.90f, 0.82f, 1f),
                    arrowColor = new Color(0.85f, 0.78f, 0.65f, 1f),
                    dialogueTextColor = new Color(0.93f, 0.90f, 0.84f, 1f), // Stary papier maszynowy
                    thoughtTextColor = new Color(0.88f, 0.86f, 0.80f, 1f),
                    thoughtDotColor = new Color(0.20f, 0.18f, 0.15f, 0.88f)
                };

            // ──────────────────────────────────────────────────────────
            // 4. KARMAZYNOWY MROK (Szlif Brzytwy & Krew)
            // ──────────────────────────────────────────────────────────
            case DialogueFrameStyle.CrimsonRazor:
                return new DialogueStyleTheme
                {
                    styleName = "Karmazynowy Mrok (Szlif Brzytwy & Krew)",
                    frameSpriteName = "Frame_CrimsonRazor",
                    bgSpriteName = "Bg_CrimsonRazor",
                    frameColor = new Color(1f, 0.92f, 0.92f, 1f),
                    bgColor = new Color(1f, 1f, 1f, 1f),
                    speakerBadgeBgColor = new Color(0.22f, 0.08f, 0.10f, 0.96f),
                    speakerBadgeTextColor = new Color(0.95f, 0.55f, 0.58f, 1f), // Karmazynowy róż/rubin
                    keyBadgeBgColor = new Color(0.24f, 0.09f, 0.11f, 0.95f),
                    keyBadgeTextColor = new Color(0.96f, 0.90f, 0.90f, 1f),
                    arrowColor = new Color(0.90f, 0.30f, 0.35f, 1f),
                    dialogueTextColor = new Color(0.95f, 0.92f, 0.92f, 1f), // Kredowa biel
                    thoughtTextColor = new Color(0.92f, 0.88f, 0.88f, 1f),
                    thoughtDotColor = new Color(0.22f, 0.08f, 0.10f, 0.88f)
                };

            // ──────────────────────────────────────────────────────────
            // 5. DYMIONY AKSAMIT (Beveled Glass Minimalizm)
            // ──────────────────────────────────────────────────────────
            case DialogueFrameStyle.SmokedVelvet:
            default:
                return new DialogueStyleTheme
                {
                    styleName = "Dymiony Aksamit (Beveled Glass Minimalizm)",
                    frameSpriteName = "Frame_SmokedVelvet",
                    bgSpriteName = "Bg_SmokedVelvet",
                    frameColor = new Color(0.95f, 0.98f, 1.0f, 1f),
                    bgColor = new Color(1f, 1f, 1f, 1f),
                    speakerBadgeBgColor = new Color(0.14f, 0.16f, 0.20f, 0.92f),
                    speakerBadgeTextColor = new Color(0.92f, 0.94f, 0.98f, 1f), // Czyste jasne srebro
                    keyBadgeBgColor = new Color(0.16f, 0.18f, 0.22f, 0.92f),
                    keyBadgeTextColor = new Color(0.96f, 0.97f, 0.99f, 1f),
                    arrowColor = new Color(0.85f, 0.90f, 0.96f, 1f),
                    dialogueTextColor = new Color(0.96f, 0.96f, 0.98f, 1f), // Krystaliczna biel
                    thoughtTextColor = new Color(0.92f, 0.93f, 0.96f, 1f),
                    thoughtDotColor = new Color(0.14f, 0.16f, 0.20f, 0.85f)
                };
        }
    }
}
