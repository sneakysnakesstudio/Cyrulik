using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public event Action OnInteractionPerformed;
    public event Action<IInteractable> OnInteractableChanged;

    [Header("Speed")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference speedAction;
    [SerializeField] private InputActionReference interactionAction;

    [Header("Interaction")]
    [SerializeField] private LayerMask interactionLayerMask;
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private bool showInteractionRaycast = true;

    [Header("Gravity")]
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float groundedVelocity = -2f;

    private CharacterController _characterController;

    private Vector2 _moveInput;
    private float _verticalVelocity;

    private IInteractable _currentInteractable;
    private IInteractable _previousInteractable;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleGravity();
        HandleMovement();
        HandleInteractionRaycast();
    }

    private void StoreMovementInput(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    private void HandleGravity()
    {
        if (_characterController.isGrounded &&
            _verticalVelocity < 0f)
        {
            _verticalVelocity = groundedVelocity;
        }
        else
        {
            _verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void HandleMovement()
    {
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection =
            cameraRight * _moveInput.x +
            cameraForward * _moveInput.y;

        moveDirection =
            Vector3.ClampMagnitude(moveDirection, 1f);

        bool isSprinting = speedAction.action.IsPressed();

        float currentSpeed =
            isSprinting
                ? sprintSpeed
                : walkSpeed;

        Vector3 finalMove =
            moveDirection * currentSpeed;

        finalMove.y = _verticalVelocity;

        _characterController.Move(
            finalMove * Time.deltaTime
        );
    }

    private void HandleInteractionRaycast()
    {
        _currentInteractable = null;

        Ray ray = new Ray(
            cameraTransform.position,
            cameraTransform.forward
        );

        bool hitSomething = Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance,
            interactionLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (hitSomething)
        {
            _currentInteractable =
                hit.collider
                    .GetComponentInParent<IInteractable>();
        }

        if (_currentInteractable != _previousInteractable)
        {
            _previousInteractable =
                _currentInteractable;

            OnInteractableChanged?.Invoke(
                _currentInteractable
            );
        }

        if (showInteractionRaycast)
        {
            Debug.DrawRay(
                ray.origin,
                ray.direction * interactionDistance,
                _currentInteractable != null
                    ? Color.green
                    : Color.red
            );
        }
    }

    private void HandleInteractionInput(InputAction.CallbackContext context)
    {
        if (_currentInteractable == null)
            return;

        _currentInteractable.Interact();

        OnInteractionPerformed?.Invoke();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        speedAction.action.Enable();
        interactionAction.action.Enable();

        moveAction.action.performed +=
            StoreMovementInput;

        moveAction.action.canceled +=
            StoreMovementInput;

        interactionAction.action.started +=
            HandleInteractionInput;
    }

    private void OnDisable()
    {
        moveAction.action.performed -=
            StoreMovementInput;

        moveAction.action.canceled -=
            StoreMovementInput;

        interactionAction.action.started -=
            HandleInteractionInput;

        moveAction.action.Disable();
        speedAction.action.Disable();
        interactionAction.action.Disable();

        _currentInteractable = null;
        _previousInteractable = null;

        OnInteractableChanged?.Invoke(null);
    }
}