using UnityEngine;

/// <summary>
/// Obsługuje pułapkę na myszy.
/// Wymaga przyniesienia sera z lodówki i położenia go na pułapce.
/// Po uzbrojeniu zalicza zadanie w PreparationStateManager.
/// </summary>
public class MouseTrap : MonoBehaviour, IConditionalInteractable
{
    [Header("Zadanie i Przedmiot")]
    [Tooltip("ID zadania w PreparationStateManager (np. 'mousetrap_baited' lub 'mouse_trap').")]
    [SerializeField] private string taskId = "mousetrap_baited";

    [Tooltip("Wymagany ID przedmiotu z PickupItem (domyślnie 'cheese').")]
    [SerializeField] private string requiredItemId = "cheese";

    [Header("Wizualia przynęty")]
    [Tooltip("Punkt (Transform), do którego przyczepi się ser po położeniu.")]
    [SerializeField] private Transform baitSnapPoint;

    [Tooltip("(Opcjonalnie) Gotowy model sera na pułapce, który włączymy, niszcząc trzymany w ręce.")]
    [SerializeField] private GameObject cheeseVisualOnTrap;

    [Header("Teksty interakcji")]
    [SerializeField] private string promptNeedItem = "Pułapka na myszy (wymaga sera z lodówki)";
    [SerializeField] private string promptPlaceItem = "Połóż ser na pułapce";
    [SerializeField] private string promptArmed = "Uzbrojona pułapka na myszy";

    [Header("Audio")]
    [SerializeField] private string soundArmTrap = "mousetrap_arm";

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    private bool _isArmed = false;

    public bool IsArmed => _isArmed;

    public bool CanInteract
    {
        get
        {
            if (_isArmed) return false;
            return IsPlayerHoldingRequiredItem();
        }
    }

    public string InteractionName
    {
        get
        {
            if (_isArmed)
                return promptArmed;

            if (IsPlayerHoldingRequiredItem())
                return promptPlaceItem;

            return promptNeedItem;
        }
    }

    private void Awake()
    {
        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }

        if (cheeseVisualOnTrap != null)
        {
            cheeseVisualOnTrap.SetActive(false);
        }
    }

    public void Interact()
    {
        if (_isArmed) return;

        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }

        if (!IsPlayerHoldingRequiredItem())
            return;

        ArmTrap();
    }

    private void ArmTrap()
    {
        _isArmed = true;

        if (playerHands != null && playerHands.HasItem)
        {
            if (cheeseVisualOnTrap != null)
            {
                // Opcja 1: Włączamy ładnie dopasowany model na pułapce, a trzymany niszczymy
                cheeseVisualOnTrap.SetActive(true);
                playerHands.DestroyHeldItem();
            }
            else
            {
                // Opcja 2: Przypinamy dokładnie ten obiekt, który gracz trzymał w rękach
                GameObject itemObj = playerHands.ReleaseHeldItem();
                if (itemObj != null)
                {
                    Transform targetParent = baitSnapPoint != null ? baitSnapPoint : transform;
                    itemObj.transform.SetParent(targetParent);
                    itemObj.transform.localPosition = Vector3.zero;
                    itemObj.transform.localRotation = Quaternion.identity;

                    // Wyłączamy fizykę i możliwość ponownego podniesienia
                    if (itemObj.TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    }

                    if (itemObj.TryGetComponent<PickupItem>(out var pickup))
                    {
                        pickup.enabled = false;
                    }
                }
            }
        }

        // Dźwięk uzbrojenia pułapki
        if (!string.IsNullOrEmpty(soundArmTrap) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundArmTrap);
        }

        // Zaliczenie zadania w PreparationStateManager
        if (!string.IsNullOrEmpty(taskId) && PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState(taskId, true);
        }

        Debug.Log($"[MouseTrap] Pułapka została uzbrojona serem! Zadanie '{taskId}' zaliczone.");
    }

    private bool IsPlayerHoldingRequiredItem()
    {
        if (playerHands == null || !playerHands.HasItem)
            return false;

        GameObject held = playerHands.HeldItem;
        if (held == null)
            return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            return string.Equals(pickup.ItemId, requiredItemId, System.StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
