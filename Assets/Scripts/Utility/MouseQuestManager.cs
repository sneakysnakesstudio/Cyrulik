using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Główny manager zadania z pułapką na myszy (MouseTrap Quest).
/// Zarządza 3 lokacjami pułapek, rozgałęzieniem przed rozpoczęciem golenia
/// oraz wyzwalaniem pojawienia się Jurka po wyrzuceniu myszy do kosza.
/// </summary>
public class MouseQuestManager : MonoBehaviour
{
    public static MouseQuestManager Instance { get; private set; }

    public enum TrapSpawnMode
    {
        AllTrapsActive,             // Wszystkie 3 pułapki są na scenie, gracz może uzbroić dowolną z nich
        RandomSingleTrapLocation    // Losowana jest 1 z 3 lokacji na starcie gry dla pojedynczej pułapki
    }

    [Header("Konfiguracja 3 Lokacji")]
    [Tooltip("Tryb działania lokacji pułapek.")]
    [SerializeField] private TrapSpawnMode spawnMode = TrapSpawnMode.AllTrapsActive;

    [Tooltip("Lista pułapek na scenie (lub 3 referencje).")]
    [SerializeField] private MouseTrap[] mouseTraps;

    [Tooltip("Dla trybu losowego: 3 punkty Transform w salonie, do których można przenieść 1 pułapkę.")]
    [SerializeField] private Transform[] trapLocationPoints;

    [Header("Referencje do Aktorów Questu")]
    [Tooltip("Skrypt poruszający uciekającą myszą po trasie (dla gałęzi porażki).")]
    [SerializeField] private MouseRunner mouseRunner;

    [Tooltip("Skrypt klienta Jurka przy drzwiach.")]
    [SerializeField] private CustomerJurek customerJurek;

    [Header("Dialogi i Przemyślenia Cyrulika (InnerDialogue)")]
    [SerializeField] private string dialogueTrapSnapped = "Coś trzasnęło w pułapce... Lepiej wyrzucę tę mysz do kosza w głównym pokoju.";
    [SerializeField] private string dialogueMouseEscape = "O cholera, mysz! Klient to zobaczył...";

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onTrapArmed;
    [SerializeField] private UnityEvent onMouseCaught;
    [SerializeField] private UnityEvent onMouseEscaped;
    [SerializeField] private UnityEvent onQuestCompleted;

    private int _selectedLocationIndex = 0;
    private bool _hasShavingStarted = false;
    private bool _questFinished = false;

    public bool HasShavingStarted => _hasShavingStarted;
    public bool QuestFinished => _questFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        SetupTrapLocations();
    }

    private void SetupTrapLocations()
    {
        if (spawnMode == TrapSpawnMode.RandomSingleTrapLocation && trapLocationPoints != null && trapLocationPoints.Length > 0)
        {
            _selectedLocationIndex = UnityEngine.Random.Range(0, trapLocationPoints.Length);
            Transform chosenPoint = trapLocationPoints[_selectedLocationIndex];

            if (mouseTraps != null && mouseTraps.Length > 0 && mouseTraps[0] != null)
            {
                mouseTraps[0].transform.position = chosenPoint.position;
                mouseTraps[0].transform.rotation = chosenPoint.rotation;
                Debug.Log($"[MouseQuestManager] Pułapka ustawiona w lokacji {_selectedLocationIndex + 1}/{trapLocationPoints.Length}");
            }
        }
    }

    /// <summary>
    /// Sprawdza, czy którakolwiek z pułapek na myszy została uzbrojona serem.
    /// </summary>
    public bool IsAnyTrapArmed()
    {
        if (mouseTraps == null || mouseTraps.Length == 0)
        {
            MouseTrap trapInScene = FindAnyObjectByType<MouseTrap>();
            return trapInScene != null && trapInScene.IsArmed;
        }

        foreach (var trap in mouseTraps)
        {
            if (trap != null && trap.IsArmed)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Wywoływane, gdy gracz zaczyna golenie / przygotowanie stanowiska.
    /// Sprawdza warunek pułapki i uruchamia właściwą gałąź:
    /// - SUKCES: zatrzaśnięcie pułapki (CatchMouse).
    /// - PORAŻKA: mysz przebiega obok fotela i płoszy klienta.
    /// </summary>
    public void OnShavingStarted()
    {
        if (_hasShavingStarted) return;
        _hasShavingStarted = true;

        bool isArmed = IsAnyTrapArmed();

        if (isArmed)
        {
            // === GAŁĄŹ SUKCESU ===
            Debug.Log("[MouseQuestManager] Golenie rozpoczęte z uzbrojoną pułapką -> Zatrzaśnięcie myszy!");

            // Zatrzaśnij uzbrojone pułapki
            if (mouseTraps != null)
            {
                foreach (var trap in mouseTraps)
                {
                    if (trap != null && trap.IsArmed)
                    {
                        trap.CatchMouse();
                    }
                }
            }
            else
            {
                MouseTrap trapInScene = FindAnyObjectByType<MouseTrap>();
                trapInScene?.CatchMouse();
            }

            if (InnerDialogueUI.Instance != null && !string.IsNullOrEmpty(dialogueTrapSnapped))
            {
                InnerDialogueUI.Instance.ShowMessage(dialogueTrapSnapped);
            }

            onMouseCaught?.Invoke();
        }
        else
        {
            // === GAŁĄŹ PORAŻKI (Brak sera na pułapce) ===
            Debug.LogWarning("[MouseQuestManager] Golenie rozpoczęte BEZ pułapki! Mysz ucieka i płoszy klienta.");

            if (InnerDialogueUI.Instance != null && !string.IsNullOrEmpty(dialogueMouseEscape))
            {
                InnerDialogueUI.Instance.ShowMessage(dialogueMouseEscape);
            }

            // Uruchom bieg myszy
            if (mouseRunner != null)
            {
                mouseRunner.StartRunning(() =>
                {
                    // Po przebiegnięciu myszy klient ucieka
                    if (customerJurek != null)
                    {
                        customerJurek.TriggerMouseScareAndLeave();
                    }
                });
            }
            else if (customerJurek != null)
            {
                customerJurek.TriggerMouseScareAndLeave();
            }

            onMouseEscaped?.Invoke();
        }
    }

    /// <summary>
    /// Wywoływane przez TrashBinInteractable po wrzuceniu złapanej myszy do kosza.
    /// Aktywuje klienta Jurka przy drzwiach.
    /// </summary>
    public void OnMouseDisposed()
    {
        if (_questFinished) return;
        _questFinished = true;

        Debug.Log("[MouseQuestManager] Mysz w koszu -> Aktywacja Jurka przy drzwiach!");

        if (customerJurek != null)
        {
            customerJurek.TriggerArrival();
        }
        else
        {
            CustomerJurek jurekInScene = FindAnyObjectByType<CustomerJurek>();
            if (jurekInScene != null)
            {
                jurekInScene.TriggerArrival();
            }
        }

        onQuestCompleted?.Invoke();
    }
}
