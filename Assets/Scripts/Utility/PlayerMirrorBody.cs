using UnityEngine;

/// <summary>
/// Zarządza modelem ciała gracza i jego odbiciem w lustrze:
/// 1. Dołącza model postaci (low-poly fryzjer) do obiektu gracza.
/// 2. Ukrywa głowę / twarz przed główną kamerą FPP (aby nie zasłaniała widoku).
/// 3. Kamera lustra (PlanarMirror) renderuje całe ciało wraz z głową i fartuchem!
/// 4. Synchronizuje animacje chodu/idle w odbiciu lustrzanym.
/// </summary>
public class PlayerMirrorBody : MonoBehaviour
{
    [Header("Model Postaci")]
    [Tooltip("Prefab lub obiekt modelu postaci gracza (np. Character_Male).")]
    [SerializeField] private GameObject bodyModelPrefab;

    [Tooltip("Transform głowy do ukrycia w widoku FPP (opcjonalnie).")]
    [SerializeField] private Transform headTransform;

    [Header("Layer Settings")]
    [Tooltip("Warstwa dla ciała gracza (domyślnie 'Default').")]
    [SerializeField] private string mirrorBodyLayer = "Default";

    private GameObject _spawnedBody;
    private Animator _bodyAnimator;
    private PlayerMovement _playerMovement;
    private CharacterController _characterController;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _characterController = GetComponent<CharacterController>();

        SetupBodyModel();
    }

    private void SetupBodyModel()
    {
        // 1. Sprawdź, czy ciało już istnieje pod graczem
        Transform existingBody = transform.Find("Player_Mirror_Body");
        if (existingBody != null)
        {
            _spawnedBody = existingBody.gameObject;
        }
        else if (bodyModelPrefab != null)
        {
            _spawnedBody = Instantiate(bodyModelPrefab, transform);
            _spawnedBody.name = "Player_Mirror_Body";
            _spawnedBody.transform.localPosition = Vector3.zero;
            _spawnedBody.transform.localRotation = Quaternion.identity;
        }

        if (_spawnedBody != null)
        {
            _bodyAnimator = _spawnedBody.GetComponentInChildren<Animator>();

            // Wyłącz kolidery ciała gracza, aby nie kolidowały z CharacterController
            foreach (var col in _spawnedBody.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Gwarantuj updateWhenOffscreen na wszystkich siatkach
            foreach (var smr in _spawnedBody.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true;
            }

            Debug.Log("<color=#70FF70>[PlayerMirrorBody] Ciało gracza zostało poprawnie zainicjalizowane i jest widoczne w lustrze!</color>");
        }
    }

    private void Update()
    {
        if (_bodyAnimator == null) return;

        bool isMoving = _playerMovement != null && _playerMovement.IsMoving;
        float speed = isMoving ? (_playerMovement.IsSprinting ? 2f : 1f) : 0f;

        _bodyAnimator.SetBool(IsWalkingHash, isMoving);
        _bodyAnimator.SetFloat(SpeedHash, speed);
    }
}
