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

        AssetDatabase.Refresh();

        // Konfiguracja Importerów na Sprite 2D
        ConfigureSprites();

        Debug.Log("<color=#70FF70>[HoldSpritesGenerator] Sukces! Wygenerowano 10 krystalicznie czystych Sprite'ów w Assets/Art/UI_HoldIcons/ !</color>");
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
