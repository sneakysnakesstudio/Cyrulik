using UnityEngine;

/// <summary>
/// Izolowane studio 3D do prezentacji obracających się przedmiotów (Item Showcase).
/// Znajduje się daleko poza mapą (np. Y = -350), posiada własną kamerę i dedykowane oświetlenie.
/// Renderuje bezpośrednio do RenderTexture, dzięki czemu obracający się model 3D
/// nigdy nie przenika przez ściany salonu ani meble.
/// Kamera jest wyłączana, gdy inspekcja nie jest aktywna (zero narzutu na GPU/CPU).
/// </summary>
public class ItemShowcaseStudio : MonoBehaviour
{
    public static ItemShowcaseStudio Instance { get; private set; }

    [Header("Render & Camera")]
    [Tooltip("Dedykowana kamera studia podglądu.")]
    [SerializeField] private Camera showcaseCamera;

    [Tooltip("RenderTexture, do której rysuje kamera studia.")]
    [SerializeField] private RenderTexture targetRenderTexture;

    [Tooltip("Punkt, w którym osadzany i obracany jest model badanego przedmiotu.")]
    [SerializeField] private Transform itemMountPoint;

    [Header("Lighting")]
    [Tooltip("Główne światło oświetlające badany przedmiot.")]
    [SerializeField] private Light mainLight;

    [Tooltip("Opcjonalne światło konturowe (Rim light) dla klimatycznego zarysu krawędzi.")]
    [SerializeField] private Light rimLight;

    [Header("Spin & Floating Animation")]
    [Tooltip("Prędkość automatycznego obracania wokół osi Y (stopnie na sekundę).")]
    [SerializeField] private float autoRotateSpeed = 35f;

    [Tooltip("Amplituda delikatnego lewitowania w górę i w dół.")]
    [SerializeField] private float bobbingAmplitude = 0.03f;

    [Tooltip("Częstotliwość lewitowania.")]
    [SerializeField] private float bobbingFrequency = 1.8f;

    [Tooltip("Czułość obracania myszką przy przeciąganiu.")]
    [SerializeField] private float dragSensitivity = 0.4f;

    private GameObject _currentModelInstance;
    private Vector3 _mountInitialLocalPos;
    private float _currentYaw = 0f;
    private float _currentPitch = 0f;
    private bool _isActive = false;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (itemMountPoint != null)
        {
            _mountInitialLocalPos = itemMountPoint.localPosition;
        }

        SetupCameraAndRT();
        DeactivateStudio();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        ClearModel();
    }

    private void SetupCameraAndRT()
    {
        if (targetRenderTexture == null)
        {
            targetRenderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32)
            {
                name = "RT_ItemShowcase",
                antiAliasing = 2,
                useMipMap = false
            };
            targetRenderTexture.Create();
        }

        if (showcaseCamera != null)
        {
            showcaseCamera.targetTexture = targetRenderTexture;
            showcaseCamera.clearFlags = CameraClearFlags.SolidColor;
            showcaseCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // Przezroczyste tło
            showcaseCamera.enabled = false; // Domyślnie wyłączona
        }
    }

    public RenderTexture GetRenderTexture()
    {
        if (targetRenderTexture == null)
        {
            SetupCameraAndRT();
        }
        return targetRenderTexture;
    }

    /// <summary>
    /// Przygotowuje studio i umieszcza w nim kopię badanego modelu.
    /// </summary>
    public void DisplayItem(GameObject sourceObject, float scaleMultiplier = 1f, Vector3 customRotation = default)
    {
        ClearModel();

        if (sourceObject == null || itemMountPoint == null)
            return;

        // Tworzymy kopię wizualną badanego obiektu
        _currentModelInstance = Instantiate(sourceObject, itemMountPoint);
        _currentModelInstance.name = "ShowcaseItem_" + sourceObject.name;
        _currentModelInstance.SetActive(true);

        // Usuwamy wszelkie niepotrzebne komponenty interakcji, fizyki i audio
        Component[] components = _currentModelInstance.GetComponentsInChildren<Component>(true);
        foreach (Component comp in components)
        {
            if (comp is Transform || comp is MeshFilter || comp is MeshRenderer || comp is SkinnedMeshRenderer)
                continue;

            if (comp is Collider || comp is Rigidbody || comp is AudioSource || comp is MonoBehaviour)
            {
                Destroy(comp);
            }
        }

        // Ustawiamy model w centrum i dopasowujemy skalę
        _currentModelInstance.transform.localPosition = Vector3.zero;
        _currentModelInstance.transform.localRotation = Quaternion.Euler(customRotation);
        _currentModelInstance.transform.localScale = sourceObject.transform.localScale * scaleMultiplier;

        // Wyśrodkowanie geometrii względem pivota studia (kompensacja offsetu mesha)
        CenterModelGeometry(_currentModelInstance);

        // Włącz wszystkie renderery
        Renderer[] renderers = _currentModelInstance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            r.enabled = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }

        _currentYaw = customRotation.y;
        _currentPitch = customRotation.x;

        ActivateStudio();
    }

    private void CenterModelGeometry(GameObject modelGo)
    {
        Renderer[] renderers = modelGo.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenterOffset = modelGo.transform.InverseTransformVector(bounds.center - modelGo.transform.position);
        modelGo.transform.localPosition = -localCenterOffset;
    }

    private void Update()
    {
        if (!_isActive || itemMountPoint == null)
            return;

        // 1. Płynny auto-obrót wokół osi Y
        _currentYaw += autoRotateSpeed * Time.unscaledDeltaTime;
        _currentYaw = Mathf.Repeat(_currentYaw, 360f);

        // 2. Delikatne lewitowanie (bobbing)
        float bobOffset = Mathf.Sin(Time.unscaledTime * bobbingFrequency) * bobbingAmplitude;
        itemMountPoint.localPosition = _mountInitialLocalPos + new Vector3(0f, bobOffset, 0f);

        // 3. Aplikacja rotacji na pivot
        itemMountPoint.localRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
    }

    /// <summary>
    /// Obsługa przeciągania myszką do ręcznego obracania przedmiotu.
    /// </summary>
    public void AddManualRotation(Vector2 delta)
    {
        _currentYaw -= delta.x * dragSensitivity;
        _currentPitch = Mathf.Clamp(_currentPitch + delta.y * dragSensitivity, -45f, 45f);
    }

    public void ActivateStudio()
    {
        _isActive = true;
        if (showcaseCamera != null)
        {
            showcaseCamera.enabled = true;
        }
        if (mainLight != null) mainLight.enabled = true;
        if (rimLight != null) rimLight.enabled = true;
    }

    public void DeactivateStudio()
    {
        _isActive = false;
        if (showcaseCamera != null)
        {
            showcaseCamera.enabled = false;
        }
        if (mainLight != null) mainLight.enabled = false;
        if (rimLight != null) rimLight.enabled = false;

        ClearModel();
    }

    private void ClearModel()
    {
        if (_currentModelInstance != null)
        {
            Destroy(_currentModelInstance);
            _currentModelInstance = null;
        }

        if (itemMountPoint != null)
        {
            for (int i = itemMountPoint.childCount - 1; i >= 0; i--)
            {
                Destroy(itemMountPoint.GetChild(i).gameObject);
            }
            itemMountPoint.localPosition = _mountInitialLocalPos;
            itemMountPoint.localRotation = Quaternion.identity;
        }
    }
}
