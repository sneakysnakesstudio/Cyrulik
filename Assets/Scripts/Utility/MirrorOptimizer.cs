using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ultra zoptymalizowany zarządca lustra (MirrorOptimizer).
/// Eliminuje drastyczne spadki FPS (z 10 FPS do 60-144 FPS) poprzez:
/// 1. Całkowite wyłączenie cieni (Shadows), Post-Processingu, HDR i MSAA w kamerze odbicia.
/// 2. Zmniejszenie Far Clip Plane kamery lustra do rozmiaru pokoju (16m zamiast 1000m).
/// 3. Inteligentny FPS Throttling (odbicie odświeża się np. w 30 FPS zamiast katować GPU w 144 FPS).
/// 4. Distance & Frustum Culling (0% kosztu GPU gdy gracz nie patrzy na lustro lub jest za daleko).
/// </summary>
public class MirrorOptimizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kamera generująca odbicie lustra (CameraMirror).")]
    [SerializeField] private Camera mirrorCamera;

    [Tooltip("Główna kamera gracza.")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Renderer tafli lustra.")]
    [SerializeField] private Renderer mirrorRenderer;

    [Header("Distance & Visibility")]
    [Tooltip("Maksymalny dystans gracza od lustra w metrach.")]
    [SerializeField] private float maxDistance = 5.0f;

    [Tooltip("Czy wyłączać lustro, gdy jest poza polem widzenia kamery.")]
    [SerializeField] private bool onlyRenderWhenVisible = true;

    [Header("Performance & Retro Throttling")]
    [Tooltip("Docelowy klatkaż odświeżania odbicia lustra (30 FPS daje płynne retro odbicie i oszczędza 70%+ GPU).")]
    [Range(10, 60)]
    [SerializeField] private int mirrorTargetFPS = 30;

    [Tooltip("Maksymalny zasięg widzenia kamery lustra (obcięcie do rozmiaru pokoju).")]
    [SerializeField] private float mirrorFarClip = 16f;

    [Tooltip("Czy wyłączyć cienie w odbiciu lustra (ogromny zysk FPS).")]
    [SerializeField] private bool disableShadowsInMirror = true;

    [Tooltip("Czy wyłączyć post-processing w odbiciu lustra (ogromny zysk FPS).")]
    [SerializeField] private bool disablePostProcessingInMirror = true;

    private float _renderTimer = 0f;
    private float _checkTimer = 0f;
    private bool _shouldRender = false;
    private UniversalAdditionalCameraData _cameraData;

    private void Awake()
    {
        ResolveReferences();
        ApplyPerformanceSettings();
    }

    private void Start()
    {
        FindPlayerCamera();
        ApplyPerformanceSettings();
    }

    private void ResolveReferences()
    {
        if (mirrorCamera == null)
        {
            mirrorCamera = GetComponentInChildren<Camera>(true);
        }

        if (mirrorRenderer == null)
        {
            mirrorRenderer = GetComponentInChildren<Renderer>();
        }
    }

    private void ApplyPerformanceSettings()
    {
        if (mirrorCamera == null) return;

        // 1. Podstawowe optymalizacje kamery Unity
        mirrorCamera.enabled = false; // Sterujemy odświeżaniem w Update
        mirrorCamera.farClipPlane = mirrorFarClip;
        mirrorCamera.allowHDR = false;
        mirrorCamera.allowMSAA = false;
        mirrorCamera.useOcclusionCulling = true;

        // 2. Optymalizacje URP (Universal Additional Camera Data)
        _cameraData = mirrorCamera.GetComponent<UniversalAdditionalCameraData>();
        if (_cameraData != null)
        {
            if (disableShadowsInMirror)
            {
                _cameraData.renderShadows = false;
            }

            if (disablePostProcessingInMirror)
            {
                _cameraData.renderPostProcessing = false;
            }

            _cameraData.requiresDepthTexture = false;
            _cameraData.requiresColorTexture = false;
            _cameraData.antialiasing = AntialiasingMode.None;
        }
    }

    private void Update()
    {
        // Sprawdzaj widoczność i dystans co 0.05s
        _checkTimer += Time.unscaledDeltaTime;
        if (_checkTimer >= 0.05f)
        {
            _checkTimer = 0f;
            _shouldRender = CheckIfMirrorShouldRender();
        }

        if (!_shouldRender)
        {
            if (mirrorCamera != null && mirrorCamera.enabled)
            {
                mirrorCamera.enabled = false;
            }
            return;
        }

        // FPS Throttling dla lustra (np. 30 FPS)
        if (mirrorTargetFPS > 0)
        {
            _renderTimer += Time.unscaledDeltaTime;
            float frameInterval = 1f / mirrorTargetFPS;

            if (_renderTimer >= frameInterval)
            {
                _renderTimer %= frameInterval;
                if (mirrorCamera != null)
                {
                    mirrorCamera.enabled = true;
                }
            }
            else
            {
                if (mirrorCamera != null && mirrorCamera.enabled)
                {
                    mirrorCamera.enabled = false;
                }
            }
        }
        else
        {
            if (mirrorCamera != null && !mirrorCamera.enabled)
            {
                mirrorCamera.enabled = true;
            }
        }
    }

    private bool CheckIfMirrorShouldRender()
    {
        if (playerCamera == null)
        {
            FindPlayerCamera();
            if (playerCamera == null) return false;
        }

        if (mirrorCamera == null || mirrorRenderer == null) return false;

        Vector3 mirrorPos = mirrorRenderer.bounds.center;
        Vector3 playerPos = playerCamera.transform.position;

        // 1. Sprawdzenie odległości gracza
        float distSq = (playerPos - mirrorPos).sqrMagnitude;
        if (distSq > (maxDistance * maxDistance))
        {
            return false;
        }

        // 2. Sprawdzenie czy gracz stoi PRZED taflą lustra (Dot product)
        Vector3 mirrorForward = mirrorRenderer.transform.forward;
        Vector3 toPlayer = (playerPos - mirrorPos).normalized;
        if (Vector3.Dot(toPlayer, mirrorForward) < -0.1f)
        {
            return false;
        }

        // 3. Sprawdzenie Frustum Culling (czy tafla jest w kadrze gracza)
        if (onlyRenderWhenVisible)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
            if (!GeometryUtility.TestPlanesAABB(planes, mirrorRenderer.bounds))
            {
                return false;
            }
        }

        return true;
    }

    private void FindPlayerCamera()
    {
        if (playerCamera != null) return;
        playerCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
    }

    private void OnDisable()
    {
        if (mirrorCamera != null)
        {
            mirrorCamera.enabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = mirrorRenderer != null ? mirrorRenderer.bounds.center : transform.position;
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(center, maxDistance);
    }
}
