using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Zarządza podnoszeniem, trzymaniem i upuszczaniem przedmiotów przez gracza.
/// Obsługuje zarówno bezpośrednie podczepianie pod gniazdo (HoldPoint / Item),
/// dedykowane modele wizualne w rękach pierwszoosobowych, jak i uniwersalne dźwięki podnoszenia / upuszczania.
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
    [Tooltip("Dodatkowa siła wyrzutu (0 = swobodny upadek prosto pod nogi na ziemię).")]
    [SerializeField] private float dropForce = 0f;

    [Header("Audio Podnoszenia i Upuszczania")]
    [Tooltip("Domyślna nazwa grupy dźwięku podnoszenia w AudioManager (np. 'item_pickup' lub 'cloth_pickup').")]
    [SerializeField] private string defaultPickupSound = "item_pickup";

    [Tooltip("Opcjonalny klip audio podnoszenia jako fallback (jeśli brak w AudioManager).")]
    [SerializeField] private AudioClip customPickupClip;

    [Tooltip("Domyślna nazwa grupy dźwięku upuszczania w AudioManager (np. 'item_drop' lub 'cloth_pickup').")]
    [SerializeField] private string defaultDropSound = "item_drop";

    [Tooltip("Opcjonalny klip audio upuszczenia jako fallback (jeśli brak w AudioManager).")]
    [SerializeField] private AudioClip customDropClip;

    [SerializeField] private AudioSource audioSource;

    private GameObject _heldItem;
    private Rigidbody _heldRigidbody;
    private Collider[] _heldColliders;
    private Renderer[] _heldRenderers;       // OPTYMALIZACJA: cache rendererów — brak GetComponentsInChildren przy każdym SetRenderersEnabled
    private GameObject _activeVisual;

    // OPTYMALIZACJA: cache koliderów i CharacterController gracza — brak GetComponentsInParent/FindAnyObjectByType przy każdym Drop
    private Collider[]        _cachedPlayerColliders;
    private CharacterController _cachedPlayerCC;

    public bool HasItem => _heldItem != null;
    public GameObject HeldItem => _heldItem;
    public Transform HoldPoint => holdPoint;

    public Transform ItemSocket => GetSocket();

    private void Awake()
    {
        DeactivateAllVisuals();

        // OPTYMALIZACJA: Buforuj kolidery i CC gracza raz — brak GetComponentsInParent/FindAnyObjectByType przy każdym Drop
        _cachedPlayerColliders = GetComponentsInParent<Collider>(true);
        if (_cachedPlayerColliders == null || _cachedPlayerColliders.Length == 0)
            _cachedPlayerColliders = GetComponentsInChildren<Collider>(true);
        _cachedPlayerCC = GetComponentInParent<CharacterController>() ?? GetComponent<CharacterController>();
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
    /// Podnosi i umieszcza obiekt w rękach gracza oraz odtwarza dźwięk podniesienia.
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
        _heldRenderers = item.GetComponentsInChildren<Renderer>(true); // OPTYMALIZACJA: cache raz przy podniesieniu

        // Wyłączamy fizykę na czas trzymania
        if (_heldRigidbody != null)
        {
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
            _heldRigidbody.useGravity = false;
            _heldRigidbody.isKinematic = true;
        }

        SetCollidersEnabled(false);

        // Pobieramy ItemId i komponent PickupItem
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
            matchedVisual.SetActive(true);
            _activeVisual = matchedVisual;

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

        // Dźwięk podniesienia przedmiotu
        PlayPickupSound(pickup);

        return true;
    }

    /// <summary>
    /// Wyrzuca aktualnie trzymany przedmiot do świata i odtwarza dźwięk upuszczenia.
    /// Przedmiot spada swobodnie prosto na ziemię bez wystrzeliwania w dal.
    /// </summary>
    public void DropHeldItem()
    {
        if (!HasItem)
            return;

        GameObject droppedItem = _heldItem;
        PickupItem pickup = droppedItem.GetComponentInChildren<PickupItem>();

        // 1. Przywracamy widoczność renderera obiektu świata
        SetRenderersEnabled(droppedItem, true);

        // 2. Bezpieczna pozycja upuszczenia - lekko z przodu i w dół, aby nie kolidował z ciałem gracza
        Vector3 dropPosition;
        if (holdPoint != null)
        {
            dropPosition = holdPoint.position + (holdPoint.forward * 0.15f) - (Vector3.up * 0.1f);
        }
        else
        {
            dropPosition = transform.position + (transform.forward * 0.35f) + (Vector3.up * 0.6f);
        }

        droppedItem.transform.SetParent(null);
        droppedItem.transform.position = dropPosition;

        // 3. Włącz kolidery i zignoruj kolizje z ciałem gracza (zapobiega fizycznemu wystrzeleniu przedmiotu)
        SetCollidersEnabled(true);
        IgnorePlayerCollisions(droppedItem);

        if (_heldRigidbody == null)
        {
            _heldRigidbody = droppedItem.GetComponent<Rigidbody>();
            if (_heldRigidbody == null)
            {
                _heldRigidbody = droppedItem.AddComponent<Rigidbody>();
                _heldRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        if (_heldRigidbody != null)
        {
            _heldRigidbody.isKinematic = false;
            _heldRigidbody.useGravity = true;
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;

            if (dropForce > 0.01f)
            {
                _heldRigidbody.AddForce(transform.forward * dropForce, ForceMode.Impulse);
            }
        }

        // Dźwięk upuszczenia przedmiotu
        PlayDropSound(pickup);

        ClearHand();
    }

    private void IgnorePlayerCollisions(GameObject droppedItem)
    {
        if (droppedItem == null) return;

        Collider[] itemColliders = droppedItem.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < itemColliders.Length; i++)
        {
            var itemCol = itemColliders[i];
            if (itemCol == null || itemCol.isTrigger) continue;

            // OPTYMALIZACJA: Używamy zbuforowanego CC z Awake — brak FindAnyObjectByType przy każdym Drop
            if (_cachedPlayerCC != null)
                Physics.IgnoreCollision(itemCol, _cachedPlayerCC, true);

            // OPTYMALIZACJA: Używamy zbuforowanej tablicy koliderów z Awake — brak GetComponentsInParent
            if (_cachedPlayerColliders != null)
            {
                for (int j = 0; j < _cachedPlayerColliders.Length; j++)
                {
                    var pCol = _cachedPlayerColliders[j];
                    if (pCol != null && pCol != itemCol)
                        Physics.IgnoreCollision(itemCol, pCol, true);
                }
            }
        }
    }

    private void PlayPickupSound(PickupItem pickup)
    {
        // 1. Sprawdź, czy przedmiot ma dedykowany dźwięk w PickupItem
        if (pickup != null && pickup.CustomPickupClip != null)
        {
            PlayClip(pickup.CustomPickupClip);
            return;
        }

        if (pickup != null && !string.IsNullOrEmpty(pickup.CustomPickupSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(pickup.CustomPickupSound);
            return;
        }

        // 2. Uniwersalny dźwięk z AudioManager
        if (!string.IsNullOrEmpty(defaultPickupSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(defaultPickupSound);
            return;
        }

        // 3. Fallback bezpośredni klip
        if (customPickupClip != null)
        {
            PlayClip(customPickupClip);
        }
    }

    private void PlayDropSound(PickupItem pickup)
    {
        // 1. Sprawdź, czy przedmiot ma dedykowany dźwięk w PickupItem
        if (pickup != null && pickup.CustomDropClip != null)
        {
            PlayClip(pickup.CustomDropClip);
            return;
        }

        if (pickup != null && !string.IsNullOrEmpty(pickup.CustomDropSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(pickup.CustomDropSound);
            return;
        }

        // 2. Uniwersalny dźwięk z AudioManager
        if (!string.IsNullOrEmpty(defaultDropSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(defaultDropSound);
            return;
        }

        // 3. Fallback bezpośredni klip
        if (customDropClip != null)
        {
            PlayClip(customDropClip);
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
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

        // OPTYMALIZACJA: Używamy zbuforowanych rendererów zamiast GetComponentsInChildren przy każdym wywołaniu
        if (_heldRenderers != null && root == _heldItem)
        {
            for (int i = 0; i < _heldRenderers.Length; i++)
            {
                if (_heldRenderers[i] != null)
                    _heldRenderers[i].enabled = enabled;
            }
            return;
        }

        // Fallback (np. dla activeVisual lub gdy brak cache)
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = enabled;
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
        _heldItem      = null;
        _heldRigidbody = null;
        _heldColliders = null;
        _heldRenderers = null; // OPTYMALIZACJA: wyczyść cache rendererów — brak wiszących referencji
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