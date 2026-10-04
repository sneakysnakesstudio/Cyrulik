public interface IConditionalInteractable : IInteractable
{
    bool CanInteract { get; }

    /// <summary>
    /// Wewnętrzny dialog gracza wyświetlany przy zablokowanej interakcji.
    /// Zwróć null lub pusty string, żeby nie pokazywać tekstu.
    /// </summary>
    string BlockedMessage => null;
}