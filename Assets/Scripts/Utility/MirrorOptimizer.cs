using UnityEngine;

/// <summary>
/// Prosty i super lekki optymalizator lustra.
/// Włącza kamerę odbicia (CameraMirror) TYLKO wtedy, gdy gracz jest w pobliżu i patrzy na lustro.
/// Gdy gracz odejdzie — kamera lustra jest całkowicie wyłączana (0% obciążenia GPU).
/// </summary>
public class MirrorOptimizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kamera generująca odbicie lustra (CameraMirror z przypisaną RenderTexture).")]
    [SerializeField] private Camera mirrorCamera;

    [Tooltip("Główna kamera gracza (jeśli puste, automatycznie pobierze Camera.main).")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Renderer tafli lustra do sprawdzania widoczności (np. Plane lub MirrorObject).")]
    [SerializeField] private Renderer mirrorRenderer;

    [Header("Distance Optimization")]
    [Tooltip("Maksymalny dystans gracza od lustra w metrach (np. 5-7 metrów). Poza tym dystansem lustro jest wyłączone.")]
    [SerializeField] private float maxDistance = 6f;

    [Header("Frustum / Visibility Optimization")]
    [Tooltip("Czy wyłączać kamerę lustra, gdy tafla znajduje się poza polem widzenia gracza (np. gracz stoi tyłem).")]
    [SerializeField] private bool onlyRenderWhenVisible = true;

    [Tooltip("Jak często sprawdzać odległość (w sekundach). 0.08s = ultra lekki koszt procesora.")]
    [SerializeField] private float checkInterval = 0.08f;

    private float _timer;

    private void Awake()
    {
        if (mirrorCamera == null)
        {
            mirrorCamera = GetComponentInChildren<Camera>();
        }

        if (mirrorRenderer == null)
        {
            mirrorRenderer = GetComponentInChildren<Renderer>();
        }
    }

    private void Start()
    {
        FindPlayerCamera();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < checkInterval)
            return;

        _timer = 0f;

        if (playerCamera == null)
        {
            FindPlayerCamera();
            if (playerCamera == null)
                return;
        }

        if (mirrorCamera == null)
            return;

        Vector3 mirrorPos = mirrorRenderer != null ? mirrorRenderer.bounds.center : transform.position;
        float distanceSq = (playerCamera.transform.position - mirrorPos).sqrMagnitude;

        // 1. Sprawdzenie odległości gracza
        if (distanceSq > maxDistance * maxDistance)
        {
            SetMirrorActive(false);
            return;
        }

        // 2. Sprawdzenie czy lustro mieści się w kadrze kamery gracza
        if (onlyRenderWhenVisible && mirrorRenderer != null)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
            if (!GeometryUtility.TestPlanesAABB(planes, mirrorRenderer.bounds))
            {
                SetMirrorActive(false);
                return;
            }
        }

        // Gracz jest blisko i patrzy w stronę lustra -> Włącz kamerę odbicia
        SetMirrorActive(true);
    }

    private void SetMirrorActive(bool active)
    {
        if (mirrorCamera != null && mirrorCamera.enabled != active)
        {
            mirrorCamera.enabled = active;
        }
    }

    private void FindPlayerCamera()
    {
        if (playerCamera != null) return;
        playerCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = mirrorRenderer != null ? mirrorRenderer.bounds.center : transform.position;
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(center, maxDistance);
    }
}
