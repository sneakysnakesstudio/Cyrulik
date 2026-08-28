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

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (innerThoughtsUI == null)
            innerThoughtsUI = InnerDialogueUI.Instance ?? FindAnyObjectByType<InnerDialogueUI>(FindObjectsInactive.Include);

        if (clientDialogueUI == null)
            clientDialogueUI = ClientDialogueUI.Instance ?? FindAnyObjectByType<ClientDialogueUI>(FindObjectsInactive.Include);
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
            innerThoughtsUI = InnerDialogueUI.Instance ?? FindAnyObjectByType<InnerDialogueUI>(FindObjectsInactive.Include);

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
            clientDialogueUI = ClientDialogueUI.Instance ?? FindAnyObjectByType<ClientDialogueUI>(FindObjectsInactive.Include);

        if (clientDialogueUI != null)
        {
            clientDialogueUI.ShowLine(speakerName, text, onComplete);
        }
        else
        {
            Debug.LogWarning($"[DialogueManager] Brak ClientDialogueUI w scenie dla wypowiedzi: [{speakerName}] \"{text}\". Użyj menu: Tools -> Cyrulik -> Create Full Dialogue System");
        }
    }

    /// <summary>
    /// Rozpoczyna całą sekwencję dialogową z klientem (wiele kwestii z przewijaniem klawiszem [E]).
    /// </summary>
    public void StartClientConversation(List<ClientDialogueUI.DialogueLine> lines, Action onComplete = null)
    {
        if (clientDialogueUI == null)
            clientDialogueUI = ClientDialogueUI.Instance ?? FindAnyObjectByType<ClientDialogueUI>(FindObjectsInactive.Include);

        if (clientDialogueUI != null)
        {
            clientDialogueUI.StartDialogue(lines, onComplete);
        }
        else
        {
            Debug.LogWarning($"[DialogueManager] Brak ClientDialogueUI w scenie dla sekwencji dialogowej. Użyj menu: Tools -> Cyrulik -> Create Full Dialogue System");
        }
    }

    // ──────────────────────────────────────────────────────────
    // 3. DIALOGI KLIENTA: JUREK (FIRST CUSTOMER)
    // ──────────────────────────────────────────────────────────
    [Header("Jurek (First Customer) Dialogue")]
    [Tooltip("Kwestie dialogowe po podejściu gracza i wciśnięciu [E] przy Jurku.")]
    [SerializeField] private List<ClientDialogueUI.DialogueLine> jurekArrivalDialogue = new List<ClientDialogueUI.DialogueLine>()
    {
        new ClientDialogueUI.DialogueLine("Jurek", "Good day, I'd like a shave..."),
        new ClientDialogueUI.DialogueLine("Barber", "Right this way, please!"),
        new ClientDialogueUI.DialogueLine("Jurek", "I parked my car outside, nobody is going to drive out of the yard, right?"),
        new ClientDialogueUI.DialogueLine("Barber", "Not at all, sir! You can leave it there as long as you wish."),
        new ClientDialogueUI.DialogueLine("Jurek", "...")
    };

    [Tooltip("Kwestie dialogowe gdy Jurek dotrze do fotela i poprosi o szklankę wody.")]
    [SerializeField] private List<ClientDialogueUI.DialogueLine> jurekWaterDialogue = new List<ClientDialogueUI.DialogueLine>()
    {
        new ClientDialogueUI.DialogueLine("Jurek", "Ah, finally... but my throat is completely dry."),
        new ClientDialogueUI.DialogueLine("Jurek", "Could you pour me a glass of water before we start?"),
        new ClientDialogueUI.DialogueLine("Barber", "Of course, sir! Just a moment.")
    };

    [Tooltip("Kwestie dialogowe gdy gracz wręcza Jurkowi szklankę wody.")]
    [SerializeField] private List<ClientDialogueUI.DialogueLine> jurekThankForWaterDialogue = new List<ClientDialogueUI.DialogueLine>()
    {
        new ClientDialogueUI.DialogueLine("Jurek", "Ah, thank you! That hits the spot."),
        new ClientDialogueUI.DialogueLine("Barber", "So, how are we cutting your hair today, sir?"),
        new ClientDialogueUI.DialogueLine("Jurek", "I don't have much time today, I'd just like a clean shave."),
        new ClientDialogueUI.DialogueLine("Barber", "Understood. Let's get right to it!")
    };

    [Tooltip("Kwestia wypowiadana przez Jurka, gdy minie czas cierpliwości (np. 30s) i nikt do niego nie podchodzi.")]
    [TextArea(2, 4)]
    [SerializeField] private string jurekTimeoutComplaint = "How much longer am I supposed to stand here?! If nobody's going to serve me, I'm taking my business elsewhere!";

    [Tooltip("Kwestia wypowiadana przez Jurka, gdy w salonie jest za ciemno i brak muzyki (nieprzygotowana atmosfera).")]
    [TextArea(2, 4)]
    [SerializeField] private string jurekGloomyComplaint = "It's too dark for me to even go further... can you light this room?";

    [Tooltip("Kwestia wypowiadana przez Jurka, gdy zauważy mysz w salonie.")]
    [TextArea(2, 4)]
    [SerializeField] private string jurekMouseScareReaction = "Jesus Christ, a rat! In a barber shop?! I'm getting out of here right now!";

    [Tooltip("Kwestia wypowiadana przez Jurka, gdy mysz zatrzaśnie się w pułapce.")]
    [TextArea(2, 4)]
    [SerializeField] private string jurekMouseCaughtReaction = "Ohh my god mouse?! Take it out immediately!";

    /// <summary>
    /// Rozpoczyna powitalny dialog Jurka z graczem. Po zakończeniu dialogu wywoływany jest callback onComplete (marsz do fotela).
    /// </summary>
    public void StartJurekArrivalDialogue(Action onComplete = null)
    {
        StartClientConversation(jurekArrivalDialogue, onComplete);
    }

    /// <summary>
    /// Rozpoczyna dialog prośby o wodę przy fotelu.
    /// </summary>
    public void StartJurekWaterDialogue(Action onComplete = null)
    {
        StartClientConversation(jurekWaterDialogue, onComplete);
    }

    /// <summary>
    /// Rozpoczyna dialog podziękowania za wodę przy fotelu.
    /// </summary>
    public void StartJurekThankForWaterDialogue(Action onComplete = null)
    {
        StartClientConversation(jurekThankForWaterDialogue, onComplete);
    }

    /// <summary>
    /// Wyświetla kwestię zniecierpliwienia Jurka po upływie czasu oczekiwania.
    /// </summary>
    public void ShowJurekTimeoutDialogue(Action onComplete = null)
    {
        ShowClientLine("Jurek", jurekTimeoutComplaint, onComplete);
    }

    /// <summary>
    /// Wyświetla odmowę Jurka ze względu na zbyt ponurą / ciemną atmosferę.
    /// </summary>
    public void ShowJurekGloomyDialogue(Action onComplete = null)
    {
        ShowClientLine("Jurek", jurekGloomyComplaint, onComplete);
    }

    /// <summary>
    /// Wyświetla reakcję Jurka na mysz.
    /// </summary>
    public void ShowJurekMouseScareDialogue(Action onComplete = null)
    {
        ShowClientLine("Jurek", jurekMouseScareReaction, onComplete);
    }

    /// <summary>
    /// Wyświetla reakcję Jurka na złapaną w pułapkę mysz.
    /// </summary>
    public void ShowJurekMouseCaughtDialogue(Action onComplete = null)
    {
        ShowClientLine("Jurek", jurekMouseCaughtReaction, onComplete);
    }

    [ContextMenu("Reset Jurek Dialogue to Default (EN)")]
    public void ResetJurekDialogueToDefaultEN()
    {
        jurekArrivalDialogue = new List<ClientDialogueUI.DialogueLine>()
        {
            new ClientDialogueUI.DialogueLine("Jurek", "Good day, I'd like a shave..."),
            new ClientDialogueUI.DialogueLine("Barber", "Right this way, please!"),
            new ClientDialogueUI.DialogueLine("Jurek", "I parked my car outside, nobody is going to drive out of the yard, right?"),
            new ClientDialogueUI.DialogueLine("Barber", "Not at all, sir! You can leave it there as long as you wish."),
            new ClientDialogueUI.DialogueLine("Jurek", "...")
        };
        jurekWaterDialogue = new List<ClientDialogueUI.DialogueLine>()
        {
            new ClientDialogueUI.DialogueLine("Jurek", "Ah, finally... but my throat is completely dry."),
            new ClientDialogueUI.DialogueLine("Jurek", "Could you pour me a glass of water before we start?"),
            new ClientDialogueUI.DialogueLine("Barber", "Of course, sir! Just a moment.")
        };
        jurekThankForWaterDialogue = new List<ClientDialogueUI.DialogueLine>()
        {
            new ClientDialogueUI.DialogueLine("Jurek", "Ah, thank you! That hits the spot."),
            new ClientDialogueUI.DialogueLine("Barber", "So, how are we cutting your hair today, sir?"),
            new ClientDialogueUI.DialogueLine("Jurek", "I don't have much time today, I'd just like a clean shave."),
            new ClientDialogueUI.DialogueLine("Barber", "Understood. Let's get right to it!")
        };
        jurekTimeoutComplaint = "How much longer am I supposed to stand here?! If nobody's going to serve me, I'm taking my business elsewhere!";
        jurekGloomyComplaint = "It's too dark for me to even go further... can you light this room?";
        jurekMouseScareReaction = "Jesus Christ, a rat! In a barber shop?! I'm getting out of here right now!";
        Debug.Log("[DialogueManager] Zresetowano dialogi Jurka do domyślnych (EN)!");
    }
}
