using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Interaktywny zlew / kran (Sink).
/// Pozwala napełnić trzymany garnek wodą oraz szklankę (empty_glass -> glass_water) przez 5 sekund.
/// </summary>
public class SinkInteractable : MonoBehaviour, IConditionalInteractable
{
    [Header("Pot Item IDs")]
    [Tooltip("Akceptowane ID pustego garnka.")]
    [SerializeField] private string[] emptyPotItemIds = new string[] { "pot", "pot_empty" };

    [Tooltip("ID garnka po napełnieniu wodą.")]
    [SerializeField] private string filledPotItemId = "pot_water";

    [Header("Glass Item IDs")]
    [Tooltip("Akceptowane ID pustej szklanki (np. 'empty_glass').")]
    [SerializeField] private string[] emptyGlassItemIds = new string[] { "empty_glass", "glass_empty", "glass" };

    [Tooltip("ID szklanki po napełnieniu wodą (np. 'filled_glass' lub 'glass_water').")]
    [SerializeField] private string filledGlassItemId = "filled_glass";

    [Tooltip("Czas nalewania wody do szklanki w sekundach.")]
    [SerializeField] private float glassPourDuration = 5.0f;

    [Header("Interaction Prompts (English)")]
    [SerializeField] private string promptFillPot = "Fill pot with water";
    [SerializeField] private string promptFillGlass = "Pour water into glass";
    [SerializeField] private string promptPouring = "Pouring water...";
    [SerializeField] private string promptPotFull = "Pot is already full of water";
    [SerializeField] private string promptGlassFull = "Glass is already full of water";
    [SerializeField] private string promptNeedItem = "Sink (Requires an empty glass or pot)";

    [Header("Audio")]
    [Tooltip("Nazwa dźwięku nalewania wody do garnka w AudioManager.")]
    [SerializeField] private string soundPourWater = "water_pour";

    [Tooltip("Nazwa dźwięku nalewania wody do szklanki w AudioManager.")]
    [SerializeField] private string soundPourGlass = "pouing_water_inGlass";

    [SerializeField] private AudioClip customWaterClip;
    [SerializeField] private AudioClip customGlassClip;
    [SerializeField] private AudioSource audioSource;

    [Header("Wizualia i Cząsteczki")]
    [Tooltip("Opcjonalny obiekt strumienia wody z kranu.")]
    [SerializeField] private GameObject waterStreamVisual;

    [Tooltip("Opcjonalny system cząsteczek wody (jeśli puste, skrypt znajdzie lub wygeneruje automatycznie).")]
    [SerializeField] private ParticleSystem waterParticleSystem;

    [Tooltip("Opcjonalny punkt wylotu wody z kranu do zespawnowania cząsteczek.")]
    [SerializeField] private Transform faucetNozzlePoint;

    [SerializeField] private float potWaterStreamDuration = 1.0f;

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Unity.Cinemachine.CinemachineBrain cinemachineBrain;
    [SerializeField] private HeadBobbing headBobbing;

    [Header("Blokowanie Gracza Podczas Nalewania")]
    [Tooltip("Czy zablokować ruch gracza i obrót kamery na czas nalewania wody.")]
    [SerializeField] private bool lockPlayerWhilePouring = true;

    private bool _isPouring = false;
    private Coroutine _pourCoroutine;

    public bool CanInteract
    {
        get
        {
            if (_isPouring) return false;
            if (IsHoldingEmptyGlass()) return true;
            if (IsHoldingEmptyPot()) return true;
            return false;
        }
    }

    public string InteractionName
    {
        get
        {
            if (_isPouring)
                return promptPouring;

            if (IsHoldingEmptyGlass())
                return promptFillGlass;

            if (IsHoldingEmptyPot())
                return promptFillPot;

            if (IsHoldingFilledGlass())
                return promptGlassFull;

            if (IsHoldingFilledPot())
                return promptPotFull;

            return promptNeedItem;
        }
    }

    private void Awake()
    {
        EnsurePlayerReferences();

        if (waterStreamVisual != null)
        {
            waterStreamVisual.SetActive(false);
        }

        EnsureWaterParticles();
    }

    private void OnDisable()
    {
        if (_isPouring)
        {
            SetPlayerLocked(false);
            StopWaterEffect();
            _isPouring = false;
        }
    }

    public void Interact()
    {
        if (_isPouring) return;

        if (IsHoldingEmptyGlass())
        {
            StartPouringWaterIntoGlass();
        }
        else if (IsHoldingEmptyPot())
        {
            FillHeldPotWithWater();
        }
    }

    private void StartPouringWaterIntoGlass()
    {
        if (_pourCoroutine != null)
            StopCoroutine(_pourCoroutine);

        _pourCoroutine = StartCoroutine(PourGlassRoutine());
    }

    private IEnumerator PourGlassRoutine()
    {
        _isPouring = true;

        // Blokada ruchu gracza i kamery
        SetPlayerLocked(true);

        // 1. Włącz strumień wody i cząsteczki
        StartWaterEffect();

        // 2. Odtwórz dźwięk nalewania wody do szklanki
        PlayGlassPourSound();

        // 3. Natychmiast posadź Jurka w momencie kliknięcia nalewania
        if (CustomerJurek.Instance != null)
        {
            CustomerJurek.Instance.OnPlayerPouringWater();
        }

        Debug.Log($"[Sink] Rozpoczęto nalewanie wody do szklanki ({glassPourDuration}s)... Gracz zablokowany.");

        // 4. Czekaj określony czas (5 sekund)
        yield return new WaitForSeconds(glassPourDuration);

        // 5. Zmień stan trzymanego przedmiotu na napełnioną szklankę
        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands != null && playerHands.HasItem)
        {
            GameObject held = playerHands.HeldItem;
            if (held != null && held.TryGetComponent<PickupItem>(out var pickup))
            {
                pickup.ItemId = filledGlassItemId;
                pickup.InteractionName = "Glass with water";
            }
        }

        // 6. Wyłącz strumień i cząsteczki
        StopWaterEffect();

        // Odblokowanie ruchu gracza i kamery
        SetPlayerLocked(false);

        _isPouring = false;
        _pourCoroutine = null;

        Debug.Log($"[Sink] Szklanka została napełniona wodą! (ItemId: '{filledGlassItemId}') - Gracz odblokowany.");
    }

    private void EnsurePlayerReferences()
    {
        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (cinemachineBrain == null)
            cinemachineBrain = FindAnyObjectByType<Unity.Cinemachine.CinemachineBrain>();

        if (headBobbing == null)
            headBobbing = FindAnyObjectByType<HeadBobbing>();
    }

    private void SetPlayerLocked(bool lockState)
    {
        if (!lockPlayerWhilePouring) return;

        EnsurePlayerReferences();

        if (lockState)
        {
            if (InputModeManager.Instance != null)
            {
                InputModeManager.Instance.SwitchToUI(unlockCursor: false);
            }

            if (playerMovement != null)
            {
                playerMovement.enabled = false;
            }

            if (cinemachineBrain != null)
            {
                cinemachineBrain.enabled = false;
            }

            if (headBobbing != null)
            {
                headBobbing.enabled = false;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (InputModeManager.Instance != null)
            {
                InputModeManager.Instance.SwitchToPlayer();
            }

            if (playerMovement != null)
            {
                playerMovement.enabled = true;
            }

            if (cinemachineBrain != null)
            {
                cinemachineBrain.enabled = true;
            }

            if (headBobbing != null)
            {
                headBobbing.enabled = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
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

        // 3. Efekt strumienia wody z kranu i cząsteczek
        StartWaterEffect();
        CancelInvoke(nameof(StopWaterEffect));
        Invoke(nameof(StopWaterEffect), potWaterStreamDuration);

        if (CustomerJurek.Instance != null)
        {
            CustomerJurek.Instance.OnPlayerPouringWater();
        }

        Debug.Log($"[Sink] Garnek został napełniony wodą! (ItemId: '{filledPotItemId}')");
    }

    private void StartWaterEffect()
    {
        if (waterStreamVisual != null)
        {
            waterStreamVisual.SetActive(true);
        }

        EnsureWaterParticles();

        if (waterParticleSystem != null)
        {
            waterParticleSystem.Play();
        }
    }

    private void StopWaterEffect()
    {
        if (waterStreamVisual != null)
        {
            waterStreamVisual.SetActive(false);
        }

        if (waterParticleSystem != null)
        {
            waterParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void EnsureWaterParticles()
    {
        if (waterParticleSystem != null) return;

        if (waterStreamVisual != null)
        {
            waterParticleSystem = waterStreamVisual.GetComponentInChildren<ParticleSystem>();
            if (waterParticleSystem != null) return;
        }

        Transform spawnParent = faucetNozzlePoint != null 
            ? faucetNozzlePoint 
            : (waterStreamVisual != null ? waterStreamVisual.transform : transform);

        GameObject psGo = new GameObject("Sink_WaterParticles_Auto");
        psGo.transform.SetParent(spawnParent, false);
        psGo.transform.localPosition = Vector3.zero;
        psGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Skierowane pionowo w dół

        waterParticleSystem = psGo.AddComponent<ParticleSystem>();
        var main = waterParticleSystem.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 0.35f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
        main.startColor = new Color(0.75f, 0.9f, 1f, 0.75f);
        main.gravityModifier = 1.2f;
        main.playOnAwake = false;

        var emission = waterParticleSystem.emission;
        emission.rateOverTime = 65f;

        var shape = waterParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 4f;
        shape.radius = 0.015f;

        var renderer = psGo.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
            ?? Shader.Find("Particles/Standard Unlit") 
            ?? Shader.Find("Sprites/Default");

        if (particleShader != null)
        {
            Material mat = new Material(particleShader);
            mat.color = new Color(0.8f, 0.92f, 1f, 0.8f);
            renderer.material = mat;
        }

        waterParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    private void PlayGlassPourSound()
    {
        if (customGlassClip != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(customGlassClip);
            else
                AudioSource.PlayClipAtPoint(customGlassClip, transform.position);
            return;
        }

        if (!string.IsNullOrEmpty(soundPourGlass) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundPourGlass);
        }
        else
        {
            PlayWaterSound();
        }
    }

    private bool IsHoldingEmptyGlass()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            if (string.IsNullOrEmpty(pickup.ItemId)) return false;

            foreach (string emptyId in emptyGlassItemIds)
            {
                if (string.Equals(pickup.ItemId, emptyId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private bool IsHoldingFilledGlass()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            return string.Equals(pickup.ItemId, filledGlassItemId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
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
