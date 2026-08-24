using UnityEngine;

/// <summary>
/// Komponent pomocniczy, który można dodać do dowolnego obiektu interaktywnego (przedmiot, lampa, zwisający pas itp.).
/// Automatycznie zarządza subtelnymi drobinkami / iskrzeniem przez ParticleManager,
/// wskazując graczowi, że obiekt jest gotowy do interakcji.
/// </summary>
public class InteractiveParticleHint : MonoBehaviour
{
    [Header("Konfiguracja Efektu")]
    [Tooltip("ID efektu z ParticleManager (np. 'interactive_glint', 'sparkles', 'dust_motes', 'lamp_dust').")]
    [SerializeField] private string effectId = "interactive_glint";

    [Tooltip("Kolor / odcień drobinek.")]
    [SerializeField] private Color particleTint = new Color(1f, 0.92f, 0.6f, 0.85f);

    [Tooltip("Przesunięcie pozycji cząsteczek względem obiektu.")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    [Tooltip("Skala cząsteczek.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float particleScale = 0.6f;

    [Header("Warunki Wyświetlania")]
    [Tooltip("Czy pokazywać drobinki tylko wtedy, gdy gracz faktycznie może wejść w interakcję (sprawdza IConditionalInteractable.CanInteract)?")]
    [SerializeField] private bool showOnlyWhenInteractable = true;

    [Tooltip("Maksymalna odległość od gracza, w której cząsteczki są emitowane (optymalizacja).")]
    [SerializeField] private float maxDistance = 6.0f;

    [Header("Efekt po Interakcji")]
    [Tooltip("Czy po kliknięciu/interakcji ma odpalić się dodatkowy rozbłysk (burst)?")]
    [SerializeField] private bool playBurstOnInteract = true;

    [Tooltip("ID efektu rozbłysku przy interakcji.")]
    [SerializeField] private string burstEffectId = "pickup_burst";

    private ParticleSystem _activeParticleSystem;
    private IConditionalInteractable _conditionalInteractable;
    private Transform _playerTransform;
    private bool _isCurrentlyActive = false;
    private float _checkTimer = 0f;

    private void Awake()
    {
        _conditionalInteractable = GetComponent<IConditionalInteractable>();
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
        // Sprawdzamy stan co 0.25s, aby nie obciążać CPU co klatkę
        _checkTimer += Time.deltaTime;
        if (_checkTimer >= 0.25f)
        {
            _checkTimer = 0f;
            UpdateParticleState(force: false);
        }
    }

    private void FindPlayer()
    {
        if (_playerTransform == null)
        {
            var movement = FindAnyObjectByType<PlayerMovement>();
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

    private void UpdateParticleState(bool force)
    {
        bool shouldBeActive = true;

        // 1. Sprawdzenie warunku interaktywności (jeśli obiekt implementuje IConditionalInteractable)
        if (showOnlyWhenInteractable && _conditionalInteractable != null)
        {
            if (!_conditionalInteractable.CanInteract)
            {
                shouldBeActive = false;
            }
        }

        // 2. Sprawdzenie dystansu do gracza
        if (shouldBeActive && maxDistance > 0f)
        {
            if (_playerTransform == null) FindPlayer();

            if (_playerTransform != null)
            {
                float distSq = (transform.position - _playerTransform.position).sqrMagnitude;
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
            _activeParticleSystem = ParticleManager.Instance.AttachLoopingEffect(
                effectId, 
                transform, 
                localOffset, 
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
    /// Wywołaj to przy interakcji z obiektem (np. z poziomu OnInteract w PickupItem lub minigrze).
    /// </summary>
    public void NotifyInteracted()
    {
        if (playBurstOnInteract && ParticleManager.Instance != null)
        {
            Vector3 worldPos = transform.TransformPoint(localOffset);
            ParticleManager.Instance.PlayEffect(burstEffectId, worldPos, Quaternion.identity, null, particleTint, particleScale);
        }

        UpdateParticleState(force: true);
    }
}
