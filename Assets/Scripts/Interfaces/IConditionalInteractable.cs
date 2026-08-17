public interface IConditionalInteractable : IInteractable
{
    bool CanInteract { get; }
}