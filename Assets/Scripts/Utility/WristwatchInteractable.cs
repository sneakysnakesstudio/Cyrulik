using UnityEngine;

/// <summary>
/// Interaktywny zegarek na biurku fryzjera.
/// [PPM] / LookAt: Wyświetla tajemniczy opis "Time."
/// [LPM] / Interact: Zakłada zegarek na rękę, odtwarza dźwięk zapięcia, wyświetla myśl "Time... is a luxury."
/// i aktywuje WristwatchController pod klawiszem [Q].
/// </summary>
public class WristwatchInteractable : MonoBehaviour, IConditionalInteractable, ILookAtHandler
{
    [Header("Interactions")]
    [SerializeField] private string interactionName = "Put on watch";

    [Header("Inspect & Thoughts")]
    [Tooltip("Opis przy zbadaniu [PPM] lub najechaniu wzrokiem.")]
    [SerializeField] private string lookAtThought = "Time.";

    [Tooltip("Myśl wewnętrzna po założeniu zegarka na rękę.")]
    [SerializeField] private string equipThought = "Time... is a luxury.";

    [Header("Audio")]
    [SerializeField] private string equipSound = "cloth_pickup";
    [SerializeField] private AudioClip customEquipClip;

    private bool _isEquipped = false;

    public bool CanInteract => !_isEquipped;
    public string InteractionName => _isEquipped ? "" : interactionName;
    public string BlockedMessage => null;

    public void OnLookAt()
    {
        if (_isEquipped) return;

        if (!string.IsNullOrEmpty(lookAtThought) && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowThought(lookAtThought);
        }
    }

    public void Interact()
    {
        if (_isEquipped) return;
        _isEquipped = true;

        // Dźwięk założenia zegarka
        if (!string.IsNullOrEmpty(equipSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(equipSound);
        }
        else if (customEquipClip != null)
        {
            AudioSource.PlayClipAtPoint(customEquipClip, transform.position);
        }

        // Myśl fryzjera
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowThought(equipThought);
        }

        // Aktywacja zegarka na ręce gracza
        if (WristwatchController.Instance != null)
        {
            WristwatchController.Instance.EquipWatch();
        }

        // Efekt cząsteczek
        if (ParticleManager.Instance != null)
        {
            ParticleManager.Instance.PlayBurst(transform.position);
        }

        // Ukryj obiekt z biurka
        gameObject.SetActive(false);
        Debug.Log("[WristwatchInteractable] Zegarek podniesiony z biurka i założony na nadgarstek.");
    }
}
