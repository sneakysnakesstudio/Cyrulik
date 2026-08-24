using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Zarządza przełączaniem map akcji (Control Schemes / Action Maps) w Unity Input System.
/// Pozwala na płynne przełączanie między trybem rozgrywki ('Player') a interfejsem ('UI' / Minigra),
/// wyłączając ruch i rozglądanie się gracza podczas czytania myśli czy minigier,
/// jednocześnie umożliwiając interakcję klawiszem E, spacją czy myszą.
/// </summary>
public class InputModeManager : MonoBehaviour
{
    private static InputModeManager _instance;

    public static InputModeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<InputModeManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("InputModeManager");
                    _instance = go.AddComponent<InputModeManager>();
                }
            }
            return _instance;
        }
    }

    public enum ControlScheme
    {
        Player,
        UI,
        Minigame
    }

    [Header("Input Asset")]
    [Tooltip("Główny zasób Input Actions (np. InputSystem_Actions). Jeśli pusty, spróbuje wyszukać automatycznie.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Action Map Names")]
    [SerializeField] private string playerMapName = "Player";
    [SerializeField] private string uiMapName = "UI";

    [Header("State")]
    [SerializeField] private ControlScheme currentScheme = ControlScheme.Player;

    public ControlScheme CurrentScheme => currentScheme;
    public event Action<ControlScheme> OnControlSchemeChanged;

    private InputActionMap _playerMap;
    private InputActionMap _uiMap;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
    }
#endif

    private void Awake()
    {
        if (_instance != null && _instance != this && _instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        InitializeActionMaps();
    }

    private void Start()
    {
        // Ustaw domyślny schemat na start gry
        SetControlScheme(currentScheme, false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void InitializeActionMaps()
    {
        if (inputActions == null)
        {
            PlayerMovement pm = FindAnyObjectByType<PlayerMovement>();
            if (pm != null)
            {
                // Spróbuj pobrać z referencji akcji
                var field = typeof(PlayerMovement).GetField("moveAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var actionRef = field.GetValue(pm) as InputActionReference;
                    if (actionRef != null && actionRef.asset != null)
                    {
                        inputActions = actionRef.asset;
                    }
                }
            }
        }

        if (inputActions != null)
        {
            _playerMap = inputActions.FindActionMap(playerMapName, false);
            _uiMap = inputActions.FindActionMap(uiMapName, false);
        }
        else
        {
            Debug.LogWarning("[InputModeManager] Nie przypisano InputActionAsset. Przełączanie schematów będzie używać stanu kursora i flag.");
        }
    }

    /// <summary>
    /// Przełącza na schemat sterowania gracza (chodzenie, interakcja w świecie, zablokowany kursor).
    /// </summary>
    public void SwitchToPlayer()
    {
        SetControlScheme(ControlScheme.Player, false);
    }

    /// <summary>
    /// Przełącza na schemat UI (brak poruszania się, obsługa dialogów/przycisków).
    /// </summary>
    public void SwitchToUI(bool unlockCursor = false)
    {
        SetControlScheme(ControlScheme.UI, unlockCursor);
    }

    /// <summary>
    /// Przełącza na schemat minigry (np. minigra ostrzenia brzytwy).
    /// </summary>
    public void SwitchToMinigame(bool unlockCursor = false)
    {
        SetControlScheme(ControlScheme.Minigame, unlockCursor);
    }

    /// <summary>
    /// Główna metoda zmiany schematu sterowania.
    /// </summary>
    public void SetControlScheme(ControlScheme newScheme, bool unlockCursor = false)
    {
        currentScheme = newScheme;

        if (inputActions == null)
        {
            InitializeActionMaps();
        }

        switch (newScheme)
        {
            case ControlScheme.Player:
                if (_uiMap != null) _uiMap.Disable();
                if (_playerMap != null) _playerMap.Enable();

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;

            case ControlScheme.UI:
                if (_playerMap != null) _playerMap.Disable();
                if (_uiMap != null) _uiMap.Enable();

                if (unlockCursor)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                break;

            case ControlScheme.Minigame:
                // W minigrze blokujemy standardowy Player map, ale aktywujemy UI lub dedykowane akcje
                if (_playerMap != null) _playerMap.Disable();
                if (_uiMap != null) _uiMap.Enable();

                if (unlockCursor)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                break;
        }

//        Debug.Log($"[InputModeManager] Control scheme changed to: {newScheme} (Cursor unlocked: {unlockCursor})");
        OnControlSchemeChanged?.Invoke(newScheme);
    }
}
