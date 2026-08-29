using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Jednorazowy konfigurator wydajności kamery lustra.
/// PlanarMirror.cs obsługuje: Frustum Culling, Distance Culling i FPS Throttling.
/// Ten komponent wyłącza kosztowne funkcje (cienie, post-processing, HDR, MSAA)
/// przy starcie i udostępnia metodę publiczną SetMirrorEnabled() do przełączania lustra z UI ustawień.
/// </summary>
public class MirrorOptimizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kamera lustra (CameraMirror). Jeśli puste, automatycznie pobierze z dzieci.")]
    [SerializeField] private Camera mirrorCamera;

    [Tooltip("Renderer tafli lustra. Jeśli puste, automatycznie pobierze z dzieci.")]
    [SerializeField] private Renderer mirrorRenderer;

    [Header("Distance & Visibility Culling")]
    [Tooltip("Maksymalny dystans gracza od lustra w metrach.")]
    [SerializeField] private float maxDistance = 6f;

    [Header("Performance Settings (stosowane raz przy starcie)")]
    [Tooltip("Wyłącz cienie w odbiciu (ogromny zysk FPS).")]
    [SerializeField] private bool disableShadowsInMirror = true;

    [Tooltip("Wyłącz post-processing w odbiciu (ogromny zysk FPS).")]
    [SerializeField] private bool disablePostProcessingInMirror = true;

    private void Awake()
    {
        if (mirrorCamera == null)
            mirrorCamera = GetComponentInChildren<Camera>(true);

        if (mirrorRenderer == null)
            mirrorRenderer = GetComponentInChildren<Renderer>();

        // WAŻNE: zawsze wyłącz kamerę lustra przy starcie — PlanarMirror renderuje ją manualnie
        if (mirrorCamera != null)
            mirrorCamera.enabled = false;
    }

    private void Start()
    {
        ApplyPerformanceSettings();
    }

    private void ApplyPerformanceSettings()
    {
        if (mirrorCamera == null) return;

        // Wyłącz drogie funkcje jednorazowo — PlanarMirror zarządza samym renderowaniem
        mirrorCamera.allowHDR = false;
        mirrorCamera.allowMSAA = false;

        var cameraData = mirrorCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            if (disableShadowsInMirror)
                cameraData.renderShadows = false;

            if (disablePostProcessingInMirror)
                cameraData.renderPostProcessing = false;

            cameraData.requiresDepthTexture = false;
            cameraData.requiresColorTexture = false;
            cameraData.antialiasing = AntialiasingMode.None;
        }
    }

    /// <summary>
    /// Włącza lub wyłącza lustro z zewnątrz (np. z ekranu ustawień graficznych lub GameOptimizer).
    /// Pobiera komponent PlanarMirror z obiektu lub jego dzieci i ustawia enableMirror.
    /// </summary>
    public void SetMirrorEnabled(bool enabled)
    {
        PlanarMirror mirror = GetComponent<PlanarMirror>();
        if (mirror == null)
            mirror = GetComponentInChildren<PlanarMirror>(true);

        if (mirror != null)
        {
            mirror.SetEnabled(enabled);
            Debug.Log($"[MirrorOptimizer] Lustro: {(enabled ? "WŁĄCZONE" : "WYŁĄCZONE")}");
        }
        else
        {
            Debug.LogWarning("[MirrorOptimizer] Nie znaleziono komponentu PlanarMirror!", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = mirrorRenderer != null ? mirrorRenderer.bounds.center : transform.position;
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(center, maxDistance);
    }
}
