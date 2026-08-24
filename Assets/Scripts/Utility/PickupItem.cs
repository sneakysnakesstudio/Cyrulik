using UnityEngine;

public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Item Info")]
    [Tooltip("ID przedmiotu (np. 'cheese', 'towel', 'wood', 'razor_blade', 'dead_mouse', 'pot').")]
    [SerializeField] private string itemId = "cheese";

    [Header("Interaction")]
    [SerializeField] private string interactionName = "Pick up";

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
    public string InteractionName { get => interactionName; set => interactionName = value; }

    public Vector3 InHandPosition => inHandPosition;
    public Vector3 InHandRotation => inHandRotation;
    public Vector3 InHandScale => inHandScale == Vector3.zero ? Vector3.one : inHandScale;

    public string CustomPickupSound => customPickupSound;
    public AudioClip CustomPickupClip => customPickupClip;
    public string CustomDropSound => customDropSound;
    public AudioClip CustomDropClip => customDropClip;

    private void Awake()
    {
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

    public void Interact()
    {
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
}