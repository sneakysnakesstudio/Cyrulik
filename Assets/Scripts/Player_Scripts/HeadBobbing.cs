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

    [Header("Smooth — płynność powrotu do pozycji neutralnej")]
    [Tooltip("Im wyższy, tym szybciej kamera wraca do centrum, gdy gracz stanie. 0.1 = bardzo leniwie, 12 = natychmiastowo.")]
    [SerializeField] private float returnSpeed = 8f;

    // Wewnętrzna faza sinusoidy (rośnie w czasie chodzenia, stoi w miejscu, gdy gracz stoi)
    private float _bobTimer;

    // Domyślna pozycja lokalna transformu (punkt bazowy, od którego liczymy offset)
    private Vector3 _defaultLocalPosition;

    private void Awake()
    {
        _defaultLocalPosition = transform.localPosition;
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
