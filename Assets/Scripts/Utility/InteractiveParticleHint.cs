using UnityEngine;

/// <summary>
/// Komponent pomocniczy, który można dodać do dowolnego obiektu interaktywnego (przedmiot, lampa, skrzynka itp.).
/// Automatycznie zarządza subtelnymi drobinkami / iskrzeniem przez ParticleManager,
/// wskazując graczowi, że obiekt jest gotowy do interakcji.
/// • Domyślnie centruje się na środku BoxCollidera / Collidera obiektu.
/// • Pokazuje się w odległości np. 4 metrów (zgodnie z hierarchią odległości).
/// </summary>
public class InteractiveParticleHint : MonoBehaviour
{
    [Header("Konfiguracja Efektu")]
    [Tooltip("ID efektu z ParticleManager (np. 'interactive_glint', 'sparkles', 'dust_motes', 'lamp_dust').")]
    [SerializeField] private string effectId = "interactive_glint";

    [Tooltip("Kolor / odcień drobinek.")]
    [SerializeField] private Color particleTint = new Color(1f, 0.92f, 0.6f, 0.85f);

    [Tooltip("Czy automatycznie przypisać pozycję cząsteczek do środka BoxCollidera / Collidera obiektu?")]
    [SerializeField] private bool snapToColliderCenter = true;

    [Tooltip("Dodatkowe przesunięcie pozycji cząsteczek (względem środka collidera lub pivotu).")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    [Tooltip("Skala cząsteczek.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float particleScale = 0.6f;

    [Header("Warunki Wyświetlania")]
    [Tooltip("Czy pokazywać drobinki tylko wtedy, gdy gracz faktycznie może wejść w interakcję (sprawdza IConditionalInteractable.CanInteract)?")]
    [SerializeField] private bool showOnlyWhenInteractable = true;

    [Tooltip("Maksymalna odległość od gracza, w której cząsteczki są emitowane (domyślnie 4 metry).")]
    [SerializeField] private float maxDistance = 4.0f;

    [Header("Efekt po Interakcji")]
    [Tooltip("Czy po kliknięciu/interakcji ma odpalić się dodatkowy rozbłysk (burst)?")]
    [SerializeField] private bool playBurstOnInteract = true;

    [Tooltip("ID efektu rozbłysku przy interakcji.")]
    [SerializeField] private string burstEffectId = "pickup_burst";

    private ParticleSystem _activeParticleSystem;
    private IConditionalInteractable _conditionalInteractable;
    private Transform _playerTransform;
    private Collider _cachedCollider;
    private bool _isCurrentlyActive = false;
    private float _checkTimer = 0f;

    private void Awake()
    {
        _conditionalInteractable = GetComponent<IConditionalInteractable>();
        // Używamy TryGetComponent zamiast GetComponent — zero alokacji GC i zero wyjątków przy braku collidera
        if (!TryGetComponent(out _cachedCollider))
        {
            _cachedCollider = GetComponentInChildren<Collider>();
        }
    }

    private void Start()
    {
        FindPlayer();
        UpdateParticleState(force: true);
    }

    private void OnEnable()
    {
        UpdateParticleState(force: true);
    }

    private void OnDisable()
    {
        StopHintParticles(immediate: true);
    }

    private void Update()
    {
        // Sprawdzamy stan co 0.2s, aby nie obciążać CPU co klatkę
        _checkTimer += Time.deltaTime;
        if (_checkTimer >= 0.2f)
        {
            _checkTimer = 0f;
            UpdateParticleState(force: false);
        }
    }

    private void FindPlayer()
    {
        if (_playerTransform == null)
        {
            var movement = PlayerMovement.Instance != null ? PlayerMovement.Instance : FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
            {
                _playerTransform = movement.transform;
            }
            else
            {
                var cam = Camera.main;
                if (cam != null) _playerTransform = cam.transform;
            }
        }
    }

    /// <summary>
    /// Zwraca lokalne przesunięcie cząsteczek względem transformu obiektu,
    /// uwzględniając środek BoxCollidera.
    /// </summary>
    public Vector3 GetEffectiveLocalOffset()
    {
        if (snapToColliderCenter)
        {
            if (_cachedCollider == null)
            {
                if (!TryGetComponent(out _cachedCollider))
                    _cachedCollider = GetComponentInChildren<Collider>();
            }

            if (_cachedCollider is BoxCollider boxCol)
            {
                if (boxCol.transform == transform)
                {
                    return boxCol.center + localOffset;
                }
                else
                {
                    Vector3 worldCenter = boxCol.transform.TransformPoint(boxCol.center);
                    return transform.InverseTransformPoint(worldCenter) + localOffset;
                }
            }
            else if (_cachedCollider != null)
            {
                Vector3 worldCenter = _cachedCollider.bounds.center;
                return transform.InverseTransformPoint(worldCenter) + localOffset;
            }
        }

        return localOffset;
    }

    /// <summary>
    /// Zwraca pozycję w świecie, gdzie pojawiają się cząsteczki (środek collidera).
    /// </summary>
    public Vector3 GetWorldCenterPosition()
    {
        return transform.TransformPoint(GetEffectiveLocalOffset());
    }

    private void UpdateParticleState(bool force)
    {
        bool shouldBeActive = true;

        // 1. Sprawdzenie warunku interaktywności
        if (showOnlyWhenInteractable && _conditionalInteractable != null)
        {
            if (!_conditionalInteractable.CanInteract)
            {
                shouldBeActive = false;
            }
        }

        // 2. Sprawdzenie dystansu do gracza od środka collidera
        if (shouldBeActive && maxDistance > 0f)
        {
            if (_playerTransform == null) FindPlayer();

            if (_playerTransform != null)
            {
                Vector3 centerPos = GetWorldCenterPosition();
                float distSq = (centerPos - _playerTransform.position).sqrMagnitude;
                if (distSq > (maxDistance * maxDistance))
                {
                    shouldBeActive = false;
                }
            }
        }

        if (shouldBeActive != _isCurrentlyActive || force)
        {
            _isCurrentlyActive = shouldBeActive;
            if (_isCurrentlyActive)
            {
                StartHintParticles();
            }
            else
            {
                StopHintParticles(immediate: false);
            }
        }
    }

    private void StartHintParticles()
    {
        if (ParticleManager.Instance != null)
        {
            Vector3 effOffset = GetEffectiveLocalOffset();
            _activeParticleSystem = ParticleManager.Instance.AttachLoopingEffect(
                effectId, 
                transform, 
                effOffset, 
                $"hint_{GetEntityId()}", 
                particleTint, 
                particleScale
            );
        }
    }

    private void StopHintParticles(bool immediate)
    {
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.DetachLoopingEffect(
                transform, 
                $"hint_{GetEntityId()}", 
                immediate
            );
        }
        _activeParticleSystem = null;
    }

    /// <summary>
    /// Wywołaj to przy interakcji z obiektem (np. z poziomu OnInteract w PickupItem).
    /// </summary>
    public void NotifyInteracted()
    {
        if (playBurstOnInteract && ParticleManager.Instance != null)
        {
            Vector3 worldPos = GetWorldCenterPosition();
            ParticleManager.Instance.PlayEffect(burstEffectId, worldPos, Quaternion.identity, null, particleTint, particleScale);
        }

        UpdateParticleState(force: true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? GetWorldCenterPosition() : (transform.position + localOffset);
        if (_cachedCollider != null && snapToColliderCenter)
        {
            center = _cachedCollider.bounds.center + transform.TransformVector(localOffset);
        }

        Gizmos.color = new Color(particleTint.r, particleTint.g, particleTint.b, 0.7f);
        Gizmos.DrawWireSphere(center, 0.15f * particleScale);

        Gizmos.color = new Color(particleTint.r, particleTint.g, particleTint.b, 0.2f);
        Gizmos.DrawWireSphere(center, maxDistance);
    }
#endif
}
