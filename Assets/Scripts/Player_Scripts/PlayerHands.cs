using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Zarządza podnoszeniem, trzymaniem i upuszczaniem przedmiotów przez gracza.
/// Obsługuje zarówno bezpośrednie podczepianie pod gniazdo (HoldPoint / Item),
/// jak i dedykowane modele wizualne w rękach pierwszoosobowych.
/// </summary>
public class PlayerHands : MonoBehaviour
{
    [System.Serializable]
    public class InHandVisual
    {
        [Tooltip("ID przedmiotu odpowiadające ItemId w PickupItem (np. 'razor_blade', 'cheese').")]
        public string itemId;

        [Tooltip("Obiekt GameObject w hierarchii gracza (np. pod HoldPoint/Item), który ma się włączyć po podniesieniu.")]
        public GameObject visualObject;
    }

    [Header("Gniazda i Referencje")]
    [Tooltip("Punkt nadrzędny trzymania (HoldPoint na kamerze gracza).")]
    [SerializeField] private Transform holdPoint;

    [Tooltip("Opcjonalny punkt docelowy 'Item' pod HoldPoint. Jeśli puste, skrypt automatycznie wyszuka dziecka 'Item' lub użyje holdPoint.")]
    [SerializeField] private Transform itemSocket;

    [SerializeField] private InputActionReference dropAction;

    [Header("Dedykowane modele w rękach (Opcjonalnie)")]
    [Tooltip("Jeśli przygotowałeś modele jako dzieci pod HoldPoint/Item, możesz je tutaj powiązać z ItemId.")]
    [SerializeField] private InHandVisual[] inHandVisuals;

    [Header("Ustawienia upuszczania")]
    [SerializeField] private float dropForce = 0.5f;

    private GameObject _heldItem;
    private Rigidbody _heldRigidbody;
    private Collider[] _heldColliders;
    private GameObject _activeVisual;

    public bool HasItem => _heldItem != null;
    public GameObject HeldItem => _heldItem;
    public Transform HoldPoint => holdPoint;

    public Transform ItemSocket => GetSocket();

    private void Awake()
    {
        DeactivateAllVisuals();
    }

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

    /// <summary>
    /// Zwraca właściwy transform, pod który mają być podczepiane przedmioty w ręku.
    /// </summary>
    public Transform GetSocket()
    {
        if (itemSocket != null)
            return itemSocket;

        if (holdPoint != null)
        {
            Transform itemChild = holdPoint.Find("Item");
            if (itemChild != null)
                return itemChild;

            return holdPoint;
        }

        return transform;
    }

    /// <summary>
    /// Podnosi i umieszcza obiekt w rękach gracza.
    /// </summary>
    public bool TryHold(GameObject item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerHands] TryHold failed: item is NULL");
            return false;
        }

        if (HasItem)
        {
            Debug.LogWarning($"[PlayerHands] TryHold failed: already holding '{_heldItem.name}'");
            return false;
        }

        if (holdPoint == null)
        {
            holdPoint = transform.Find("HoldPoint") 
                     ?? transform.Find("CinemachineCamera/HoldPoint") 
                     ?? transform.Find("Main Camera/HoldPoint");

            if (holdPoint == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) holdPoint = cam.transform;
            }

            Debug.Log($"[PlayerHands] holdPoint was null, auto-resolved to: {(holdPoint != null ? holdPoint.name : "STILL NULL")}");
        }

        if (holdPoint == null)
        {
            Debug.LogError("[PlayerHands] TryHold failed: holdPoint is NULL and could not be resolved!");
            return false;
        }

        _heldItem = item;
        _heldRigidbody = item.GetComponent<Rigidbody>();
        _heldColliders = item.GetComponentsInChildren<Collider>();

        // Wyłączamy fizykę na czas trzymania
        if (_heldRigidbody != null)
        {
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
            _heldRigidbody.useGravity = false;
            _heldRigidbody.isKinematic = true;
        }

        SetCollidersEnabled(false);

        // Pobieramy ItemId
        string currentItemId = null;
        PickupItem pickup = item.GetComponentInChildren<PickupItem>();
        if (pickup != null)
        {
            currentItemId = pickup.ItemId;
        }

        Debug.Log($"[PlayerHands] Successfully holding '{item.name}' with ItemId='{currentItemId}'");

        // 1. Sprawdzamy, czy gracz ma przygotowany dedykowany model wizualny pod HoldPoint/Item
        GameObject matchedVisual = FindVisualForItemId(currentItemId);
        if (matchedVisual != null)
        {
            Debug.Log($"[PlayerHands] Activating in-hand visual model: '{matchedVisual.name}'");
            // Włączamy model w ręku
            matchedVisual.SetActive(true);
            _activeVisual = matchedVisual;

            // Ukrywamy renderery rzeczywistego obiektu ze świata i parsujemy go pod socket
            item.transform.SetParent(GetSocket());
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            SetRenderersEnabled(item, false);
        }
        else
        {
            // 2. Podczepiamy rzeczywisty obiekt ze świata pod socket
            Transform socket = GetSocket();
            Debug.Log($"[PlayerHands] Attaching physical item to socket: '{socket.name}'");
            item.transform.SetParent(socket);

            if (pickup != null)
            {
                item.transform.localPosition = pickup.InHandPosition;
                item.transform.localRotation = Quaternion.Euler(pickup.InHandRotation);
                item.transform.localScale = pickup.InHandScale;
            }
            else
            {
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
            }

            SetRenderersEnabled(item, true);
        }

        return true;
    }

    /// <summary>
    /// Wyrzuca aktualnie trzymany przedmiot do świata.
    /// </summary>
    public void DropHeldItem()
    {
        if (!HasItem)
            return;

        GameObject droppedItem = _heldItem;

        // Przywracamy renderery obiektu świata, jeśli były ukryte
        SetRenderersEnabled(droppedItem, true);
        droppedItem.transform.SetParent(null);
        SetCollidersEnabled(true);

        if (_heldRigidbody != null)
        {
            _heldRigidbody.isKinematic = false;
            _heldRigidbody.useGravity = true;
            _heldRigidbody.AddForce(transform.forward * dropForce, ForceMode.Impulse);
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

    private void SetRenderersEnabled(GameObject root, bool enabled)
    {
        if (root == null) return;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = enabled;
        }
    }

    private GameObject FindVisualForItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        // A. Sprawdź listę w Inspektorze
        if (inHandVisuals != null)
        {
            foreach (var visual in inHandVisuals)
            {
                if (visual != null && visual.visualObject != null &&
                    string.Equals(visual.itemId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    return visual.visualObject;
                }
            }
        }

        // B. Sprawdź dzieci pod HoldPoint / Item o pasującej nazwie
        Transform socket = GetSocket();
        if (socket != null)
        {
            // Sprawdź dzieci socketu
            for (int i = 0; i < socket.childCount; i++)
            {
                Transform child = socket.GetChild(i);
                string childName = child.name.ToLowerInvariant();
                string targetId = itemId.ToLowerInvariant();

                if (childName.Contains(targetId) ||
                    (targetId.Contains("blade") && childName.Contains("blade")) ||
                    (targetId.Contains("cheese") && childName.Contains("cheese")))
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private void DeactivateAllVisuals()
    {
        if (_activeVisual != null)
        {
            _activeVisual.SetActive(false);
            _activeVisual = null;
        }

        if (inHandVisuals != null)
        {
            foreach (var visual in inHandVisuals)
            {
                if (visual != null && visual.visualObject != null)
                {
                    visual.visualObject.SetActive(false);
                }
            }
        }
    }

    public void ClearHand()
    {
        DeactivateAllVisuals();
        _heldItem = null;
        _heldRigidbody = null;
        _heldColliders = null;
    }

    /// <summary>
    /// Zwalnia trzymany obiekt z rąk (np. do położenia na pułapce).
    /// </summary>
    public GameObject ReleaseHeldItem()
    {
        if (!HasItem)
            return null;

        GameObject item = _heldItem;
        SetRenderersEnabled(item, true);
        item.transform.SetParent(null);
        SetCollidersEnabled(true);
        ClearHand();
        return item;
    }

    /// <summary>
    /// Niszczy / zużywa trzymany w rękach przedmiot.
    /// </summary>
    public void DestroyHeldItem()
    {
        if (!HasItem)
            return;

        GameObject item = _heldItem;
        ClearHand();
        Destroy(item);
    }
}