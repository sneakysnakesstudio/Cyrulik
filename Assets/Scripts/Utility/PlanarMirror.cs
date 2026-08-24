using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Proste lustro — stała kamera (CameraMirror) renderuje widok pokoju do RenderTexture,
/// która jest wyświetlana na tafli lustra. Kamera się NIE rusza.
/// Optymalizacje: brak cieni/PP, distance culling, FPS throttling.
/// </summary>
public class PlanarMirror : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mirrorCamera;
    [SerializeField] private MeshRenderer mirrorRenderer;

    [Header("Render Texture")]
    [Tooltip("Wysokość textury. 256 = PSX styl, 512 = czyściej.")]
    [SerializeField] private int textureHeight = 512;
    [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;

    [Header("Performance")]
    [Tooltip("Powyżej tej odległości lustro przestaje renderować.")]
    [SerializeField] private float maxRenderDistance = 8f;
    [Tooltip("FPS throttling — 30 FPS odbicia zamiast 144. 0 = bez limitu.")]
    [SerializeField] private int mirrorTargetFPS = 30;

    private RenderTexture _rt;
    private Camera _playerCamera;
    private float _timer;

    private void Start()
    {
        _playerCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        if (mirrorCamera == null)
            mirrorCamera = GetComponentInChildren<Camera>(true);

        if (mirrorRenderer == null)
            mirrorRenderer = GetComponentInChildren<MeshRenderer>();

        SetupCamera();
        CreateRT();
    }

    private void SetupCamera()
    {
        if (mirrorCamera == null) return;

        mirrorCamera.allowHDR = false;
        mirrorCamera.allowMSAA = false;
        mirrorCamera.useOcclusionCulling = true;
        mirrorCamera.enabled = true;

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
            name = "MirrorRT",
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false
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
        if (mirrorCamera == null) return;

        // Distance culling
        if (_playerCamera != null)
        {
            Vector3 mirrorPos = mirrorRenderer != null
                ? mirrorRenderer.bounds.center
                : transform.position;

            float dist = Vector3.Distance(_playerCamera.transform.position, mirrorPos);
            if (dist > maxRenderDistance)
            {
                mirrorCamera.enabled = false;
                return;
            }
        }

        // FPS throttling
        if (mirrorTargetFPS > 0)
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < 1f / mirrorTargetFPS)
            {
                mirrorCamera.enabled = false;
                return;
            }
            _timer = 0f;
        }

        mirrorCamera.enabled = true;
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