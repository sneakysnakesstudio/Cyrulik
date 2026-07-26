using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupItem : MonoBehaviour, IInteractable
{
    private PlayerHands _playerHands;

    private void Awake()
    {
        _playerHands = FindFirstObjectByType<PlayerHands>();
    }

    public void Interact()
    {
        if (_playerHands == null)
            return;

        _playerHands.TryHold(gameObject);
    }
}