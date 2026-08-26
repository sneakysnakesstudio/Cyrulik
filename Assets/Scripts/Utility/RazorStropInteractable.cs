using UnityEngine;

/// <summary>
/// 3D World Interactable component for the Barber Razor Strop.
/// When the player looks at the hanging leather strop and presses Interact (E / LMB),
/// this launches the Razor Stropping Minigame.
/// </summary>
public class RazorStropInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [Tooltip("Text displayed on the crosshair UI when looking at the strop.")]
    [SerializeField] private string interactionName = "Sharpen Razor";

    [Tooltip("Reference to the RazorMinigame manager in the scene.")]
    [SerializeField] private RazorMinigame razorMinigame;

    [Header("Requirements (Optional)")]
    [Tooltip("Require player to hold a razor blade in hands to interact?")]
    [SerializeField] private bool requireBladeInHands = false;

    [Header("Audio & Juice")]
    [SerializeField] private string interactSound = "card_flip";

    public string InteractionName => interactionName;

    private void Awake()
    {
        FindMinigame();
    }

    private void Start()
    {
        FindMinigame();
    }

    private void FindMinigame()
    {
        if (razorMinigame == null)
        {
            razorMinigame = FindAnyObjectByType<RazorMinigame>(FindObjectsInactive.Include);
        }
    }

    public void Interact()
    {
        FindMinigame();

        if (razorMinigame == null)
        {
            Debug.LogError("[RazorStropInteractable] RazorMinigame component not found in the scene!");
            return;
        }

        if (razorMinigame.IsActive)
        {
            return;
        }

        // Optional physical sway reaction on 3D strap
        HangingStrapSway sway = GetComponent<HangingStrapSway>() ?? GetComponentInParent<HangingStrapSway>();
        if (sway != null)
        {
            // Trigger visual sway
        }

        if (!string.IsNullOrEmpty(interactSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(interactSound);
        }

        Debug.Log("[RazorStropInteractable] Player interacted with Razor Strop -> Starting Minigame!");
        razorMinigame.StartMinigame();
    }
}