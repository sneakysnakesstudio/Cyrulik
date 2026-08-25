using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Zarządza klientem (Jurek) – jego pojawieniem się o określonej godzinie (17:01:33),
/// wejściem po schodach, otwarciem drzwi z dźwiękiem dzwoneczka, wejściem do salonu
/// oraz dialogiem powitalnym i reakcją na mysz.
/// </summary>
public class CustomerJurek : MonoBehaviour, IConditionalInteractable
{
    [Header("Postać / Wizualia Jurka")]
    [Tooltip("Główny obiekt z modelem Jurka (do włączenia/wyłączenia).")]
    [SerializeField] private GameObject jurekVisual;

    [Tooltip("Opcjonalny komponent Animator (do włączania animacji chodu i idle).")]
    [SerializeField] private Animator animator;
    [Tooltip("Nazwa parametru w Animatorze (np. 'IsWalking' jako Bool lub 'Speed' jako Float).")]
    [SerializeField] private string walkingAnimBool = "IsWalking";

    [Header("Punkt Startowy (Spawn / Wysiadka z auta)")]
    [Tooltip("Punkt w scenie, gdzie Jurek pojawia się na starcie (np. przy aucie). Jeśli pusty, użyje pierwszego punktu trasy schodów.")]
    [SerializeField] private Transform spawnPoint;

    public enum MovementSpeedMode
    {
        SpeedInMetersPerSecond, // Prędkość w m/s (automatycznie dostosowuje czas do odległości)
        FixedDurationInSeconds  // Sztywny czas w sekundach
    }

    [Header("Prędkość i Tempo Chodu")]
    [Tooltip("Tryb prędkości: SpeedInMetersPerSecond (zalecane – tempo stałe) lub FixedDurationInSeconds.")]
    [SerializeField] private MovementSpeedMode speedMode = MovementSpeedMode.SpeedInMetersPerSecond;

    [Range(0.2f, 4.0f)]
    [Tooltip("Prędkość marszu Jurka w metrach na sekundę (np. 0.8 - 1.2 m/s dla wolnego, spokojnego chodu).")]
    [SerializeField] private float walkSpeed = 1.0f;

    [Range(0.2f, 2.0f)]
    [Tooltip("Mnożnik prędkości odtwarzania animacji chodu (np. 0.8 - 1.0), aby tempo kroków pasowało do prędkości.")]
    [SerializeField] private float animationSpeed = 1.0f;

    [Header("Harmonogram Czasowy (Game Time)")]
    [Tooltip("Czy Jurek ma pojawiać się automatycznie o określonej godzinie w grze?")]
    [SerializeField] private bool triggerByGameTime = true;
    [SerializeField] private int arrivalHour = 17;
    [SerializeField] private int arrivalMinute = 1;
    [SerializeField] private int arrivalSecond = 33;

    [Header("Trasa 1: Wejście po schodach przed drzwi")]
    [Tooltip("Punkty trasy od dołu schodów (lub ulicy) aż pod same drzwi wejściowe.")]
    [SerializeField] private Transform[] stairsWaypoints;
    [Tooltip("Czas w sekundach potrzebny na wejście po schodach (używane gdy tryb to FixedDurationInSeconds).")]
    [SerializeField] private float stairsWalkDuration = 6.0f;

    [Header("Drzwi i Dzwonek")]
    [Tooltip("Komponent DoorInteractable drzwi wejściowych.")]
    [SerializeField] private DoorInteractable frontDoor;

    [Tooltip("Dźwięk dzwoneczka przy wejściu (AudioClip) - odtwarza się gdy Jurek otwiera drzwi.")]
    [SerializeField] private AudioClip doorBellClip;
    [SerializeField] private string soundDoorBell = "door_bell";

    [Tooltip("Dźwięk pukania do drzwi (jeśli używany zamiast dzwonka).")]
    [SerializeField] private string soundKnock = "door_knock";
    [SerializeField] private AudioClip customKnockClip;

    [Tooltip("Opóźnienie otwarcia drzwi po dotarciu Jurka pod wejście (w sekundach).")]
    [SerializeField] private float doorOpenDelay = 0.5f;

    [Header("Trasa 2: Wejście do środka salonu")]
    [Tooltip("Punkty trasy od progu drzwi, przez schody, aż do fotela fryzjerskiego.")]
    [SerializeField] private Transform[] insideWaypoints;
    [Tooltip("Czas przejścia od drzwi do miejsca docelowego w salonie (używane gdy tryb to FixedDurationInSeconds).")]
    [SerializeField] private float insideWalkDuration = 5.0f;

    [Tooltip("Typ ścieżki (Linear idealny na schody w linii prostej, CatmullRom dla zaokrąglonych łuków).")]
    [SerializeField] private PathType pathType = PathType.Linear;

    [Tooltip("Czy automatycznie zamknąć drzwi po wejściu Jurka do środka?")]
    [SerializeField] private bool autoCloseDoor = true;
    [SerializeField] private float autoCloseDoorDelay = 1.5f;

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
    [SerializeField] private float exitWalkDuration = 3.5f;

    [Header("Interakcja ręczna (jeśli nie auto-dialog)")]
    [SerializeField] private string interactionName = "Porozmawiaj z Jurkiem";

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onJurekSpawned;
    [SerializeField] private UnityEvent onJurekAtDoor;
    [SerializeField] private UnityEvent onJurekEntered;
    [SerializeField] private UnityEvent onJurekLeft;

    [Tooltip("Czy ukrywać model Jurka na starcie gry (odznacz to, jeśli chcesz widzieć postać cały czas podczas testowania w scenie)?")]
    [SerializeField] private bool hideOnStart = false;

    private bool _hasArrived = false;
    private bool _isWalking = false;
    private bool _hasLeft = false;
    private Tween _movementTween;

    public bool HasArrived => _hasArrived;
    public bool HasLeft => _hasLeft;
    public bool IsWalking => _isWalking;

    public bool CanInteract => _hasArrived && !_isWalking && !_hasLeft && (ClientDialogueUI.Instance == null || !ClientDialogueUI.Instance.IsDialogueActive);
    public string InteractionName => interactionName;

    private void Awake()
    {
        if (jurekVisual == null)
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                jurekVisual = smr.transform.parent != null && smr.transform.parent != transform ? smr.transform.parent.gameObject : smr.gameObject;
            }
            else
            {
                jurekVisual = gameObject;
            }
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (hideOnStart && jurekVisual != null && jurekVisual != gameObject)
        {
            jurekVisual.SetActive(false);
        }
        else
        {
            EnsureVisualsActive();
        }
    }

    private void Update()
    {
        // Sprawdzanie nadejścia wyznaczonej godziny w grze
        if (!_hasArrived && triggerByGameTime && GameTimeController.Instance != null)
        {
            if (GameTimeController.Instance.HasTimeReached(arrivalHour, arrivalMinute, arrivalSecond))
            {
                TriggerArrival();
            }
        }
    }

    /// <summary>
    /// Rozpoczyna sekwencję przyjścia Jurka (wejście po schodach -> dzwonek/drzwi -> wejście do środka).
    /// Może być wywołane przez czas (Update) lub przez zdarzenie w grze (np. MouseQuestManager).
    /// </summary>
    public void TriggerArrival()
    {
        if (_hasArrived) return;
        _hasArrived = true;

        // 1. Ustawienie pozycji startowej (spawnPoint lub pierwszy punkt schodów)
        Transform startTransform = spawnPoint != null ? spawnPoint : (stairsWaypoints != null && stairsWaypoints.Length > 0 ? stairsWaypoints[0] : null);
        if (startTransform != null)
        {
            transform.position = startTransform.position;
            transform.rotation = startTransform.rotation;
            Debug.Log($"[CustomerJurek] Jurek zespawnowany w punkcie '{startTransform.name}' na pozycji {transform.position}!");
        }
        else
        {
            Debug.LogWarning("[CustomerJurek] Brak przypisanego Spawn Point ani Stairs Waypoints! Jurek startuje z obecnej pozycji obiektu.");
        }

        onJurekSpawned?.Invoke();

        EnsureVisualsActive();

        // 2. Jeśli zdefiniowano trasę, Jurek idzie po wyznaczonych punktach
        if (stairsWaypoints != null && stairsWaypoints.Length > 0 && stairsWaypoints[0] != null)
        {
            float duration = GetMovementDuration(stairsWaypoints, stairsWalkDuration);
            WalkAlongWaypoints(stairsWaypoints, duration, OnReachedDoor);
        }
        else
        {
            // Brak schodów - od razu przejdź pod drzwi
            OnReachedDoor();
        }
    }

    /// <summary>
    /// Gwarantuje pełną widoczność modelu Jurka – włącza dzieci, naprawia SkinnedMeshRenderer bounds (updateWhenOffscreen),
    /// usuwa przesunięcia lokalne i weryfikuje warstwy renderowania.
    /// </summary>
    public void EnsureVisualsActive()
    {
        if (jurekVisual == null)
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                jurekVisual = smr.transform.parent != null && smr.transform.parent != transform ? smr.transform.parent.gameObject : smr.gameObject;
            }
            else
            {
                jurekVisual = gameObject;
            }
        }

        if (jurekVisual != null)
        {
            jurekVisual.SetActive(true);
            if (jurekVisual != gameObject)
            {
                jurekVisual.transform.localPosition = Vector3.zero;
                jurekVisual.transform.localRotation = Quaternion.identity;
            }

            if (jurekVisual.transform.localScale.sqrMagnitude < 0.0001f)
            {
                jurekVisual.transform.localScale = Vector3.one;
            }

            // 1. Aktywuj i napraw wszystkie SkinnedMeshRenderery (usuwa problem culling bounding boxa w Mixamo)
            SkinnedMeshRenderer[] smrs = jurekVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr != null)
                {
                    smr.gameObject.SetActive(true);
                    smr.enabled = true;
                    smr.updateWhenOffscreen = true; // KLUCZOWE: Zapobiega znikaniu siatki poza frustum kamery
                }
            }

            // 2. Aktywuj zwykłe MeshRenderery jeśli istnieją
            MeshRenderer[] mrs = jurekVisual.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in mrs)
            {
                if (mr != null)
                {
                    mr.gameObject.SetActive(true);
                    mr.enabled = true;
                }
            }

            // 3. Włącz wszystkie obiekty potomne
            Transform[] allChildren = jurekVisual.GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                if (child != null)
                {
                    child.gameObject.SetActive(true);
                }
            }

            Debug.Log($"[CustomerJurek] Aktywowano widoczność Jurka! Znaleziono {smrs.Length} SkinnedMeshRendererów.");
        }
    }

    /// <summary>
    /// Krok 2: Jurek dotarł pod drzwi wejściowe.
    /// Dźwięk dzwoneczka / pukania, odblokowanie i otwarcie drzwi, następnie wejście do środka.
    /// </summary>
    private void OnReachedDoor()
    {
        Debug.Log("[CustomerJurek] Jurek dotarł pod drzwi wejściowe.");
        onJurekAtDoor?.Invoke();

        SetWalkingAnimation(false);

        // Odtwórz dzwonek i pukanie
        PlayDoorBellSound();
        PlayKnockSound();

        // Odblokuj i otwórz drzwi po krótkiej chwili
        DOVirtual.DelayedCall(doorOpenDelay, () =>
        {
            if (frontDoor != null)
            {
                frontDoor.Unlock();
                frontDoor.OpenDoor();
            }

            // Krok 3: Wejście do środka salonu
            EnterSalon();
        }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    /// <summary>
    /// Krok 3: Jurek wchodzi przez otwarte drzwi do wnętrza salonu.
    /// </summary>
    private void EnterSalon()
    {
        if (insideWaypoints != null && insideWaypoints.Length > 0)
        {
            float duration = GetMovementDuration(insideWaypoints, insideWalkDuration);
            WalkAlongWaypoints(insideWaypoints, duration, OnFullyEnteredSalon);
        }
        else
        {
            OnFullyEnteredSalon();
        }
    }

    private float GetMovementDuration(Transform[] points, float fallbackDuration)
    {
        if (speedMode == MovementSpeedMode.FixedDurationInSeconds)
            return fallbackDuration;

        if (points == null || points.Length == 0 || walkSpeed <= 0.05f)
            return fallbackDuration;

        float totalDist = 0f;
        Vector3 currentPos = transform.position;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null)
            {
                totalDist += Vector3.Distance(currentPos, points[i].position);
                currentPos = points[i].position;
            }
        }

        return Mathf.Max(0.2f, totalDist / walkSpeed);
    }

    /// <summary>
    /// Krok 4: Jurek stanął na swoim miejscu docelowym w salonie.
    /// Zamknięcie drzwi za nim i rozpoczęcie powitalnego dialogu.
    /// </summary>
    private void OnFullyEnteredSalon()
    {
        Debug.Log("[CustomerJurek] Jurek wszedł do salonu i czeka na obsługę.");
        SetWalkingAnimation(false);
        onJurekEntered?.Invoke();

        // Opcjonalne zamknięcie drzwi za klientem
        if (autoCloseDoor && frontDoor != null)
        {
            DOVirtual.DelayedCall(autoCloseDoorDelay, () =>
            {
                if (frontDoor != null && frontDoor.IsOpen)
                {
                    frontDoor.CloseDoor();
                }
            }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        // Rozpoczęcie dialogu powitalnego
        if (autoTriggerDialogueOnArrival && ClientDialogueUI.Instance != null && arrivalDialogueLines != null && arrivalDialogueLines.Length > 0)
        {
            DOVirtual.DelayedCall(0.8f, StartArrivalDialogue)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    /// <summary>
    /// Płynne poruszanie się po tablicy punktów za pomocą DOTween wraz z obracaniem postaci.
    /// </summary>
    private void WalkAlongWaypoints(Transform[] points, float duration, Action onComplete)
    {
        if (points == null || points.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        _isWalking = true;
        SetWalkingAnimation(true);

        Vector3[] waypoints = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            waypoints[i] = points[i].position;
        }

        _movementTween?.Kill();

        if (points.Length == 1)
        {
            transform.DOLookAt(waypoints[0], 0.2f, AxisConstraint.Y);
            _movementTween = transform.DOMove(waypoints[0], duration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    _isWalking = false;
                    onComplete?.Invoke();
                });
        }
        else
        {
            _movementTween = transform.DOPath(waypoints, duration, pathType, PathMode.Full3D)
                .SetLookAt(0.05f)
                .SetEase(Ease.Linear)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    _isWalking = false;
                    onComplete?.Invoke();
                });
        }
    }

    private void SetWalkingAnimation(bool walking)
    {
        if (animator == null) return;

        animator.speed = walking ? animationSpeed : 1.0f;

        if (!string.IsNullOrEmpty(walkingAnimBool))
        {
            // Sprawdź typ parametru w Animatorze
            foreach (var param in animator.parameters)
            {
                if (param.name == walkingAnimBool)
                {
                    if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        animator.SetBool(walkingAnimBool, walking);
                    }
                    else if (param.type == AnimatorControllerParameterType.Float)
                    {
                        animator.SetFloat(walkingAnimBool, walking ? 1.0f : 0.0f);
                    }
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Wywoływane, gdy mysz przestraszy klienta (porażka – brak sera na pułapce).
    /// </summary>
    public void TriggerMouseScareAndLeave(Action onComplete = null)
    {
        if (_hasLeft) return;

        _hasLeft = true;
        _movementTween?.Kill();

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
        SetWalkingAnimation(true);

        if (frontDoor != null && !frontDoor.IsOpen)
        {
            frontDoor.Unlock();
            frontDoor.OpenDoor();
        }

        if (exitDestination != null)
        {
            transform.DOLookAt(exitDestination.position, 0.2f, AxisConstraint.Y);
            _movementTween = transform.DOMove(exitDestination.position, exitWalkDuration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    SetWalkingAnimation(false);
                    if (jurekVisual != null) jurekVisual.SetActive(false);
                    onJurekLeft?.Invoke();
                    onComplete?.Invoke();
                });
        }
        else
        {
            SetWalkingAnimation(false);
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

    private void PlayDoorBellSound()
    {
        if (doorBellClip != null)
        {
            AudioSource.PlayClipAtPoint(doorBellClip, transform.position);
            return;
        }

        if (!string.IsNullOrEmpty(soundDoorBell) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundDoorBell);
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

    #region Debug Overlay Helpers

    /// <summary>
    /// Natychmiastowe przeniesienie Jurka na szczyt schodów pod drzwi, odegranie dzwonka i wejście.
    /// </summary>
    public void ForceSpawnAtDoor()
    {
        _movementTween?.Kill();
        _hasLeft = false;
        _hasArrived = true;

        EnsureVisualsActive();

        if (stairsWaypoints != null && stairsWaypoints.Length > 0)
        {
            Transform topPoint = stairsWaypoints[stairsWaypoints.Length - 1];
            if (topPoint != null)
            {
                transform.position = topPoint.position;
                transform.rotation = topPoint.rotation;
            }
        }

        OnReachedDoor();
    }

    /// <summary>
    /// Natychmiastowe przeniesienie Jurka bezpośrednio do salonu (pominięcie marszu).
    /// </summary>
    public void ForceSpawnInsideSalon()
    {
        _movementTween?.Kill();
        _hasLeft = false;
        _hasArrived = true;
        _isWalking = false;

        EnsureVisualsActive();

        if (insideWaypoints != null && insideWaypoints.Length > 0)
        {
            Transform finalPoint = insideWaypoints[insideWaypoints.Length - 1];
            if (finalPoint != null)
            {
                transform.position = finalPoint.position;
                transform.rotation = finalPoint.rotation;
            }
        }

        if (frontDoor != null)
        {
            frontDoor.Unlock();
        }

        OnFullyEnteredSalon();
    }

    /// <summary>
    /// Resetuje stan Jurka do stanu początkowego (przydatne do wielokrotnych testów w DebugOverlay).
    /// </summary>
    public void ResetCustomerState()
    {
        _movementTween?.Kill();
        _hasArrived = false;
        _hasLeft = false;
        _isWalking = false;

        SetWalkingAnimation(false);

        if (jurekVisual != null && jurekVisual != gameObject)
        {
            jurekVisual.SetActive(false);
        }

        Transform startTransform = spawnPoint != null ? spawnPoint : (stairsWaypoints != null && stairsWaypoints.Length > 0 ? stairsWaypoints[0] : null);
        if (startTransform != null)
        {
            transform.position = startTransform.position;
            transform.rotation = startTransform.rotation;
        }

        Debug.Log("[CustomerJurek] Stan klienta został zresetowany do wartości początkowych.");
    }

    public void PlayDoorBellManual() => PlayDoorBellSound();
    public void PlayKnockManual() => PlayKnockSound();

    #endregion

    #region Gizmos & Visual Path Debug

    private void OnDrawGizmosSelected()
    {
        // 1. Punkt Startowy (Spawn Point) - Kolor błękitny / Cyan
        if (spawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.35f);
            Gizmos.DrawRay(spawnPoint.position, spawnPoint.forward * 0.8f);
        }

        // 2. Trasa Schodów (Stairs Waypoints) - Kolor żółty
        if (stairsWaypoints != null && stairsWaypoints.Length > 0)
        {
            Gizmos.color = Color.yellow;
            Vector3 prev = spawnPoint != null ? spawnPoint.position : (stairsWaypoints[0] != null ? stairsWaypoints[0].position : transform.position);

            for (int i = 0; i < stairsWaypoints.Length; i++)
            {
                if (stairsWaypoints[i] != null)
                {
                    Gizmos.DrawSphere(stairsWaypoints[i].position, 0.2f);
                    Gizmos.DrawLine(prev, stairsWaypoints[i].position);
                    prev = stairsWaypoints[i].position;
                }
            }
        }

        // 3. Trasa wewnątrz salonu (Inside Waypoints) - Kolor zielony
        if (insideWaypoints != null && insideWaypoints.Length > 0)
        {
            Gizmos.color = Color.green;
            Vector3 prev = stairsWaypoints != null && stairsWaypoints.Length > 0 && stairsWaypoints[stairsWaypoints.Length - 1] != null
                ? stairsWaypoints[stairsWaypoints.Length - 1].position
                : (spawnPoint != null ? spawnPoint.position : transform.position);

            for (int i = 0; i < insideWaypoints.Length; i++)
            {
                if (insideWaypoints[i] != null)
                {
                    Gizmos.DrawSphere(insideWaypoints[i].position, 0.2f);
                    Gizmos.DrawLine(prev, insideWaypoints[i].position);
                    prev = insideWaypoints[i].position;
                }
            }
        }

        // 4. Punkt ucieczki (Exit Destination) - Kolor czerwony
        if (exitDestination != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(exitDestination.position, new Vector3(0.5f, 1.5f, 0.5f));
        }
    }

    #endregion

    private void OnDestroy()
    {
        _movementTween?.Kill();
    }
}

