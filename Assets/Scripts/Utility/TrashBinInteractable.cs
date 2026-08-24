using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interaktywny kosz na śmieci (np. w głównym pokoju).
/// Gracz może do niego wyrzucić złapaną mysz (ItemId: 'dead_mouse' lub 'caught_mouse').
/// Po wyrzuceniu myszy wywoływane jest zdarzenie OnMouseDisposed, które aktywuje pojawienie się klienta (Jurek).
/// </summary>
public class TrashBinInteractable : MonoBehaviour, IConditionalInteractable
{
    public static event Action OnAnyMouseDisposed;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        OnAnyMouseDisposed = null;
    }
#endif

    [Header("Zadanie i Przedmioty")]
    [Tooltip("Akceptowane ID przedmiotu z PickupItem (domyślnie 'dead_mouse' i 'caught_mouse').")]
    [SerializeField] private string[] acceptedItemIds = new string[] { "dead_mouse", "caught_mouse" };

    [Tooltip("ID zadania w PreparationStateManager (np. 'mouse_disposed').")]
    [SerializeField] private string taskId = "mouse_disposed";

    [Header("Interaction Prompts")]
    [SerializeField] private string promptNeedItem = "Trash bin";
    [SerializeField] private string promptThrow = "Throw mouse into trash";
    [SerializeField] private string promptAlreadyDisposed = "Trash bin";

    [Header("Audio")]
    [SerializeField] private string soundThrow = "cloth_pickup";
    [SerializeField] private AudioClip customThrowClip;

    [Header("Animacja pokrywy (Opcjonalnie)")]
    [Tooltip("Transform pokrywy kosza do animacji otwarcia.")]
    [SerializeField] private Transform lidTransform;
    [SerializeField] private Vector3 lidOpenRotation = new Vector3(-45f, 0f, 0f);
    [SerializeField] private float lidAnimDuration = 0.25f;

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onMouseThrown;

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    private bool _hasDisposedMouse = false;
    private Vector3 _lidInitialRotation;
    private Tween _lidTween;

    public bool HasDisposedMouse => _hasDisposedMouse;

    public bool CanInteract
    {
        get
        {
            return IsPlayerHoldingAcceptedItem();
        }
    }

    public string InteractionName
    {
        get
        {
            if (_hasDisposedMouse)
                return promptAlreadyDisposed;

            if (IsPlayerHoldingAcceptedItem())
                return promptThrow;

            return promptNeedItem;
        }
    }

    private void Awake()
    {
        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }

        if (lidTransform != null)
        {
            _lidInitialRotation = lidTransform.localEulerAngles;
        }
    }

    private void OnDestroy()
    {
        _lidTween?.Kill();
    }

    public void Interact()
    {
        if (!CanInteract)
            return;

        DisposeHeldMouse();
    }

    private void DisposeHeldMouse()
    {
        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands == null || !playerHands.HasItem)
            return;

        _hasDisposedMouse = true;

        // Niszczymy mysz z rąk gracza
        playerHands.DestroyHeldItem();

        // Animacja pokrywy
        AnimateLid();

        // Dźwięk wyrzucenia
        PlayThrowSound();

        // Zaliczenie zadania w PreparationStateManager
        if (!string.IsNullOrEmpty(taskId) && PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState(taskId, true);
        }

        // Zdarzenia
        onMouseThrown?.Invoke();
        OnAnyMouseDisposed?.Invoke();

        if (MouseQuestManager.Instance != null)
        {
            MouseQuestManager.Instance.OnMouseDisposed();
        }

        Debug.Log("[TrashBin] Mysz została wyrzucona do kosza! Wywołano zdarzenie pojawienia się klienta Jurka.");
    }

    private void AnimateLid()
    {
        if (lidTransform == null) return;

        _lidTween?.Kill();
        Sequence seq = DOTween.Sequence();
        seq.Append(lidTransform.DOLocalRotate(lidOpenRotation, lidAnimDuration).SetEase(Ease.OutQuad));
        seq.AppendInterval(0.2f);
        seq.Append(lidTransform.DOLocalRotate(_lidInitialRotation, lidAnimDuration).SetEase(Ease.InQuad));
        seq.SetLink(lidTransform.gameObject, LinkBehaviour.KillOnDestroy);
        _lidTween = seq;
    }

    private void PlayThrowSound()
    {
        if (customThrowClip != null)
        {
            AudioSource.PlayClipAtPoint(customThrowClip, transform.position);
            return;
        }

        if (!string.IsNullOrEmpty(soundThrow) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundThrow);
        }
    }

    private bool IsPlayerHoldingAcceptedItem()
    {
        if (playerHands == null || !playerHands.HasItem)
            return false;

        GameObject held = playerHands.HeldItem;
        if (held == null)
            return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            if (string.IsNullOrEmpty(pickup.ItemId)) return false;

            if (acceptedItemIds != null)
            {
                foreach (string id in acceptedItemIds)
                {
                    if (string.Equals(pickup.ItemId, id, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }
}
