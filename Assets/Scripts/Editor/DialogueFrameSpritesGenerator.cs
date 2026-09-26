#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generator natywnych tekstur UI / Sprite'ów 9-slice dla 5 wersji graficznych ramek dialogów i myśli.
/// Tworzy czyste, antyaliasowane tekstury PNG z przezroczystością i konfiguruje 9-slice borders
/// bezpośrednio w Assets/Art/UI_DialogueFrames/.
/// </summary>
public static class DialogueFrameSpritesGenerator
{
    public const string FOLDER_PATH = "Assets/Art/UI_DialogueFrames";
    private const int TEX_SIZE = 256;
    private const int BORDER_MARGIN = 32;

    [InitializeOnLoadMethod]
    private static void AutoCheckSprites()
    {
        // Jeśli sprite'y jeszcze nie istnieją, wygeneruj je automatycznie po kompilacji
        string testFile = Path.Combine(FOLDER_PATH, "Frame_BarberBrass.png");
        if (!File.Exists(testFile))
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(testFile))
                {
                    GenerateAllDialogueSprites();
                }
            };
        }
    }

    [MenuItem("Tools/Cyrulik/Generate Dialogue Frame Sprites (5 Wersji)", false, 21)]
    public static void GenerateAllDialogueSprites()
    {
        if (!Directory.Exists(FOLDER_PATH))
        {
            Directory.CreateDirectory(FOLDER_PATH);
            AssetDatabase.Refresh();
        }

        // ══════════════════════════════════════════════════════════
        // WERSJA 1: KLASYCZNY CYRULIK (BARBER BRASS & MAHOGANY)
        // ══════════════════════════════════════════════════════════
        // 1A. Ramka mosiężna podwójna z nitami i ściętymi narożnikami
        CreateAndSavePng("Frame_BarberBrass.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;
            float px = Mathf.Min(x, w - 1 - x);
            float py = Mathf.Min(y, h - 1 - y);

            // Narożniki ścięte 45 stopni
            float cornerDist = (32f - px) + (32f - py);
            if (px < 32 && py < 32 && cornerDist > 32f)
            {
                // Za ścięciem
                return Color.clear;
            }

            // Mosiężny nit w narożniku (środek w x=24, y=24 od rogów)
            float rivetDist = Vector2.Distance(new Vector2(px, py), new Vector2(22f, 22f));
            if (px <= 34 && py <= 34 && rivetDist <= 6.5f)
            {
                // Nit z mosiądzu (gradient światłocienia 3D)
                float angle = Mathf.Atan2(py - 22f, px - 22f);
                float light = Mathf.Cos(angle + Mathf.PI * 0.75f) * 0.35f + 0.65f;
                float rim = SmoothBand(rivetDist, 4.5f, 6.5f, 1f);
                if (rivetDist <= 4.5f)
                    return new Color(0.95f * light, 0.82f * light, 0.52f * light, 1f);
                else
                    return new Color(0.55f * rim, 0.42f * rim, 0.22f * rim, 1f);
            }

            // Podwójna linia mosiężna
            // Linia zewnętrzna: d = 6..9px od krawędzi
            // Przerwa: d = 9..13px
            // Linia wewnętrzna: d = 13..15px
            float dMin = Mathf.Min(px, py);

            // Ścięcie 45 stopni na linii
            if (px < 32 && py < 32)
            {
                dMin = Mathf.Min(dMin, 32f - cornerDist * 0.5f);
            }

            float lineOuter = SmoothBand(dMin, 6f, 9f, 0.8f);
            float lineInner = SmoothBand(dMin, 13f, 15f, 0.8f);

            if (lineOuter > 0.05f)
            {
                // Złocisto-mosiężny gradient
                Color brassBright = new Color(0.92f, 0.78f, 0.48f, lineOuter);
                return brassBright;
            }

            if (lineInner > 0.05f)
            {
                Color brassSubtle = new Color(0.72f, 0.58f, 0.35f, lineInner * 0.85f);
                return brassSubtle;
            }

            // Subtelny ciemno-mosiężny podkład pomiędzy liniami
            if (dMin >= 8f && dMin <= 14f)
            {
                return new Color(0.35f, 0.28f, 0.16f, 0.25f);
            }

            return Color.clear;
        });

        // 1B. Tło: ciemny heban z mahoniowym ciepłym winietowaniem
        CreateAndSavePng("Bg_BarberBrass.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float px = Mathf.Min(x, w - 1 - x);
            float py = Mathf.Min(y, h - 1 - y);
            if (px < 32 && py < 32 && ((32f - px) + (32f - py)) > 32f)
                return Color.clear;

            float cx = w * 0.5f, cy = h * 0.5f;
            float distNorm = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / (w * 0.707f);
            float vignette = Mathf.Clamp01(distNorm);

            Color centerColor = new Color(0.085f, 0.065f, 0.060f, 0.96f);
            Color edgeColor = new Color(0.045f, 0.035f, 0.032f, 0.98f);
            return Color.Lerp(centerColor, edgeColor, vignette);
        });

        // ══════════════════════════════════════════════════════════
        // WERSJA 2: RETRO NOIR / PSX (STAL INDUSTRIALNA & NAROŻNIKI [ ])
        // ══════════════════════════════════════════════════════════
        // 2A. Ramka: ostre narożniki kątowe [ ] i techniczne nacięcia
        CreateAndSavePng("Frame_RetroNoir.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float px = Mathf.Min(x, w - 1 - x);
            float py = Mathf.Min(y, h - 1 - y);

            // Narożnik Bracket [ ]: grubość 4px, długość 28px
            bool inCornerBracketX = px >= 5f && px <= 9f && py >= 5f && py <= 30f;
            bool inCornerBracketY = py >= 5f && py <= 9f && px >= 5f && px <= 30f;

            if (inCornerBracketX || inCornerBracketY)
            {
                // Wyrazisty stalowy narożnik
                return new Color(0.75f, 0.85f, 0.95f, 0.95f);
            }

            // Cienka linia łącząca ramki (d = 7..8px)
            if (px >= 6.5f && px <= 7.5f && py > 30f)
                return new Color(0.40f, 0.48f, 0.55f, 0.65f);
            if (py >= 6.5f && py <= 7.5f && px > 30f)
                return new Color(0.40f, 0.48f, 0.55f, 0.65f);

            // Wewnętrzny akcent w rogu (mała kropka/kwadracik celowniczy)
            if (px >= 14f && px <= 17f && py >= 14f && py <= 17f)
            {
                return new Color(0.65f, 0.78f, 0.88f, 0.80f);
            }

            return Color.clear;
        });

        // 2B. Tło: chłodny antracyt / popiel PSX
        CreateAndSavePng("Bg_RetroNoir.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float px = Mathf.Min(x, w - 1 - x);
            float py = Mathf.Min(y, h - 1 - y);
            if (px < 6 || py < 6) return Color.clear;

            float cx = w * 0.5f, cy = h * 0.5f;
            float distNorm = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / (w * 0.707f);

            Color centerColor = new Color(0.065f, 0.075f, 0.090f, 0.94f);
            Color edgeColor = new Color(0.035f, 0.040f, 0.050f, 0.97f);
            return Color.Lerp(centerColor, edgeColor, distNorm);
        });

        // ══════════════════════════════════════════════════════════
        // WERSJA 3: AKTA & MASZYNOPIS (VINTAGE LEDGER / PRL 1993)
        // ══════════════════════════════════════════════════════════
        // 3A. Ramka: urzędowa linia z drobnym ściegiem maszynowym i krzyżykami
        CreateAndSavePng("Frame_VintageLedger.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float px = Mathf.Min(x, w - 1 - x);
            float py = Mathf.Min(y, h - 1 - y);

            // Krzyżyk rejestracyjny w rogach (x=16, y=16)
            bool isCrossH = (Mathf.Abs(px - 16f) <= 6f && Mathf.Abs(py - 16f) <= 1f);
            bool isCrossV = (Mathf.Abs(py - 16f) <= 6f && Mathf.Abs(px - 16f) <= 1f);
            if (isCrossH || isCrossV)
            {
                return new Color(0.80f, 0.74f, 0.60f, 0.90f);
            }

            // Podwójna linia kancelaryjna:
            // Linia 1: d = 6..8px
            // Linia 2: d = 11..12px (kropkowana / przerywana co 8px)
            float dMin = Mathf.Min(px, py);

            if (dMin >= 6f && dMin <= 7.5f)
            {
                return new Color(0.68f, 0.62f, 0.50f, 0.85f);
            }

            if (dMin >= 11f && dMin <= 12f)
            {
                // Przerywana linia maszynopisu
                float step = (px < py) ? (y % 8f) : (x % 8f);
                if (step < 5f)
                    return new Color(0.52f, 0.47f, 0.38f, 0.65f);
            }

            return Color.clear;
        });

        // 3B. Tło: ciemny, pożółkły pergamin archiwalny
        CreateAndSavePng("Bg_VintageLedger.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float px = Mathf.Min(x, w - 1 - x);
            float py = Mathf.Min(y, h - 1 - y);
            if (px < 5 || py < 5) return Color.clear;

            float cx = w * 0.5f, cy = h * 0.5f;
            float distNorm = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / (w * 0.707f);

            Color centerColor = new Color(0.095f, 0.085f, 0.070f, 0.95f);
            Color edgeColor = new Color(0.055f, 0.048f, 0.038f, 0.98f);
            return Color.Lerp(centerColor, edgeColor, distNorm);
        });

        // ══════════════════════════════════════════════════════════
        // WERSJA 4: KARMAZYNOWY MROK / BRZYTWA (CRIMSON RAZOR)
        // ══════════════════════════════════════════════════════════
        // 4A. Ramka: ostre fazowania ostrza brzytwy i ciemny karmazyn
        CreateAndSavePng("Frame_CrimsonRazor.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float px = Mathf.Min(x, w - 1 - x);
            float py = Mathf.Min(y, h - 1 - y);

            // Ścięcie narożnika jak czubek brzytwy
            float bevel = (28f - px) + (28f - py);
            if (px < 28 && py < 28 && bevel > 28f)
                return Color.clear;

            float dMin = Mathf.Min(px, py);
            if (px < 28 && py < 28)
            {
                dMin = Mathf.Min(dMin, 28f - bevel * 0.5f);
            }

            // Ostra krawędź ze stali (szlif brzytwy)
            float bladeLine = SmoothBand(dMin, 5f, 7.5f, 0.8f);
            if (bladeLine > 0.05f)
            {
                // Gradient od zimnej stali do zaschniętego karmazynu
                float crimsonMix = Mathf.Clamp01((px + py) / 60f);
                Color steel = new Color(0.90f, 0.88f, 0.88f, bladeLine);
                Color crimson = new Color(0.85f, 0.18f, 0.22f, bladeLine * 0.95f);
                return Color.Lerp(steel, crimson, crimsonMix * 0.65f);
            }

            // Wewnętrzny karmazynowy cienki pasek (d = 12..13.5px)
            float innerCrimson = SmoothBand(dMin, 12f, 13.5f, 0.6f);
            if (innerCrimson > 0.05f)
            {
                return new Color(0.60f, 0.10f, 0.14f, innerCrimson * 0.70f);
            }

            return Color.clear;
        });

        // 4B. Tło: smoliście czarny heban z ciemnoczerwoną poświatą
        CreateAndSavePng("Bg_CrimsonRazor.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float px = Mathf.Min(x, w - 1 - x);
            float py = Mathf.Min(y, h - 1 - y);
            if (px < 28 && py < 28 && ((28f - px) + (28f - py)) > 28f)
                return Color.clear;

            float cx = w * 0.5f, cy = h * 0.5f;
            float distNorm = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / (w * 0.707f);

            // Czerń z mikroskopijnym bordowym tintem na obrzeżu
            Color centerColor = new Color(0.045f, 0.030f, 0.032f, 0.96f);
            Color edgeColor = new Color(0.080f, 0.020f, 0.025f, 0.98f);
            return Color.Lerp(centerColor, edgeColor, distNorm);
        });

        // ══════════════════════════════════════════════════════════
        // WERSJA 5: DYMIONY AKSAMIT / BEVELED GLASS (MINIMALIZM)
        // ══════════════════════════════════════════════════════════
        // 5A. Ramka: subtelne zaokrąglenie, górny 1px highlight, dolny cień
        CreateAndSavePng("Frame_SmokedVelvet.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float radius = 18f;
            float cx = (x < radius) ? radius : (x > w - 1 - radius ? w - 1 - radius : x);
            float cy = (y < radius) ? radius : (y > h - 1 - radius ? h - 1 - radius : y);
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

            float outerAlpha = Mathf.Clamp01((radius - dist + 1f) / 1.5f);
            float innerAlpha = Mathf.Clamp01((radius - 3.5f - dist + 1f) / 1.5f);
            float rim = Mathf.Clamp01(outerAlpha - innerAlpha);

            if (rim <= 0.02f) return Color.clear;

            // Światło z góry: jeśli y jest wysoko, jaśniejsza krawędź
            float vNorm = (float)y / h;
            float highlight = Mathf.Lerp(0.35f, 0.95f, vNorm);

            return new Color(0.85f * highlight, 0.90f * highlight, 0.95f * highlight, rim * 0.75f);
        });

        // 5B. Tło: przydymiony onyks / ciemne szkło
        CreateAndSavePng("Bg_SmokedVelvet.png", TEX_SIZE, TEX_SIZE, (x, y, w, h) =>
        {
            float radius = 18f;
            float cx = (x < radius) ? radius : (x > w - 1 - radius ? w - 1 - radius : x);
            float cy = (y < radius) ? radius : (y > h - 1 - radius ? h - 1 - radius : y);
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

            float alpha = Mathf.Clamp01((radius - dist + 1f) / 1.5f);
            if (alpha <= 0.01f) return Color.clear;

            Color baseColor = new Color(0.07f, 0.08f, 0.10f, 0.92f);
            return new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        });

        AssetDatabase.Refresh();

        // ══════════════════════════════════════════════════════════
        // Konfiguracja Importerów: Sprite 2D + 9-Slice Border (32px)
        // ══════════════════════════════════════════════════════════
        ConfigureAllSprites();

        // ══════════════════════════════════════════════════════════
        // Tworzenie / Aktualizacja DialogueStyleDatabase w Resources
        // ══════════════════════════════════════════════════════════
        UpdateDatabaseAsset();

        Debug.Log("<color=#70FF70>[DialogueFrameSpritesGenerator] Sukces! Wygenerowano pełen zestaw 5 wersji graficznych ramek (9-slice) w " + FOLDER_PATH + "/ !</color>");
    }

    private static void UpdateDatabaseAsset()
    {
        string resourcesDir = "Assets/Resources";
        if (!Directory.Exists(resourcesDir))
        {
            Directory.CreateDirectory(resourcesDir);
            AssetDatabase.Refresh();
        }

        string dbPath = "Assets/Resources/DialogueStyleDatabase.asset";
        DialogueStyleDatabase db = AssetDatabase.LoadAssetAtPath<DialogueStyleDatabase>(dbPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<DialogueStyleDatabase>();
            AssetDatabase.CreateAsset(db, dbPath);
        }

        DialogueFrameStyle[] allStyles = new DialogueFrameStyle[]
        {
            DialogueFrameStyle.BarberBrass,
            DialogueFrameStyle.RetroNoir,
            DialogueFrameStyle.VintageLedger,
            DialogueFrameStyle.CrimsonRazor,
            DialogueFrameStyle.SmokedVelvet
        };

        db.styles = new DialogueStyleDatabase.StyleEntry[allStyles.Length];
        for (int i = 0; i < allStyles.Length; i++)
        {
            DialogueStyleTheme theme = DialogueStyleTheme.GetTheme(allStyles[i]);
            string framePath = $"{FOLDER_PATH}/{theme.frameSpriteName}.png";
            string bgPath = $"{FOLDER_PATH}/{theme.bgSpriteName}.png";

            db.styles[i] = new DialogueStyleDatabase.StyleEntry
            {
                style = allStyles[i],
                displayName = theme.styleName,
                frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath),
                bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath),
                theme = theme
            };
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private static float SmoothBand(float dist, float inner, float outer, float feather)
    {
        float a1 = Mathf.Clamp01((dist - inner) / feather);
        float a2 = Mathf.Clamp01((outer - dist) / feather);
        return Mathf.Min(a1, a2);
    }

    private static void CreateAndSavePng(string fileName, int w, int h, System.Func<int, int, int, int, Color> colorFunc)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                pixels[y * w + x] = colorFunc(x, y, w, h);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        byte[] pngData = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string fullPath = Path.Combine(FOLDER_PATH, fileName);
        File.WriteAllBytes(fullPath, pngData);
    }

    private static void ConfigureAllSprites()
    {
        string[] files = Directory.GetFiles(FOLDER_PATH, "*.png");
        foreach (string file in files)
        {
            string unityPath = file.Replace("\\", "/");
            TextureImporter importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                
                // Margines 9-slice: 32px z każdej strony (lewa, dół, prawa, góra)
                importer.spriteBorder = new Vector4(BORDER_MARGIN, BORDER_MARGIN, BORDER_MARGIN, BORDER_MARGIN);
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
