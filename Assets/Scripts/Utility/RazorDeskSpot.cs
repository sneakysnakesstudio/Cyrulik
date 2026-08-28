using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Dedykowane miejsce na biurku / toaletce fryzjerskiej (Desk Snap Point),
/// na które gracz może odłożyć brzytwę (Razor), dokładnie tak samo jak garnek na piecu.
/// </summary>
public class RazorDeskSpot : MonoBehaviour, IConditionalInteractable
{
    [Header("Snap Point")]
    [Tooltip("Punkt na biurku, do którego brzytwa zostanie idealnie dopasowana. Jeśli puste, użyje pozycji tego obiektu.")]
    [SerializeField] private Transform razorSnapPoint;

    [Header("Początkowy obiekt brzytwy (Opcjonalnie)")]
    [Tooltip("Obiekt brzytwy, który leży na biurku od startu gry. Jeśli puste, skrypt spróbuje go automatycznie znaleźć w pobliżu.")]
    [SerializeField] private GameObject initialRazorObject;

    [Header("Item IDs")]
    [Tooltip("Akceptowane identyfikatory przedmiotu brzytwy w rękach gracza.")]
    [SerializeField] private string[] acceptedRazorItemIds = new string[] 
    { 
        "razor", "razor_blade", "blade", "razor_sharpened", "sharp_razor", "dull_razor" 
    };

    [Header("Interaction Prompts (English)")]
    [SerializeField] private string promptPlaceRazor = "Place razor on desk";
    [SerializeField] private string promptDeskEmpty = "Desk (Place razor here)";
    [SerializeField] private string promptAlreadyOnDesk = "Razor is on the desk";

    [Header("Audio")]
    [Tooltip("Nazwa dźwięku odłożenia brzytwy w AudioManager (np. 'card_flip', 'item_drop').")]
    [SerializeField] private string soundPlaceRazor = "card_flip";
    [SerializeField] private AudioClip customPlaceClip;

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onRazorPlaced;
    [SerializeField] private UnityEvent onRazorTaken;

    private PlayerHands _playerHands;
    private GameObject _currentRazorOnDesk;
    private Vector3 _originalWorldPos;
    private Quaternion _originalWorldRot;
    private Vector3 _originalScale = Vector3.one;
    private bool _hasRecordedTransform = false;

    public bool HasRazorOnDesk => _currentRazorOnDesk != null && _currentRazorOnDesk.activeInHierarchy && _currentRazorOnDesk.transform.parent != null && (_currentRazorOnDesk.transform.parent == transform || _currentRazorOnDesk.transform.parent == razorSnapPoint);

    public bool CanInteract
    {
        get
        {
            if (HasRazorOnDesk)
                return false; // Gracz wchodzi w interakcję bezpośrednio z brzytwą (PickupItem)

            return IsHoldingRazor();
        }
    }

    public string InteractionName
    {
        get
        {
            if (!HasRazorOnDesk && IsHoldingRazor())
                return promptPlaceRazor;

            if (HasRazorOnDesk)
                return promptAlreadyOnDesk;

            return promptDeskEmpty;
        }
    }

    private void Awake()
    {
        if (_playerHands == null)
            _playerHands = FindAnyObjectByType<PlayerHands>();

        if (razorSnapPoint == null)
        {
            Transform found = transform.Find("Razor_SnapPoint") ?? transform.Find("SnapPoint");
            razorSnapPoint = found != null ? found : transform;
        }
    }

    private void Start()
    {
        // 1. Sprawdź, czy na biurku na starcie leży brzytwa
        if (initialRazorObject != null)
        {
            SetInitialRazor(initialRazorObject);
        }
        else
        {
            // Spróbuj znaleźć brzytwę będącą dzieckiem lub w bliskim sąsiedztwie
            PickupItem childPickup = GetComponentInChildren<PickupItem>();
            if (childPickup != null && IsRazorPickup(childPickup))
            {
                SetInitialRazor(childPickup.gameObject);
            }
        }
    }

    private void SetInitialRazor(GameObject razorGo)
    {
        _currentRazorOnDesk = razorGo;
        _originalWorldPos = razorGo.transform.position;
        _originalWorldRot = razorGo.transform.rotation;
        _originalScale = razorGo.transform.localScale;
        _hasRecordedTransform = true;

        if (razorGo.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public void Interact()
    {
        if (_playerHands == null)
            _playerHands = FindAnyObjectByType<PlayerHands>();

        if (!IsHoldingRazor())
            return;

        PlaceRazorOnDesk();
    }

    /// <summary>
    /// Odkłada trzymaną brzytwę na biurko w wyznaczonym miejscu.
    /// </summary>
    public void PlaceRazorOnDesk()
    {
        if (_playerHands == null || !_playerHands.HasItem)
            return;

        GameObject heldRazor = _playerHands.ReleaseHeldItem();
        if (heldRazor == null)
            return;

        _currentRazorOnDesk = heldRazor;

        // Upewnij się, że obiekt i renderery są włączone
        heldRazor.SetActive(true);
        var renderers = heldRazor.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = true;
        }

        // Przypnij do SnapPointu lub pozycji początkowej
        Transform targetParent = razorSnapPoint != null ? razorSnapPoint : transform;
        heldRazor.transform.SetParent(targetParent, true);

        if (_hasRecordedTransform)
        {
            heldRazor.transform.position = _originalWorldPos;
            heldRazor.transform.rotation = _originalWorldRot;
            heldRazor.transform.localScale = _originalScale;
        }
        else if (razorSnapPoint != null && razorSnapPoint != transform)
        {
            heldRazor.transform.localPosition = Vector3.zero;
            heldRazor.transform.localRotation = Quaternion.identity;
        }
        else
        {
            heldRazor.transform.position = transform.position + Vector3.up * 0.02f;
        }

        // Zabezpiecz fizykę, aby brzytwa leżała stabilnie
        if (heldRazor.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Włącz komponent PickupItem z ewentualną zaktualizowaną nazwą
        if (heldRazor.TryGetComponent<PickupItem>(out var pickup))
        {
            pickup.enabled = true;
            bool isSharpened = PreparationStateManager.Instance != null && PreparationStateManager.Instance.IsTaskCompleted("razor_sharpened");
            if (isSharpened)
            {
                pickup.InteractionName = "Pick up sharpened razor";
            }
        }

        // Dźwięk odłożenia
        PlayPlaceSound();

        onRazorPlaced?.Invoke();
        Debug.Log("[RazorDeskSpot] Brzytwa została bezpiecznie odłożona na biurko!");
    }

    private void PlayPlaceSound()
    {
        if (customPlaceClip != null)
        {
            AudioSource.PlayClipAtPoint(customPlaceClip, transform.position);
            return;
        }

        if (!string.IsNullOrEmpty(soundPlaceRazor) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundPlaceRazor);
        }
    }

    private bool IsHoldingRazor()
    {
        if (_playerHands == null || !_playerHands.HasItem)
            return false;

        GameObject held = _playerHands.HeldItem;
        if (held == null)
            return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            return IsRazorPickup(pickup);
        }

        string n = held.name.ToLowerInvariant();
        return n.Contains("razor") || n.Contains("brzytwa") || n.Contains("blade");
    }

    private bool IsRazorPickup(PickupItem pickup)
    {
        if (pickup == null) return false;

        string id = pickup.ItemId != null ? pickup.ItemId.Trim().ToLowerInvariant() : "";
        foreach (var accepted in acceptedRazorItemIds)
        {
            if (!string.IsNullOrEmpty(accepted) && id == accepted.Trim().ToLowerInvariant())
                return true;
        }

        string name = pickup.name.ToLowerInvariant();
        return name.Contains("razor") || name.Contains("brzytwa") || name.Contains("blade");
    }
}
