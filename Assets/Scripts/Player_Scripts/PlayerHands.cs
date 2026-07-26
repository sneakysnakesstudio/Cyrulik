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

    private void Awake()
    {
        Debug.Log($"[PlayerHands] Awake na obiekcie: {gameObject.name}", this);

        if (holdPoint == null)
        {
            Debug.LogError(
                "[PlayerHands] HOLD POINT NIE JEST PODPIĘTY W INSPECTORZE!",
                this
            );
        }
        else
        {
            Debug.Log(
                $"[PlayerHands] HoldPoint podpięty: {holdPoint.name}.",
                holdPoint
            );
        }
    }

    private void OnEnable()
    {
        if (dropAction == null)
        {
            Debug.LogWarning(
                "[PlayerHands] Drop Action nie jest podpięty. Podnoszenie zadziała, ale G nie upuści przedmiotu.",
                this
            );

            return;
        }

        dropAction.action.Enable();
        dropAction.action.performed += OnDropPerformed;

        Debug.Log("[PlayerHands] Drop Action zostało włączone.", this);
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
        Debug.Log(
            $"[PlayerHands] TryHold() wywołane. Próba podniesienia: " +
            $"{(item == null ? "NULL" : item.name)}.",
            this
        );

        if (item == null)
        {
            Debug.LogError(
                "[PlayerHands] Nie można podnieść przedmiotu, ponieważ item jest NULL!",
                this
            );

            return false;
        }

        if (holdPoint == null)
        {
            Debug.LogError(
                $"[PlayerHands] Nie można podnieść {item.name}, ponieważ HoldPoint nie jest podpięty!",
                this
            );

            return false;
        }

        if (HasItem)
        {
            Debug.LogWarning(
                $"[PlayerHands] Ręka jest zajęta przez: {_heldItem.name}. " +
                $"Nie można podnieść: {item.name}.",
                this
            );

            return false;
        }

        _heldItem = item;
        _heldRigidbody = item.GetComponent<Rigidbody>();
        _heldColliders = item.GetComponentsInChildren<Collider>();

        Debug.Log(
            $"[PlayerHands] Rigidbody: {(_heldRigidbody != null ? "ZNALEZIONO" : "BRAK")}, " +
            $"liczba Colliderów: {_heldColliders.Length}.",
            item
        );

        if (_heldRigidbody != null)
        {
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
            _heldRigidbody.useGravity = false;
            _heldRigidbody.isKinematic = true;

            Debug.Log("[PlayerHands] Wyłączono fizykę przedmiotu.", item);
        }

        SetCollidersEnabled(false);

        item.transform.SetParent(holdPoint);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        Debug.Log(
            $"[PlayerHands] SUKCES: {item.name} przeniesiono do HoldPoint. " +
            $"Pozycja lokalna: {item.transform.localPosition}.",
            item
        );

        return true;
    }

    public void DropHeldItem()
    {
        Debug.Log("[PlayerHands] Wywołano DropHeldItem().", this);

        if (!HasItem)
        {
            Debug.LogWarning(
                "[PlayerHands] Nie można upuścić przedmiotu — ręka jest pusta.",
                this
            );

            return;
        }

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

        Debug.Log($"[PlayerHands] Upuszczono: {droppedItem.name}.", droppedItem);
    }

    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        Debug.Log("[PlayerHands] Wykryto przycisk Drop.", this);
        DropHeldItem();
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_heldColliders == null)
        {
            Debug.LogWarning("[PlayerHands] Lista colliderów jest null.", this);
            return;
        }

        foreach (Collider itemCollider in _heldColliders)
        {
            if (itemCollider != null)
                itemCollider.enabled = enabled;
        }

        Debug.Log(
            $"[PlayerHands] Collidery przedmiotu ustawiono na: {enabled}.",
            this
        );
    }

    private void ClearHand()
    {
        _heldItem = null;
        _heldRigidbody = null;
        _heldColliders = null;
    }
}