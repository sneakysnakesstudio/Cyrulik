using UnityEngine;

/// <summary>
/// Komponent umieszczany na obiekcie/prefabie garnka.
/// Zarządza stanem napełnienia wodą (włącza/wyłącza wizualia wody i zmienia ItemId w PickupItem).
/// </summary>
public class PotItem : MonoBehaviour
{
    [Header("Item IDs")]
    [Tooltip("ID pustego garnka.")]
    [SerializeField] private string emptyPotItemId = "pot_empty";

    [Tooltip("ID garnka napełnionego wodą.")]
    [SerializeField] private string filledPotItemId = "pot_water";

    [Header("Wizualia wody")]
    [Tooltip("Obiekt tafli/wody wewnątrz garnka.")]
    [SerializeField] private GameObject waterVisual;

    [Header("Stan początkowy")]
    [SerializeField] private bool startsWithWater = false;

    private PickupItem _pickupItem;
    private bool _hasWater;

    public bool HasWater => _hasWater;
    public string EmptyPotItemId => emptyPotItemId;
    public string FilledPotItemId => filledPotItemId;

    private void Awake()
    {
        _pickupItem = GetComponent<PickupItem>();
        SetWater(startsWithWater);
    }

    /// <summary>
    /// Ustawia stan napełnienia garnka wodą.
    /// </summary>
    public void SetWater(bool hasWater)
    {
        _hasWater = hasWater;

        if (waterVisual != null)
        {
            waterVisual.SetActive(hasWater);
        }

        if (_pickupItem != null)
        {
            _pickupItem.ItemId = hasWater ? filledPotItemId : emptyPotItemId;
            _pickupItem.InteractionName = hasWater ? "Pick up pot with water" : "Pick up pot";
        }
    }
}
