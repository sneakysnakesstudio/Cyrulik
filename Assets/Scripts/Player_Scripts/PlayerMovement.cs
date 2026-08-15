using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public event Action OnInteractionPerformed;
    public event Action<IInteractable> OnInteractableChanged;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference interactionAction;

    [Header("Interaction")]
    [SerializeField] private LayerMask interactionLayerMask;
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private bool showInteractionRaycast = true;

    [Header("Gravity")]
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float groundedVelocity = -2f;

    [Header("Footsteps")]
    [SerializeField] private string footstepAudioGroup = "player_steps";
    [SerializeField] private float footstepInterval = 0.5f;
    [SerializeField] private float firstStepDelay = 0.15f;
    [SerializeField] private float minimumMovementSpeed = 0.1f;

    private CharacterController _characterController;

    private Vector2 _moveInput;
    private float _verticalVelocity;

    private IInteractable _currentInteractable;
    private IInteractable _previousInteractable;

    private float _footstepTimer;
    private bool _wasMoving;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleGravity();
        HandleMovement();
        HandleFootsteps();
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

        Vector3 finalMove =
            moveDirection * walkSpeed;

        finalMove.y = _verticalVelocity;

        _characterController.Move(
            finalMove * Time.deltaTime
        );
    }

    private void HandleFootsteps()
    {
        Vector3 horizontalVelocity =
            _characterController.velocity;

        horizontalVelocity.y = 0f;

        bool isMoving =
            _characterController.isGrounded &&
            horizontalVelocity.magnitude > minimumMovementSpeed;

        if (!isMoving)
        {
            _footstepTimer = 0f;
            _wasMoving = false;
            return;
        }

        if (!_wasMoving)
        {
            _wasMoving = true;
            _footstepTimer = firstStepDelay;
        }

        _footstepTimer -= Time.deltaTime;

        if (_footstepTimer > 0f)
            return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(
                footstepAudioGroup
            );
        }

        _footstepTimer = footstepInterval;
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

    private void HandleInteractionInput(
        InputAction.CallbackContext context
    )
    {
        if (_currentInteractable == null)
            return;

        _currentInteractable.Interact();

        OnInteractionPerformed?.Invoke();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
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
        interactionAction.action.Disable();

        _moveInput = Vector2.zero;

        _footstepTimer = 0f;
        _wasMoving = false;

        _currentInteractable = null;
        _previousInteractable = null;

        OnInteractableChanged?.Invoke(null);
    }
}