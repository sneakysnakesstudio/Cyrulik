using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Komponent do badania obiektów otoczenia (lustro, gazeta, plakat, okno, drzwi, radio itp.).
/// Pozwala wywołać myśl wewnętrzną bohatera (Inner Thought w chmurce):
/// 1. Po najechaniu celownikiem (Trigger On Look At)
/// 2. Po naciśnięciu interakcji [E / LPM] (bez podnoszenia do rąk)
/// </summary>
public class InspectThoughtInteractable : MonoBehaviour, IInteractable, ILookAtHandler
{
    [Header("Interakcja")]
    [Tooltip("Napis wyświetlany na celowniku (np. 'Examine', 'Look at', 'Read note', 'Inspect').")]
    [SerializeField] private string interactionName = "Examine";

    [Header("Myśl wewnętrzna (Inner Thought)")]
    [Tooltip("Treść myśli bohatera w chmurce dialogowej.")]
    [TextArea(2, 4)]
    [SerializeField] private string thoughtText = "Interesting...";

    [Header("Wyzwalanie")]
    [Tooltip("Czy myśl ma się wyświetlać po naciśnięciu klawisza interakcji [E / LPM]?")]
    [SerializeField] private bool triggerOnInteract = true;

    [Tooltip("Czy myśl ma się wyświetlić automatycznie przy samym spojrzeniu na obiekt celownikiem (bez klikania)?")]
    [SerializeField] private bool triggerOnLookAt = false;

    [Tooltip("Czy ta myśl ma się wyświetlić tylko raz w trakcie całej gry?")]
    [SerializeField] private bool showThoughtOnlyOnce = true;

    [Header("Audio i Efekty")]
    [Tooltip("Opcjonalny dźwięk zbadania przedmiotu w AudioManager (np. 'paper_turn', 'click').")]
    [SerializeField] private string inspectSound = "";

    [Tooltip("Opcjonalny błysk cząsteczek przy interakcji.")]
    [SerializeField] private bool spawnParticlesOnInteract = false;

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onInspected;

    private bool _hasTriggered = false;

    public string InteractionName => interactionName;
    public string ThoughtText { get => thoughtText; set => thoughtText = value; }

    public void OnLookAt()
    {
        if (triggerOnLookAt)
        {
            TriggerThought();
        }
    }

    public void Interact()
    {
        if (!triggerOnInteract) return;

        if (spawnParticlesOnInteract && ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayBurst(transform.position);
        }

        TriggerThought();
        onInspected?.Invoke();
    }

    public void TriggerThought()
    {
        if (string.IsNullOrEmpty(thoughtText)) return;

        if (_hasTriggered && showThoughtOnlyOnce) return;

        _hasTriggered = true;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowThought(thoughtText);
        }

        if (!string.IsNullOrEmpty(inspectSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(inspectSound);
        }
    }

    /// <summary>
    /// Resetuje stan, aby myśl mogła pojawić się ponownie.
    /// </summary>
    public void ResetTrigger()
    {
        _hasTriggered = false;
    }
}
