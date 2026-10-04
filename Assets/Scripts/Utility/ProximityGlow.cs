using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Profesjonalny system podświetlenia przedmiotów interaktywnych:
/// 1. [Outline] - Lśniąca obwódka (Inverted Hull) wokół sylwetki modelu z subtelnym pulsowaniem.
/// 2. [AreaZone] - Świetlisty okrąg / strefa na podłodze pod przedmiotem.
/// 3. [OutlineAndArea] - Jednoczesna obwódka modelu oraz strefa na ziemi.
/// 4. [SurfaceEmission] - Klasyczna emisja całej powierzchni materiału.
/// 
/// • Używa MaterialPropertyBlock – zero alokacji pamięci (GC), zero psucia materiałów w projekcie.
/// • W spoczynku całkowicie wygaszony (nie świeci sam z siebie na starcie).
/// • Płynne rozjaśnianie i wygaszanie przez DOTween.
/// </summary>
[DisallowMultipleComponent]
public class ProximityGlow : MonoBehaviour
{
    public enum HighlightMode
    {
        Outline,          // Lśniąca obwódka (krawędzie/obrys sylwetki)
        AreaZone,         // Świetlisty obszar/okrąg na podłodze pod obiektem
        OutlineAndArea,   // Obwódka modelu + obszar na podłodze
        SurfaceEmission   // Podświetlenie całej powierzchni materiału
    }

    // ─────────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────────

    [Header("Tryb Podświetlenia")]
    [Tooltip("Wybierz styl: Outline (lśniąca obwódka), AreaZone (okrąg strefy), OutlineAndArea lub SurfaceEmission.")]
    [SerializeField] private HighlightMode highlightMode = HighlightMode.Outline;

    [Header("Kolor i Jasność Obwódki")]
    [Tooltip("Docelowy kolor lśniącej obwódki (HDR). Ciepły bursztynowy / złoty retro blask.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color glowColor = new Color(1.7f, 1.25f, 0.4f, 1.0f);

    [Tooltip("Grubość obwódki wokół modelu.")]
    [Range(0.005f, 0.05f)]
    [SerializeField] private float outlineThickness = 0.018f;

    [Tooltip("Intensywność blasku obwódki (mnożnik HDR).")]
    [Range(0.5f, 3.0f)]
    [SerializeField] private float glowIntensity = 1.6f;

    [Tooltip("Szybkość lśnienia/falowania blasku (0 = stałe światło bez pulsowania).")]
    [Range(0f, 8f)]
    [SerializeField] private float gleamPulseSpeed = 3.2f;

    [Header("Obszar / Strefa (Area Zone)")]
    [Tooltip("Promień świetlistego okręgu na podłodze pod obiektem.")]
    [Range(0.3f, 2.5f)]
    [SerializeField] private float areaRadius = 0.7f;

    [Tooltip("Warstwy podłoża, na których ma leżeć okrąg strefy.")]
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Płynność Przejścia")]
    [Tooltip("Czas płynnego zapalania i gaśnięcia w sekundach.")]
    [SerializeField] private float fadeDuration = 0.18f;

    [Header("Aktywacja")]
    [Tooltip("Czy aktywować podświetlenie po najechaniu celownikiem (Raycast)?")]
    [SerializeField] private bool activateOnAim = true;

    [Tooltip("Czy aktywować podświetlenie automatycznie, gdy gracz podejdzie blisko (Distance)?")]
    [SerializeField] private bool activateOnProximity = true;

    [Tooltip("Dystans w metrach, przy którym włącza się podświetlenie zbliżeniowe. Jeśli zaznaczono 'syncWithInteractionDistance', wartość ta jest nadpisywana przez zasięg interakcji gracza.")]
    [SerializeField] private float proximityRange = 3.0f;

    [Tooltip("Automatycznie synchronizuj zasięg proximity glow z zasięgiem interakcji gracza (PlayerMovement.interactionDistance). Zalecane, by oba zakresy były zawsze identyczne.")]
    [SerializeField] private bool syncWithInteractionDistance = true;

    [Header("Renderery (opcjonalnie – auto-wykrywa z dzieci)")]
    [Tooltip("Przypisz ręcznie jeśli chcesz podświetlić konkretny mesh zamiast wszystkich dzieci.")]
    [SerializeField] private Renderer[] targetRenderers;

    // ─────────────────────────────────────────────────
    // PRYWATNE
    // ─────────────────────────────────────────────────

    private MaterialPropertyBlock _mpb;
    private bool _isGlowing = false;
    private Tween _fadeTween;
    private float _currentIntensity = 0f;

    // Cache identyfikatorów shaderów
    private static readonly int OutlineColorID     = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");
    private static readonly int OutlineIntensityID = Shader.PropertyToID("_OutlineIntensity");
    private static readonly int PulseSpeedID       = Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseAmountID      = Shader.PropertyToID("_PulseAmount");
    private static readonly int EmissionColorID    = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorID        = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID            = Shader.PropertyToID("_Color");

    // Struktury dla trybu Outline
    private static Material _sharedOutlineMaterial;
    private List<GameObject> _outlineObjects;
    private List<MeshRenderer> _outlineRenderers;

    // Struktury dla trybu AreaZone
    private GameObject _areaZoneObject;
    private MeshRenderer _areaZoneRenderer;
    private static Material _sharedAreaMaterial;

    private bool[] _supportsEmission;
    private Transform _playerTransform;

    // ─────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }

        // Cache czy shadery obsługują emisję
        _supportsEmission = new bool[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null) continue;
            Material mat = targetRenderers[i].sharedMaterial;
            _supportsEmission[i] = mat != null && mat.HasProperty(EmissionColorID);
        }

        // Przygotuj obiekty obwódki i obszaru
        if (highlightMode == HighlightMode.Outline || highlightMode == HighlightMode.OutlineAndArea)
        {
            SetupOutlineMeshes();
        }

        if (highlightMode == HighlightMode.AreaZone || highlightMode == HighlightMode.OutlineAndArea)
        {
            SetupAreaZone();
        }

        // Całkowite wygaszenie na starcie
        _currentIntensity = 0f;
        _isGlowing = false;
        ApplyIntensity(0f);
    }

    private void Start()
    {
        _playerTransform = Camera.main != null ? Camera.main.transform : null;

        if (syncWithInteractionDistance)
        {
            SyncProximityRangeWithInteraction();
        }

        ApplyIntensity(0f);
    }

    private void SyncProximityRangeWithInteraction()
    {
        if (PlayerMovement.Instance != null)
        {
            proximityRange = PlayerMovement.Instance.InteractionDistance;
        }
    }

    private void OnEnable()
    {
        if (activateOnAim)
        {
            if (PlayerMovement.Instance != null)
                PlayerMovement.Instance.OnInteractableChanged += HandleInteractableChanged;
            else
                StartCoroutine(RegisterWhenReady());
        }

        if (!_isGlowing)
        {
            ApplyIntensity(0f);
        }
    }

    private void OnDisable()
    {
        if (PlayerMovement.Instance != null)
        {
            PlayerMovement.Instance.OnInteractableChanged -= HandleInteractableChanged;
        }

        _fadeTween?.Kill();
        ApplyIntensity(0f);
        _isGlowing = false;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();

        if (_outlineObjects != null)
        {
            foreach (var go in _outlineObjects)
            {
                if (go != null) Destroy(go);
            }
            _outlineObjects.Clear();
        }

        if (_areaZoneObject != null)
        {
            Destroy(_areaZoneObject);
        }
    }

    private Collider _cachedCollider;

    /// <summary>
    /// Zwraca pozycję geometrycznego środka obiektu (z BoxCollidera / Collidera / Renderera).
    /// </summary>
    public Vector3 GetCenterPosition()
    {
        if (_cachedCollider == null)
            _cachedCollider = GetComponent<BoxCollider>() ?? GetComponent<Collider>() ?? GetComponentInChildren<Collider>();

        if (_cachedCollider != null)
            return _cachedCollider.bounds.center;

        if (targetRenderers != null && targetRenderers.Length > 0 && targetRenderers[0] != null)
            return targetRenderers[0].bounds.center;

        return transform.position;
    }

    private void Update()
    {
        // Synchronizacja zasięgu z PlayerMovement (co 30 klatek – minimalne obciążenie)
        if (syncWithInteractionDistance && Time.frameCount % 30 == 0)
        {
            SyncProximityRangeWithInteraction();
        }

        // Sprawdzanie odległości (tylko co 5 klatek – zero obciążenia CPU)
        if (activateOnProximity && Time.frameCount % 5 == 0)
        {
            if (_playerTransform == null && Camera.main != null)
                _playerTransform = Camera.main.transform;

            if (_playerTransform != null)
            {
                Vector3 center = GetCenterPosition();
                float distSq = (center - _playerTransform.position).sqrMagnitude;
                bool inRange = distSq <= (proximityRange * proximityRange);

                if (inRange && !_isGlowing)
                    SetGlow(true);
                else if (!inRange && _isGlowing && !IsAimedAtByPlayer())
                    SetGlow(false);
            }
        }
    }

    private bool IsAimedAtByPlayer()
    {
        if (PlayerMovement.Instance == null) return false;
        IInteractable current = PlayerMovement.Instance.CurrentInteractable;
        if (current == null) return false;

        Component c = current as Component;
        if (c == null) return false;

        return c.gameObject == gameObject || c.transform.IsChildOf(transform) || transform.IsChildOf(c.transform);
    }

    // ─────────────────────────────────────────────────
    // SETUP STRUKTUR WIZUALNYCH
    // ─────────────────────────────────────────────────

    private void SetupOutlineMeshes()
    {
        if (_outlineObjects != null && _outlineObjects.Count > 0) return;

        if (_sharedOutlineMaterial == null)
        {
            Shader shader = Shader.Find("Cyrulik/InteractableOutline");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            _sharedOutlineMaterial = new Material(shader)
            {
                name = "InteractableOutline_RuntimeSharedMat",
                hideFlags = HideFlags.DontSave
            };
        }

        _outlineObjects = new List<GameObject>();
        _outlineRenderers = new List<MeshRenderer>();

        foreach (var r in targetRenderers)
        {
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            // Tworzymy lekki dedykowany obiekt potomny dla obwódki
            GameObject outlineGo = new GameObject($"_OutlineMesh_{r.gameObject.name}");
            outlineGo.transform.SetParent(r.transform, false);
            outlineGo.transform.localPosition = Vector3.zero;
            outlineGo.transform.localRotation = Quaternion.identity;
            outlineGo.transform.localScale = Vector3.one;

            var outlineMf = outlineGo.AddComponent<MeshFilter>();
            outlineMf.sharedMesh = mf.sharedMesh;

            var outlineMr = outlineGo.AddComponent<MeshRenderer>();
            outlineMr.sharedMaterial = _sharedOutlineMaterial;
            outlineMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineMr.receiveShadows = false;
            outlineMr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            outlineMr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            outlineGo.SetActive(false);
            _outlineObjects.Add(outlineGo);
            _outlineRenderers.Add(outlineMr);
        }
    }

    private void SetupAreaZone()
    {
        if (_areaZoneObject != null) return;

        // Tworzymy proceduralny krąg pod obiektem
        _areaZoneObject = new GameObject($"_AreaZone_{gameObject.name}");
        _areaZoneObject.transform.SetParent(transform, false);

        // Znajdź podłogę pod obiektem
        Vector3 groundPos = Vector3.zero;
        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 4f, groundLayerMask))
        {
            groundPos = transform.InverseTransformPoint(hit.point + Vector3.up * 0.02f);
        }
        else
        {
            groundPos = new Vector3(0f, -0.1f, 0f);
        }

        _areaZoneObject.transform.localPosition = groundPos;
        _areaZoneObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Leży poziomo

        var mf = _areaZoneObject.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateRingMesh(areaRadius, areaRadius * 0.82f, 32);

        if (_sharedAreaMaterial == null)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _sharedAreaMaterial = new Material(unlitShader)
            {
                name = "AreaZone_RuntimeSharedMat",
                hideFlags = HideFlags.DontSave
            };
        }

        _areaZoneRenderer = _areaZoneObject.AddComponent<MeshRenderer>();
        _areaZoneRenderer.sharedMaterial = _sharedAreaMaterial;
        _areaZoneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _areaZoneRenderer.receiveShadows = false;

        _areaZoneObject.SetActive(false);
    }

    private Mesh CreateRingMesh(float outerRadius, float innerRadius, int segments)
    {
        Mesh mesh = new Mesh { name = "AreaZone_RingMesh" };
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];

        float angleStep = 360f / segments;

        for (int i = 0; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            vertices[i * 2]     = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
            vertices[i * 2 + 1] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);

            if (i < segments)
            {
                int root = i * 2;
                triangles[i * 6]     = root;
                triangles[i * 6 + 1] = root + 1;
                triangles[i * 6 + 2] = root + 2;

                triangles[i * 6 + 3] = root + 1;
                triangles[i * 6 + 4] = root + 3;
                triangles[i * 6 + 5] = root + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ─────────────────────────────────────────────────
    // OBSŁUGA INTERAKCJI
    // ─────────────────────────────────────────────────

    private void HandleInteractableChanged(IInteractable interactable)
    {
        bool shouldGlow = false;

        if (interactable != null && interactable is Component interactableComponent)
        {
            if (interactableComponent.gameObject == gameObject)
            {
                shouldGlow = true;
            }
            else if (interactableComponent.transform.IsChildOf(transform))
            {
                shouldGlow = true;
            }
            else if (transform.IsChildOf(interactableComponent.transform))
            {
                IInteractable directParentInteractable = GetComponentInParent<IInteractable>();
                if (directParentInteractable == interactable)
                {
                    shouldGlow = true;
                }
            }
        }

        if (shouldGlow != _isGlowing)
        {
            SetGlow(shouldGlow);
        }
    }

    public void SetGlow(bool on)
    {
        if (_isGlowing == on) return;
        _isGlowing = on;

        _fadeTween?.Kill();

        if (on)
        {
            // Włącz obiekty wizualne przed animacją fade-in
            SetVisualsActive(true);
        }

        float target = on ? 1.0f : 0f;

        _fadeTween = DOTween
            .To(
                () => _currentIntensity,
                x =>
                {
                    _currentIntensity = x;
                    ApplyIntensity(x);
                },
                target,
                fadeDuration
            )
            .SetEase(on ? Ease.OutQuad : Ease.InQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                if (!on)
                {
                    SetVisualsActive(false);
                }
            });
    }

    private void SetVisualsActive(bool active)
    {
        if (_outlineObjects != null)
        {
            foreach (var go in _outlineObjects)
            {
                if (go != null) go.SetActive(active);
            }
        }

        if (_areaZoneObject != null)
        {
            _areaZoneObject.SetActive(active);
        }
    }

    private void ApplyIntensity(float intensity)
    {
        float scaledIntensity = intensity * glowIntensity;

        // 1. Aplikacja parametrów do obwódki (Outline)
        if (_outlineRenderers != null && _outlineRenderers.Count > 0)
        {
            foreach (var mr in _outlineRenderers)
            {
                if (mr == null) continue;
                mr.GetPropertyBlock(_mpb);
                _mpb.SetColor(OutlineColorID, glowColor);
                _mpb.SetFloat(OutlineThicknessID, outlineThickness);
                _mpb.SetFloat(OutlineIntensityID, scaledIntensity);
                _mpb.SetFloat(PulseSpeedID, gleamPulseSpeed);
                _mpb.SetFloat(PulseAmountID, 0.2f);
                mr.SetPropertyBlock(_mpb);
            }
        }

        // 2. Aplikacja parametrów do obszaru (AreaZone)
        if (_areaZoneRenderer != null)
        {
            _areaZoneRenderer.GetPropertyBlock(_mpb);
            Color areaColor = new Color(glowColor.r, glowColor.g, glowColor.b, intensity * 0.7f);
            _mpb.SetColor(BaseColorID, areaColor);
            _mpb.SetColor(ColorID, areaColor);
            _areaZoneRenderer.SetPropertyBlock(_mpb);
        }

        // 3. Aplikacja do powierzchni (SurfaceEmission fallback)
        if (highlightMode == HighlightMode.SurfaceEmission && targetRenderers != null)
        {
            Color finalEmission = glowColor * scaledIntensity;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer r = targetRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);

                if (_supportsEmission != null && _supportsEmission.Length > i && _supportsEmission[i])
                {
                    _mpb.SetColor(EmissionColorID, finalEmission);
                }
                else
                {
                    Color baseColor = Color.white;
                    if (r.sharedMaterial != null)
                    {
                        if (r.sharedMaterial.HasProperty(BaseColorID))
                            baseColor = r.sharedMaterial.GetColor(BaseColorID);
                        else if (r.sharedMaterial.HasProperty(ColorID))
                            baseColor = r.sharedMaterial.GetColor(ColorID);
                    }

                    float brightness = Mathf.Lerp(1f, 1.35f, intensity);
                    Color tinted = baseColor * brightness;
                    tinted.a = baseColor.a;

                    if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorID))
                        _mpb.SetColor(BaseColorID, tinted);
                    else
                        _mpb.SetColor(ColorID, tinted);
                }

                r.SetPropertyBlock(_mpb);
            }
        }
    }

    private System.Collections.IEnumerator RegisterWhenReady()
    {
        float waited = 0f;
        while (PlayerMovement.Instance == null && waited < 2f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (PlayerMovement.Instance != null && activateOnAim)
        {
            PlayerMovement.Instance.OnInteractableChanged += HandleInteractableChanged;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.4f);
        if (highlightMode == HighlightMode.AreaZone || highlightMode == HighlightMode.OutlineAndArea)
        {
            Gizmos.DrawWireSphere(transform.position, areaRadius);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
    }
#endif
}
