using UnityEngine;

/// <summary>
/// Symuluje fizyczne, organiczne kołysanie wiszącego paska (np. pasa do ostrzenia brzytwy na ścianie)
/// lub sznurka od lampy. Reaguje na podmuchy powietrza w pomieszczeniu, przejście gracza oraz interakcje.
/// </summary>
public class HangingStrapSway : MonoBehaviour
{
    [Header("Punkt Zawieszenia (Pivot)")]
    [Tooltip("Transform reprezentujący górny punkt obrotu paska (jeśli pusty, używa tego obiektu).")]
    [SerializeField] private Transform pivotTransform;

    [Tooltip("Opcjonalny drugi segment paska (np. dolny uchwyt / kółko), który kołysze się z lekkim opóźnieniem.")]
    [SerializeField] private Transform lowerSegmentTransform;

    [Header("Swobodny Przeciąg / Wiatr (Idle Sway)")]
    [Tooltip("Czy pasek ma delikatnie oddychać/falować w przeciągu pokoju.")]
    [SerializeField] private bool enableIdleSway = true;

    [Tooltip("Maksymalny kąt swobodnego kołysania (w stopniach).")]
    [Range(0.2f, 5f)]
    [SerializeField] private float idleSwayAngle = 1.2f;

    [Tooltip("Częstotliwość swobodnego kołysania.")]
    [Range(0.2f, 3f)]
    [SerializeField] private float idleSwaySpeed = 1.0f;

    [Header("Dynamika Wahadła (Spring-Damper)")]
    [Tooltip("Częstotliwość drgań własnych (jak szybko wraca do pionu).")]
    [Range(1f, 10f)]
    [SerializeField] private float naturalFrequency = 3.5f;

    [Tooltip("Tłumienie drgań (wyższa wartość = szybsze wygaszanie bujania).")]
    [Range(0.5f, 5f)]
    [SerializeField] private float dampingRatio = 1.8f;

    [Header("Reakcja na Gracza")]
    [Tooltip("Czy pasek ma się lekko poruszać, gdy gracz przechodzi tuż obok?")]
    [SerializeField] private bool reactToPlayerPassing = true;

    [Tooltip("Odległość wykrywania przejścia gracza.")]
    [SerializeField] private float playerTriggerDistance = 1.2f;

    [Tooltip("Siła odchylenia przy przejściu gracza.")]
    [SerializeField] private float playerDraftForce = 4.0f;

    private Quaternion _initialRotation;
    private Quaternion _lowerInitialRotation;
    private Transform _playerTransform;

    // Zmienne symulacji sprężystości (kąt w stopniach i prędkość kątowa)
    private Vector2 _currentAngle = Vector2.zero;
    private Vector2 _angularVelocity = Vector2.zero;

    private float _timeOffset;
    private Vector3 _lastPlayerPos;

    private void Awake()
    {
        if (pivotTransform == null)
            pivotTransform = transform;

        _initialRotation = pivotTransform.localRotation;

        if (lowerSegmentTransform != null)
            _lowerInitialRotation = lowerSegmentTransform.localRotation;

        _timeOffset = Random.Range(0f, 100f);
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (_playerTransform == null)
        {
            var movement = FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
            {
                _playerTransform = movement.transform;
                _lastPlayerPos = _playerTransform.position;
            }
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 1. Sprawdzanie ruchu gracza w pobliżu (podmuch przy przejściu)
        CheckPlayerProximity();

        // 2. Symulacja fizyki sprężysto-tłumionej wahadła (Harmonic Oscillator)
        // a = -omega^2 * x - 2 * zeta * omega * v
        Vector2 springForce = -Mathf.Pow(naturalFrequency, 2) * _currentAngle;
        Vector2 dampingForce = -2f * dampingRatio * naturalFrequency * _angularVelocity;
        Vector2 angularAcceleration = springForce + dampingForce;

        _angularVelocity += angularAcceleration * dt;
        _currentAngle += _angularVelocity * dt;

        // 3. Swobodne falowanie wiatru (Perlin/Sine noise)
        Vector2 idleOffset = Vector2.zero;
        if (enableIdleSway)
        {
            float t = (Time.time + _timeOffset) * idleSwaySpeed;
            float swayX = Mathf.Sin(t) * idleSwayAngle;
            float swayZ = Mathf.Cos(t * 0.7f) * (idleSwayAngle * 0.4f);
            idleOffset = new Vector2(swayX, swayZ);
        }

        Vector2 totalAngle = _currentAngle + idleOffset;

        // 4. Aplikacja rotacji do głównego punktu paska
        Quaternion swayRot = Quaternion.Euler(totalAngle.x, 0f, totalAngle.y);
        pivotTransform.localRotation = _initialRotation * swayRot;

        // 5. Opcjonalny niższy segment (dół paska buja się z przesunięciem fazowym)
        if (lowerSegmentTransform != null)
        {
            Quaternion lowerSwayRot = Quaternion.Euler(totalAngle.x * 1.3f, 0f, totalAngle.y * 1.3f);
            lowerSegmentTransform.localRotation = _lowerInitialRotation * lowerSwayRot;
        }
    }

    private void CheckPlayerProximity()
    {
        if (!reactToPlayerPassing) return;
        if (_playerTransform == null) FindPlayer();
        if (_playerTransform == null) return;

        Vector3 playerPos = _playerTransform.position;
        float dist = Vector3.Distance(transform.position, playerPos);

        if (dist < playerTriggerDistance)
        {
            float playerSpeed = (playerPos - _lastPlayerPos).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            if (playerSpeed > 0.8f)
            {
                // Odpychamy pasek w stronę przeciwną do ruchu gracza
                Vector3 playerMoveDir = (playerPos - _lastPlayerPos).normalized;
                Vector3 localDir = transform.InverseTransformDirection(playerMoveDir);
                
                Nudge(new Vector2(localDir.z, -localDir.x) * (playerDraftForce * Mathf.Min(playerSpeed, 3f)));
            }
        }

        _lastPlayerPos = playerPos;
    }

    /// <summary>
    /// Popycha / wprowadza pasek w ruch kołyszący (np. przy kliknięciu, naostrzeniu brzytwy lub interakcji).
    /// </summary>
    public void Nudge(Vector2 impulse)
    {
        _angularVelocity += impulse;
    }

    /// <summary>
    /// Wygodne wywołanie kołysania (np. z poziomu RazorMinigame lub OnInteract).
    /// </summary>
    public void Sway(float strength = 8f)
    {
        float sign = Random.value > 0.5f ? 1f : -1f;
        Nudge(new Vector2(strength * sign, Random.Range(-strength * 0.3f, strength * 0.3f)));
    }
}
