using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(10000)]
public class PlanarMirror : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Camera mirrorCamera;
    [SerializeField] private Transform mirrorSurface;
    [SerializeField] private MeshRenderer mirrorRenderer;

    [Header("Rendering")]
    [SerializeField] private LayerMask reflectionMask = ~0;

    [Tooltip("Wysokość RenderTexture. 256 dobrze pasuje do PSX.")]
    [SerializeField] private int textureHeight = 256;

    [SerializeField] private FilterMode filterMode = FilterMode.Point;

    [Header("Clipping")]
    [Tooltip("Przesuwa near plane lekko przed taflę.")]
    [SerializeField] private float clipOffset = 0.01f;

    [Header("Optimization")]
    [Tooltip("Maksymalna odległość renderowania lustra w metrach.")]
    [SerializeField] private float maxRenderDistance = 6f;

    [Tooltip("Docelowy klatkaż odświeżania lustra (30 FPS oszczędza 50-70% GPU). 0 = bez limitu.")]
    [SerializeField] private int mirrorTargetFPS = 30;

    [Tooltip("Nie renderuj lustra, gdy gracz patrzy w inną stronę.")]
    [SerializeField] private bool enableFrustumCulling = true;

    private RenderTexture _renderTexture;
    private UniversalRenderPipeline.SingleCameraRequest _renderRequest;
    private bool _requestSupported;
    private bool _wrongSideWarningShown;
    private float _timeSinceLastMirrorRender;

    // Cache: GetComponent w LateUpdate to duży koszt – robimy to raz w Awake
    private MeshFilter _mirrorMeshFilter;
    private Bounds _mirrorMeshBounds;

    private void Awake()
    {
        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        if (mirrorRenderer == null && mirrorSurface != null)
        {
            mirrorRenderer =
                mirrorSurface.GetComponent<MeshRenderer>();
        }

        if (sourceCamera == null ||
            mirrorCamera == null ||
            mirrorSurface == null ||
            mirrorRenderer == null)
        {
            Debug.LogError(
                "[PlanarMirror] Brakuje reference w Inspectorze.",
                this
            );

            enabled = false;
            return;
        }

        // Kamera lustra NIE renderuje się normalnie.
        mirrorCamera.enabled = false;
        mirrorCamera.targetTexture = null;
        mirrorCamera.useOcclusionCulling = false;

        // Wyłączamy cienie wewnątrz kamery lustra — gigantyczny wzrost FPS!
        var additionalCamData = mirrorCamera.GetComponent<UniversalAdditionalCameraData>();
        if (additionalCamData != null)
        {
            additionalCamData.renderShadows = false;
        }

        CreateRenderTexture();

        _renderRequest =
            new UniversalRenderPipeline.SingleCameraRequest();

        _renderRequest.destination = _renderTexture;

        _requestSupported =
            RenderPipeline.SupportsRenderRequest(
                mirrorCamera,
                _renderRequest
            );

        if (!_requestSupported)
        {
            Debug.LogError(
                "[PlanarMirror] URP nie obsługuje SingleCameraRequest " +
                "dla MirrorCamera. Upewnij się, że MirrorCamera " +
                "ma Render Type = Base.",
                this
            );
        }

        // Keszujemy MeshFilter i bounds — zamiast GetComponent w każdej klatce LateUpdate
        _mirrorMeshFilter = mirrorSurface.GetComponent<MeshFilter>();
        if (_mirrorMeshFilter != null && _mirrorMeshFilter.sharedMesh != null)
        {
            _mirrorMeshBounds = _mirrorMeshFilter.sharedMesh.bounds;
        }
        else
        {
            Debug.LogError(
                "[PlanarMirror] MirrorSurface musi być Quadem z MeshFilter.",
                mirrorSurface
            );
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!_requestSupported)
        {
            return;
        }

        // 1. Frustum Culling: jeśli tafla lustra nie jest widoczna na ekranie gracza -> NIE renderuj!
        if (enableFrustumCulling && mirrorRenderer != null && !mirrorRenderer.isVisible)
        {
            return;
        }

        // 2. Distance Culling: jeśli gracz odszedł dalej niż maxRenderDistance -> NIE renderuj!
        if (sourceCamera != null && mirrorSurface != null)
        {
            float distSq = (sourceCamera.transform.position - mirrorSurface.position).sqrMagnitude;
            if (distSq > (maxRenderDistance * maxRenderDistance))
            {
                return;
            }
        }

        // 3. FPS Throttling: renderuj lustro w mirrorTargetFPS (np. 30 FPS zamiast 144 FPS)
        if (mirrorTargetFPS > 0)
        {
            _timeSinceLastMirrorRender += Time.unscaledDeltaTime;
            if (_timeSinceLastMirrorRender < (1f / mirrorTargetFPS))
            {
                return;
            }
            _timeSinceLastMirrorRender = 0f;
        }

        RenderMirror();
    }

    private void RenderMirror()
    {
        if (!TryGetMirrorCorners(
                out Vector3 bottomLeft,
                out Vector3 bottomRight,
                out Vector3 topLeft,
                out Vector3 center))
        {
            return;
        }

        // ---------------------------------------------------
        // PŁASZCZYZNA LUSTRA
        // ---------------------------------------------------

        Vector3 normal =
            mirrorSurface.forward.normalized;

        Vector3 right =
            mirrorSurface.right.normalized;

        Vector3 up =
            mirrorSurface.up.normalized;

        Vector3 sourcePosition =
            sourceCamera.transform.position;

        float sourceSide =
            Vector3.Dot(
                sourcePosition - center,
                normal
            );

        // Main Camera musi znajdować się przed lustrem.
        if (sourceSide <= 0.001f)
        {
            if (!_wrongSideWarningShown)
            {
                Debug.LogWarning(
                    "[PlanarMirror] MirrorSurface jest odwrócone. " +
                    "Obróć Quad o 180 stopni tak, żeby jego lokalne +Z " +
                    "wskazywało w stronę gracza.",
                    mirrorSurface
                );

                _wrongSideWarningShown = true;
            }

            return;
        }

        _wrongSideWarningShown = false;

        // ---------------------------------------------------
        // WIRTUALNE OKO ZA LUSTREM
        // ---------------------------------------------------

        Vector3 virtualEye =
            sourcePosition -
            2f * sourceSide * normal;

        // Kamera znajduje się za taflą i patrzy PROSTOPADLE
        // przez lustro.
        //
        // NIE kopiujemy rotation Main Camera.
        mirrorCamera.transform.SetPositionAndRotation(
            virtualEye,
            mirrorSurface.rotation
        );

        // ---------------------------------------------------
        // ODLEGŁOŚĆ WIRTUALNEGO OKA OD LUSTRA
        // ---------------------------------------------------

        float distanceToMirror =
            Vector3.Dot(
                center - virtualEye,
                normal
            );

        if (distanceToMirror <= 0.001f)
        {
            return;
        }

        // Near plane ustawiamy przy samej tafli.
        // Dzięki temu kamera za ścianą nie renderuje
        // ściany znajdującej się pomiędzy nią a lustrem.
        float near =
            Mathf.Max(
                0.01f,
                distanceToMirror + clipOffset
            );

        float far =
            Mathf.Max(
                near + 1f,
                sourceCamera.farClipPlane +
                distanceToMirror * 2f
            );

        // ---------------------------------------------------
        // OFF-AXIS PROJECTION
        //
        // To jest najważniejsza część.
        // RenderTexture odpowiada dokładnie powierzchni lustra,
        // a nie całemu ekranowi Main Camera.
        // ---------------------------------------------------

        Vector3 eyeToBottomLeft =
            bottomLeft - virtualEye;

        Vector3 eyeToBottomRight =
            bottomRight - virtualEye;

        Vector3 eyeToTopLeft =
            topLeft - virtualEye;

        float scale =
            near / distanceToMirror;

        float left =
            Vector3.Dot(
                eyeToBottomLeft,
                right
            ) * scale;

        float rightPlane =
            Vector3.Dot(
                eyeToBottomRight,
                right
            ) * scale;

        float bottom =
            Vector3.Dot(
                eyeToBottomLeft,
                up
            ) * scale;

        float top =
            Vector3.Dot(
                eyeToTopLeft,
                up
            ) * scale;

        if (left >= rightPlane ||
            bottom >= top)
        {
            return;
        }

        mirrorCamera.nearClipPlane = near;
        mirrorCamera.farClipPlane = far;

        mirrorCamera.projectionMatrix =
            PerspectiveOffCenter(
                left,
                rightPlane,
                bottom,
                top,
                near,
                far
            );

        // ---------------------------------------------------
        // CAMERA SETTINGS
        // ---------------------------------------------------

        mirrorCamera.clearFlags =
            sourceCamera.clearFlags;

        mirrorCamera.backgroundColor =
            sourceCamera.backgroundColor;

        mirrorCamera.allowHDR =
            sourceCamera.allowHDR;

        mirrorCamera.allowMSAA =
            sourceCamera.allowMSAA;

        int mask =
            sourceCamera.cullingMask &
            reflectionMask.value;

        // Kamera lustra nie może renderować swojej własnej tafli.
        mask &=
            ~(1 << mirrorSurface.gameObject.layer);

        mirrorCamera.cullingMask = mask;

        // ---------------------------------------------------
        // RENDER DO TEXTURY
        // ---------------------------------------------------

        RenderPipeline.SubmitRenderRequest(
            mirrorCamera,
            _renderRequest
        );
    }

    private bool TryGetMirrorCorners(
        out Vector3 bottomLeft,
        out Vector3 bottomRight,
        out Vector3 topLeft,
        out Vector3 center)
    {
        bottomLeft = default;
        bottomRight = default;
        topLeft = default;
        center = default;

        // Używamy zkeszowanego MeshFilter i bounds z Awake — zero GetComponent per klatkę
        if (_mirrorMeshFilter == null)
            return false;

        Bounds bounds = _mirrorMeshBounds;

        float z =
            bounds.center.z;

        bottomLeft =
            mirrorSurface.TransformPoint(
                new Vector3(
                    bounds.min.x,
                    bounds.min.y,
                    z
                )
            );

        bottomRight =
            mirrorSurface.TransformPoint(
                new Vector3(
                    bounds.max.x,
                    bounds.min.y,
                    z
                )
            );

        topLeft =
            mirrorSurface.TransformPoint(
                new Vector3(
                    bounds.min.x,
                    bounds.max.y,
                    z
                )
            );

        center =
            mirrorSurface.TransformPoint(
                bounds.center
            );

        return true;
    }

    private Matrix4x4 PerspectiveOffCenter(
        float left,
        float right,
        float bottom,
        float top,
        float near,
        float far)
    {
        float x =
            2f * near /
            (right - left);

        float y =
            2f * near /
            (top - bottom);

        float a =
            (right + left) /
            (right - left);

        float b =
            (top + bottom) /
            (top - bottom);

        float c =
            -(far + near) /
            (far - near);

        float d =
            -(2f * far * near) /
            (far - near);

        float e = -1f;

        Matrix4x4 matrix =
            new Matrix4x4();

        matrix[0, 0] = x;
        matrix[0, 1] = 0f;
        matrix[0, 2] = a;
        matrix[0, 3] = 0f;

        matrix[1, 0] = 0f;
        matrix[1, 1] = y;
        matrix[1, 2] = b;
        matrix[1, 3] = 0f;

        matrix[2, 0] = 0f;
        matrix[2, 1] = 0f;
        matrix[2, 2] = c;
        matrix[2, 3] = d;

        matrix[3, 0] = 0f;
        matrix[3, 1] = 0f;
        matrix[3, 2] = e;
        matrix[3, 3] = 0f;

        return matrix;
    }

    private void CreateRenderTexture()
    {
        if (!TryGetMirrorCorners(
                out Vector3 bottomLeft,
                out Vector3 bottomRight,
                out Vector3 topLeft,
                out _))
        {
            return;
        }

        float width =
            Vector3.Distance(
                bottomLeft,
                bottomRight
            );

        float height =
            Vector3.Distance(
                bottomLeft,
                topLeft
            );

        float aspect =
            width / Mathf.Max(
                0.001f,
                height
            );

        int heightPixels =
            Mathf.Max(
                64,
                textureHeight
            );

        int widthPixels =
            Mathf.Clamp(
                Mathf.RoundToInt(
                    heightPixels * aspect
                ),
                64,
                2048
            );

        _renderTexture =
            new RenderTexture(
                widthPixels,
                heightPixels,
                24,
                RenderTextureFormat.ARGB32
            );

        _renderTexture.name =
            "RT_Mirror_Runtime";

        _renderTexture.filterMode =
            filterMode;

        _renderTexture.wrapMode =
            TextureWrapMode.Clamp;

        _renderTexture.useMipMap = false;

        _renderTexture.Create();

        Material material =
            mirrorRenderer.material;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture(
                "_BaseMap",
                _renderTexture
            );
        }
        else
        {
            material.mainTexture =
                _renderTexture;
        }
    }

    private void OnDestroy()
    {
        if (_renderTexture == null)
        {
            return;
        }

        _renderTexture.Release();
        Destroy(_renderTexture);
    }
}