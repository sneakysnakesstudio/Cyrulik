using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Miska fryzjerska (Bowl):
/// 1. Gracz podchodzi z garnkiem gorącej wody (pot_water) i wlewa wodę do miski.
/// 2. Gdy w misce jest gorąca woda, gracz podchodzi z ręcznikiem (towel / dirty_towel).
/// 3. Po interakcji ręcznik w rękach gracza natychmiast zamienia swoje ID na "clean_towel".
/// 4. Czysty gorący ręcznik można zanieść i podać Jurkowi (CustomerJurek).
/// </summary>
public class BowlInteractable : MonoBehaviour, IConditionalInteractable
{
    [Header("Item IDs")]
    [Tooltip("ID garnka z gorącą wodą.")]
    [SerializeField] private string hotWaterPotId = "pot_water";

    [Tooltip("ID czystego ręcznika nadawany po zamoczeniu w misce.")]
    [SerializeField] private string cleanTowelResultId = "clean_towel";

    [Header("Interaction Prompts")]
    [SerializeField] private string promptNeedWater = "Pour hot water into bowl";
    [SerializeField] private string promptDipTowel = "Dip towel in hot water (Get clean towel)";
    [SerializeField] private string promptBowlReady = "Bowl has hot water (Bring a towel)";
    [SerializeField] private string promptDone = "Bowl with hot water";

    [Header("Wizualia")]
    [Tooltip("Obiekt tafli wody w misce.")]
    [SerializeField] private GameObject waterInBowlVisual;

    [Header("Audio")]
    [SerializeField] private string soundWaterPour = "water_pour";
    [SerializeField] private string soundCloth = "cloth_pickup";

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onWaterAdded;
    [SerializeField] private UnityEvent onTowelCleaned;

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    // Stan miski
    private bool _hasWater = false;

    public bool HasWater => _hasWater;

    // ── IConditionalInteractable ──────────────────────────────────────────────

    public bool CanInteract
    {
        get
        {
            EnsureRefs();

            // 1. Brak wody -> gracz musi trzymać garnek z wodą
            if (!_hasWater)
            {
                return IsPlayerHoldingHotWater();
            }

            // 2. Jest woda -> gracz może zanurzyć ręcznik
            if (_hasWater)
            {
                return IsPlayerHoldingAnyTowel();
            }

            return false;
        }
    }

    public string InteractionName
    {
        get
        {
            if (!_hasWater)
            {
                return promptNeedWater;
            }

            if (IsPlayerHoldingAnyTowel())
            {
                return promptDipTowel;
            }

            return promptBowlReady;
        }
    }

    public string BlockedMessage => null;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureRefs();

        if (waterInBowlVisual != null)
        {
            waterInBowlVisual.SetActive(false);
        }
    }

    public void Interact()
    {
        EnsureRefs();

        // Krok 1: Wlewanie gorącej wody
        if (!_hasWater)
        {
            if (IsPlayerHoldingHotWater())
            {
                AddHotWater();
            }
            return;
        }

        // Krok 2: Zamoczenie ręcznika w misce i zamiana ID na clean_towel
        if (_hasWater && IsPlayerHoldingAnyTowel())
        {
            DipAndCleanTowel();
        }
    }

    // ── Prywatne ─────────────────────────────────────────────────────────────

    private void AddHotWater()
    {
        _hasWater = true;

        if (playerHands != null && playerHands.HasItem)
        {
            GameObject held = playerHands.HeldItem;
            if (held != null && held.TryGetComponent<PotItem>(out var pot))
            {
                pot.SetWater(false);
            }
            else
            {
                playerHands.DestroyHeldItem();
            }
        }

        if (waterInBowlVisual != null)
        {
            waterInBowlVisual.SetActive(true);
        }

        PlaySound(soundWaterPour);
        onWaterAdded?.Invoke();

        Debug.Log("[Bowl] Gorąca woda wlana do miski!");
    }

    private void DipAndCleanTowel()
    {
        if (playerHands == null || !playerHands.HasItem) return;

        GameObject held = playerHands.HeldItem;
        if (held == null) return;

        // Zamieniamy ID przedmiotu w ręku gracza na clean_towel
        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            pickup.ItemId = cleanTowelResultId;
            pickup.InteractionName = "Clean towel";
        }

        held.name = "CleanTowel_InHand";

        if (PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState("clean_towel", true);
            PreparationStateManager.Instance.SetTaskState("towel_prepared", true);
        }

        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayBurst(transform.position + Vector3.up * 0.1f);
        }

        PlaySound(soundCloth);
        onTowelCleaned?.Invoke();

        Debug.Log("[Bowl] Ręcznik zamoczony w gorącej wodzie! ID zamienione na 'clean_towel'. Można go dać Jurkowi.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsPlayerHoldingHotWater()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PotItem>(out var pot))
        {
            return pot.HasWater;
        }

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            string id = pickup.ItemId != null ? pickup.ItemId.Trim().ToLowerInvariant() : "";
            return id == hotWaterPotId || id == "pot_water" || id == "water_pot" || (id.Contains("pot") && id.Contains("water"));
        }

        return false;
    }

    private bool IsPlayerHoldingAnyTowel()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            string id = pickup.ItemId != null ? pickup.ItemId.Trim().ToLowerInvariant() : "";
            return id == "towel" || id == "dirty_towel" || id == "dry_towel" || id == "recznik" || held.name.ToLowerInvariant().Contains("towel");
        }

        return held.name.ToLowerInvariant().Contains("towel");
    }

    private void EnsureRefs()
    {
        if (playerHands == null) playerHands = FindAnyObjectByType<PlayerHands>();
    }

    private void PlaySound(string soundName)
    {
        if (!string.IsNullOrEmpty(soundName) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundName);
        }
    }
}
