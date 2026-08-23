using System;
using UnityEngine;

/// <summary>
/// Interaktywny zlew / kran (Sink).
/// Pozwala napełnić trzymany garnek wodą (zamienia pusty garnek w garnek z wodą).
/// Krok 1 w queście przygotowania gorącego ręcznika.
/// </summary>
public class SinkInteractable : MonoBehaviour, IConditionalInteractable
{
    [Header("Item IDs")]
    [Tooltip("Akceptowane ID pustego garnka.")]
    [SerializeField] private string[] emptyPotItemIds = new string[] { "pot", "pot_empty" };

    [Tooltip("ID garnka po napełnieniu wodą.")]
    [SerializeField] private string filledPotItemId = "pot_water";

    [Header("Interaction Prompts (English)")]
    [SerializeField] private string promptFill = "Fill pot with water";
    [SerializeField] private string promptAlreadyFull = "Pot is already full of water";
    [SerializeField] private string promptNeedPot = "Sink (Requires a pot)";

    [Header("Audio")]
    [SerializeField] private string soundPourWater = "water_pour";
    [SerializeField] private AudioClip customWaterClip;
    [SerializeField] private AudioSource audioSource;

    [Header("Wizualia (Opcjonalnie)")]
    [Tooltip("Opcjonalny strumień wody z kranu włączany na chwilę przy nalewaniu.")]
    [SerializeField] private GameObject waterStreamVisual;
    [SerializeField] private float waterStreamDuration = 1.0f;

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    public bool CanInteract
    {
        get
        {
            return IsHoldingEmptyPot();
        }
    }

    public string InteractionName
    {
        get
        {
            if (IsHoldingEmptyPot())
                return promptFill;

            if (IsHoldingFilledPot())
                return promptAlreadyFull;

            return promptNeedPot;
        }
    }

    private void Awake()
    {
        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }

        if (waterStreamVisual != null)
        {
            waterStreamVisual.SetActive(false);
        }
    }

    public void Interact()
    {
        if (!IsHoldingEmptyPot())
            return;

        FillHeldPotWithWater();
    }

    private void FillHeldPotWithWater()
    {
        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands == null || !playerHands.HasItem)
            return;

        GameObject held = playerHands.HeldItem;
        if (held == null) return;

        // 1. Zaktualizuj komponent PotItem jeśli istnieje
        if (held.TryGetComponent<PotItem>(out var potItem))
        {
            potItem.SetWater(true);
        }
        else if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            pickup.ItemId = filledPotItemId;
            pickup.InteractionName = "Pot with water";
        }

        // 2. Dźwięk nalewania wody
        PlayWaterSound();

        // 3. Efekt strumienia wody z kranu
        if (waterStreamVisual != null)
        {
            waterStreamVisual.SetActive(true);
            CancelInvoke(nameof(StopWaterStream));
            Invoke(nameof(StopWaterStream), waterStreamDuration);
        }

        Debug.Log($"[Sink] Garnek został napełniony wodą! ItemId zmieniony na '{filledPotItemId}'.");
    }

    private void StopWaterStream()
    {
        if (waterStreamVisual != null)
        {
            waterStreamVisual.SetActive(false);
        }
    }

    private void PlayWaterSound()
    {
        if (customWaterClip != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(customWaterClip);
            else
                AudioSource.PlayClipAtPoint(customWaterClip, transform.position);
            return;
        }

        if (!string.IsNullOrEmpty(soundPourWater) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundPourWater);
        }
    }

    private bool IsHoldingEmptyPot()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            if (string.IsNullOrEmpty(pickup.ItemId)) return false;

            foreach (string emptyId in emptyPotItemIds)
            {
                if (string.Equals(pickup.ItemId, emptyId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private bool IsHoldingFilledPot()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            return string.Equals(pickup.ItemId, filledPotItemId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
