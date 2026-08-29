using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Zero-Lag Retro Mirror — wydajny system renderowania odbicia lustrzanego w stylu PSX.
/// Kamera lustra NIGDY nie jest włączona automatycznie przez URP — renderujemy ją manualnie
/// przez mirrorCamera.Render() wyłącznie gdy:
/// 1. Lustro jest widoczne w kamerze gracza (frustum culling).
/// 2. Gracz jest wystarczająco blisko (distance culling).
/// 3. Upłynął wymagany czas od ostatniego renderu (FPS throttling).
/// Dzięki temu eliminujemy główną przyczynę lagów (ciągłe przebudowywanie pipelinu URP).
/// </summary>
public class PlanarMirror : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mirrorCamera;
    [SerializeField] private MeshRenderer mirrorRenderer;

    [Header("Render Texture")]
    [Tooltip("Wysokość tekstury. 256 = PSX retro styl (4x mniej GPU niż 512).")]
    [SerializeField] private int textureHeight = 256;
    [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;

    [Header("Performance")]
    [Tooltip("Powyżej tej odległości lustro przestaje renderować.")]
    [SerializeField] private float maxRenderDistance = 5f;

    [Tooltip("Ile razy na sekundę odświeżać odbicie. 15 = retro PSX, 30 = płynniej.")]
    [SerializeField] private int mirrorTargetFPS = 15;

    [Tooltip("Maksymalny dystans renderowania kamery lustra (farClipPlane). Nie renderujemy obiektów zza ścian.")]
    [SerializeField] private float mirrorFarClip = 6f;

    [Header("Enable / Disable Mirror")]
    [Tooltip("Wyłącz lustro całkowicie, aby zaoszczędzić GPU na słabych maszynach.")]
    [SerializeField] public bool enableMirror = true;

    private RenderTexture _rt;
    private Camera _playerCamera;
    private float _timer;
    private Plane[] _frustumPlanes;

    private void Start()
    {
        _playerCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        if (mirrorCamera == null)
            mirrorCamera = GetComponentInChildren<Camera>(true);

        if (mirrorRenderer == null)
            mirrorRenderer = GetComponentInChildren<MeshRenderer>();

        if (!enableMirror)
        {
            DisableMirror();
            return;
        }

        SetupCamera();
        CreateRT();

        // WAŻNE: kamera musi być zawsze wyłączona — renderujemy ją manualnie przez Render()
        // URP nie doda jej do swojego pipelinu, więc nie będzie powodować lagów
        if (mirrorCamera != null)
            mirrorCamera.enabled = false;
    }

    private void SetupCamera()
    {
        if (mirrorCamera == null) return;

        // Kamera wyłączona na stałe — manualny render
        mirrorCamera.enabled = false;
        mirrorCamera.allowHDR = false;
        mirrorCamera.allowMSAA = false;
        mirrorCamera.useOcclusionCulling = true;

        // Ograniczamy zasięg — nie renderujemy obiektów zza ścian
        mirrorCamera.farClipPlane = mirrorFarClip;

        var data = mirrorCamera.GetComponent<UniversalAdditionalCameraData>();
        if (data != null)
        {
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            data.antialiasing = AntialiasingMode.None;
        }
    }

    private void CreateRT()
    {
        if (mirrorCamera == null) return;

        float aspect = mirrorCamera.aspect > 0 ? mirrorCamera.aspect : 1f;
        int w = Mathf.Clamp(Mathf.RoundToInt(textureHeight * aspect), 64, 2048);
        int h = Mathf.Max(64, textureHeight);

        _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
        {
            name       = "MirrorRT",
            filterMode = filterMode,
            wrapMode   = TextureWrapMode.Clamp,
            useMipMap  = false
        };
        _rt.Create();

        mirrorCamera.targetTexture = _rt;

        if (mirrorRenderer != null)
        {
            Material mat = mirrorRenderer.material;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", _rt);
            else
                mat.mainTexture = _rt;
        }
    }

    private void LateUpdate()
    {
        if (!enableMirror || mirrorCamera == null) return;

        // --- THROTTLING: renderuj nie częściej niż mirrorTargetFPS razy na sekundę ---
        if (mirrorTargetFPS > 0)
        {
            _timer += Time.unscaledDeltaTime;
            float interval = 1f / mirrorTargetFPS;
            if (_timer < interval) return;
            _timer = 0f;
        }

        // --- DISTANCE CULLING: nie renderuj gdy gracz jest za daleko ---
        if (_playerCamera != null)
        {
            Vector3 mirrorPos = mirrorRenderer != null
                ? mirrorRenderer.bounds.center
                : transform.position;

            float distSqr = (mirrorPos - _playerCamera.transform.position).sqrMagnitude;
            if (distSqr > maxRenderDistance * maxRenderDistance) return;
        }

        // --- FRUSTUM CULLING: renderuj tylko gdy tafla lustra jest widoczna ---
        if (mirrorRenderer != null && _playerCamera != null)
        {
            _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_playerCamera);
            if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, mirrorRenderer.bounds))
                return;
        }

        // --- MANUALNY RENDER: tylko na żądanie, bez angażowania pipelinu URP ---
        mirrorCamera.Render();
    }

    /// <summary>
    /// Włącza lub wyłącza renderowanie lustra w czasie gry (np. z ekranu ustawień graficznych).
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        enableMirror = enabled;
        if (!enabled)
            DisableMirror();
    }

    private void DisableMirror()
    {
        if (mirrorCamera != null)
            mirrorCamera.enabled = false;
    }

    private void OnDestroy()
    {
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }
    }
}
