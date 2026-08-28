using UnityEngine;

public class PickupItem : MonoBehaviour, IConditionalInteractable, ILookAtHandler
{
    [Header("Item Info")]
    [Tooltip("ID przedmiotu (np. 'cheese', 'towel', 'wood', 'razor_blade', 'dead_mouse', 'pot', 'razor').")]
    [SerializeField] private string itemId = "cheese";

    [Header("Interaction")]
    [SerializeField] private string interactionName = "Pick up";

    [Header("Wymagania Specjalne (np. Brzytwa)")]
    [Tooltip("Czy ten przedmiot wymaga wcześniejszego podania ciepłego ręcznika Jurkowi (np. brzytwa)?")]
    [SerializeField] private bool requireTowelGivenFirst = false;

    [Header("Inner Thought / Inspect Mode (Myśli bohatera)")]
    [Tooltip("Myśl wewnętrzna wyświetlana w chmurce przy zbadaniu lub podniesieniu tego przedmiotu.")]
    [SerializeField] private string thoughtText = "";

    [Tooltip("Stare pole dla kompatybilności wstecznej.")]
    [SerializeField, HideInInspector] private string firstPickupThought = "";

    [Tooltip("Jeśli zaznaczone, interakcja TYLKO wyświetli myśl w chmurce i NIE podniesie przedmiotu do rąk (obiekt zostaje na swoim miejscu).")]
    [SerializeField] private bool onlyShowThoughtDoNotPickup = false;

    [Tooltip("Jeśli zaznaczone, myśl wyświetli się automatycznie, gdy tylko gracz SPOJRZY na ten obiekt celownikiem (bez konieczności klikania).")]
    [SerializeField] private bool triggerThoughtOnLookAt = false;

    [Tooltip("Czy ta myśl ma się wyświetlić tylko raz w trakcie całej gry?")]
    [SerializeField] private bool showThoughtOnlyOnce = true;

    [Tooltip("Opcjonalny dźwięk przy zbadaniu przedmiotu (nazwa w AudioManager).")]
    [SerializeField] private string inspectSound = "";

    private bool _hasShownPickupThought = false;

    [Header("In-Hand Transform (Optional)")]
    [Tooltip("Lokalna pozycja w ręku.")]
    [SerializeField] private Vector3 inHandPosition = Vector3.zero;

    [Tooltip("Lokalna rotacja w ręku.")]
    [SerializeField] private Vector3 inHandRotation = Vector3.zero;

    [Tooltip("Lokalna skala w ręku.")]
    [SerializeField] private Vector3 inHandScale = Vector3.one;

    [Header("Physics Settings")]
    [Tooltip("Jeśli zaznaczone, przedmiot na starcie ma Rigidbody zamrożone (isKinematic = true), dzięki czemu stabilnie leży w lodówce/na półkach i nie wypada z powodu fizyki.")]
    [SerializeField] private bool freezeInPlaceAtStart = true;

    [Header("Audio Overrides (Opcjonalnie)")]
    [Tooltip("Dedykowana nazwa dźwięku podniesienia w AudioManager (jeśli puste, użyje uniwersalnego z PlayerHands).")]
    [SerializeField] private string customPickupSound = "";

    [Tooltip("Dedykowany AudioClip podniesienia jako fallback.")]
    [SerializeField] private AudioClip customPickupClip;

    [Tooltip("Dedykowana nazwa dźwięku upuszczenia w AudioManager (jeśli puste, użyje uniwersalnego z PlayerHands).")]
    [SerializeField] private string customDropSound = "";

    [Tooltip("Dedykowany AudioClip upuszczenia jako fallback.")]
    [SerializeField] private AudioClip customDropClip;

    [Header("References")]
    [SerializeField] private PlayerHands _playerHands;

    public string ItemId { get => itemId; set => itemId = value; }

    public bool CanInteract
    {
        get
        {
            if (IsRazorItem() || requireTowelGivenFirst)
            {
                if (!IsTowelGiven())
                    return false;
            }
            return true;
        }
    }

    public string InteractionName
    {
        get
        {
            if ((IsRazorItem() || requireTowelGivenFirst) && !IsTowelGiven())
            {
                return $"{interactionName} (Apply hot towel to client first)";
            }
            return interactionName;
        }
        set => interactionName = value;
    }

    public string BlockedMessage => "I need to prepare and apply the hot towel to Jurek before taking the razor.";

    public string ThoughtText
    {
        get => !string.IsNullOrEmpty(thoughtText) ? thoughtText : firstPickupThought;
        set => thoughtText = value;
    }

    public bool OnlyShowThoughtDoNotPickup { get => onlyShowThoughtDoNotPickup; set => onlyShowThoughtDoNotPickup = value; }
    public bool TriggerThoughtOnLookAt { get => triggerThoughtOnLookAt; set => triggerThoughtOnLookAt = value; }

    public Vector3 InHandPosition { get => inHandPosition; set => inHandPosition = value; }
    public Vector3 InHandRotation { get => inHandRotation; set => inHandRotation = value; }
    public Vector3 InHandScale { get => inHandScale == Vector3.zero ? Vector3.one : inHandScale; set => inHandScale = value; }

    public string CustomPickupSound => customPickupSound;
    public AudioClip CustomPickupClip => customPickupClip;
    public string CustomDropSound => customDropSound;
    public AudioClip CustomDropClip => customDropClip;

    private void Awake()
    {
        if (gameObject.isStatic)
        {
            Debug.LogWarning($"[PickupItem] Obiekt '{name}' jest oznaczony jako STATIC! Przedmioty do podnoszenia NIE mogą być Static, ponieważ Unity piecze ich meshe w Static Batching i nie pozwala ich przenosić.", this);
        }

        // Auto-fix ID dla brzytwy jeśli w scenie oznaczono jako cheese
        if ((name.ToLowerInvariant().Contains("razor") || interactionName.ToLowerInvariant().Contains("razor")) && itemId == "cheese")
        {
            itemId = "razor";
        }

        if (_playerHands == null)
        {
            _playerHands = FindAnyObjectByType<PlayerHands>();
        }

        // Zabezpieczenie przed wypadaniem przedmiotów z lodówki / półek na starcie gry
        if (freezeInPlaceAtStart)
        {
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gameObject.isStatic)
        {
            Debug.LogWarning($"[PickupItem] Obiekt '{name}' jest oznaczony jako STATIC w Inspectorze! Odznacz pole 'Static' na samej górze Inspectora dla tego obiektu.", this);
        }
    }
#endif

    public void OnLookAt()
    {
        if (triggerThoughtOnLookAt)
        {
            TriggerThought();
        }
    }

    public void TriggerThought()
    {
        string text = ThoughtText;
        if (string.IsNullOrEmpty(text)) return;

        if (_hasShownPickupThought && showThoughtOnlyOnce) return;

        _hasShownPickupThought = true;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowThought(text);
        }

        if (!string.IsNullOrEmpty(inspectSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(inspectSound);
        }
    }

    public void Interact()
    {
        // Blokada podniesienia brzytwy przed podaniem ręcznika Jurkowi
        if ((IsRazorItem() || requireTowelGivenFirst) && !IsTowelGiven())
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.ShowThought("I shouldn't take the razor yet. I need to prepare and apply the hot towel to the customer first.");
            }
            return;
        }

        // 1. Zawsze wywołaj myśl jeśli jest ustawiona
        TriggerThought();

        // 2. Jeśli zaznaczono 'onlyShowThoughtDoNotPickup', to NIE podnoś przedmiotu do rąk
        if (onlyShowThoughtDoNotPickup)
        {
            return;
        }

        // 3. W przeciwnym razie podnieś do rąk gracza
        if (_playerHands == null)
        {
            _playerHands = FindAnyObjectByType<PlayerHands>();
        }

        if (_playerHands == null)
            return;

        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayBurst(transform.position);
        }

        _playerHands.TryHold(gameObject);
    }

    private bool IsRazorItem()
    {
        string id = itemId != null ? itemId.ToLowerInvariant() : "";
        string n = name.ToLowerInvariant();
        string iname = interactionName != null ? interactionName.ToLowerInvariant() : "";
        return id.Contains("razor") || id.Contains("blade") || n.Contains("razor") || n.Contains("blade") || iname.Contains("razor");
    }

    private bool IsTowelGiven()
    {
        if (CustomerJurek.Instance != null && CustomerJurek.Instance.HasReceivedTowel)
            return true;

        if (PreparationStateManager.Instance != null)
        {
            return PreparationStateManager.Instance.IsTaskCompleted("clean_towel") || PreparationStateManager.Instance.IsTaskCompleted("towel_prepared");
        }

        return false;
    }
}