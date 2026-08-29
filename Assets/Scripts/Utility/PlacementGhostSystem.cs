using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// System zarysu miejsca odłożenia (Placement Ghost) i fizycznego badania przedmiotów w dłoniach:
/// 1. Gdy trzymasz skrzynię lub przedmiot, przed Tobą pojawia się półprzezroczysty zarys (bursztynowy/zielony = poprawne miejsce, czerwony = niepoprawne).
/// 2. Kliknięcie [LPM] stawia przedmiot idealnie w miejscu zarysu, stabilnie zamrażając jego fizykę.
/// 3. Przytrzymanie [PPM] pozwala na swobodne obracanie trzymanego przedmiotu w dłoniach we wszystkich osiach (tryb inspekcji 3D).
/// </summary>
public class PlacementGhostSystem : MonoBehaviour
{
    public static PlacementGhostSystem Instance { get; private set; }

    [Header("Raycast & Range")]
    [Tooltip("Maksymalny zasięg stawiania przedmiotów od kamery.")]
    [SerializeField] private float maxPlaceDistance = 3.2f;

    [Tooltip("Warstwy powierzchni, na których można stawiać przedmioty (np. podłoga, stół, meble).")]
    [SerializeField] private LayerMask surfaceLayerMask = ~0;

    [Tooltip("Maksymalny kąt nachylenia powierzchni w stopniach (powyżej tego kąta zarys jest czerwony).")]
    [SerializeField] private float maxSlopeAngle = 40f;

    [Header("Ghost Visual Colors")]
    [SerializeField] private Color validGhostColor = new Color(0.92f, 0.75f, 0.32f, 0.45f); // Bursztynowo-złoty retro
    [SerializeField] private Color invalidGhostColor = new Color(0.92f, 0.22f, 0.22f, 0.45f); // Czerwony błąd

    [Header("Item Rotate Mode (PPM)")]
    [Tooltip("Czułość obracania przedmiotu w dłoniach myszką przy przytrzymaniu PPM.")]
    [SerializeField] private float rotationSensitivity = 3.5f;

    [Header("Audio")]
    [SerializeField] private string placeSound = "item_drop";
    [SerializeField] private AudioClip customPlaceClip;

    private Camera _camera;
    private PlayerHands _playerHands;
    private GameObject _ghostRoot;
    private List<Renderer> _ghostRenderers = new List<Renderer>();
    private Material _ghostMaterial;

    private bool _isValidPlacement = false;
    private Vector3 _targetPlacePosition;
    private Quaternion _targetPlaceRotation;

    private bool _isRotatingItem = false;
    private Quaternion _customItemRotation = Quaternion.identity;

    public bool IsRotatingItem => _isRotatingItem;
    public bool IsValidPlacement => _isValidPlacement;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _camera = GetComponentInChildren<Camera>() ?? Camera.main ?? FindAnyObjectByType<Camera>();
        _playerHands = GetComponent<PlayerHands>() ?? FindAnyObjectByType<PlayerHands>();

        CreateGhostMaterial();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_ghostRoot != null) Destroy(_ghostRoot);
        if (_ghostMaterial != null) Destroy(_ghostMaterial);
    }

    private void CreateGhostMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
        _ghostMaterial = new Material(shader)
        {
            name = "PlacementGhost_Mat"
        };

        // Konfiguracja przezroczystości URP
        if (_ghostMaterial.HasProperty("_Surface"))
            _ghostMaterial.SetFloat("_Surface", 1); // Transparent
        if (_ghostMaterial.HasProperty("_Blend"))
            _ghostMaterial.SetFloat("_Blend", 0); // Alpha

        _ghostMaterial.SetColor("_BaseColor", validGhostColor);
        if (_ghostMaterial.HasProperty("_Color"))
            _ghostMaterial.SetColor("_Color", validGhostColor);
    }

    private void Update()
    {
        if (_playerHands == null || !_playerHands.HasItem)
        {
            HideGhost();
            _customItemRotation = Quaternion.identity;
            _isRotatingItem = false;
            return;
        }

        GameObject held = _playerHands.HeldItem;
        if (held == null)
        {
            HideGhost();
            return;
        }

        // 1. Obsługa obracania przedmiotu w dłoniach pod [PPM]
        HandleItemRotation(held);

        // 2. Obsługa zarysu miejsca odłożenia (Placement Ghost)
        UpdatePlacementGhost(held);

        // 3. Stawianie na [LPM] (jeśli nie klikamy w inny interaktywny obiekt)
        HandlePlacementInput(held);
    }

    private void HandleItemRotation(GameObject held)
    {
        bool ppmPressed = false;
        if (Mouse.current != null)
        {
            ppmPressed = Mouse.current.rightButton.isPressed;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(1)) ppmPressed = true;
#endif

        if (ppmPressed)
        {
            _isRotatingItem = true;
            Vector2 delta = Vector2.zero;
            if (Mouse.current != null)
            {
                delta = Mouse.current.delta.ReadValue();
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            else
            {
                delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            }
#endif

            if (delta.sqrMagnitude > 0.001f)
            {
                Vector3 rotEuler = new Vector3(-delta.y * rotationSensitivity, delta.x * rotationSensitivity, 0f);
                _customItemRotation = Quaternion.Euler(rotEuler) * _customItemRotation;

                // Obracaj model w rękach
                held.transform.localRotation = _customItemRotation;
            }
        }
        else
        {
            _isRotatingItem = false;
        }
    }

    private void UpdatePlacementGhost(GameObject held)
    {
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null) return;
        }

        Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);

        // Rzucamy promień z kamery
        if (Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, surfaceLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Ignoruj kolizje z graczem i samym trzymanym obiektem
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(held.transform))
            {
                HideGhost();
                return;
            }

            // Sprawdzanie kąta nachylenia powierzchni
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            _isValidPlacement = slopeAngle <= maxSlopeAngle;

            // Wylicz pozycję postawienia
            Vector3 itemExtent = Vector3.zero;
            Collider col = held.GetComponentInChildren<Collider>();
            if (col != null)
            {
                itemExtent = Vector3.up * (col.bounds.extents.y * 0.95f);
            }
            else
            {
                itemExtent = Vector3.up * 0.1f;
            }

            _targetPlacePosition = hit.point + itemExtent;

            // Rotacja wyrównana do gracza + customowy obrót z PPM
            Vector3 lookDir = _camera.transform.forward;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                _targetPlaceRotation = Quaternion.LookRotation(lookDir) * _customItemRotation;
            }
            else
            {
                _targetPlaceRotation = held.transform.rotation;
            }

            ShowGhostAt(held, _targetPlacePosition, _targetPlaceRotation, _isValidPlacement);
        }
        else
        {
            _isValidPlacement = false;
            HideGhost();
        }
    }

    private void ShowGhostAt(GameObject held, Vector3 position, Quaternion rotation, bool isValid)
    {
        EnsureGhostModel(held);

        if (_ghostRoot != null)
        {
            _ghostRoot.SetActive(true);
            _ghostRoot.transform.position = position;
            _ghostRoot.transform.rotation = rotation;

            Color targetColor = isValid ? validGhostColor : invalidGhostColor;
            SetGhostColor(targetColor);
        }
    }

    private void EnsureGhostModel(GameObject held)
    {
        if (_ghostRoot != null && _ghostRoot.name != $"Ghost_{held.name}")
        {
            Destroy(_ghostRoot);
            _ghostRoot = null;
            _ghostRenderers.Clear();
        }

        if (_ghostRoot == null)
        {
            _ghostRoot = new GameObject($"Ghost_{held.name}");
            _ghostRenderers.Clear();

            MeshFilter[] meshFilters = held.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf != null && mf.sharedMesh != null)
                {
                    GameObject subGhost = new GameObject(mf.name, typeof(MeshFilter), typeof(MeshRenderer));
                    subGhost.transform.SetParent(_ghostRoot.transform, false);
                    subGhost.transform.localPosition = mf.transform.localPosition;
                    subGhost.transform.localRotation = mf.transform.localRotation;
                    subGhost.transform.localScale = mf.transform.localScale;

                    subGhost.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var mr = subGhost.GetComponent<MeshRenderer>();
                    mr.material = _ghostMaterial;
                    _ghostRenderers.Add(mr);
                }
            }

            // Usunięcie koliderów z obiektu ducha
            foreach (var c in _ghostRoot.GetComponentsInChildren<Collider>())
            {
                Destroy(c);
            }
        }
    }

    private void SetGhostColor(Color c)
    {
        if (_ghostMaterial != null)
        {
            if (_ghostMaterial.HasProperty("_BaseColor"))
                _ghostMaterial.SetColor("_BaseColor", c);
            if (_ghostMaterial.HasProperty("_Color"))
                _ghostMaterial.SetColor("_Color", c);
        }
    }

    private void HideGhost()
    {
        if (_ghostRoot != null && _ghostRoot.activeSelf)
        {
            _ghostRoot.SetActive(false);
        }
    }

    private void HandlePlacementInput(GameObject held)
    {
        if (!_isValidPlacement || _isRotatingItem) return;

        // Jeśli wciśnięto klawisz E lub LPM
        bool placeInput = false;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            placeInput = true;
        }

        if (placeInput)
        {
            PlaceItemAtGhost(held);
        }
    }

    /// <summary>
    /// Stawia trzymany przedmiot precyzyjnie w miejscu zarysu ducha.
    /// </summary>
    public void PlaceItemAtGhost(GameObject held)
    {
        if (held == null || _playerHands == null) return;

        GameObject released = _playerHands.ReleaseHeldItem();
        if (released == null) return;

        released.transform.position = _targetPlacePosition;
        released.transform.rotation = _targetPlaceRotation;

        // Zamrażamy fizykę, aby przedmiot stał idealnie stabilnie
        if (released.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Efekt cząsteczek i audio
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayBurst(_targetPlacePosition);
        }

        if (AudioManager.Instance != null && !string.IsNullOrEmpty(placeSound))
        {
            AudioManager.Instance.Play(placeSound);
        }
        else if (customPlaceClip != null)
        {
            AudioSource.PlayClipAtPoint(customPlaceClip, _targetPlacePosition);
        }

        HideGhost();
        Debug.Log($"[PlacementGhostSystem] Przedmiot '{released.name}' został precyzyjnie postawiony na powierzchni.");
    }
}
