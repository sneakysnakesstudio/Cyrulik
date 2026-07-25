using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference speedAction;

    [Header("Gravity")]
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float groundedVelocity = -2f;

    private CharacterController _characterController;
    private Vector2 _moveInput;
    private float _verticalVelocity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        speedAction.action.Enable();

        moveAction.action.performed += StoreMovementInput;
        moveAction.action.canceled += StoreMovementInput;
    }

    private void OnDisable()
    {
        moveAction.action.performed -= StoreMovementInput;
        moveAction.action.canceled -= StoreMovementInput;

        moveAction.action.Disable();
        speedAction.action.Disable();
    }

    private void Update()
    {
        HandleGravity();
        HandleMovement();
    }

    private void StoreMovementInput(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    private void HandleGravity()
    {
        if (_characterController.isGrounded && _verticalVelocity < 0f)
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

        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        bool isSprinting = speedAction.action.IsPressed();
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 finalMove = moveDirection * currentSpeed;
        finalMove.y = _verticalVelocity;

        _characterController.Move(finalMove * Time.deltaTime);
    }
}