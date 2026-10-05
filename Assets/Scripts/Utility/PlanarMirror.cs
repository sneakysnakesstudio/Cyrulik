using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ultra-Optimized Planar Mirror (URP) — maksymalnie wydajny system odbicia lustrzanego.
/// Kluczowe optymalizacje:
/// 1. Zero alokacji GC: zbuforowana tablica Plane[6] do Frustum Cullingu (zero garbage collection hitching).
/// 2. Distance Culling + Frustum Culling: lustro nie renderuje się, gdy gracz jest za daleko lub nie patrzy na taflę.
/// 3. Back-Face Culling: brak renderowania, gdy gracz stoi za ścianą / za taflą lustra.
/// 4. Wyłączenie post-processingu, cieni, volumów, MSAA i HDR na kamerze lustra (kolosalny zysk GPU).
/// 5. Elastyczność: obsługa stałego kadru (zdefiniowanego w prefabie) lub dynamicznego odbicia planarnego (Dynamic Reflection).
/// </summary>
public class PlanarMirror : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kamera generująca odbicie (dziecko prefabu lustra).")]
    [SerializeField] private Camera mirrorCamera;

    [Tooltip("Renderer tafli lustra, na który nakładana jest tekstura odbicia.")]
    [SerializeField] private MeshRenderer mirrorRenderer;

    [Header("Render Texture")]
    [Tooltip("Wysokość tekstury odbicia w pikselach. 256 = styl PSX (błyskawiczny render), 384 = idealny kompromis.")]
    [SerializeField] private int textureHeight = 256;
    [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;

    [Header("Performance & Culling")]
    [Tooltip("Powyżej tej odległości (w metrach) lustro całkowicie wyłącza renderowanie.")]
    [SerializeField] private float maxRenderDistance = 6.0f;

    [Tooltip("Docelowy klatkaż odświeżania odbicia (FPS). 30 = płynne odbicie przy 50% oszczędności GPU, 0 = każda klatka.")]
    [SerializeField] private int mirrorTargetFPS = 30;

    [Tooltip("Maksymalny zasięg widzenia kamery lustra (farClipPlane). Obiekty dalej niż ten dystans nie są rysowane.")]
    [SerializeField] private float mirrorFarClip = 6.0f;

    [Tooltip("Warstwy renderowane w lustrze. Wyklucz UI i cząsteczki.")]
    [SerializeField] private LayerMask reflectionLayers = ~0;

    [Header("Reflection Mode")]
    [Tooltip("Gdy wyłączone (domyślnie): kamera zachowuje kadr ustawiony w prefabie/scenie. Gdy włączone: kamera dynamicznie podąża za pozycją gracza.")]
    [SerializeField] private bool dynamicReflection = false;

    [Tooltip("Oś normalnej tafli lustra (w przestrzeni lokalnej renderera tafli) używana do dynamicznego odbicia.")]
    [SerializeField] private Vector3 mirrorFacingAxis = Vector3.right;

    [Header("Enable / Disable Mirror")]
    [Tooltip("Główny włącznik lustra (np. do wyłączenia w opcjach graficznych).")]
    [SerializeField] public bool enableMirror = true;

    private RenderTexture _rt;
    private Camera _playerCamera;
    private float _timer;
    private MaterialPropertyBlock _propBlock;

    // Cache struktur — zero alokacji GC w pętli renderowania!
    private readonly Plane[] _frustumPlanes = new Plane[6];
    private static readonly int BaseMapPropId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexPropId = Shader.PropertyToID("_MainTex");

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
    }

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

        // Kamera musi być wyłączona w URP — renderujemy ją ręcznie tylko gdy widać taflę!
        if (mirrorCamera != null)
            mirrorCamera.enabled = false;
    }

    private void SetupCamera()
    {
        if (mirrorCamera == null) return;

        mirrorCamera.enabled = false;
        mirrorCamera.allowHDR = false;
        mirrorCamera.allowMSAA = false;
        mirrorCamera.useOcclusionCulling = false; // Mniejszy narzut CPU przy małym farClip
        mirrorCamera.farClipPlane = mirrorFarClip;
        mirrorCamera.cullingMask = reflectionLayers;

        var data = mirrorCamera.GetComponent<UniversalAdditionalCameraData>();
        if (data != null)
        {
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            data.antialiasing = AntialiasingMode.None;
            data.volumeLayerMask = 0; // Brak obliczeń wolumenów post-processingu
        }
    }

    private void CreateRT()
    {
        if (mirrorCamera == null) return;

        float aspect = mirrorCamera.aspect > 0 ? mirrorCamera.aspect : 1f;
        int w = Mathf.Clamp(Mathf.RoundToInt(textureHeight * aspect), 64, 1024);
        int h = Mathf.Max(64, textureHeight);

        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }

        _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
        {
            name = "MirrorRT_Runtime",
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        _rt.Create();

        mirrorCamera.targetTexture = _rt;

        if (mirrorRenderer != null)
        {
            mirrorRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetTexture(BaseMapPropId, _rt);
            _propBlock.SetTexture(MainTexPropId, _rt);
            mirrorRenderer.SetPropertyBlock(_propBlock);

            // Fallback na wypadek gdyby shader wymagał sharedMaterial
            if (mirrorRenderer.sharedMaterial != null)
            {
                if (mirrorRenderer.sharedMaterial.HasProperty(BaseMapPropId))
                    mirrorRenderer.sharedMaterial.SetTexture(BaseMapPropId, _rt);
                else
                    mirrorRenderer.sharedMaterial.mainTexture = _rt;
            }
        }
    }

    private void LateUpdate()
    {
        if (!enableMirror || mirrorCamera == null || mirrorRenderer == null) return;

        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
            if (_playerCamera == null) return;
        }

        Vector3 mirrorPos = mirrorRenderer.bounds.center;
        Vector3 playerPos = _playerCamera.transform.position;
        Vector3 toPlayer = playerPos - mirrorPos;

        // 1. DISTANCE CULLING — szybki test odległości
        float distSqr = toPlayer.sqrMagnitude;
        if (distSqr > maxRenderDistance * maxRenderDistance)
            return;

        // 2. BACK-FACE CULLING — gracz stoi za lustrem / za ścianą
        Vector3 facing = dynamicReflection
            ? mirrorRenderer.transform.TransformDirection(mirrorFacingAxis).normalized
            : mirrorCamera.transform.forward;

        if (Vector3.Dot(facing, toPlayer) < -0.2f)
            return;

        // 3. FRUSTUM CULLING — tafla lustra musi być widoczna na ekranie gracza (zero alokacji GC)
        GeometryUtility.CalculateFrustumPlanes(_playerCamera, _frustumPlanes);
        if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, mirrorRenderer.bounds))
            return;

        // 4. FPS THROTTLING (opcjonalny)
        if (mirrorTargetFPS > 0)
        {
            _timer += Time.unscaledDeltaTime;
            float interval = 1f / mirrorTargetFPS;
            if (_timer < interval) return;
            _timer = 0f;
        }

        // 5. DYNAMIC PLANAR REFLECTION (jeśli włączone)
        if (dynamicReflection)
        {
            Vector3 mirrorNormal = facing;
            float planeD = -Vector3.Dot(mirrorNormal, mirrorPos);
            Vector3 reflectedPos = playerPos - 2f * (Vector3.Dot(mirrorNormal, playerPos) + planeD) * mirrorNormal;
            mirrorCamera.transform.position = reflectedPos;

            Vector3 reflectedForward = Vector3.Reflect(_playerCamera.transform.forward, mirrorNormal);
            Vector3 reflectedUp = Vector3.Reflect(_playerCamera.transform.up, mirrorNormal);
            mirrorCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, reflectedUp);
            mirrorCamera.fieldOfView = _playerCamera.fieldOfView;
        }

        // 6. RENDER LUSTRA
        mirrorCamera.Render();
    }

    /// <summary>
    /// Włącza lub wyłącza renderowanie lustra w czasie gry (np. z menu opcji graficznych).
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
            _rt = null;
        }
    }
}
