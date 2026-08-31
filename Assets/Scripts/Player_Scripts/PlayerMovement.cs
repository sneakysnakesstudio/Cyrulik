using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public event Action<IInteractable> OnInteractableChanged;
    public event Action OnInteractionPerformed;
    public event Action<string> OnInteractionBlocked;

    [Header("Speed")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 6.5f;

    [Header("Sprint Stamina (3.5 - 5s Dash)")]
    [SerializeField] private bool useStamina = true;
    [Tooltip("Maksymalny czas ciągłego sprintu w sekundach (3.5 - 5s).")]
    [SerializeField] private float maxStamina = 4.0f;
    [Tooltip("Tempo zużywania staminy na sekundę sprintu.")]
    [SerializeField] private float staminaDrainRate = 1.0f;
    [Tooltip("Tempo regeneracji staminy na sekundę odpoczynku.")]
    [SerializeField] private float staminaRegenRate = 0.85f;
    [Tooltip("Procent naładowania staminy (np. 0.25 = 25%) wymagany do wznowienia sprintu po wyczerpaniu.")]
    [SerializeField] private float staminaResumeThreshold = 0.25f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference speedAction;
    [SerializeField] private InputActionReference interactAction;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionLayerMask = ~0;

    [Header("Gravity")]
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float groundedVelocity = -2f;

    [Header("Footsteps Audio")]
    [SerializeField] private bool enableFootsteps = true;
    [SerializeField] private string footstepAudioGroup = "player_steps";
    [Tooltip("Dystans w metrach między krokami podczas marszu.")]
    [SerializeField] private float stepDistanceWalk = 1.8f;
    [Tooltip("Dystans w metrach między krokami podczas sprintu.")]
    [SerializeField] private float stepDistanceSprint = 1.4f;

    [Header("Safety / Anti-Void")]
    [Tooltip("Minimalna wysokość Y, poniżej której gracz jest automatycznie cofany do bezpiecznej pozycji.")]
    [SerializeField] private float voidKillY = -4f;

    private Vector3 _lastSafeGroundedPosition;
    private bool _hasSafePosition = false;

    private CharacterController _characterController;

    private Vector2 _moveInput;
    private float _verticalVelocity;
    private float _stepDistanceCounter;

    private IInteractable _currentInteractable;

    // Cache dla CheckForInteractable — unikamy GetComponentInParent() każdą klatkę
    private Collider _lastHitCollider;
    private IInteractable _lastHitInteractable;

    /// <summary>Czy gracz aktualnie się porusza (uwzględnia input oraz velocity CC).</summary>
    public bool IsMoving =>
        _characterController != null &&
        (_moveInput.sqrMagnitude > 0.01f ||
         (_characterController.velocity.x * _characterController.velocity.x +
          _characterController.velocity.z * _characterController.velocity.z > 0.01f));

    private float _currentStamina;
    private bool _isExhausted = false;
    private bool _isSprinting = false;

    public float CurrentStamina => _currentStamina;
    public float MaxStamina => maxStamina;
    public bool IsSprinting => _isSprinting;
    public bool IsExhausted => _isExhausted;

    private void Awake()
    {
        _characterController =
            GetComponent<CharacterController>();

        _lastSafeGroundedPosition = transform.position;
        _hasSafePosition = true;

        _currentStamina = maxStamina;
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        speedAction.action.Enable();
        interactAction.action.Enable();

        moveAction.action.performed +=
            StoreMovementInput;

        moveAction.action.canceled +=
            StoreMovementInput;

        // STARTED = reakcja natychmiast po wciśnięciu E.
        interactAction.action.started +=
            HandleInteraction;
    }

    private void OnDisable()
    {
        moveAction.action.performed -=
            StoreMovementInput;

        moveAction.action.canceled -=
            StoreMovementInput;

        interactAction.action.started -=
            HandleInteraction;

        moveAction.action.Disable();
        speedAction.action.Disable();
        interactAction.action.Disable();
    }

    private void Update()
    {
        HandleGravity();
        HandleMovement();
        CheckForInteractable();
        CheckVoidAndSafety();
    }

    private void CheckVoidAndSafety()
    {
        if (_characterController != null && _characterController.isGrounded && transform.position.y > (voidKillY + 1f))
        {
            _lastSafeGroundedPosition = transform.position;
            _hasSafePosition = true;
        }

        if (transform.position.y < voidKillY || float.IsNaN(transform.position.x) || float.IsInfinity(transform.position.x))
        {
            RecoverPlayerToSafePosition();
        }
    }

    public void RecoverPlayerToSafePosition()
    {
        if (_characterController != null)
        {
            _characterController.enabled = false;
        }

        transform.position = _hasSafePosition ? (_lastSafeGroundedPosition + Vector3.up * 0.1f) : new Vector3(0f, 1f, 0f);
        _verticalVelocity = 0f;

        if (_characterController != null)
        {
            _characterController.enabled = true;
        }

        Debug.LogWarning("[PlayerMovement] Wykryto wypadnięcie poza mapę! Gracz został bezpiecznie przywrócony na podłogę.");
    }

    private void StoreMovementInput(
        InputAction.CallbackContext context
    )
    {
        _moveInput =
            context.ReadValue<Vector2>();
    }

    private void HandleGravity()
    {
        if (_characterController.isGrounded)
        {
            if (_verticalVelocity < 0f)
            {
                _verticalVelocity =
                    groundedVelocity;
            }
        }
        else
        {
            _verticalVelocity +=
                gravity * Time.deltaTime;
        }
    }

    private void HandleMovement()
    {
        if (cameraTransform == null)
            return;

        Vector3 forward =
            cameraTransform.forward;

        Vector3 right =
            cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection =
            forward * _moveInput.y +
            right * _moveInput.x;

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        bool wantsToSprint = speedAction.action.IsPressed() && _moveInput.sqrMagnitude > 0.01f;

        if (useStamina)
        {
            if (wantsToSprint && !_isExhausted && _currentStamina > 0f)
            {
                _isSprinting = true;
                _currentStamina -= staminaDrainRate * Time.deltaTime;
                if (_currentStamina <= 0f)
                {
                    _currentStamina = 0f;
                    _isExhausted = true;
                    _isSprinting = false;
                }
            }
            else
            {
                _isSprinting = false;
                if (_currentStamina < maxStamina)
                {
                    _currentStamina += staminaRegenRate * Time.deltaTime;
                    if (_currentStamina >= maxStamina)
                    {
                        _currentStamina = maxStamina;
                    }
                }

                if (_isExhausted && _currentStamina >= (maxStamina * staminaResumeThreshold))
                {
                    _isExhausted = false;
                }
            }

            if (SprintStaminaUI.Instance != null)
            {
                SprintStaminaUI.Instance.UpdateStamina(_currentStamina, maxStamina, _isExhausted, _isSprinting);
            }
        }
        else
        {
            _isSprinting = wantsToSprint;
        }

        float currentSpeed = _isSprinting ? sprintSpeed : walkSpeed;

        Vector3 velocity =
            moveDirection * currentSpeed;

        velocity.y =
            _verticalVelocity;

        _characterController.Move(
            velocity * Time.deltaTime
        );

        Vector3 horizontalVelocity = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);
        HandleFootsteps(horizontalVelocity, _isSprinting);
    }

    private void HandleFootsteps(Vector3 horizontalVelocity, bool isSprinting)
    {
        if (!enableFootsteps || !_characterController.isGrounded)
            return;

        float speed = horizontalVelocity.magnitude;
        if (speed < 0.15f)
        {
            return;
        }

        float stepInterval = isSprinting ? stepDistanceSprint : stepDistanceWalk;
        _stepDistanceCounter += speed * Time.deltaTime;

        if (_stepDistanceCounter >= stepInterval)
        {
            _stepDistanceCounter = 0f;
            if (!string.IsNullOrEmpty(footstepAudioGroup) && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(footstepAudioGroup);
            }
        }
    }

    private void CheckForInteractable()
    {
        if (cameraTransform == null)
            return;

        Ray ray = new Ray(
            cameraTransform.position,
            cameraTransform.forward
        );

        IInteractable foundInteractable = null;

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactionDistance,
                interactionLayerMask,
                QueryTriggerInteraction.Ignore
            ))
        {
            // Keszujemy GetComponentInParent — wywołujemy tylko gdy hit collider się zmienił
            if (hit.collider != _lastHitCollider)
            {
                _lastHitCollider = hit.collider;
                _lastHitInteractable = hit.collider.GetComponentInParent<IInteractable>();
            }
            foundInteractable = _lastHitInteractable;
        }
        else
        {
            _lastHitCollider = null;
            _lastHitInteractable = null;
        }

        if (foundInteractable == _currentInteractable)
            return;

        _currentInteractable = foundInteractable;

        OnInteractableChanged?.Invoke(_currentInteractable);

        if (_currentInteractable is ILookAtHandler lookAtHandler)
        {
            lookAtHandler.OnLookAt();
        }
    }

    private void HandleInteraction(
        InputAction.CallbackContext context
    )
    {
        if (_currentInteractable == null)
            return;

        // Jeśli obiekt wymaga przytrzymania (Hold to Open), procesem zarządza Crosshair!
        if (_currentInteractable is IHoldInteractable holdInteractable && holdInteractable.RequiresHold)
            return;

        PerformInteraction();
    }

    public void PerformInteraction()
    {
        if (_currentInteractable == null)
            return;

        if (_currentInteractable is IConditionalInteractable conditional)
        {
            if (!conditional.CanInteract)
            {
                OnInteractionBlocked?.Invoke(conditional.BlockedMessage);
                return;
            }
        }

        _currentInteractable.Interact();

        OnInteractionPerformed?.Invoke();
    }
}