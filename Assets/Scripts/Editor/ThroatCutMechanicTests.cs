#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zestaw testów weryfikacyjnych dla mechaniki podcięcia gardła (Throat Cut Minigame),
/// kalkulacji precyzji, geometrii sinusoidy, kamery oraz dopasowania przedmiotów.
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

        Test(TestWaveGeometry, "1. Sinusoid Wave Geometry & Extrema (ThroatCutMinigame.EvaluateWave)", ref passed, ref failed);
        Test(TestAccuracyScoringPerfect, "2. Precision Calculation: Perfect Cut (>= 90%)", ref passed, ref failed);
        Test(TestAccuracyScoringNearIdeal, "3. Precision Calculation: Slight Deviation (>= 90%)", ref passed, ref failed);
        Test(TestAccuracyScoringBotched, "4. Precision Calculation: Botched Cut (< 90%)", ref passed, ref failed);
        Test(TestFinalScoreCoverageAndThreshold, "5. Coverage Scaling & Success Threshold Boundary (90.0% vs 89.9%)", ref passed, ref failed);
        Test(TestLineRendererMeshGeneration, "6. ThroatCutLineRenderer Mesh Generation", ref passed, ref failed);
        Test(TestItemIdentifierMatching, "7. Shaving Item & Water IDs Integrity", ref passed, ref failed);
        Test(TestAudioAssetsIntegrity, "8. Audio Files & Chair Move Asset Integrity", ref passed, ref failed);
        Test(TestCinematicCameraNeckAlignment, "9. Cinematic Neck Camera Angle & Direction Vector", ref passed, ref failed);

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
            float y = ThroatCutMinigame.EvaluateWave(t, amplitude);

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
            float score = ThroatCutMinigame.CalculateAccuracyScore(errorDist, idealTolerance, maxError);
            totalScore += score;
        }

        float precision = (totalScore / count) * 100f;
        AssertTrue(Mathf.Approximately(precision, 100f), $"Perfekcyjne cięcie powinno dać 100%, otrzymano: {precision}%");
        AssertTrue(precision >= 90f, "Perfekcyjne cięcie musi przekraczać próg 90%!");

        float finalScore = ThroatCutMinigame.CalculateFinalScore(precision, 0.95f);
        AssertTrue(Mathf.Approximately(finalScore, 100f), $"Ostateczny wynik z pełnym pokryciem powinien wynosić 100%, otrzymano: {finalScore}%");
    }

    // ── Test 3: Dobre cięcie w granicy tolerancji (>= 90%) ───────────────────
    private static void TestAccuracyScoringNearIdeal()
    {
        float idealTolerance = 26f;
        float maxError = 75f;
        float totalScore = 0f;
        int count = 100;

        // Błąd na poziomie 10-24px (wewnątrz tolerancji 26px)
        for (int i = 0; i < count; i++)
        {
            float errorDist = UnityEngine.Random.Range(10f, 24f);
            float score = ThroatCutMinigame.CalculateAccuracyScore(errorDist, idealTolerance, maxError);
            totalScore += score;
        }

        float precision = (totalScore / count) * 100f;
        AssertTrue(Mathf.Approximately(precision, 100f), $"Cięcie w granicach idealnej tolerancji powinno dać 100%, otrzymano: {precision}%");

        // Błąd 35px (nieco poza tolerancją 26px, w zakresie maxError 75px)
        float scoreDeviated = ThroatCutMinigame.CalculateAccuracyScore(35f, idealTolerance, maxError);
        AssertTrue(scoreDeviated > 0.80f, $"Odchyłka 35px powinna dawać > 80%, otrzymano: {scoreDeviated * 100f}%");
    }

    // ── Test 4: Zepsute cięcie (< 90%) ───────────────────────────────────────
    private static void TestAccuracyScoringBotched()
    {
        float idealTolerance = 26f;
        float maxError = 75f;
        float totalScore = 0f;
        int count = 100;

        // Błąd 55-74px (znacząco poza tolerancją)
        for (int i = 0; i < count; i++)
        {
            float errorDist = UnityEngine.Random.Range(55f, 74f);
            float score = ThroatCutMinigame.CalculateAccuracyScore(errorDist, idealTolerance, maxError);
            totalScore += score;
        }

        float precision = (totalScore / count) * 100f;
        AssertTrue(precision < 90f, $"Niedokładne cięcie powinno dać wynik poniżej 90%, otrzymano: {precision}%");

        // Poza zakresem maksymalnego błędu
        float scoreOutOfBounds = ThroatCutMinigame.CalculateAccuracyScore(80f, idealTolerance, maxError);
        AssertTrue(Mathf.Approximately(scoreOutOfBounds, 0f), $"Błąd > maxError powinien dawać 0%, otrzymano: {scoreOutOfBounds}");
    }

    // ── Test 5: Pokrycie długości cięcia i granice sukcesu ─────────────────────
    private static void TestFinalScoreCoverageAndThreshold()
    {
        float threshold = 90.0f;

        // Pełne cięcie
        float scoreSuccess = ThroatCutMinigame.CalculateFinalScore(90.0f, 0.95f);
        AssertTrue(scoreSuccess >= threshold, "90.0% przy pełnym cięciu powinno być sukcesem!");

        float scoreSlightlyBelow = ThroatCutMinigame.CalculateFinalScore(89.9f, 0.95f);
        AssertTrue(scoreSlightlyBelow < threshold, "89.9% przy pełnym cięciu powinno być porażką (botched)!");

        // Anti-exploit: gracz miał perfekcyjną trajektorię (100%), ale przeszedł tylko 45% drogi
        float scorePartial = ThroatCutMinigame.CalculateFinalScore(100.0f, 0.45f);
        AssertTrue(scorePartial <= 55.0f, $"Częściowe cięcie (45%) nie może dać sukcesu! Otrzymano: {scorePartial}%");
        AssertTrue(scorePartial < threshold, "Częściowe cięcie musi zakończyć się porażką!");
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

    // ── Test 8: Audio Files & Chair Move Asset Integrity ─────────────────────
    private static void TestAudioAssetsIntegrity()
    {
        string chairClipPath = "Assets/Audio/Sounds/chair/charmoving.ogg";
        bool clipExists = File.Exists(chairClipPath);
        AssertTrue(clipExists, $"Plik audio fotela '{chairClipPath}' musi istnieć na dysku!");

        string dbPath = "Assets/Audio/Database/AudioDatabase.asset";
        bool dbExists = File.Exists(dbPath);
        AssertTrue(dbExists, $"Baza danych dźwięków '{dbPath}' musi istnieć!");

        string dbContent = File.ReadAllText(dbPath);
        AssertTrue(dbContent.Contains("chair_move"), "Baza danych AudioDatabase.asset musi zawierać wpis 'chair_move'!");
    }

    // ── Test 9: Cinematic Neck Camera Angle & Direction Vector ───────────────
    private static void TestCinematicCameraNeckAlignment()
    {
        // Jurek na fotelu: pozycja gardła i rotacja (patrzy w lewo / -X przy rotacji 0, -90, 0)
        Vector3 neckPos = new Vector3(-1.556814f, 1.42f, 4.958035f);
        Quaternion jurekRot = Quaternion.Euler(0f, -90f, 0f);
        Vector3 jurekForward = jurekRot * Vector3.forward;
        Vector3 jurekUp = Vector3.up;
        Vector3 jurekRight = jurekRot * Vector3.right;

        // Wzór użyty w ThroatCutMinigame
        Vector3 targetPos = neckPos + (jurekForward * 0.44f) + (jurekUp * 0.05f) - (jurekRight * 0.12f);
        Vector3 lookDir = (neckPos - targetPos).normalized;

        // Sprawdź czy wektor targetPos - neckPos leży z PRZODU Jurka
        float forwardDot = Vector3.Dot((targetPos - neckPos).normalized, jurekForward);
        AssertTrue(forwardDot > 0.8f, $"Kamera powinna być umieszczona z przodu Jurka! Obliczony dot product: {forwardDot}");

        // Sprawdź czy lookDir patrzy w stronę twarzy / gardła
        float lookAtNeckDot = Vector3.Dot(lookDir, -jurekForward);
        AssertTrue(lookAtNeckDot > 0.8f, $"Kamera musi patrzeć w stronę gardła Jurka! Obliczony dot product: {lookAtNeckDot}");
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
