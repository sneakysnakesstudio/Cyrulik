using UnityEngine;

public class MultiSwitchTaskTracker : MonoBehaviour
{
    [Header("Task")]
    [Tooltip("ID głównego zadania w PreparationStateManager (np. 'lights').")]
    [SerializeField] private string taskId = "lights";

    [Header("Switches")]
    [Tooltip("Lista przełączników, które muszą być WŁĄCZONE, aby zaliczyć zadanie.")]
    [SerializeField] private LampSwitch[] switchesToTrack;

    private void OnEnable()
    {
        if (switchesToTrack == null) return;
        
        foreach (LampSwitch lampSwitch in switchesToTrack)
        {
            if (lampSwitch != null)
            {
                lampSwitch.OnLightStateChanged += HandleSwitchChanged;
            }
        }
    }

    private void OnDisable()
    {
        if (switchesToTrack == null) return;

        foreach (LampSwitch lampSwitch in switchesToTrack)
        {
            if (lampSwitch != null)
            {
                lampSwitch.OnLightStateChanged -= HandleSwitchChanged;
            }
        }
    }

    private void Start()
    {
        CheckState();
    }

    private void HandleSwitchChanged(bool isOn)
    {
        CheckState();
    }

    private void CheckState()
    {
        if (switchesToTrack == null || switchesToTrack.Length == 0) 
            return;

        bool allOn = true;
        foreach (LampSwitch lampSwitch in switchesToTrack)
        {
            if (lampSwitch != null && !lampSwitch.IsOn)
            {
                allOn = false;
                break;
            }
        }

        if (PreparationStateManager.Instance != null && !string.IsNullOrWhiteSpace(taskId))
        {
            PreparationStateManager.Instance.SetTaskState(taskId, allOn);
        }
    }
}
