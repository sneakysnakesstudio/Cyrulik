using System;
using UnityEngine;

/// <summary>
/// Interaktywny wiszący pas skórzany (Razor Strop).
/// Wymaga trzymania brzytwy w dłoniach, aby rozpocząć minigrę ostrzenia.
/// </summary>
public class RazorStropInteractable : MonoBehaviour, IConditionalInteractable
{
    [Header("Interaction Prompts")]
    [SerializeField] private string promptSharpen = "Sharpen Razor";
    [SerializeField] private string promptNeedRazor = "Strop (Requires razor from desk)";

    [Header("Requirements")]
    [Tooltip("Wymagane ID przedmiotów brzytwy z PickupItem.")]
    [SerializeField] private string[] acceptedRazorIds = new string[]
    {
        "razor", "razor_blade", "dull_razor", "blade", "sharp_razor", "razor_sharpened"
    };

    [Header("Referencje")]
    [SerializeField] private RazorMinigame razorMinigame;
    [SerializeField] private PlayerHands playerHands;

    [Header("Audio")]
    [SerializeField] private string interactSound = "card_flip";

    public bool CanInteract
    {
        get
        {
            if (razorMinigame != null && razorMinigame.IsActive)
                return false;

            return IsPlayerHoldingRazor();
        }
    }

    public string InteractionName
    {
        get
        {
            return IsPlayerHoldingRazor() ? promptSharpen : promptNeedRazor;
        }
    }

    public string BlockedMessage => "I need to hold the razor in my hands to sharpen it on the strop.";

    private void Awake()
    {
        FindRefs();
    }

    private void FindRefs()
    {
        if (razorMinigame == null)
            razorMinigame = FindAnyObjectByType<RazorMinigame>(FindObjectsInactive.Include);

        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();
    }

    public void Interact()
    {
        FindRefs();

        if (razorMinigame == null)
        {
            Debug.LogError("[RazorStropInteractable] Brak RazorMinigame w scenie!");
            return;
        }

        if (razorMinigame.IsActive)
            return;

        if (!IsPlayerHoldingRazor())
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.ShowThought("I need to pick up the razor from the desk first.");
            }
            return;
        }

        if (!string.IsNullOrEmpty(interactSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(interactSound);
        }

        Debug.Log("[RazorStropInteractable] Gracz trzyma brzytwę -> Uruchamiam minigrę ostrzenia!");
        razorMinigame.StartMinigame();
    }

    private bool IsPlayerHoldingRazor()
    {
        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands == null || !playerHands.HasItem)
            return false;

        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            string id = pickup.ItemId != null ? pickup.ItemId.Trim().ToLowerInvariant() : "";
            foreach (var acc in acceptedRazorIds)
            {
                if (!string.IsNullOrEmpty(acc) && id == acc.Trim().ToLowerInvariant())
                    return true;
            }
        }

        string n = held.name.ToLowerInvariant();
        return n.Contains("razor") || n.Contains("brzytwa") || n.Contains("blade");
    }
}