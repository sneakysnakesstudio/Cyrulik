using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Kontroluje podgląd zegarka na nadgarstku pod klawiszem [Q].
/// Gdy gracz wciśnie [Q], lewy nadgarstek / zegarek unosi się płynnie przed kamerę FPP,
/// a wskazówki (lub wyświetlacz) pokazują dokładny czas z GameTimeController w czasie rzeczywistym.
/// </summary>
public class WristwatchController : MonoBehaviour
{
    public static WristwatchController Instance { get; private set; }

    [Header("Watch State")]
    [Tooltip("Czy gracz założył już zegarek (podniósł z biurka)?")]
    [SerializeField] private bool hasWatchEquipped = true;

    [Header("Visual Elements")]
    [Tooltip("Główny obiekt zegarka pod kamerą gracza (HoldPoint/Wristwatch).")]
    [SerializeField] private GameObject watchVisualRoot;

    [Tooltip("Opcjonalny transform wskazówki godzinowej (obraca się w osi Z lub Y).")]
    [SerializeField] private Transform hourHandTransform;

    [Tooltip("Opcjonalny transform wskazówki minutowej.")]
    [SerializeField] private Transform minuteHandTransform;

    [Tooltip("Opcjonalny transform wskazówki sekundowej.")]
    [SerializeField] private Transform secondHandTransform;

    [Tooltip("Opcjonalny tekst cyfrowy czasu na tarczy (np. '16:58:30').")]
    [SerializeField] private TextMeshProUGUI digitalTimeText;

    [Header("Animation Settings")]
    [Tooltip("Pozycja schowanego zegarka (poza widokiem u dołu ekranu).")]
    [SerializeField] private Vector3 hiddenLocalPosition = new Vector3(-0.25f, -0.45f, 0.4f);
    [SerializeField] private Vector3 hiddenLocalRotation = new Vector3(45f, 20f, -30f);

    [Tooltip("Pozycja uniesionego zegarka (przed oczami gracza).")]
    [SerializeField] private Vector3 visibleLocalPosition = new Vector3(-0.18f, -0.15f, 0.38f);
    [SerializeField] private Vector3 visibleLocalRotation = new Vector3(10f, 15f, -5f);

    [SerializeField] private float transitionDuration = 0.22f;

    [Header("Audio")]
    [SerializeField] private string raiseSound = "cloth_pickup";
    [SerializeField] private string lowerSound = "item_drop";
    [SerializeField] private AudioClip customRaiseClip;

    private bool _isLookingAtWatch = false;
    private Tween _moveTween;
    private Tween _rotTween;

    public bool HasWatchEquipped => hasWatchEquipped;
    public bool IsLookingAtWatch => _isLookingAtWatch;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (watchVisualRoot == null)
        {
            // Spróbuj znaleźć pod kamerą
            Transform cam = GetComponentInChildren<Camera>()?.transform ?? transform;
            Transform found = cam.Find("Wristwatch") ?? cam.Find("HoldPoint/Wristwatch");
            if (found != null)
            {
                watchVisualRoot = found.gameObject;
            }
        }

        if (watchVisualRoot != null)
        {
            watchVisualRoot.transform.localPosition = hiddenLocalPosition;
            watchVisualRoot.transform.localRotation = Quaternion.Euler(hiddenLocalRotation);
            watchVisualRoot.SetActive(hasWatchEquipped);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _moveTween?.Kill();
        _rotTween?.Kill();
    }

    private void Update()
    {
        if (!hasWatchEquipped) return;

        // Klawisz Q (Input System lub klasyczny)
        bool qPressed = false;
        if (Keyboard.current != null)
        {
            qPressed = Keyboard.current.qKey.isPressed;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.Q)) qPressed = true;
#endif

        if (qPressed && !_isLookingAtWatch)
        {
            SetLookingAtWatch(true);
        }
        else if (!qPressed && _isLookingAtWatch)
        {
            SetLookingAtWatch(false);
        }

        // Aktualizacja tarczy zegarka
        if (_isLookingAtWatch || (watchVisualRoot != null && watchVisualRoot.activeSelf))
        {
            UpdateWatchDial();
        }
    }

    /// <summary>
    /// Unosi lub opuszcza zegarek przed oczyma gracza.
    /// </summary>
    public void SetLookingAtWatch(bool looking)
    {
        _isLookingAtWatch = looking;

        if (watchVisualRoot == null) return;

        watchVisualRoot.SetActive(true);

        _moveTween?.Kill();
        _rotTween?.Kill();

        Vector3 targetPos = looking ? visibleLocalPosition : hiddenLocalPosition;
        Vector3 targetRot = looking ? visibleLocalRotation : hiddenLocalRotation;

        _moveTween = watchVisualRoot.transform
            .DOLocalMove(targetPos, transitionDuration)
            .SetEase(looking ? Ease.OutCubic : Ease.InCubic)
            .SetUpdate(true);

        _rotTween = watchVisualRoot.transform
            .DOLocalRotate(targetRot, transitionDuration)
            .SetEase(looking ? Ease.OutCubic : Ease.InCubic)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (!looking && !hasWatchEquipped)
                {
                    watchVisualRoot.SetActive(false);
                }
            });

        // Dźwięk ruchu dłoni
        if (looking && AudioManager.Instance != null && !string.IsNullOrEmpty(raiseSound))
        {
            AudioManager.Instance.Play(raiseSound);
        }
    }

    /// <summary>
    /// Obraca wskazówki zegarka synchronicznie z czasem w grze.
    /// </summary>
    private void UpdateWatchDial()
    {
        int hour = 16;
        int minute = 58;
        int second = 0;

        if (GameTimeController.Instance != null)
        {
            hour = GameTimeController.Instance.Hour;
            minute = GameTimeController.Instance.Minute;
            second = GameTimeController.Instance.Second;
        }
        else
        {
            DateTime now = DateTime.Now;
            hour = now.Hour;
            minute = now.Minute;
            second = now.Second;
        }

        // Wyświetlacz cyfrowy (jeśli istnieje)
        if (digitalTimeText != null)
        {
            digitalTimeText.text = $"{hour:00}:{minute:00}:{second:00}";
        }

        // Wskazówki analogowe (kąty 360 stopni)
        if (hourHandTransform != null)
        {
            float hourAngle = (hour % 12 + minute / 60f) * 30f; // 360 / 12 = 30 st/h
            hourHandTransform.localRotation = Quaternion.Euler(0f, 0f, -hourAngle);
        }

        if (minuteHandTransform != null)
        {
            float minAngle = (minute + second / 60f) * 6f; // 360 / 60 = 6 st/min
            minuteHandTransform.localRotation = Quaternion.Euler(0f, 0f, -minAngle);
        }

        if (secondHandTransform != null)
        {
            float secAngle = second * 6f; // 360 / 60 = 6 st/sec
            secondHandTransform.localRotation = Quaternion.Euler(0f, 0f, -secAngle);
        }
    }

    /// <summary>
    /// Wywoływane po podniesieniu/założeniu zegarka z biurka.
    /// </summary>
    public void EquipWatch()
    {
        hasWatchEquipped = true;
        if (watchVisualRoot != null)
        {
            watchVisualRoot.SetActive(true);
            watchVisualRoot.transform.localPosition = hiddenLocalPosition;
            watchVisualRoot.transform.localRotation = Quaternion.Euler(hiddenLocalRotation);
        }
        Debug.Log("<color=#F4D06F>[WristwatchController] Zegarek założony na nadgarstek! Wciśnij [Q], aby sprawdzić godzinę.</color>");
    }
}
