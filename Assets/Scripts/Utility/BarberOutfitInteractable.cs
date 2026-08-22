using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Komponent umieszczany na obiekcie wdzianka fryzjera (na wieszaku, w szafie lub na półce).
/// Po wejściu w interakcję [E]:
/// 1. Blokuje ruch i kamerę gracza (InputModeManager -> UI).
/// 2. Płynnie ściemnia ekran do czerni (ScreenFader FadeOut).
/// 3. Na czarnym ekranie odtwarza dźwięk ubierania się, zalicza zadanie 'dressed_up' i ukrywa wiszące wdzianko.
/// 4. Rozjaśnia ekran (ScreenFader FadeIn).
/// 5. Wyświetla myśl fryzjera w chmurce ("Time to get to work.") i odblokowuje drzwi wyjściowe (First Doors).
/// </summary>
public class BarberOutfitInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionName = "Put on barber outfit";

    [Header("Task & Quest")]
    [Tooltip("ID zadania w PreparationStateManager (wymaganego przez First Doors).")]
    [SerializeField] private string taskId = "dressed_up";

    [Tooltip("Myśl gracza wyświetlana w chmurce po ubraniu się.")]
    [SerializeField] private string dressedThought = "Time to get to work.";

    [Header("Fade Settings")]
    [Tooltip("Czas ściemniania ekranu w sekundach.")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Tooltip("Czas trzymania czarnego ekranu (efekt przebierania się).")]
    [SerializeField] private float blackScreenDuration = 0.8f;

    [Tooltip("Czas rozjaśniania ekranu w sekundach.")]
    [SerializeField] private float fadeInDuration = 0.5f;

    [Header("Visual Feedback (Obiekt w świecie)")]
    [Tooltip("Obiekt wdzianka w szafie/na wieszaku, który ma zniknąć po ubraniu. Jeśli puste, ukryje ten obiekt.")]
    [SerializeField] private GameObject outfitWorldObject;

    [Tooltip("Opcjonalny model ubrań na ciele/rękach gracza do włączenia po ubraniu.")]
    [SerializeField] private GameObject playerDressedVisual;

    [Header("Audio")]
    [Tooltip("Nazwa dźwięku ubierania/szelestu tkaniny w AudioManager.")]
    [SerializeField] private string dressSoundGroup = "";

    [Tooltip("Opcjonalny bezpośredni plik audio.")]
    [SerializeField] private AudioClip dressAudioClip;

    [Header("References (Auto-resolves if empty)")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private InnerDialogueUI innerDialogueUI;

    public string InteractionName => _isDressed ? "" : interactionName;

    private bool _isDressed = false;
    private bool _isInteracting = false;

    private void Awake()
    {
        if (screenFader == null)
            screenFader = ScreenFader.Instance ?? FindAnyObjectByType<ScreenFader>();

        if (innerDialogueUI == null)
            innerDialogueUI = InnerDialogueUI.Instance ?? FindAnyObjectByType<InnerDialogueUI>();

        if (outfitWorldObject == null)
            outfitWorldObject = gameObject;
    }

    public void Interact()
    {
        if (_isDressed || _isInteracting)
            return;

        StartCoroutine(DressingSequenceRoutine());
    }

    private IEnumerator DressingSequenceRoutine()
    {
        _isInteracting = true;

        // 1. Zablokuj sterowanie graczem
        if (InputModeManager.Instance != null)
        {
            InputModeManager.Instance.SwitchToUI(unlockCursor: false);
        }

        if (screenFader == null)
            screenFader = ScreenFader.Instance ?? FindAnyObjectByType<ScreenFader>();

        // 2. Ściemnienie ekranu do czerni
        bool fadeOutDone = false;
        if (screenFader != null)
        {
            screenFader.FadeOut(fadeOutDuration, () => fadeOutDone = true);
            while (!fadeOutDone)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(fadeOutDuration);
        }

        // 3. Efekty na czarnym ekranie: Dźwięk, zaliczenie zadania, ukrycie wdzianka
        _isDressed = true;

        // Dźwięk ubierania
        PlayDressAudio();

        // Zaliczenie taska w PreparationStateManager
        if (PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.CompleteTask(taskId);
            Debug.Log($"[BarberOutfit] Task '{taskId}' completed!");
        }

        // Ukrycie wiszącego wdzianka w świecie
        if (outfitWorldObject != null)
        {
            // Jeśli to ten sam GameObject, wyłączamy renderery i collider, żeby Coroutine dokończyła działanie
            if (outfitWorldObject == gameObject)
            {
                var colliders = GetComponentsInChildren<Collider>();
                foreach (var col in colliders) col.enabled = false;

                var renderers = GetComponentsInChildren<Renderer>();
                foreach (var rend in renderers) rend.enabled = false;
            }
            else
            {
                outfitWorldObject.SetActive(false);
            }
        }

        // Włączenie ubrań na modelu gracza
        if (playerDressedVisual != null)
        {
            playerDressedVisual.SetActive(true);
        }

        // Czekanie w ciemności (odgłosy ubierania)
        yield return new WaitForSeconds(blackScreenDuration);

        // 4. Rozjaśnienie ekranu
        bool fadeInDone = false;
        if (screenFader != null)
        {
            screenFader.FadeIn(fadeInDuration, () => fadeInDone = true);
            while (!fadeInDone)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(fadeInDuration);
        }

        _isInteracting = false;

        // 5. Myśl gracza w chmurce i przywrócenie sterowania po dialogu
        if (innerDialogueUI == null)
            innerDialogueUI = InnerDialogueUI.Instance ?? FindAnyObjectByType<InnerDialogueUI>();

        if (innerDialogueUI != null && !string.IsNullOrEmpty(dressedThought))
        {
            innerDialogueUI.ShowMessage(dressedThought);
        }
        else
        {
            if (InputModeManager.Instance != null)
            {
                InputModeManager.Instance.SwitchToPlayer();
            }
        }

        Debug.Log("[BarberOutfit] Dressing sequence completed successfully.");
    }

    private void PlayDressAudio()
    {
        if (dressAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(dressAudioClip, Camera.main != null ? Camera.main.transform.position : transform.position);
            return;
        }

        if (!string.IsNullOrEmpty(dressSoundGroup) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(dressSoundGroup);
        }
    }
}
