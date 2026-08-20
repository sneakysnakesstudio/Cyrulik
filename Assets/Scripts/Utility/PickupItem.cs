using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Item Info")]
    [Tooltip("ID przedmiotu (np. 'cheese', 'towel', 'wood', 'razor_blade').")]
    [SerializeField] private string itemId = "cheese";

    [Header("Interaction")]
    [SerializeField] private string interactionName = "Pick up";

    [Header("In-Hand Transform (Optional)")]
    [Tooltip("Lokalna pozycja w ręku.")]
    [SerializeField] private Vector3 inHandPosition = Vector3.zero;

    [Tooltip("Lokalna rotacja w ręku.")]
    [SerializeField] private Vector3 inHandRotation = Vector3.zero;

    [Tooltip("Lokalna skala w ręku.")]
    [SerializeField] private Vector3 inHandScale = Vector3.one;

    [Header("References")]
    [SerializeField] private PlayerHands _playerHands;

    public string ItemId { get => itemId; set => itemId = value; }
    public string InteractionName { get => interactionName; set => interactionName = value; }

    public Vector3 InHandPosition => inHandPosition;
    public Vector3 InHandRotation => inHandRotation;
    public Vector3 InHandScale => inHandScale == Vector3.zero ? Vector3.one : inHandScale;

    private void Awake()
    {
        if (_playerHands == null)
        {
            _playerHands = FindAnyObjectByType<PlayerHands>();
        }
    }

    public void Interact()
    {
        if (_playerHands == null)
        {
            _playerHands = FindAnyObjectByType<PlayerHands>();
        }

        if (_playerHands == null)
            return;

        _playerHands.TryHold(gameObject);
    }
}