using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zoptymalizowany, wydajny komponent graficzny UI do rysowania płynnych linii na Canvasie (0 GC w Update).
/// Używany w minigrze podcięcia gardła (ThroatCutMinigame) do rysowania:
/// 1. Falistej sinusoidy wyznaczającej idealny tor cięcia chirurgicznego.
/// 2. Dynamicznej linii cięcia krwią (arterial blood cut line), rozwijającej się za ostrzem brzytwy.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ThroatCutLineRenderer : Graphic
{
    [Header("Line Settings")]
    [Tooltip("Grubość linii w pikselach UI.")]
    [SerializeField] private float lineWidth = 6.0f;

    [Tooltip("Kolor początkowy linii (gradient).")]
    [SerializeField] private Color startColor = Color.white;

    [Tooltip("Kolor końcowy linii (gradient).")]
    [SerializeField] private Color endColor = Color.white;

    [Tooltip("Czy linia ma mieć zaokrąglone / wygładzone końce i łączenia.")]
    [SerializeField] private bool roundJoints = true;

    [Tooltip("Opcjonalny tryb linii przerywanej / lontu (przydatny dla fali pomocniczej).")]
    [SerializeField] private bool isDotted = false;

    [Tooltip("Długość kreski w trybie przerywanym.")]
    [SerializeField] private float dashLength = 12.0f;

    [Tooltip("Długość przerwy między kreskami.")]
    [SerializeField] private float gapLength = 8.0f;

    // Bufor punktów do generowania siatki — reużywalny, zero alokacji pamięci w pętli renderowania
    private readonly List<Vector2> _points = new List<Vector2>(128);

    public float LineWidth
    {
        get => lineWidth;
        set
        {
            if (Mathf.Approximately(lineWidth, value)) return;
            lineWidth = value;
            SetVerticesDirty();
        }
    }

    public Color StartColor
    {
        get => startColor;
        set
        {
            startColor = value;
            SetVerticesDirty();
        }
    }

    public Color EndColor
    {
        get => endColor;
        set
        {
            endColor = value;
            SetVerticesDirty();
        }
    }

    public IReadOnlyList<Vector2> Points => _points;

    /// <summary>
    /// Ustawia całą listę punktów linii (kopiuje bez alokacji).
    /// </summary>
    public void SetPoints(IList<Vector2> newPoints)
    {
        _points.Clear();
        if (newPoints != null)
        {
            for (int i = 0; i < newPoints.Count; i++)
            {
                _points.Add(newPoints[i]);
            }
        }
        SetVerticesDirty();
    }

    /// <summary>
    /// Dodaje pojedynczy punkt do linii (np. podczas rysowania cięcia brzytwą).
    /// </summary>
    public void AddPoint(Vector2 point)
    {
        _points.Add(point);
        SetVerticesDirty();
    }

    /// <summary>
    /// Czyści wszystkie punkty linii.
    /// </summary>
    public void ClearPoints()
    {
        if (_points.Count == 0) return;
        _points.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        int count = _points.Count;
        if (count < 2 || lineWidth <= 0.01f)
            return;

        float halfWidth = lineWidth * 0.5f;

        if (isDotted)
        {
            DrawDottedLine(vh, halfWidth);
        }
        else
        {
            DrawSolidLine(vh, halfWidth);
        }
    }

    private void DrawSolidLine(VertexHelper vh, float halfWidth)
    {
        int count = _points.Count;
        float totalLength = 0f;

        // 1. Oblicz całkowitą długość linii do interpolacji kolorów (gradient)
        for (int i = 0; i < count - 1; i++)
        {
            totalLength += Vector2.Distance(_points[i], _points[i + 1]);
        }
        if (totalLength <= 0.001f) totalLength = 1f;

        float currentDist = 0f;

        // 2. Generowanie pasm quadów
        for (int i = 0; i < count - 1; i++)
        {
            Vector2 p0 = _points[i];
            Vector2 p1 = _points[i + 1];

            Vector2 dir = p1 - p0;
            float segLen = dir.magnitude;
            if (segLen <= 0.0001f) continue;

            dir /= segLen;
            Vector2 normal = new Vector2(-dir.y, dir.x) * halfWidth;

            float t0 = Mathf.Clamp01(currentDist / totalLength);
            float t1 = Mathf.Clamp01((currentDist + segLen) / totalLength);
            currentDist += segLen;

            Color c0 = Color.Lerp(startColor, endColor, t0) * color;
            Color c1 = Color.Lerp(startColor, endColor, t1) * color;

            int vertIndex = vh.currentVertCount;

            // 4 wierzchołki segmentu
            vh.AddVert(p0 - normal, c0, new Vector2(0f, 0f));
            vh.AddVert(p0 + normal, c0, new Vector2(0f, 1f));
            vh.AddVert(p1 + normal, c1, new Vector2(1f, 1f));
            vh.AddVert(p1 - normal, c1, new Vector2(1f, 0f));

            vh.AddTriangle(vertIndex + 0, vertIndex + 1, vertIndex + 2);
            vh.AddTriangle(vertIndex + 2, vertIndex + 3, vertIndex + 0);

            // Łączenie segmentów (miter join), aby linia była ciągła bez przerw
            if (roundJoints && i < count - 2)
            {
                Vector2 p2 = _points[i + 2];
                Vector2 nextDir = (p2 - p1).normalized;
                Vector2 nextNormal = new Vector2(-nextDir.y, nextDir.x) * halfWidth;

                int joinIndex = vh.currentVertCount;
                vh.AddVert(p1, c1, new Vector2(0.5f, 0.5f));
                vh.AddVert(p1 + normal, c1, new Vector2(1f, 1f));
                vh.AddVert(p1 + nextNormal, c1, new Vector2(0f, 1f));

                vh.AddTriangle(joinIndex + 0, joinIndex + 1, joinIndex + 2);

                vh.AddVert(p1, c1, new Vector2(0.5f, 0.5f));
                vh.AddVert(p1 - normal, c1, new Vector2(1f, 0f));
                vh.AddVert(p1 - nextNormal, c1, new Vector2(0f, 0f));

                vh.AddTriangle(joinIndex + 3, joinIndex + 4, joinIndex + 5);
            }
        }
    }

    private void DrawDottedLine(VertexHelper vh, float halfWidth)
    {
        int count = _points.Count;
        float dashPattern = Mathf.Max(0.5f, dashLength + gapLength);
        float currentPatternPos = 0f;

        for (int i = 0; i < count - 1; i++)
        {
            Vector2 p0 = _points[i];
            Vector2 p1 = _points[i + 1];

            Vector2 dir = p1 - p0;
            float segLen = dir.magnitude;
            if (segLen <= 0.0001f) continue;

            dir /= segLen;
            Vector2 normal = new Vector2(-dir.y, dir.x) * halfWidth;

            float covered = 0f;
            while (covered < segLen)
            {
                float inPattern = currentPatternPos % dashPattern;
                if (inPattern < dashLength)
                {
                    // Rysujemy kreskę
                    float remainingDash = dashLength - inPattern;
                    float drawDist = Mathf.Min(segLen - covered, remainingDash);

                    Vector2 sub0 = p0 + dir * covered;
                    Vector2 sub1 = sub0 + dir * drawDist;

                    Color c = Color.Lerp(startColor, endColor, (float)i / count) * color;

                    int vertIndex = vh.currentVertCount;
                    vh.AddVert(sub0 - normal, c, new Vector2(0f, 0f));
                    vh.AddVert(sub0 + normal, c, new Vector2(0f, 1f));
                    vh.AddVert(sub1 + normal, c, new Vector2(1f, 1f));
                    vh.AddVert(sub1 - normal, c, new Vector2(1f, 0f));

                    vh.AddTriangle(vertIndex + 0, vertIndex + 1, vertIndex + 2);
                    vh.AddTriangle(vertIndex + 2, vertIndex + 3, vertIndex + 0);

                    covered += drawDist;
                    currentPatternPos += drawDist;
                }
                else
                {
                    // Przerwa
                    float remainingGap = dashPattern - inPattern;
                    float skipDist = Mathf.Min(segLen - covered, remainingGap);

                    covered += skipDist;
                    currentPatternPos += skipDist;
                }
            }
        }
    }
}
