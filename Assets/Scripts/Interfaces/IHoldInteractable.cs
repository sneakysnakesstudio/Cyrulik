public interface IHoldInteractable : IInteractable
{
    /// <summary>
    /// Czas w sekundach wymagany do przytrzymania przycisku interakcji (np. 0.4s).
    /// </summary>
    float HoldDuration { get; }

    /// <summary>
    /// Czy interakcja wymaga przytrzymania (zamiana kółeczka w kwadrat)?
    /// </summary>
    bool RequiresHold { get; }
}
