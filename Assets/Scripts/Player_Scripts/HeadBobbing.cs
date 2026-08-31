using UnityEngine;

/// <summary>
/// Head bobbing — delikatne kiwanie kamerą podczas chodzenia.
/// Dołącz ten skrypt do TEGO SAMEGO obiektu co kamera (lub do jej rodzica,
/// który nie kręci się z myszą), i podepnij referencje w Inspectorze.
///
/// Typowa hierarchia gracza:
///   PlayerRoot  [CharacterController, PlayerMovement]
///     └─ CameraHolder  [ten skrypt HeadBobbing]
///           └─ CinemachineCamera / Camera
/// </summary>
[DefaultExecutionOrder(1000)]
public class HeadBobbing : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Skrypt PlayerMovement — potrzebny tylko do odczytu IsMoving.")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Bob — zakres ruchu (jednostki Unity)")]
    [Tooltip("O ile jednostek głowa przesuwa się w górę/dół.")]
    [SerializeField] private float bobAmplitudeY = 0.04f;

    [Tooltip("O ile jednostek głowa przeszkadza się w lewo/prawo.")]
    [SerializeField] private float bobAmplitudeX = 0.02f;

    [Header("Bob — tempo")]
    [Tooltip("Jak szybko głowa kołysze się podczas chodzenia (cykli na sekundę). Typowo 1.8–2.5.")]
    [SerializeField] private float bobFrequency = 2.0f;

    [Header("Idle Bob — oddychanie / ruch w spoczynku")]
    [Tooltip("Czy włączyć subtelny ruch/oddychanie głowy, gdy gracz stoi w miejscu.")]
    [SerializeField] private bool enableIdleBob = true;

    [Tooltip("Amplituda pionowa w spoczynku (oddychanie).")]
    [SerializeField] private float idleAmplitudeY = 0.005f;

    [Tooltip("Amplituda pozioma w spoczynku (delikatne kołysanie).")]
    [SerializeField] private float idleAmplitudeX = 0.0025f;

    [Tooltip("Tempo oddychania/kołysania w spoczynku (cykli na sekundę). Typowo 0.8–1.2.")]
    [SerializeField] private float idleFrequency = 1.0f;

    [Tooltip("Płynność śledzenia i przejść ruchu w spoczynku.")]
    [SerializeField] private float idleSmoothSpeed = 4f;

    [Header("Smooth — płynność powrotu do pozycji neutralnej")]
    [Tooltip("Im wyższy, tym szybciej kamera wraca do centrum, gdy gracz stanie. 0.1 = bardzo leniwie, 12 = natychmiastowo.")]
    [SerializeField] private float returnSpeed = 8f;

    [Header("Concussion / Drunk Sway (Wstrząs po obuchu)")]
    [Tooltip("Maksymalny kąt przechyłu głowy na boki (Roll w stopniach) podczas stanu obucha.")]
    [SerializeField] private float concussionMaxRollAngle = 3.0f;
    [Tooltip("Częstotliwość pływania kamery podczas wstrząsu.")]
    [SerializeField] private float concussionFrequency = 1.8f;
    [Tooltip("Dodatkowa amplituda pozycji kamery podczas wstrząsu.")]
    [SerializeField] private float concussionAmplitude = 0.035f;

    public static HeadBobbing Instance { get; private set; }

    // Wewnętrzna faza sinusoidy chodu
    private float _bobTimer;

    // Wewnętrzna faza sinusoidy w spoczynku
    private float _idleTimer;

    // Stan obucha / wstrząsu
    private float _concussionTimer = 0f;
    private float _concussionDuration = 1f;
    private float _concussionIntensity = 1f;

    // Domyślna pozycja i rotacja lokalna transformu (punkt bazowy)
    private Vector3 _defaultLocalPosition;
    private Quaternion _defaultLocalRotation;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void Awake()
    {
        Instance = this;
        _defaultLocalPosition = transform.localPosition;
        _defaultLocalRotation = transform.localRotation;
        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        if (_defaultLocalPosition != Vector3.zero)
        {
            transform.localPosition = _defaultLocalPosition;
            transform.localRotation = _defaultLocalRotation;
        }
        _bobTimer = 0f;
        _idleTimer = 0f;
        _concussionTimer = 0f;
    }

    /// <summary>
    /// Wyzwala efekt lekkiego wstrząśnienia głowy / uderzenia obuchem (mocniejsze, leniwe gibanie kamery i przechył lewo-prawo).
    /// </summary>
    /// <param name="duration">Czas trwania efektu w sekundach.</param>
    /// <param name="intensity">Mnożnik siły kołysania (domyślnie 1.0).</param>
    public void TriggerConcussion(float duration = 3.5f, float intensity = 1.0f)
    {
        _concussionDuration = Mathf.Max(0.1f, duration);
        _concussionTimer = _concussionDuration;
        _concussionIntensity = intensity;
    }

    private void LateUpdate()
    {
        // Obsługa stanu obucha / wstrząsu
        float concussionFactor = 0f;
        if (_concussionTimer > 0f)
        {
            _concussionTimer -= Time.deltaTime;
            concussionFactor = Mathf.Clamp01(_concussionTimer / _concussionDuration) * _concussionIntensity;

            // Pływający roll (przechył Z-axis)
            float rollZ = Mathf.Sin(Time.time * concussionFrequency * Mathf.PI) * (concussionMaxRollAngle * concussionFactor);
            float pitchX = Mathf.Cos(Time.time * concussionFrequency * 0.7f * Mathf.PI) * (concussionMaxRollAngle * 0.4f * concussionFactor);
            transform.localRotation = _defaultLocalRotation * Quaternion.Euler(pitchX, 0f, rollZ);
        }
        else
        {
            // Płynny powrót rotacji do bazowej
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _defaultLocalRotation, Time.deltaTime * returnSpeed);
        }

        // Wyłącz standardowe kiwanie głową jeśli trwa dialog lub minigra (ale pozwól na lekki concussion wobble)
        if (IsDialogueOrUIActive())
        {
            _bobTimer = 0f;
            ReturnToDefault(concussionFactor);
            return;
        }

        bool isMoving = playerMovement != null && playerMovement.enabled && playerMovement.IsMoving;

        if (isMoving)
        {
            ApplyBob(concussionFactor);
        }
        else if (enableIdleBob || concussionFactor > 0.01f)
        {
            ApplyIdleBob(concussionFactor);
        }
        else
        {
            ReturnToDefault(concussionFactor);
        }
    }

    private bool IsDialogueOrUIActive()
    {
        if (RazorMinigame.Instance != null && RazorMinigame.Instance.IsActive)
            return true;

        if (InputModeManager.Instance != null && InputModeManager.Instance.CurrentScheme != InputModeManager.ControlScheme.Player)
            return true;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsAnyDialogueActive)
            return true;

        if (InnerDialogueUI.Instance != null && InnerDialogueUI.Instance.IsDialogueActive)
            return true;

        if (ClientDialogueUI.Instance != null && ClientDialogueUI.Instance.IsDialogueActive)
            return true;

        return false;
    }

    private void ApplyBob(float concussionFactor)
    {
        // Faza rośnie proporcjonalnie do częstotliwości i deltaTime
        float speedMultiplier = Mathf.Lerp(1f, 0.75f, concussionFactor); // Obuch lekko spowalnia rytm
        _bobTimer += Time.deltaTime * (bobFrequency * speedMultiplier) * (2f * Mathf.PI);

        float extraAmp = 1f + (concussionFactor * 1.8f);
        float offsetY = Mathf.Sin(_bobTimer) * (bobAmplitudeY * extraAmp);
        float offsetX = Mathf.Cos(_bobTimer * 0.5f) * (bobAmplitudeX * extraAmp);

        if (concussionFactor > 0.01f)
        {
            offsetY += Mathf.Sin(Time.time * concussionFrequency) * (concussionAmplitude * concussionFactor);
            offsetX += Mathf.Cos(Time.time * concussionFrequency * 0.6f) * (concussionAmplitude * concussionFactor);
        }

        Vector3 targetPos = _defaultLocalPosition + new Vector3(offsetX, offsetY, 0f);

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            Time.deltaTime * returnSpeed * 4f
        );
    }

    private void ApplyIdleBob(float concussionFactor)
    {
        _idleTimer += Time.deltaTime * idleFrequency * (2f * Mathf.PI);

        float extraAmp = 1f + (concussionFactor * 2.5f);
        float offsetY = Mathf.Sin(_idleTimer) * (idleAmplitudeY * extraAmp);
        float offsetX = Mathf.Cos(_idleTimer * 0.5f) * (idleAmplitudeX * extraAmp);

        if (concussionFactor > 0.01f)
        {
            offsetY += Mathf.Sin(Time.time * concussionFrequency) * (concussionAmplitude * concussionFactor);
            offsetX += Mathf.Cos(Time.time * concussionFrequency * 0.7f) * (concussionAmplitude * concussionFactor);
        }

        Vector3 targetPos = _defaultLocalPosition + new Vector3(offsetX, offsetY, 0f);

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            Time.deltaTime * idleSmoothSpeed
        );

        _bobTimer = 0f;
    }

    private void ReturnToDefault(float concussionFactor)
    {
        Vector3 targetPos = _defaultLocalPosition;
        if (concussionFactor > 0.01f)
        {
            float offsetY = Mathf.Sin(Time.time * concussionFrequency) * (concussionAmplitude * concussionFactor);
            float offsetX = Mathf.Cos(Time.time * concussionFrequency * 0.7f) * (concussionAmplitude * concussionFactor);
            targetPos += new Vector3(offsetX, offsetY, 0f);
        }

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            Time.deltaTime * returnSpeed
        );

        if (Vector3.Distance(transform.localPosition, _defaultLocalPosition) < 0.0005f && concussionFactor <= 0.01f)
        {
            _bobTimer = 0f;
        }
    }

#if UNITY_EDITOR
    // Wizualizacja pozycji bazowej w edytorze
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.05f);
    }
#endif
}
