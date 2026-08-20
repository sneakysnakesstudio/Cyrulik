using UnityEngine;

/// <summary>
/// Pudełko na żyletki działające jako dyspenser / zasobnik.
/// Pudełko stoi w miejscu, a kliknięcie na nie wyciąga pojedynczą żyletkę
/// i wkłada ją bezpośrednio do rąk gracza (PlayerHands).
/// </summary>
public class RazorBladeDispenser : MonoBehaviour, IConditionalInteractable
{
    [Header("Interaction")]
    [SerializeField] private string promptTakeBlade = "Take razor blade";
    [SerializeField] private string promptHandsFull = "Hands are full";
    [SerializeField] private string promptEmpty = "Empty blade box";

    [Header("Blade Settings")]
    [Tooltip("Prefab pojedynczej żyletki. Jeśli pozostawisz puste, skrypt sam wygeneruje mały obiekt żyletki.")]
    [SerializeField] private GameObject bladePrefab;

    [Tooltip("ID przedmiotu (musi być zgodne z requiredBladeItemId w RazorMinigame, domyślnie 'razor_blade').")]
    [SerializeField] private string bladeItemId = "razor_blade";

    [Tooltip("Nazwa wyświetlana przy podnoszeniu samej żyletki z ziemi.")]
    [SerializeField] private string bladeItemName = "Razor blade";

    [Header("Ilość")]
    [Tooltip("Czy w pudełku jest nieskończona ilość żyletek?")]
    [SerializeField] private bool infiniteBlades = true;

    [Tooltip("Liczba dostępnych żyletek (jeśli infiniteBlades jest wyłączone).")]
    [SerializeField] private int bladeCount = 10;

    [Header("Audio")]
    [SerializeField] private string soundTakeBlade = "blade_pickup";

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    public bool CanInteract
    {
        get
        {
            if (playerHands == null)
                playerHands = FindAnyObjectByType<PlayerHands>();

            if (playerHands != null && playerHands.HasItem)
                return false;

            if (!infiniteBlades && bladeCount <= 0)
                return false;

            return true;
        }
    }

    public string InteractionName
    {
        get
        {
            if (playerHands == null)
                playerHands = FindAnyObjectByType<PlayerHands>();

            if (!infiniteBlades && bladeCount <= 0)
                return promptEmpty;

            if (playerHands != null && playerHands.HasItem)
                return promptHandsFull;

            return promptTakeBlade;
        }
    }

    private void Awake()
    {
        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }
    }

    public void Interact()
    {
        if (!CanInteract)
            return;

        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands == null)
            return;

        // Stwórz instancję pojedynczej żyletki
        GameObject bladeInstance = CreateBladeInstance();

        if (bladeInstance != null)
        {
            // Włóż żyletkę do rąk gracza
            bool success = playerHands.TryHold(bladeInstance);

            if (success)
            {
                if (!infiniteBlades)
                    bladeCount--;

                if (!string.IsNullOrEmpty(soundTakeBlade) && AudioManager.Instance != null)
                {
                    AudioManager.Instance.Play(soundTakeBlade);
                }

                Debug.Log($"[RazorBladeDispenser] Wyciągnięto żyletkę z pudełka. Zostało: {(infiniteBlades ? "Nieskończoność" : bladeCount.ToString())}");
            }
            else
            {
                Destroy(bladeInstance);
            }
        }
    }

    private GameObject CreateBladeInstance()
    {
        if (bladePrefab != null)
        {
            GameObject instance = Instantiate(bladePrefab);
            var pickupComp = instance.GetComponentInChildren<PickupItem>();
            if (pickupComp == null)
            {
                pickupComp = instance.AddComponent<PickupItem>();
            }
            pickupComp.ItemId = bladeItemId;
            pickupComp.InteractionName = bladeItemName;
            return instance;
        }

        // Fallback: Jeśli użytkownik nie przypisał dedykowanego modelu żyletki,
        // tworzymy lekki obiekt z PickupItem i małym colliderem
        GameObject fallbackBlade = new GameObject("RazorBlade");
        
        // Dodajemy prosty wizualny płaski kształt
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(fallbackBlade.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(0.05f, 0.002f, 0.025f);
        
        // Usuwamy domyślny collider z wizualizacji (będzie na głównym obiekcie)
        if (visual.TryGetComponent<Collider>(out var visualCol))
        {
            Destroy(visualCol);
        }

        // Konfigurujemy fizykę i interakcję
        BoxCollider boxCol = fallbackBlade.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(0.05f, 0.005f, 0.025f);

        Rigidbody rb = fallbackBlade.AddComponent<Rigidbody>();
        rb.mass = 0.05f;

        PickupItem pickup = fallbackBlade.AddComponent<PickupItem>();
        pickup.ItemId = bladeItemId;
        pickup.InteractionName = bladeItemName;

        fallbackBlade.layer = LayerMask.NameToLayer("Interactable") != -1 
            ? LayerMask.NameToLayer("Interactable") 
            : 0;

        return fallbackBlade;
    }
}
