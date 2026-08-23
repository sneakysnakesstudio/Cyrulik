using UnityEngine;

/// <summary>
/// Zadanie "Set the right mood" (proper_atmosphere).
/// Śledzi TYLKO te włączniki lamp oraz radio, które przeciągniesz do pól w Inspectorze.
/// Zadanie jest zaliczone tylko wtedy, gdy wszystkie dodane lampy ORAZ dodane radio są włączone.
/// </summary>
public class MultiSwitchTaskTracker : MonoBehaviour
{
    [Header("Task")]
    [Tooltip("ID zadania w PreparationStateManager (domyślnie 'proper_atmosphere').")]
    [SerializeField] private string taskId = "proper_atmosphere";

    [Header("Lights to Turn On")]
    [Tooltip("Przeciągnij tutaj włączniki lamp (LampSwitch), które gracz musi włączyć.")]
    [SerializeField] private LampSwitch[] switchesToTrack;

    [Header("Radio to Turn On")]
    [Tooltip("Przeciągnij tutaj radio (RadioInteractable), które gracz musi włączyć.")]
    [SerializeField] private RadioInteractable radioToTrack;

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Start()
    {
        CheckState();
    }

    private void SubscribeEvents()
    {
        if (switchesToTrack != null)
        {
            foreach (LampSwitch lampSwitch in switchesToTrack)
            {
                if (lampSwitch != null)
                {
                    lampSwitch.OnLightStateChanged += HandleStateChanged;
                }
            }
        }

        if (radioToTrack != null)
        {
            radioToTrack.OnRadioStateChanged += HandleStateChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (switchesToTrack != null)
        {
            foreach (LampSwitch lampSwitch in switchesToTrack)
            {
                if (lampSwitch != null)
                {
                    lampSwitch.OnLightStateChanged -= HandleStateChanged;
                }
            }
        }

        if (radioToTrack != null)
        {
            radioToTrack.OnRadioStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(bool state)
    {
        CheckState();
    }

    private void CheckState()
    {
        // 1. Sprawdź lampy (wszystkie przypisane włączniki muszą być ON)
        bool allLightsOn = true;
        if (switchesToTrack != null && switchesToTrack.Length > 0)
        {
            foreach (LampSwitch lampSwitch in switchesToTrack)
            {
                if (lampSwitch != null && !lampSwitch.IsOn)
                {
                    allLightsOn = false;
                    break;
                }
            }
        }

        // 2. Sprawdź radio (jeśli jest przypisane, musi być ON)
        bool radioOn = true;
        if (radioToTrack != null)
        {
            radioOn = radioToTrack.IsOn;
        }

        bool isAtmosphereReady = allLightsOn && radioOn;

        // Jeśli nic nie przypisano, nie zaliczaj
        if ((switchesToTrack == null || switchesToTrack.Length == 0) && radioToTrack == null)
        {
            isAtmosphereReady = false;
        }

        if (PreparationStateManager.Instance != null && !string.IsNullOrWhiteSpace(taskId))
        {
            PreparationStateManager.Instance.SetTaskState(taskId, isAtmosphereReady);
        }

        Debug.Log($"[AtmosphereTracker] Lights: {(allLightsOn ? "ON" : "OFF")}, Radio: {(radioOn ? "ON" : "OFF")} -> Task '{taskId}': {(isAtmosphereReady ? "COMPLETED" : "INCOMPLETE")}");
    }
}
