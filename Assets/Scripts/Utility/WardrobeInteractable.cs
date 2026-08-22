using DG.Tweening;
using UnityEngine;

/// <summary>
/// Szafa z wdziankiem fryzjera. Po interakcji gracz "ubiera się",
/// co ustawia task w PreparationStateManager i odblokowuje firstDoors.
/// Opcjonalnie wykonuje fade ekranu (krótkie zaciemnienie = "przebieranie się").
/// </summary>
public class WardrobeInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionName = "Wardrobe";

    [Header("Task")]
    [Tooltip("ID taska ustawianego po ubraniu się.")]
    [SerializeField] private string taskId = "dressed_up";

    [Tooltip("Tekst wyświetlany jako wewnętrzna myśl po ubraniu się.")]
    [SerializeField] private string dressedMessage = "Time to get to work.";

    [Header("Visual Feedback")]
    [Tooltip("Opcjonalny ScreenFader do krótkiego zaciemnienia przy przebieraniu.")]
    [SerializeField] private ScreenFader screenFader;

    [Tooltip("Czas zaciemnienia (sek). Ustaw 0 żeby pominąć.")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Tooltip("Czas czekania na czarnym ekranie.")]
    [SerializeField] private float blackScreenDuration = 0.8f;

    [Header("Audio")]
    [SerializeField] private string dressSoundName = "";

    [Header("Inner Dialogue")]
    [Tooltip("Opcjonalna referencja do InnerDialogueUI — po ubraniu wyświetli dressedMessage.")]
    [SerializeField] private InnerDialogueUI innerDialogueUI;

    private bool _isDressed = false;

    public string InteractionName => _isDressed ? "" : interactionName;

    private void Awake()
    {
        if (innerDialogueUI == null)
        {
            innerDialogueUI = FindAnyObjectByType<InnerDialogueUI>();
        }
    }

    public void Interact()
    {
        if (_isDressed)
            return;

        _isDressed = true;

        // Ustaw task w PreparationStateManager
        if (PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.CompleteTask(taskId);
        }

        // Audio
        if (!string.IsNullOrEmpty(dressSoundName) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(dressSoundName);
        }

        // Visual feedback: krótki fade lub od razu wewnętrzny dialog
        if (screenFader != null && fadeDuration > 0f)
        {
            DressWithFade();
        }
        else
        {
            ShowDressedDialogue();
        }

        Debug.Log($"[WardrobeInteractable] Gracz ubrał się! Task '{taskId}' completed.");
    }

    private void DressWithFade()
    {
        // Szybkie zaciemnienie + pojawianie się = efekt "przebierania się"
        Sequence dressSequence = DOTween.Sequence();

        // Nie mamy bezpośredniego dostępu do ScreenFader panelu,
        // więc używamy prostego CanvasGroup fade jeśli dostępny
        // Albo po prostu pokazujemy dialog po małym opóźnieniu
        DOVirtual.DelayedCall(fadeDuration + blackScreenDuration, () =>
        {
            ShowDressedDialogue();
        }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void ShowDressedDialogue()
    {
        if (innerDialogueUI != null && !string.IsNullOrEmpty(dressedMessage))
        {
            innerDialogueUI.ShowMessage(dressedMessage);
        }
    }
}
