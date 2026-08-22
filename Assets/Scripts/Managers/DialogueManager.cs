using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Główny menedżer dialogów w grze (Singleton).
/// Rozróżnia dwa niezależne style dialogowe:
/// 1. Myśli wewnętrzne fryzjera (Styl chmurki / Thought Bubble) -> InnerDialogueUI
/// 2. Rozmowa z klientem / NPC (Styl prostokątnej ramki z imieniem) -> ClientDialogueUI
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("References (Auto-resolves if empty)")]
    [Tooltip("Komponent chmurki myśli wewnętrznych bohatera.")]
    [SerializeField] private InnerDialogueUI innerThoughtsUI;

    [Tooltip("Komponent prostokątnej ramki dialogowej klienta / NPC.")]
    [SerializeField] private ClientDialogueUI clientDialogueUI;

    public bool IsAnyDialogueActive =>
        (clientDialogueUI != null && clientDialogueUI.IsDialogueActive) ||
        (innerThoughtsUI != null && innerThoughtsUI.IsDialogueActive) ||
        (InnerDialogueUI.Instance != null && InnerDialogueUI.Instance.IsDialogueActive) ||
        (ClientDialogueUI.Instance != null && ClientDialogueUI.Instance.IsDialogueActive);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (innerThoughtsUI == null)
            innerThoughtsUI = InnerDialogueUI.Instance ?? FindAnyObjectByType<InnerDialogueUI>();

        if (clientDialogueUI == null)
            clientDialogueUI = ClientDialogueUI.Instance ?? FindAnyObjectByType<ClientDialogueUI>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ──────────────────────────────────────────────────────────
    // 1. MYŚLI WEWNĘTRZNE (CHMURKA MYŚLI)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Wyświetla wewnętrzną myśl bohatera w chmurce (np. "Muszę się najpierw ubrać...").
    /// </summary>
    public void ShowThought(string thoughtText)
    {
        if (innerThoughtsUI == null)
            innerThoughtsUI = InnerDialogueUI.Instance ?? FindAnyObjectByType<InnerDialogueUI>();

        if (innerThoughtsUI != null)
        {
            innerThoughtsUI.ShowMessage(thoughtText);
        }
        else
        {
            Debug.LogWarning($"[DialogueManager] Brak InnerDialogueUI w scenie dla myśli: \"{thoughtText}\"");
        }
    }

    // ──────────────────────────────────────────────────────────
    // 2. DIALOG Z KLIENTEM / NPC (PROSTOKĄTNA RAMKA Z IMIENIEM)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Wyświetla pojedynczą kwestię wypowiedzianą przez klienta w prostokątnej ramce.
    /// </summary>
    public void ShowClientLine(string speakerName, string text, Action onComplete = null)
    {
        if (clientDialogueUI == null)
            clientDialogueUI = ClientDialogueUI.Instance ?? FindAnyObjectByType<ClientDialogueUI>();

        if (clientDialogueUI != null)
        {
            clientDialogueUI.ShowLine(speakerName, text, onComplete);
        }
        else
        {
            Debug.LogWarning($"[DialogueManager] Brak ClientDialogueUI w scenie dla wypowiedzi: [{speakerName}] \"{text}\"");
        }
    }

    /// <summary>
    /// Rozpoczyna całą sekwencję dialogową z klientem (wiele kwestii z przewijaniem klawiszem [E]).
    /// </summary>
    public void StartClientConversation(List<ClientDialogueUI.DialogueLine> lines, Action onComplete = null)
    {
        if (clientDialogueUI == null)
            clientDialogueUI = ClientDialogueUI.Instance ?? FindAnyObjectByType<ClientDialogueUI>();

        if (clientDialogueUI != null)
        {
            clientDialogueUI.StartDialogue(lines, onComplete);
        }
        else
        {
            Debug.LogWarning($"[DialogueManager] Brak ClientDialogueUI w scenie dla sekwencji dialogowej.");
        }
    }
}
