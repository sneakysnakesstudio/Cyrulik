using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupItem : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerHands _playerHands;

    public void Interact()
    {
        if (_playerHands == null)
            return;

        _playerHands.TryHold(gameObject);
    }
}