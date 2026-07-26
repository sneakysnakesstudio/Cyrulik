using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHands : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private InputActionReference dropAction;

    [Header("Drop Settings")]
    [SerializeField] private float dropForce = 0.5f;

    private GameObject _heldItem;
    private Rigidbody _heldRigidbody;
    private Collider[] _heldColliders;

    public bool HasItem => _heldItem != null;
    public GameObject HeldItem => _heldItem;

    private void OnEnable()
    {
        if (dropAction == null)
            return;

        dropAction.action.Enable();
        dropAction.action.performed += OnDropPerformed;
    }

    private void OnDisable()
    {
        if (dropAction == null)
            return;

        dropAction.action.performed -= OnDropPerformed;
        dropAction.action.Disable();
    }

    public bool TryHold(GameObject item)
    {
        if (item == null || holdPoint == null || HasItem)
            return false;

        _heldItem = item;
        _heldRigidbody = item.GetComponent<Rigidbody>();
        _heldColliders = item.GetComponentsInChildren<Collider>();

        if (_heldRigidbody != null)
        {
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
            _heldRigidbody.useGravity = false;
            _heldRigidbody.isKinematic = true;
        }

        SetCollidersEnabled(false);

        item.transform.SetParent(holdPoint);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        return true;
    }

    public void DropHeldItem()
    {
        if (!HasItem)
            return;

        GameObject droppedItem = _heldItem;

        droppedItem.transform.SetParent(null);

        SetCollidersEnabled(true);

        if (_heldRigidbody != null)
        {
            _heldRigidbody.isKinematic = false;
            _heldRigidbody.useGravity = true;

            _heldRigidbody.AddForce(
                transform.forward * dropForce,
                ForceMode.Impulse
            );
        }

        ClearHand();
    }

    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        DropHeldItem();
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_heldColliders == null)
            return;

        foreach (Collider itemCollider in _heldColliders)
        {
            if (itemCollider != null)
                itemCollider.enabled = enabled;
        }
    }

    private void ClearHand()
    {
        _heldItem = null;
        _heldRigidbody = null;
        _heldColliders = null;
    }
}