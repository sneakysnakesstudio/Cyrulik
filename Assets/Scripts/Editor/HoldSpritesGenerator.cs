#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generator natywnych tekstur UI / Sprite'ów dla systemów Hold Progress Ring & Square Morph Frame.
/// Tworzy czyste, antyaliasowane tekstury PNG z przezroczystością bezpośrednio w Assets/Art/UI_HoldIcons/.
/// </summary>
public static class HoldSpritesGenerator
{
    private const string FOLDER_PATH = "Assets/Art/UI_HoldIcons";

    [MenuItem("Tools/Cyrulik/Generate Hold UI Sprites (10 Ikon/Ramek)", false, 20)]
    public static void GenerateAllSprites()
    {
        if (!Directory.Exists(FOLDER_PATH))
        {
            Directory.CreateDirectory(FOLDER_PATH);
            AssetDatabase.Refresh();
        }

        // 1. Smooth Ring
        CreateAndSavePng("HoldRing_Smooth.png", 256, 256, (x, y, w, h) =>
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            float outerR = w * 0.46f;
            float innerR = w * 0.38f;
            float alpha = SmoothBand(dist, innerR, outerR, 2f);
            return new Color(1f, 1f, 1f, alpha);
        });

        // 2. Clockwork Notched Ring
        CreateAndSavePng("HoldRing_Clockwork.png", 256, 256, (x, y, w, h) =>
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            Vector2 p = new Vector2(x - cx, y - cy);
            float dist = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            float outerR = w * 0.46f;
            float innerR = w * 0.38f;
            float ringAlpha = SmoothBand(dist, innerR, outerR, 2f);

            // 12 ząbków zegara (co 30 stopni)
            float notchDist = Mathf.Abs((angle % 30f) - 15f);
            float notchMask = notchDist > 3.5f ? 1f : 0.2f;

            return new Color(1f, 1f, 1f, ringAlpha * notchMask);
        });

        // 3. Segmented 4-Quad Ring
        CreateAndSavePng("HoldRing_Segmented4.png", 256, 256, (x, y, w, h) =>
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            Vector2 p = new Vector2(x - cx, y - cy);
            float dist = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            float outerR = w * 0.46f;
            float innerR = w * 0.36f;
            float ringAlpha = SmoothBand(dist, innerR, outerR, 2f);

            // 4 przerwy po 10 stopni na osiach 0, 90, 180, 270
            float modAngle = angle % 90f;
            float cutMask = (modAngle > 6f && modAngle < 84f) ? 1f : 0f;

            return new Color(1f, 1f, 1f, ringAlpha * cutMask);
        });

        // 4. Square Morph Corner Brackets [  ]
        CreateAndSavePng("SquareMorph_Brackets.png", 256, 256, (x, y, w, h) =>
        {
            float margin = 24f;
            float thickness = 14f;
            float armLength = 65f;

            bool isTop = y >= h - margin - thickness && y <= h - margin;
            bool isBottom = y >= margin && y <= margin + thickness;
            bool isLeft = x >= margin && x <= margin + thickness;
            bool isRight = x >= w - margin - thickness && x <= w - margin;

            bool inLeftArm = x <= margin + armLength;
            bool inRightArm = x >= w - margin - armLength;
            bool inTopArm = y >= h - margin - armLength;
            bool inBottomArm = y <= margin + armLength;

            bool active = (isTop && (inLeftArm || inRightArm)) ||
                          (isBottom && (inLeftArm || inRightArm)) ||
                          (isLeft && (inTopArm || inBottomArm)) ||
                          (isRight && (inTopArm || inBottomArm));

            return active ? Color.white : Color.clear;
        });

        // 5. Square Morph Solid Rounded Frame
        CreateAndSavePng("SquareMorph_RoundedBox.png", 256, 256, (x, y, w, h) =>
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float dx = Mathf.Abs(x - cx);
            float dy = Mathf.Abs(y - cy);
            float boxSize = w * 0.42f;
            float radius = 28f;
            float thickness = 12f;

            float qx = Mathf.Max(dx - (boxSize - radius), 0f);
            float qy = Mathf.Max(dy - (boxSize - radius), 0f);
            float dist = Mathf.Sqrt(qx * qx + qy * qy);

            float outerAlpha = Mathf.Clamp01((radius - dist + 1f) / 2f);
            float innerAlpha = Mathf.Clamp01((radius - thickness - dist + 1f) / 2f);

            return new Color(1f, 1f, 1f, Mathf.Clamp01(outerAlpha - innerAlpha));
        });

        // 6. Diamond Frame
        CreateAndSavePng("Diamond_Frame.png", 256, 256, (x, y, w, h) =>
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float manhattan = Mathf.Abs(x - cx) + Mathf.Abs(y - cy);
            float targetR = w * 0.45f;
            float thickness = 10f;

            float dist = Mathf.Abs(manhattan - targetR);
            float alpha = Mathf.Clamp01((thickness - dist) / 2f);
            return new Color(1f, 1f, 1f, alpha);
        });

        // 7. Razor Icon
        CreateAndSavePng("Icon_Razor.png", 256, 256, (x, y, w, h) =>
        {
            // Ostrze brzytwy
            float u = (float)x / w;
            float v = (float)y / h;

            // Rękojeść + ostrze
            bool blade = (u >= 0.2f && u <= 0.8f && v >= 0.45f && v <= 0.65f);
            bool edge = blade && (v >= 0.45f && v <= 0.48f);
            bool handle = (u >= 0.15f && u <= 0.35f && v >= 0.32f && v <= 0.48f);

            if (blade || handle)
            {
                return edge ? new Color(1f, 0.9f, 0.6f, 1f) : Color.white;
            }
            return Color.clear;
        });

        // 8. Crucifix Icon
        CreateAndSavePng("Icon_Crucifix.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;

            bool vert = (u >= 0.44f && u <= 0.56f && v >= 0.15f && v <= 0.85f);
            bool horiz = (u >= 0.25f && u <= 0.75f && v >= 0.58f && v <= 0.70f);

            return (vert || horiz) ? Color.white : Color.clear;
        });

        // 9. Blood Drop Icon
        CreateAndSavePng("Icon_BloodDrop.png", 256, 256, (x, y, w, h) =>
        {
            float cx = w * 0.5f;
            float cy = h * 0.38f;
            float r = w * 0.26f;
            float dCircle = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

            bool inCircle = dCircle <= r;
            bool inTip = y >= cy && y <= h * 0.82f && Mathf.Abs(x - cx) <= (1f - (y - cy) / (h * 0.44f)) * r;

            return (inCircle || inTip) ? Color.white : Color.clear;
        });

        // 10. Hand Grip Icon
        CreateAndSavePng("Icon_HandGrip.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;
            bool palm = (u >= 0.3f && u <= 0.7f && v >= 0.25f && v <= 0.6f);
            bool fingers = (u >= 0.3f && u <= 0.7f && v >= 0.6f && v <= 0.78f);
            return (palm || fingers) ? Color.white : Color.clear;
        });

        // 11. Question Mark Icon (Pytajnik do badania otoczenia [Inspect / Thought / Krzyż])
        CreateAndSavePng("Icon_QuestionMark.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;
            float cx = 0.5f;

            // Kropka na dole (y: 0.15 .. 0.25)
            float distDot = Vector2.Distance(new Vector2(u, v), new Vector2(cx, 0.20f));
            if (distDot <= 0.055f) return Color.white;

            // Pionowy słupek pytajnika (y: 0.35 .. 0.48)
            if (u >= 0.44f && u <= 0.56f && v >= 0.35f && v <= 0.48f) return Color.white;

            // Łuk pytajnika (y: 0.48 .. 0.85)
            float distArc = Vector2.Distance(new Vector2(u, v), new Vector2(cx, 0.65f));
            if (distArc >= 0.12f && distArc <= 0.23f && (v >= 0.55f || u >= 0.44f)) return Color.white;

            return Color.clear;
        });

        // 12. Default Dot Icon (Domyślna kropka celownika)
        CreateAndSavePng("Default_Dot.png", 64, 64, (x, y, w, h) =>
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            float r = w * 0.4f;
            float alpha = Mathf.Clamp01((r - dist + 1f) / 1.5f);
            return new Color(1f, 1f, 1f, alpha);
        });

        // 13. Lock Icon (Kłódka)
        CreateAndSavePng("Icon_Lock.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;

            // Korpus kłódki
            bool body = (u >= 0.25f && u <= 0.75f && v >= 0.15f && v <= 0.55f);

            // Pałąk kłódki
            float distShackle = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.55f));
            bool shackle = (distShackle >= 0.14f && distShackle <= 0.24f && v >= 0.55f && v <= 0.85f);

            return (body || shackle) ? Color.white : Color.clear;
        });

        // 14. Sun Rays Corona (Promyczki Słoneczka do Hold to Interact)
        CreateAndSavePng("Hold_SunRays.png", 256, 256, (x, y, w, h) =>
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            Vector2 p = new Vector2(x - cx, y - cy);
            float dist = p.magnitude;
            float normDist = dist / (w * 0.5f);

            if (normDist < 0.30f || normDist > 0.95f) return Color.clear;

            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            // 8 głównych promyków co 45 stopni + 8 mniejszych co 45 stopni (łącznie 16 promyczków)
            float mod8 = Mathf.Abs((angle % 45f) - 22.5f);
            float mod16 = Mathf.Abs((angle % 22.5f) - 11.25f);

            // Główny promyk
            bool isMainRay = (mod8 < 4.0f) && (normDist >= 0.32f && normDist <= 0.92f);
            // Mniejszy promyk pośredni
            bool isSubRay = (mod16 < 2.5f) && (normDist >= 0.35f && normDist <= 0.68f);

            if (isMainRay || isSubRay)
            {
                float tipFade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.70f, 0.92f, normDist));
                return new Color(1f, 1f, 1f, isMainRay ? tipFade : tipFade * 0.75f);
            }

            return Color.clear;
        });

        // 16. Exclamation Mark Icon (Wykrzyknik [!] - ważne/zagadka/uwaga)
        CreateAndSavePng("Icon_ExclamationMark.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;
            float cx = 0.5f;

            // Kropka na dole (y: 0.15 .. 0.25)
            float distDot = Vector2.Distance(new Vector2(u, v), new Vector2(cx, 0.20f));
            if (distDot <= 0.055f) return Color.white;

            // Zwężający się słupek wykrzyknika (y: 0.35 .. 0.85)
            if (v >= 0.35f && v <= 0.85f)
            {
                float halfWidth = Mathf.Lerp(0.045f, 0.075f, (v - 0.35f) / 0.50f);
                if (Mathf.Abs(u - cx) <= halfWidth) return Color.white;
            }

            return Color.clear;
        });

        // 17. Eye Icon (Oko - patrzenie/obserwacja)
        CreateAndSavePng("Icon_Eye.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;

            // Migdałowy kontur oka
            float dx = Mathf.Abs(u - 0.5f) * 2f;
            float topY = 0.5f + 0.25f * (1f - dx * dx);
            float botY = 0.5f - 0.25f * (1f - dx * dx);

            bool inEye = v >= botY && v <= topY && dx <= 0.95f;
            bool onEdge = inEye && (v >= topY - 0.045f || v <= botY + 0.045f);

            // Źrenica w środku
            float pupilDist = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
            bool isPupil = pupilDist <= 0.11f;

            if (onEdge || isPupil) return Color.white;
            return Color.clear;
        });

        // 18. Magnifying Glass Icon (Lupa - badanie szczegółów)
        CreateAndSavePng("Icon_Magnifier.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;

            // Obręcz soczewki (środek w 0.42, 0.58)
            float distLens = Vector2.Distance(new Vector2(u, v), new Vector2(0.42f, 0.58f));
            bool isRim = distLens >= 0.18f && distLens <= 0.25f;

            // Rączka lupy pod kątem 45 stopni
            Vector2 handleStart = new Vector2(0.58f, 0.42f);
            Vector2 handleEnd = new Vector2(0.82f, 0.18f);
            float distToLine = DistanceToSegment(new Vector2(u, v), handleStart, handleEnd);
            bool isHandle = distToLine <= 0.038f;

            return (isRim || isHandle) ? Color.white : Color.clear;
        });

        // 19. Key Icon (Klucz)
        CreateAndSavePng("Icon_Key.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;

            // Kółko główki klucza (góra)
            float distHead = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.72f));
            bool isHeadRing = distHead >= 0.08f && distHead <= 0.16f;

            // Trzonek klucza (dół)
            bool isShaft = (Mathf.Abs(u - 0.5f) <= 0.035f) && (v >= 0.18f && v <= 0.64f);

            // Ząbki klucza
            bool isTooth1 = (u >= 0.50f && u <= 0.65f) && (v >= 0.20f && v <= 0.27f);
            bool isTooth2 = (u >= 0.50f && u <= 0.60f) && (v >= 0.33f && v <= 0.40f);

            return (isHeadRing || isShaft || isTooth1 || isTooth2) ? Color.white : Color.clear;
        });

        // 20. Speech Bubble (Dymek myśli / rozmowy)
        CreateAndSavePng("Icon_SpeechBubble.png", 256, 256, (x, y, w, h) =>
        {
            float u = (float)x / w;
            float v = (float)y / h;

            // Zaokrąglony prostokąt dymka
            bool inBody = (u >= 0.20f && u <= 0.80f) && (v >= 0.35f && v <= 0.75f);
            // Dzióbek
            bool inTail = (u >= 0.25f && u <= 0.42f) && (v >= 0.18f && v <= 0.35f) && (u - 0.25f <= (v - 0.18f));

            return (inBody || inTail) ? Color.white : Color.clear;
        });

        AssetDatabase.Refresh();

        // Konfiguracja Importerów na Sprite 2D
        ConfigureSprites();

        Debug.Log("<color=#70FF70>[HoldSpritesGenerator] Sukces! Wygenerowano pełny zestaw 20 Sprite'ów w Assets/Art/UI_HoldIcons/ !</color>");
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + t * ab);
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

    private static void ConfigureSprites()
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
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
