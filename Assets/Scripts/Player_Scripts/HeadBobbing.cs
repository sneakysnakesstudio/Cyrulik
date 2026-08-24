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

    // Wewnętrzna faza sinusoidy chodu
    private float _bobTimer;

    // Wewnętrzna faza sinusoidy w spoczynku
    private float _idleTimer;

    // Domyślna pozycja lokalna transformu (punkt bazowy, od którego liczymy offset)
    private Vector3 _defaultLocalPosition;

    private void Awake()
    {
        _defaultLocalPosition = transform.localPosition;
        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }
    }

    private void LateUpdate()
    {
        // Wyłącz kiwanie głową jeśli trwa dialog, myśli, minigra lub schemat UI
        if (IsDialogueOrUIActive())
        {
            _bobTimer = 0f;
            ReturnToDefault();
            return;
        }

        bool isMoving = playerMovement != null && playerMovement.enabled && playerMovement.IsMoving;

        if (isMoving)
        {
            ApplyBob();
        }
        else if (enableIdleBob)
        {
            ApplyIdleBob();
        }
        else
        {
            ReturnToDefault();
        }
    }

    private bool IsDialogueOrUIActive()
    {
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

    private void ApplyBob()
    {
        // Faza rośnie proporcjonalnie do częstotliwości i deltaTime
        _bobTimer += Time.deltaTime * bobFrequency * (2f * Mathf.PI);

        float offsetY = Mathf.Sin(_bobTimer) * bobAmplitudeY;

        // Przesunięcie boczne — pół częstotliwości, żeby dawało naturalny chód
        float offsetX = Mathf.Cos(_bobTimer * 0.5f) * bobAmplitudeX;

        Vector3 targetPos = _defaultLocalPosition + new Vector3(offsetX, offsetY, 0f);

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            Time.deltaTime * returnSpeed * 4f   // szybkie śledzenie, żeby sinusoida była płynna
        );
    }

    private void ApplyIdleBob()
    {
        // Faza rośnie proporcjonalnie do częstotliwości oddychania
        _idleTimer += Time.deltaTime * idleFrequency * (2f * Mathf.PI);

        // Subtelne oddychanie pionowe i bardzo łagodne kołysanie poziome
        float offsetY = Mathf.Sin(_idleTimer) * idleAmplitudeY;
        float offsetX = Mathf.Cos(_idleTimer * 0.5f) * idleAmplitudeX;

        Vector3 targetPos = _defaultLocalPosition + new Vector3(offsetX, offsetY, 0f);

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            Time.deltaTime * idleSmoothSpeed
        );

        // Reset timera chodu, aby kolejny krok zaczynał się płynnie
        _bobTimer = 0f;
    }

    private void ReturnToDefault()
    {
        // Płynny powrót do pozycji neutralnej po zatrzymaniu się
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            _defaultLocalPosition,
            Time.deltaTime * returnSpeed
        );

        // Gdy jesteśmy bardzo blisko centrum — resetuj timer, żeby następny ruch
        // startował zawsze od "dołu" sinusoidy, nie z losowego miejsca
        if (Vector3.Distance(transform.localPosition, _defaultLocalPosition) < 0.0005f)
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
