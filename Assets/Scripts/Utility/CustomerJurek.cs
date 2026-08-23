using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Zarządza klientem (Jurek) – jego pojawieniem się przy drzwiach po wyrzuceniu myszy
/// oraz reakcją ucieczki w przypadku, gdy mysz przestraszy go na stanowisku fryzjerskim.
/// </summary>
public class CustomerJurek : MonoBehaviour, IConditionalInteractable
{
    [Header("Postać / Wizualia Jurka")]
    [Tooltip("Główny obiekt z modelem Jurka (do włączenia/wyłączenia).")]
    [SerializeField] private GameObject jurekVisual;

    [Tooltip("Punkt startowy / spawn Jurka przy drzwiach.")]
    [SerializeField] private Transform doorArrivalPoint;

    [Header("Drzwi")]
    [Tooltip("Komponent DoorInteractable drzwi wejściowych (opcjonalnie do odblokowania/otwarcia).")]
    [SerializeField] private DoorInteractable frontDoor;

    [Header("Dźwięki")]
    [SerializeField] private string soundKnock = "door_knock";
    [SerializeField] private AudioClip customKnockClip;

    [Header("Dialog po przyjściu (Opcjonalnie)")]
    [SerializeField] private bool autoTriggerDialogueOnArrival = true;
    [SerializeField] private string jurekSpeakerName = "Jurek";
    [TextArea(2, 4)]
    [SerializeField] private string[] arrivalDialogueLines = new string[]
    {
        "Dzień dobry! Słyszałem, że to najlepszy cyrulik w mieście.",
        "Mogę prosić o porządne golenie?"
    };

    [Header("Reakcja na Mysza (Fail Branch)")]
    [TextArea(2, 4)]
    [SerializeField] private string mouseScareReactionText = "Jezus Maria, mysz! W salonie fryzjerskim?! Wychodzę stąd natychmiast!";
    [SerializeField] private Transform exitDestination;
    [SerializeField] private float exitWalkDuration = 3f;

    [Header("Interakcja ręczna (jeśli nie auto-dialog)")]
    [SerializeField] private string interactionName = "Porozmawiaj z Jurkiem";

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onJurekArrived;
    [SerializeField] private UnityEvent onJurekLeft;

    private bool _hasArrived = false;
    private bool _hasLeft = false;

    public bool HasArrived => _hasArrived;
    public bool HasLeft => _hasLeft;

    public bool CanInteract => _hasArrived && !_hasLeft && !ClientDialogueUI.Instance.IsDialogueActive;
    public string InteractionName => interactionName;

    private void Awake()
    {
        if (jurekVisual == null)
        {
            jurekVisual = gameObject;
        }

        // Domyślnie na starcie Jurek nie stoi jeszcze przy drzwiach
        if (jurekVisual != null && jurekVisual != gameObject)
        {
            jurekVisual.SetActive(false);
        }
    }

    /// <summary>
    /// Wywoływane po wyrzuceniu myszy do kosza – Jurek puka do drzwi i pojawia się przy wejściu.
    /// </summary>
    public void TriggerArrival()
    {
        if (_hasArrived) return;

        _hasArrived = true;

        // 1. Dźwięk pukania do drzwi
        PlayKnockSound();

        // 2. Ustawienie pozycji przy drzwiach i aktywacja
        if (doorArrivalPoint != null)
        {
            transform.position = doorArrivalPoint.position;
            transform.rotation = doorArrivalPoint.rotation;
        }

        if (jurekVisual != null)
        {
            jurekVisual.SetActive(true);
        }

        // 3. Odblokowanie drzwi wejściowych
        if (frontDoor != null)
        {
            frontDoor.Unlock();
        }

        onJurekArrived?.Invoke();

        Debug.Log("[CustomerJurek] Jurek zapukał do drzwi i czeka na wejście!");

        // 4. Opcjonalny automatyczny dialog
        if (autoTriggerDialogueOnArrival && ClientDialogueUI.Instance != null && arrivalDialogueLines != null && arrivalDialogueLines.Length > 0)
        {
            DOVirtual.DelayedCall(1.0f, StartArrivalDialogue)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    /// <summary>
    /// Wywoływane, gdy mysz przestraszy klienta (porażka – brak sera na pułapce).
    /// </summary>
    public void TriggerMouseScareAndLeave(Action onComplete = null)
    {
        if (_hasLeft) return;

        _hasLeft = true;

        Debug.Log("[CustomerJurek] Klient ucieka z salonu po zauważeniu myszy!");

        if (ClientDialogueUI.Instance != null && !string.IsNullOrEmpty(mouseScareReactionText))
        {
            ClientDialogueUI.Instance.ShowLine(jurekSpeakerName, mouseScareReactionText, () =>
            {
                WalkOut(onComplete);
            });
        }
        else
        {
            WalkOut(onComplete);
        }
    }

    private void WalkOut(Action onComplete)
    {
        if (exitDestination != null)
        {
            transform.DOMove(exitDestination.position, exitWalkDuration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (jurekVisual != null) jurekVisual.SetActive(false);
                    onJurekLeft?.Invoke();
                    onComplete?.Invoke();
                });
        }
        else
        {
            if (jurekVisual != null) jurekVisual.SetActive(false);
            onJurekLeft?.Invoke();
            onComplete?.Invoke();
        }
    }

    public void Interact()
    {
        if (!CanInteract) return;

        StartArrivalDialogue();
    }

    private void StartArrivalDialogue()
    {
        if (ClientDialogueUI.Instance == null || arrivalDialogueLines == null) return;

        List<ClientDialogueUI.DialogueLine> lines = new List<ClientDialogueUI.DialogueLine>();
        foreach (string line in arrivalDialogueLines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(new ClientDialogueUI.DialogueLine(jurekSpeakerName, line));
            }
        }

        if (lines.Count > 0)
        {
            ClientDialogueUI.Instance.StartDialogue(lines);
        }
    }

    private void PlayKnockSound()
    {
        if (customKnockClip != null)
        {
            AudioSource.PlayClipAtPoint(customKnockClip, transform.position);
            return;
        }

        if (!string.IsNullOrEmpty(soundKnock) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundKnock);
        }
    }
}
