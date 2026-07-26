using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupItem : MonoBehaviour, IInteractable
{
    private PlayerHands _playerHands;

    private void Awake()
    {
        Debug.Log($"[PickupItem] Awake na obiekcie: {gameObject.name}", this);

        _playerHands = FindFirstObjectByType<PlayerHands>();

        if (_playerHands == null)
        {
            Debug.LogError(
                $"[PickupItem] {gameObject.name}: NIE ZNALEZIONO PlayerHands na scenie!",
                this
            );

            return;
        }

        Debug.Log(
            $"[PickupItem] {gameObject.name}: znaleziono PlayerHands na obiekcie " +
            $"{_playerHands.gameObject.name}.",
            this
        );
    }

    public void Interact()
    {
        Debug.Log(
            $"[PickupItem] Wywołano Interact() na: {gameObject.name}.",
            this
        );

        if (_playerHands == null)
        {
            Debug.LogError(
                $"[PickupItem] {gameObject.name}: nie można podnieść, bo PlayerHands jest null!",
                this
            );

            return;
        }

        bool wasPickedUp = _playerHands.TryHold(gameObject);

        if (wasPickedUp)
        {
            Debug.Log(
                $"[PickupItem] SUKCES: podniesiono {gameObject.name}.",
                this
            );
        }
        else
        {
            Debug.LogWarning(
                $"[PickupItem] NIE UDAŁO SIĘ podnieść {gameObject.name}.",
                this
            );
        }
    }
}