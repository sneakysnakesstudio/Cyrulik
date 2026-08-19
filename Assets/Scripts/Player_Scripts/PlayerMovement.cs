using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public event Action<IInteractable> OnInteractableChanged;
    public event Action OnInteractionPerformed;
    public event Action OnInteractionBlocked;

    [Header("Speed")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 6f;

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

    private CharacterController _characterController;

    private Vector2 _moveInput;
    private float _verticalVelocity;

    private IInteractable _currentInteractable;

    /// <summary>Czy gracz aktualnie się porusza (uwzględnia kolizje — sprawdza velocity CC).</summary>
    public bool IsMoving =>
        _characterController != null &&
        _characterController.isGrounded &&
        new Vector2(
            _characterController.velocity.x,
            _characterController.velocity.z
        ).sqrMagnitude > 0.01f;

    private void Awake()
    {
        _characterController =
            GetComponent<CharacterController>();
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

        bool isSprinting =
            speedAction.action.IsPressed();

        float currentSpeed =
            isSprinting
                ? sprintSpeed
                : walkSpeed;

        Vector3 velocity =
            moveDirection * currentSpeed;

        velocity.y =
            _verticalVelocity;

        _characterController.Move(
            velocity * Time.deltaTime
        );
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
            foundInteractable =
                hit.collider
                    .GetComponentInParent<IInteractable>();
        }

        if (foundInteractable == _currentInteractable)
            return;

        _currentInteractable =
            foundInteractable;

        OnInteractableChanged?.Invoke(
            _currentInteractable
        );
    }

    private void HandleInteraction(
        InputAction.CallbackContext context
    )
    {
        if (_currentInteractable == null)
            return;

        // NOWE:
        // obiekt może istnieć jako interactable,
        // ale chwilowo blokować wykonanie akcji.
        if (_currentInteractable
            is IConditionalInteractable conditional)
        {
            if (!conditional.CanInteract)
            {
                OnInteractionBlocked?.Invoke();
                return;
            }
        }

        _currentInteractable.Interact();

        OnInteractionPerformed?.Invoke();
    }
}