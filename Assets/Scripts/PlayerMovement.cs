using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
  [Header("Speed")] [SerializeField] private float walkSpeed = 5f;
  [Header("Speed")] [SerializeField] private float runSpeed = 5f;

  [Header("References")]
  [SerializeField] private Transform cameraTransform;
  [SerializeField] private InputActionReference moveAction;
  
  private CharacterController _charecterController;
  private Vector2 _moveInput;

  private void Awake()
  {
    _charecterController = GetComponent<CharacterController>();
  }

  private void OnEnable()
  {
    moveAction.action.Enable();
    
    moveAction.action.performed += StoreMovementInput;
    moveAction.action.canceled += StoreMovementInput;
  }

  private void OnDisable()
  {
    moveAction.action.performed -= StoreMovementInput;
    moveAction.action.canceled -= StoreMovementInput;
  }

  private void StoreMovementInput(InputAction.CallbackContext context)
  {
    _moveInput = context.ReadValue<Vector2>();
  }

  private void Update()
  {
    HandleMovement();
  }

  private void HandleMovement()
  {
      var move = cameraTransform.TransformDirection(new Vector3(_moveInput.x, 0f, _moveInput.y).normalized);
      var currentSpeed = walkSpeed;
      var finalMove = move * currentSpeed;
      
      _charecterController.Move(finalMove * Time.deltaTime);
  }
  
  
}
