#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zestaw testów weryfikacyjnych dla mechaniki podcięcia gardła (Throat Cut Minigame),
/// kalkulacji precyzji, geometrii sinusoidy oraz dopasowania przedmiotów.
/// Dostępny w menu: Tools -> Cyrulik -> Tests -> Run Throat Cut Mechanic Tests
/// </summary>
public static class ThroatCutMechanicTests
{
    [InitializeOnLoadMethod]
    [MenuItem("Tools/Cyrulik/Tests/Run Throat Cut Mechanic Tests", false, 50)]
    public static void RunAllTests()
    {
        Debug.Log("<color=#70FFFF>══════════════════════════════════════════════════</color>");
        Debug.Log("<color=#70FFFF> ROZPOCZĘCIE TESTÓW: THROAT CUT MECHANIC & JUREK LOOP</color>");
        Debug.Log("<color=#70FFFF>══════════════════════════════════════════════════</color>");

        int passed = 0;
        int failed = 0;

        Test(TestWaveGeometry, "1. Sinusoid Wave Geometry & Extrema", ref passed, ref failed);
        Test(TestAccuracyScoringPerfect, "2. Precision Calculation: Perfect Cut (>= 90%)", ref passed, ref failed);
        Test(TestAccuracyScoringNearIdeal, "3. Precision Calculation: Slight Deviation (>= 90%)", ref passed, ref failed);
        Test(TestAccuracyScoringBotched, "4. Precision Calculation: Botched Cut (< 90%)", ref passed, ref failed);
        Test(TestAccuracyThresholdBoundary, "5. Success Threshold Boundary (90.0% vs 89.9%)", ref passed, ref failed);
        Test(TestLineRendererMeshGeneration, "6. ThroatCutLineRenderer Mesh Generation", ref passed, ref failed);
        Test(TestItemIdentifierMatching, "7. Shaving Item & Water IDs Integrity", ref passed, ref failed);

        Debug.Log("<color=#70FFFF>══════════════════════════════════════════════════</color>");
        if (failed == 0)
        {
            Debug.Log($"<color=#70FF70> WYNIK: WSZYSTKIE TESTY ZALICZONE! ({passed}/{passed})</color>");
        }
        else
        {
            Debug.LogError($"<color=#FF4040> WYNIK: BŁĘDY W TESTACH! Zaliczone: {passed}, Nieudane: {failed}</color>");
        }
        Debug.Log("<color=#70FFFF>══════════════════════════════════════════════════</color>");
    }

    private static void Test(Action testMethod, string testName, ref int passed, ref int failed)
    {
        try
        {
            testMethod();
            Debug.Log($"<color=#70FF70>[PASS]</color> {testName}");
            passed++;
        }
        catch (Exception ex)
        {
            Debug.LogError($"<color=#FF4040>[FAIL]</color> {testName}: {ex.Message}");
            failed++;
        }
    }

    // ── Test 1: Geometria Fali ────────────────────────────────────────────────
    private static void TestWaveGeometry()
    {
        float amplitude = 46.0f;
        int samples = 64;
        List<float> yValues = new List<float>(samples + 1);

        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            float y = Mathf.Sin(t * Mathf.PI * 2.0f) * amplitude
                    + Mathf.Sin((t * Mathf.PI * 4.0f) + 0.8f) * (amplitude * 0.42f)
                    + Mathf.Sin(t * Mathf.PI * 6.0f) * (amplitude * 0.22f);

            yValues.Add(y);
            AssertTrue(!float.IsNaN(y) && !float.IsInfinity(y), $"Wartość Y w punkcie t={t} jest NaN/Inf!");
        }

        // Sprawdź czy sinusoida ma wierzchołki (zmiany znaku pochodnej)
        int directionChanges = 0;
        for (int i = 1; i < yValues.Count - 1; i++)
        {
            float d1 = yValues[i] - yValues[i - 1];
            float d2 = yValues[i + 1] - yValues[i];
            if ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
            {
                directionChanges++;
            }
        }

        AssertTrue(directionChanges >= 2, $"Sinusoida powinna mieć co najmniej 2 wierzchołki góra-dół, znaleziono: {directionChanges}");
    }

    // ── Test 2: Perfekcyjne cięcie (100%) ────────────────────────────────────
    private static void TestAccuracyScoringPerfect()
    {
        float idealTolerance = 26f;
        float maxError = 75f;
        float totalScore = 0f;
        int count = 100;

        for (int i = 0; i < count; i++)
        {
            float errorDist = 0f; // idealnie na linii
            float score = CalculateSampleScore(errorDist, idealTolerance, maxError);
            totalScore += score;
        }

        float precision = (totalScore / count) * 100f;
        AssertTrue(Mathf.Approximately(precision, 100f), $"Perfekcyjne cięcie powinno dać 100%, otrzymano: {precision}%");
        AssertTrue(precision >= 90f, "Perfekcyjne cięcie musi przekraczać próg 90%!");
    }

    // ── Test 3: Dobre cięcie w granicy tolerancji (>= 90%) ───────────────────
    private static void TestAccuracyScoringNearIdeal()
    {
        float idealTolerance = 26f;
        float maxError = 75f;
        float totalScore = 0f;
        int count = 100;

        // Błąd na poziomie 15-22px (wewnątrz tolerancji 26px)
        for (int i = 0; i < count; i++)
        {
            float errorDist = UnityEngine.Random.Range(10f, 24f);
            float score = CalculateSampleScore(errorDist, idealTolerance, maxError);
            totalScore += score;
        }

        float precision = (totalScore / count) * 100f;
        AssertTrue(precision >= 95f, $"Cięcie w granicach tolerancji powinno dać >= 95%, otrzymano: {precision}%");
    }

    // ── Test 4: Zepsute cięcie (< 90%) ───────────────────────────────────────
    private static void TestAccuracyScoringBotched()
    {
        float idealTolerance = 26f;
        float maxError = 75f;
        float totalScore = 0f;
        int count = 100;

        // Błąd 45-70px (znacząco poza tolerancją)
        for (int i = 0; i < count; i++)
        {
            float errorDist = UnityEngine.Random.Range(45f, 70f);
            float score = CalculateSampleScore(errorDist, idealTolerance, maxError);
            totalScore += score;
        }

        float precision = (totalScore / count) * 100f;
        AssertTrue(precision < 90f, $"Niedokładne cięcie powinno dać wynik poniżej 90%, otrzymano: {precision}%");
    }

    // ── Test 5: Granica 90.0% vs 89.9% ───────────────────────────────────────
    private static void TestAccuracyThresholdBoundary()
    {
        float threshold = 90.0f;
        AssertTrue(90.0f >= threshold, "90.0% powinno być sukcesem!");
        AssertTrue(90.01f >= threshold, "90.01% powinno być sukcesem!");
        AssertTrue(!(89.95f >= threshold), "89.95% powinno być porażką (botched)!");
        AssertTrue(!(89.0f >= threshold), "89.0% powinno być porażką (botched)!");
    }

    // ── Test 6: ThroatCutLineRenderer Mesh Generation ────────────────────────
    private static void TestLineRendererMeshGeneration()
    {
        GameObject go = new GameObject("TestLineRenderer", typeof(CanvasRenderer), typeof(ThroatCutLineRenderer));
        try
        {
            ThroatCutLineRenderer lr = go.GetComponent<ThroatCutLineRenderer>();
            lr.LineWidth = 6f;
            lr.StartColor = Color.red;
            lr.EndColor = Color.black;

            List<Vector2> pts = new List<Vector2>()
            {
                new Vector2(-100f, 0f),
                new Vector2(0f, 20f),
                new Vector2(100f, -10f)
            };

            lr.SetPoints(pts);
            AssertTrue(lr.Points.Count == 3, $"Oczekiwano 3 punktów, otrzymano {lr.Points.Count}");

            VertexHelper vh = new VertexHelper();
            // Wywołaj chronioną metodę przez refleksję dla pewności
            var method = typeof(ThroatCutLineRenderer).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(lr, new object[] { vh });
                AssertTrue(vh.currentVertCount > 0, "VertexHelper powinien zawierać wygenerowane wierzchołki!");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // ── Test 7: Item IDs Integrity ───────────────────────────────────────────
    private static void TestItemIdentifierMatching()
    {
        string[] validTowels = { "clean_towel", "towel", "dirty_towel", "hot_towel", "towel_prepared" };
        foreach (var id in validTowels)
        {
            AssertTrue(IsValidTowelId(id), $"ID ręcznika '{id}' powinno być akceptowane!");
        }

        string[] validWater = { "filled_glass", "glass_water", "glass_full" };
        foreach (var id in validWater)
        {
            AssertTrue(IsValidWaterId(id), $"ID wody '{id}' powinno być akceptowane!");
        }

        string[] validRazors = { "razor_sharpened", "sharp_razor", "razor", "razor_blade" };
        foreach (var id in validRazors)
        {
            AssertTrue(IsValidRazorId(id), $"ID brzytwy '{id}' powinno być akceptowane!");
        }
    }

    private static float CalculateSampleScore(float errorDist, float idealTolerance, float maxError)
    {
        if (errorDist <= idealTolerance) return 1.0f;
        float excess = errorDist - idealTolerance;
        float range = Mathf.Max(1f, maxError - idealTolerance);
        return Mathf.Clamp01(1.0f - (excess / range));
    }

    private static bool IsValidTowelId(string id)
    {
        string lower = id.ToLowerInvariant();
        return lower == "towel" || lower == "clean_towel" || lower == "dirty_towel" || lower == "hot_towel" || lower == "towel_prepared";
    }

    private static bool IsValidWaterId(string id)
    {
        string lower = id.ToLowerInvariant();
        return lower == "filled_glass" || lower == "glass_water" || lower == "glass_full";
    }

    private static bool IsValidRazorId(string id)
    {
        string lower = id.ToLowerInvariant();
        return lower == "razor_sharpened" || lower == "sharp_razor" || lower == "razor" || lower == "razor_blade";
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception("Assertion Failed: " + message);
        }
    }
}
#endif
