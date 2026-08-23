using UnityEngine;

/// <summary>
/// Obsługuje pułapkę na myszy:
/// 1. Wymaga przyniesienia sera z lodówki i położenia go na pułapce.
/// 2. Po uzbrojeniu instancjonuje przypisany w Inspectorze prefab sera (Cheese Prefab).
/// 3. Po wywołaniu CatchMouse() (np. przez MouseQuestManager) ser znika, a pojawia się złapana mysz.
/// 4. Gracz może podnieść złapaną mysz do ręki i zanieść ją do kosza.
/// </summary>
public class MouseTrap : MonoBehaviour, IConditionalInteractable
{
    [Header("Zadanie i Przedmiot")]
    [Tooltip("ID zadania uzbrojenia w PreparationStateManager (np. 'mousetrap_baited').")]
    [SerializeField] private string taskId = "mousetrap_baited";

    [Tooltip("Wymagany ID przedmiotu z PickupItem (domyślnie 'cheese').")]
    [SerializeField] private string requiredItemId = "cheese";

    [Tooltip("ItemId nadawany podnoszonej martwej myszy (domyślnie 'dead_mouse').")]
    [SerializeField] private string caughtMouseItemId = "dead_mouse";

    [Header("Prefab Sera (Przynęta)")]
    [Tooltip("Przeciągnij tutaj prefab sera z Project/Assets. Zostanie zinstancjonowany na pułapce po położeniu sera przez gracza.")]
    [SerializeField] private GameObject cheesePrefab;

    [Tooltip("Punkt (Transform) na pułapce, w którym ma pojawić się ser. Jeśli pusty, użyje środka pułapki.")]
    [SerializeField] private Transform baitSnapPoint;

    [Tooltip("Lokalne przesunięcie pozycji sera na pułapce.")]
    [SerializeField] private Vector3 baitLocalOffset = Vector3.zero;

    [Tooltip("Lokalna rotacja sera na pułapce.")]
    [SerializeField] private Vector3 baitLocalRotation = Vector3.zero;

    [Tooltip("Lokalna skala sera na pułapce (jeśli 0, użyje skali 1).")]
    [SerializeField] private Vector3 baitLocalScale = Vector3.one;

    [Header("Model Złapanej Myszy")]
    [Tooltip("Wizualny model zatrzaśniętej myszy na pułapce po wywołaniu CatchMouse().")]
    [SerializeField] private GameObject caughtMouseVisualOnTrap;

    [Tooltip("Opcjonalny prefab martwej myszy, który trafia do rąk gracza po podniesieniu. Jeśli brak, skrypt utworzy obiekt z PickupItem.")]
    [SerializeField] private GameObject deadMousePrefab;

    [Header("Alternatywny gotowy model sera (Opcjonalnie)")]
    [Tooltip("Jeśli wolisz gotowy obiekt sera w hierarchii pułapki zamiast prefabu.")]
    [SerializeField] private GameObject cheeseVisualOnTrap;

    [Header("Interaction Prompts")]
    [SerializeField] private string promptNeedItem = "Mouse trap (Requires cheese from fridge)";
    [SerializeField] private string promptPlaceItem = "Place cheese on trap";
    [SerializeField] private string promptArmed = "Armed mouse trap";
    [SerializeField] private string promptTakeMouse = "Pick up caught mouse";
    [SerializeField] private string promptEmptySnapped = "Empty mouse trap";

    [Header("Audio")]
    [SerializeField] private string soundArmTrap = "mousetrap_arm";
    [SerializeField] private string soundSnapTrap = "mousetrap_snap";
    [SerializeField] private string soundPickupMouse = "cloth_pickup";

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    private bool _isArmed = false;
    private bool _hasCaughtMouse = false;
    private bool _mousePickedUp = false;
    private GameObject _spawnedCheeseInstance;

    public bool IsArmed => _isArmed;
    public bool HasCaughtMouse => _hasCaughtMouse;
    public bool MousePickedUp => _mousePickedUp;

    public bool CanInteract
    {
        get
        {
            // 1. Złapana mysz gotowa do podniesienia
            if (_hasCaughtMouse)
            {
                if (playerHands == null) playerHands = FindAnyObjectByType<PlayerHands>();
                return playerHands != null && !playerHands.HasItem;
            }

            // 2. Pułapka już zbrojona lub pusta po zebraniu myszy
            if (_isArmed || _mousePickedUp)
                return false;

            // 3. Sprawdź, czy gracz trzyma ser
            return IsPlayerHoldingRequiredItem();
        }
    }

    public string InteractionName
    {
        get
        {
            if (_hasCaughtMouse)
                return promptTakeMouse;

            if (_mousePickedUp)
                return promptEmptySnapped;

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

        if (caughtMouseVisualOnTrap != null)
        {
            caughtMouseVisualOnTrap.SetActive(false);
        }
    }

    public void Interact()
    {
        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }

        // Krok A: Zbieranie złapanej myszy
        if (_hasCaughtMouse)
        {
            TakeCaughtMouse();
            return;
        }

        // Krok B: Uzbrajanie serem
        if (!_isArmed && !_mousePickedUp && IsPlayerHoldingRequiredItem())
        {
            ArmTrap();
        }
    }

    /// <summary>
    /// Kładzie ser na pułapce i ją uzbraja.
    /// </summary>
    public void ArmTrap()
    {
        if (_isArmed) return;

        _isArmed = true;

        Transform targetParent = baitSnapPoint != null ? baitSnapPoint : transform;

        // 1. Niszczymy ser z rąk gracza
        if (playerHands != null && playerHands.HasItem)
        {
            playerHands.DestroyHeldItem();
        }

        // 2. Spawnowanie przypisanego prefabu sera na pułapce
        if (cheesePrefab != null)
        {
            _spawnedCheeseInstance = Instantiate(cheesePrefab, targetParent);
            _spawnedCheeseInstance.transform.localPosition = baitLocalOffset;
            _spawnedCheeseInstance.transform.localRotation = Quaternion.Euler(baitLocalRotation);
            
            Vector3 scale = baitLocalScale == Vector3.zero ? Vector3.one : baitLocalScale;
            _spawnedCheeseInstance.transform.localScale = scale;

            // Wyłączamy interakcje i fizykę na serze na pułapce
            if (_spawnedCheeseInstance.TryGetComponent<PickupItem>(out var pickup))
            {
                pickup.enabled = false;
            }

            if (_spawnedCheeseInstance.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            foreach (var col in _spawnedCheeseInstance.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }
        else if (cheeseVisualOnTrap != null)
        {
            // Opcja zapasowa: jeśli podpięto istniejący obiekt w scenie
            cheeseVisualOnTrap.SetActive(true);
        }

        // Dźwięk uzbrojenia
        if (!string.IsNullOrEmpty(soundArmTrap) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundArmTrap);
        }

        // Zaliczenie zadania w PreparationStateManager
        if (!string.IsNullOrEmpty(taskId) && PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState(taskId, true);
        }

        Debug.Log($"[MouseTrap] Pułapka '{gameObject.name}' została uzbrojona serem! Zadanie '{taskId}' zaliczone.");
    }

    /// <summary>
    /// Wywoływane, gdy mysz wejdzie w pułapkę. Pułapka zatrzaskuje się i pojawia się martwa mysz.
    /// </summary>
    public void CatchMouse()
    {
        if (!_isArmed) return;

        _isArmed = false;
        _hasCaughtMouse = true;

        // Usuwamy / wyłączamy ser
        if (_spawnedCheeseInstance != null)
        {
            Destroy(_spawnedCheeseInstance);
            _spawnedCheeseInstance = null;
        }

        if (cheeseVisualOnTrap != null)
        {
            cheeseVisualOnTrap.SetActive(false);
        }

        // Włączamy model złapanej myszy
        if (caughtMouseVisualOnTrap != null)
        {
            caughtMouseVisualOnTrap.SetActive(true);
        }

        // Dźwięk zatrzaśnięcia
        if (!string.IsNullOrEmpty(soundSnapTrap) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundSnapTrap);
        }

        Debug.Log($"[MouseTrap] Pułapka '{gameObject.name}' ZATRZAŚNIĘTA! Mysz złapana.");
    }

    /// <summary>
    /// Gracz zabiera złapaną mysz z pułapki do ręki.
    /// </summary>
    private void TakeCaughtMouse()
    {
        if (!_hasCaughtMouse) return;

        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands == null || playerHands.HasItem)
            return;

        // Ukryj wizualia na pułapce
        if (caughtMouseVisualOnTrap != null)
        {
            caughtMouseVisualOnTrap.SetActive(false);
        }

        _hasCaughtMouse = false;
        _mousePickedUp = true;

        // Stwórz instancję martwej myszy do rąk
        GameObject mouseInstance = null;
        if (deadMousePrefab != null)
        {
            mouseInstance = Instantiate(deadMousePrefab);
        }
        else
        {
            // Fallback - generujemy prosty obiekt PickupItem dla dead_mouse
            mouseInstance = new GameObject("DeadMouse_Item");
            var pickup = mouseInstance.AddComponent<PickupItem>();
            pickup.ItemId = caughtMouseItemId;
            pickup.InteractionName = "Dead Mouse";
        }

        if (mouseInstance != null)
        {
            var pickup = mouseInstance.GetComponent<PickupItem>();
            if (pickup != null)
            {
                pickup.ItemId = caughtMouseItemId;
            }

            playerHands.TryHold(mouseInstance);
        }

        if (!string.IsNullOrEmpty(soundPickupMouse) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundPickupMouse);
        }

        Debug.Log($"[MouseTrap] Gracz podniósł złapaną mysz ({caughtMouseItemId}). Należy wyrzucić ją do kosza.");
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
