using UnityEngine;

public class PreparationTaskTrigger : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionName = "Use";

    [Header("Task")]
    [Tooltip("Identyfikator zadania w PreparationStateManager (np. stove_lit, water_heated).")]
    [SerializeField] private string taskId;

    [Tooltip("Czy interakcja ma oznaczyć zadanie jako zaliczone (true) czy cofnąć (false).")]
    [SerializeField] private bool completeOnInteract = true;

    [Tooltip("Czy interakcja może nastąpić tylko raz.")]
    [SerializeField] private bool oneShot = true;

    [Header("Audio")]
    [Tooltip("Opcjonalna nazwa grupy dźwiękowej z AudioDatabase.")]
    [SerializeField] private string soundGroup;

    private bool _hasTriggered;

    public string InteractionName => interactionName;

    public void Interact()
    {
        if (oneShot && _hasTriggered)
            return;

        _hasTriggered = true;

        if (!string.IsNullOrWhiteSpace(soundGroup))
        {
            AudioManager.Instance?.Play(soundGroup);
        }

        if (!string.IsNullOrWhiteSpace(taskId) && PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState(taskId, completeOnInteract);
        }
    }
}
