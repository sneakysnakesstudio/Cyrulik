using DG.Tweening;
using UnityEngine;

/// <summary>
/// Szafa z wdziankiem fryzjera. Po interakcji gracz "ubiera się",
/// co ustawia task w PreparationStateManager i odblokowuje firstDoors.
/// Obsługuje opcjonalny wymóg przyniesienia klucza w rękach (requireKey).
/// </summary>
public class WardrobeInteractable : MonoBehaviour, IConditionalInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionName = "Wardrobe";

    [Header("Key Requirement (Wymóg Klucza)")]
    [Tooltip("Czy szafa wymaga przyniesienia klucza w rękach przed ubraniem się?")]
    [SerializeField] private bool requireKey = false;

    [Tooltip("Wymagany ItemId klucza w PickupItem (domyślnie 'wardrobe_key' lub 'key').")]
    [SerializeField] private string requiredKeyItemId = "wardrobe_key";

    [Tooltip("Czy klucz ma zniknąć z rąk gracza po otwarciu szafy?")]
    [SerializeField] private bool consumeKeyOnUnlock = true;

    [Tooltip("Wewnętrzna myśl gracza, gdy próbuje otworzyć szafę bez klucza.")]
    [SerializeField] private string keyMissingMessage = "It's locked. I need to find the wardrobe key...";

    [Tooltip("Dźwięk odblokowania zamka kluczem (np. 'drawer_open').")]
    [SerializeField] private string unlockSoundName = "drawer_open";

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
    [SerializeField] private string dressSoundName = "dressup_sound";

    [Header("Inner Dialogue")]
    [Tooltip("Opcjonalna referencja do InnerDialogueUI — po ubraniu wyświetli dressedMessage.")]
    [SerializeField] private InnerDialogueUI innerDialogueUI;

    private bool _isDressed = false;
    private bool _isUnlocked = false;

    public string InteractionName
    {
        get
        {
            if (_isDressed) return string.Empty;
            if (requireKey && !_isUnlocked)
            {
                return IsPlayerHoldingRequiredKey() ? "Unlock wardrobe with key" : "Wardrobe (Locked)";
            }
            return interactionName;
        }
    }

    public bool CanInteract
    {
        get
        {
            if (_isDressed) return false;
            if (requireKey && !_isUnlocked)
            {
                return IsPlayerHoldingRequiredKey();
            }
            return true;
        }
    }

    public string BlockedMessage
    {
        get
        {
            if (requireKey && !_isUnlocked && !IsPlayerHoldingRequiredKey())
            {
                if (DialogueManager.Instance != null && !string.IsNullOrEmpty(keyMissingMessage))
                {
                    DialogueManager.Instance.ShowThought(keyMissingMessage);
                }
                else if (innerDialogueUI != null && !string.IsNullOrEmpty(keyMissingMessage))
                {
                    innerDialogueUI.ShowMessage(keyMissingMessage);
                }
                return keyMissingMessage;
            }
            return null;
        }
    }

    public void UnlockDirect()
    {
        _isUnlocked = true;
    }

    private void Awake()
    {
        _isUnlocked = !requireKey;

        if (innerDialogueUI == null)
        {
            innerDialogueUI = FindAnyObjectByType<InnerDialogueUI>();
        }
    }

    public void Interact()
    {
        if (_isDressed)
            return;

        // Jeśli wymaga klucza i nie odblokowano:
        if (requireKey && !_isUnlocked)
        {
            if (!IsPlayerHoldingRequiredKey())
            {
                if (!string.IsNullOrEmpty(keyMissingMessage))
                {
                    if (DialogueManager.Instance != null)
                        DialogueManager.Instance.ShowThought(keyMissingMessage);
                    else if (innerDialogueUI != null)
                        innerDialogueUI.ShowMessage(keyMissingMessage);
                }
                return;
            }

            _isUnlocked = true;
            Debug.Log("[WardrobeInteractable] Szafa odblokowana kluczem!");

            if (consumeKeyOnUnlock)
            {
                PlayerHands playerHands = FindAnyObjectByType<PlayerHands>();
                if (playerHands != null)
                {
                    playerHands.DestroyHeldItem();
                }
            }

            if (!string.IsNullOrEmpty(unlockSoundName) && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(unlockSoundName);
            }
        }

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

        // Visual feedback
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

    private bool IsPlayerHoldingRequiredKey()
    {
        PlayerHands playerHands = FindAnyObjectByType<PlayerHands>();
        if (playerHands == null || !playerHands.HasItem)
            return false;

        GameObject held = playerHands.HeldItem;
        if (held == null)
            return false;

        PickupItem pickup = held.GetComponentInChildren<PickupItem>();
        if (pickup == null)
            pickup = held.GetComponentInParent<PickupItem>();

        string heldId = pickup != null ? pickup.ItemId : held.name;
        if (!string.IsNullOrEmpty(heldId))
        {
            string idLower = heldId.Trim().ToLowerInvariant();
            string reqLower = string.IsNullOrEmpty(requiredKeyItemId) ? "key" : requiredKeyItemId.Trim().ToLowerInvariant();

            if (idLower == reqLower || idLower == "key" || idLower == "wardrobe_key" || idLower == "klucz" || idLower.Contains("key") || idLower.Contains("klucz"))
            {
                return true;
            }
        }

        string objLower = held.name.ToLowerInvariant();
        if (objLower.Contains("key") || objLower.Contains("klucz"))
        {
            return true;
        }

        return false;
    }

    private void DressWithFade()
    {
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
