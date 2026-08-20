using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Item Info")]
    [Tooltip("ID przedmiotu (np. 'cheese', 'towel', 'wood').")]
    [SerializeField] private string itemId = "cheese";

    [Header("Interaction")]
    [SerializeField] private string interactionName = "Podnieś ser";

    [Header("References")]
    [SerializeField] private PlayerHands _playerHands;

    public string ItemId => itemId;
    public string InteractionName => interactionName;

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