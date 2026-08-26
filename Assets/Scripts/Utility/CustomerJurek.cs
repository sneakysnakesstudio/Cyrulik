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
    [Tooltip("Punkt w scenie, gdzie Jurek pojawia się na starcie (np. przy aucie).")]
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

    [Tooltip("Typ ścieżki (Linear idealny na schody w linii prostej, CatmullRom dla zaokrąglonych łuków).")]
    [SerializeField] private PathType pathType = PathType.Linear;

    [Header("Harmonogram Czasowy (Game Time)")]
    [Tooltip("Czy Jurek ma pojawiać się automatycznie o określonej godzinie w grze?")]
    [SerializeField] private bool triggerByGameTime = true;
    [SerializeField] private int arrivalHour = 17;
    [SerializeField] private int arrivalMinute = 1;
    [SerializeField] private int arrivalSecond = 33;

    [Header("1. Trasa: Od auta do początku schodków")]
    [Tooltip("Punkty trasy od auta (spawnPoint) aż do dolnego stopnia schodów na zewnątrz.")]
    [SerializeField] private Transform[] approachWaypoints;
    [SerializeField] private float approachWalkDuration = 4.0f;

    [Header("2. Trasa: Po zewnętrznych schodkach do drzwi")]
    [Tooltip("Punkty trasy wspinania się po zewnętrznych schodkach pod same drzwi.")]
    [SerializeField] private Transform[] stairsWaypoints;
    [SerializeField] private float stairsWalkDuration = 4.0f;

    [Header("Drzwi wejściowe i Dzwonek")]
    [Tooltip("Komponent DoorInteractable drzwi wejściowych.")]
    [SerializeField] private DoorInteractable frontDoor;
    [SerializeField] private AudioClip doorBellClip;
    [SerializeField] private string soundDoorBell = "door_bell";
    [SerializeField] private string soundKnock = "door_knock";
    [SerializeField] private AudioClip customKnockClip;
    [SerializeField] private float doorOpenDelay = 0.5f;
    [SerializeField] private bool autoCloseDoor = true;
    [Tooltip("Czas w sekundach po wejściu przez próg drzwi, po którym drzwi się zamykają (domyślnie 1.0s).")]
    [SerializeField] private float autoCloseDoorDelay = 1.0f;

    [Header("Dźwięki kroków (Footsteps SFX)")]
    [Tooltip("Czy Jurek ma odtwarzać dźwięki kroków podczas chodu?")]
    [SerializeField] private bool playFootsteps = true;
    [Tooltip("Interwał czasowy między krokami w sekundach (domyślnie 0.45s).")]
    [SerializeField] private float footstepInterval = 0.45f;
    [Tooltip("Nazwa grupy dźwięków w AudioManager dla kroków (np. 'player_steps').")]
    [SerializeField] private string footstepSoundGroup = "player_steps";
    [Tooltip("Opcjonalne bezpośrednie AudioClipy kroków (zostaw puste aby używać AudioManager).")]
    [SerializeField] private AudioClip[] customFootstepClips;
    [Range(0f, 1f)]
    [SerializeField] private float footstepVolume = 0.7f;

    [Header("Wejście do salonu -> Waiting Point (Dół salonu)")]
    [Tooltip("Opcjonalne punkty przejścia tuż za progiem drzwi do punktu oczekiwania.")]
    [SerializeField] private Transform[] insideEntranceWaypoints;
    [Tooltip("Docelowy punkt na dole salonu, gdzie Jurek staje i czeka na podejście gracza.")]
    [SerializeField] private Transform waitingSpot;
    [SerializeField] private float waitingSpotWalkDuration = 3.0f;

    [Header("Oczekiwanie na gracza (Patience Timer)")]
    [Tooltip("Czy włączyć odliczanie czasu cierpliwości Jurka?")]
    [SerializeField] private bool usePatienceTimer = true;
    [Tooltip("Czas w sekundach, przez jaki Jurek czeka na interakcję gracza (np. 30s).")]
    [SerializeField] private float patienceDuration = 30.0f;
    [Tooltip("Czy Jurek ma wyjść z salonu po upływie czasu oczekiwania?")]
    [SerializeField] private bool leaveOnTimeout = true;

    [Header("Śledzenie gracza wzrokiem (Look At Player)")]
    [Tooltip("Czy Jurek podczas oczekiwania w salonie ma płynnie obracać się w stronę podchodzącego gracza?")]
    [SerializeField] private bool lookAtPlayerWhileWaiting = true;
    [Tooltip("Prędkość płynnego obrotu w stronę gracza.")]
    [SerializeField] private float rotationSpeedTowardsPlayer = 4.5f;
    [Tooltip("Transform gracza (jeśli pusty, skrypt znajdzie go automatycznie).")]
    [SerializeField] private Transform playerTransform;

    [Header("Interakcja z graczem")]
    [SerializeField] private string interactionName = "Talk to Jurek";

    [Header("Wymóg Przygotowania Salonu (Atmosfera)")]
    [Tooltip("Czy Jurek wymaga włączonych świateł i radia przed rozpoczęciem usługi?")]
    [SerializeField] private bool requireAtmosphere = true;
    [Tooltip("ID zadania w PreparationStateManager (domyślnie 'proper_atmosphere').")]
    [SerializeField] private string atmosphereTaskId = "proper_atmosphere";
    [TextArea(2, 4)]
    [SerializeField] private string gloomyFailReason = "The client felt the atmosphere was too gloomy and left.";

    [Header("3. Trasa: Po wewnętrznych schodkach do Fotela (Górny podest)")]
    [Tooltip("Punkty trasy prowadzące od Waiting Pointu, po wewnętrznych schodkach na podest (dodaj tutaj punkty schodków!).")]
    [SerializeField] private Transform[] toChairWaypoints;
    [Tooltip("Docelowy punkt przy stanowisku fryzjerskim / fotelu.")]
    [SerializeField] private Transform chairSpot;
    [SerializeField] private float chairWalkDuration = 4.5f;

    [Header("Trasa Wyjścia (Fail Branch)")]
    [SerializeField] private Transform exitDestination;
    [SerializeField] private float exitWalkDuration = 3.5f;

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onJurekSpawned;
    [SerializeField] private UnityEvent onJurekAtDoor;
    [SerializeField] private UnityEvent onJurekReachedWaitingSpot;
    [SerializeField] private UnityEvent onPlayerInteracted;
    [SerializeField] private UnityEvent onDialogueCompleted;
    [SerializeField] private UnityEvent onReachedBarberChair;
    [SerializeField] private UnityEvent onPatienceTimeout;
    [SerializeField] private UnityEvent onJurekLeft;

    [Tooltip("Czy ukrywać model Jurka na starcie gry (odznacz to, jeśli chcesz widzieć postać cały czas podczas testowania w scenie)?")]
    [SerializeField] private bool hideOnStart = false;

    private bool _hasArrived = false;
    private bool _isWalking = false;
    private bool _hasLeft = false;
    private bool _isWaitingForPlayer = false;
    private bool _hasInteractedWithPlayer = false;
    private bool _hasReachedChair = false;
    private float _patienceRemaining = 0f;
    private float _footstepTimer = 0f;
    private Tween _movementTween;

    public bool HasArrived => _hasArrived;
    public bool HasLeft => _hasLeft;
    public bool IsWalking => _isWalking;
    public bool IsWaitingForPlayer => _isWaitingForPlayer;
    public bool HasReachedChair => _hasReachedChair;
    public float PatienceRemaining => _patienceRemaining;

    public bool CanInteract => _hasArrived && !_isWalking && !_hasLeft && !_hasInteractedWithPlayer && (ClientDialogueUI.Instance == null || !ClientDialogueUI.Instance.IsDialogueActive);
    public string InteractionName => interactionName;
    public string BlockedMessage => null;

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

        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                var pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null) playerTransform = pm.transform;
            }
        }

        // Zapewnij obecność Collisera do detekcji promienia interakcji gracza
        if (GetComponent<Collider>() == null && GetComponentInChildren<Collider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.35f;
            col.height = 1.8f;
        }

        // Ustaw warstwę Interactable jeśli istnieje
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer != -1 && gameObject.layer == 0)
        {
            gameObject.layer = interactableLayer;
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
        // 1. Sprawdzanie nadejścia wyznaczonej godziny w grze
        if (!_hasArrived && triggerByGameTime && GameTimeController.Instance != null)
        {
            if (GameTimeController.Instance.HasTimeReached(arrivalHour, arrivalMinute, arrivalSecond))
            {
                TriggerArrival();
            }
        }

        // 2. Oczekiwanie na gracza w salonie + płynne obracanie się twarzą do gracza
        if (_isWaitingForPlayer && !_hasInteractedWithPlayer && !_hasLeft)
        {
            if (usePatienceTimer)
            {
                _patienceRemaining -= Time.deltaTime;
                if (_patienceRemaining <= 0f)
                {
                    TriggerPatienceTimeout();
                    return;
                }
            }

            // Płynne śledzenie gracza wzrokiem (obrót wokół osi Y)
            if (lookAtPlayerWhileWaiting && playerTransform != null)
            {
                Vector3 lookDir = playerTransform.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.05f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeedTowardsPlayer);
                }
            }
        }

        // 3. Odtwarzanie dźwięków kroków podczas marszu
        if (_isWalking && playFootsteps)
        {
            _footstepTimer -= Time.deltaTime * Mathf.Max(0.2f, animationSpeed);
            if (_footstepTimer <= 0f)
            {
                _footstepTimer = footstepInterval;
                PlayFootstepSound();
            }
        }
        else
        {
            _footstepTimer = 0.05f;
        }
    }

    /// <summary>
    /// Rozpoczyna sekwencję przyjścia Jurka.
    /// Krok 1: Trasa od auta do początku schodków.
    /// </summary>
    public void TriggerArrival()
    {
        if (_hasArrived) return;
        _hasArrived = true;

        // 1. Ustawienie pozycji startowej przy aucie
        Transform startTransform = spawnPoint != null ? spawnPoint : (approachWaypoints != null && approachWaypoints.Length > 0 ? approachWaypoints[0] : (stairsWaypoints != null && stairsWaypoints.Length > 0 ? stairsWaypoints[0] : null));
        if (startTransform != null)
        {
            transform.position = startTransform.position;
            transform.rotation = startTransform.rotation;
            Debug.Log($"[CustomerJurek] Jurek zespawnowany w punkcie '{startTransform.name}' na pozycji {transform.position}!");
        }
        else
        {
            Debug.LogWarning("[CustomerJurek] Brak przypisanego Spawn Point! Jurek startuje z obecnej pozycji obiektu.");
        }

        onJurekSpawned?.Invoke();

        EnsureVisualsActive();

        // 2. Krok 1: Marsz od auta do początku schodków
        if (approachWaypoints != null && approachWaypoints.Length > 0 && approachWaypoints[0] != null)
        {
            float duration = GetMovementDuration(approachWaypoints, approachWalkDuration);
            WalkAlongWaypoints(approachWaypoints, duration, WalkUpStairsToDoor);
        }
        else
        {
            WalkUpStairsToDoor();
        }
    }

    /// <summary>
    /// Krok 2: Marsz po zewnętrznych schodkach pod same drzwi.
    /// </summary>
    private void WalkUpStairsToDoor()
    {
        if (stairsWaypoints != null && stairsWaypoints.Length > 0 && stairsWaypoints[0] != null)
        {
            float duration = GetMovementDuration(stairsWaypoints, stairsWalkDuration);
            WalkAlongWaypoints(stairsWaypoints, duration, OnReachedDoor);
        }
        else
        {
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
    /// Krok 3: Jurek dotarł pod drzwi wejściowe.
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

            // Krok 4: Wejście do środka salonu do Waiting Pointu
            EnterSalonToWaitingSpot();
        }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    /// <summary>
    /// Krok 4: Jurek wchodzi przez otwarte drzwi do wnętrza salonu i idzie do Waiting Pointu.
    /// </summary>
    private void EnterSalonToWaitingSpot()
    {
        // Zamknij drzwi 1 sekundę po tym, jak Jurek przekroczy próg wejścia
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

        List<Transform> route = new List<Transform>();
        if (insideEntranceWaypoints != null && insideEntranceWaypoints.Length > 0)
        {
            foreach (var wp in insideEntranceWaypoints)
            {
                if (wp != null) route.Add(wp);
            }
        }

        if (waitingSpot != null && (route.Count == 0 || route[route.Count - 1] != waitingSpot))
        {
            route.Add(waitingSpot);
        }

        if (route.Count > 0)
        {
            float duration = GetMovementDuration(route.ToArray(), waitingSpotWalkDuration);
            WalkAlongWaypoints(route.ToArray(), duration, OnFullyReachedWaitingSpot);
        }
        else
        {
            OnFullyReachedWaitingSpot();
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
    /// Krok 5: Jurek zajął miejsce w Waiting Point (dół salonu).
    /// Zatrzymuje się, obraca twarzą do pokoju i czeka na interakcję gracza (np. 30s).
    /// </summary>
    private void OnFullyReachedWaitingSpot()
    {
        Debug.Log("[CustomerJurek] Jurek dotarł do Waiting Pointu i czeka na interakcję gracza.");
        SetWalkingAnimation(false);

        // Płynne obrócenie w wyznaczoną stronę (bez teleportu pozycji!)
        if (waitingSpot != null)
        {
            transform.DORotateQuaternion(Quaternion.Euler(0f, waitingSpot.rotation.eulerAngles.y, 0f), 0.35f)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        onJurekReachedWaitingSpot?.Invoke();

        // Rozpoczęcie odliczania czasu cierpliwości na podejście gracza
        _hasInteractedWithPlayer = false;
        _isWaitingForPlayer = true;
        _patienceRemaining = patienceDuration;

        Debug.Log($"[CustomerJurek] Jurek oczekuje na gracza przez {patienceDuration:0} sekund. Podejdź i naciśnij [E]!");
    }

    /// <summary>
    /// Wywoływane gdy minie czas cierpliwości (30s) i gracz nie podszedł do Jurka.
    /// </summary>
    private void TriggerPatienceTimeout()
    {
        if (!_isWaitingForPlayer || _hasInteractedWithPlayer || _hasLeft) return;

        _isWaitingForPlayer = false;
        Debug.Log("[CustomerJurek] Upłynął czas cierpliwości Jurka (brak interakcji gracza w wyznaczonym czasie)!");

        onPatienceTimeout?.Invoke();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowJurekTimeoutDialogue(() =>
            {
                if (leaveOnTimeout)
                {
                    WalkOut(() => ShowEndScreenWithFade("The client waited too long and left.", false));
                }
            });
        }
        else if (leaveOnTimeout)
        {
            WalkOut(() => ShowEndScreenWithFade("The client waited too long and left.", false));
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

        List<Vector3> validPoints = new List<Vector3>();
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null) validPoints.Add(points[i].position);
        }

        if (validPoints.Count == 0)
        {
            _isWalking = false;
            onComplete?.Invoke();
            return;
        }

        _movementTween?.Kill();

        if (validPoints.Count == 1)
        {
            transform.DOLookAt(validPoints[0], 0.2f, AxisConstraint.Y);
            _movementTween = transform.DOMove(validPoints[0], duration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnUpdate(() =>
                {
                    // Blokada pochylania w pionie (zawsze idealnie prosta postawa)
                    transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
                })
                .OnComplete(() =>
                {
                    _isWalking = false;
                    onComplete?.Invoke();
                });
        }
        else
        {
            _movementTween = transform.DOPath(validPoints.ToArray(), duration, pathType, PathMode.Full3D)
                .SetLookAt(0.05f)
                .SetEase(Ease.Linear)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnUpdate(() =>
                {
                    // Blokada pochylania w pionie (zawsze idealnie prosta postawa)
                    transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
                })
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

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowJurekMouseScareDialogue(() =>
            {
                WalkOut(() =>
                {
                    ShowEndScreenWithFade("The client saw a rat in the salon and ran away!", false);
                    onComplete?.Invoke();
                });
            });
        }
        else
        {
            WalkOut(() =>
            {
                ShowEndScreenWithFade("The client saw a rat in the salon and ran away!", false);
                onComplete?.Invoke();
            });
        }
    }

    private void WalkOut(Action onComplete = null)
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
                .OnUpdate(() =>
                {
                    transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
                })
                .OnComplete(() =>
                {
                    SetWalkingAnimation(false);
                    if (jurekVisual != null && jurekVisual != gameObject) jurekVisual.SetActive(false);
                    onJurekLeft?.Invoke();
                    onComplete?.Invoke();
                });
        }
        else
        {
            SetWalkingAnimation(false);
            if (jurekVisual != null && jurekVisual != gameObject) jurekVisual.SetActive(false);
            onJurekLeft?.Invoke();
            onComplete?.Invoke();
        }
    }

    public void Interact()
    {
        if (!CanInteract) return;

        _isWaitingForPlayer = false;
        _hasInteractedWithPlayer = true;

        if (playerTransform != null)
        {
            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.05f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        Debug.Log("[CustomerJurek] Gracz podszedł i wszedł w interakcję z Jurkiem!");
        onPlayerInteracted?.Invoke();

        // 1. Sprawdź czy salon został odpowiednio przygotowany (lampki + radio)
        bool atmospherePassed = !requireAtmosphere || (PreparationStateManager.Instance != null && PreparationStateManager.Instance.IsTaskCompleted(atmosphereTaskId));

        if (!atmospherePassed)
        {
            Debug.Log("<color=#FF6060>[CustomerJurek] Atmosfera w salonie nieprzygotowana (brak lamp/radia)! Jurek odmawia golenia i wychodzi.</color>");
            TriggerGloomyAtmosphereFail();
            return;
        }

        // 2. Jeśli atmosfera jest gotowa, normalny dialog powitalny
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartJurekArrivalDialogue(OnDialogueCompleted);
        }
        else
        {
            OnDialogueCompleted();
        }
    }

    /// <summary>
    /// Wywoływane, gdy gracz nie zapalił lamp i radia (fail state ponurej atmosfery).
    /// </summary>
    public void TriggerGloomyAtmosphereFail()
    {
        if (_hasLeft) return;
        _hasLeft = true;
        _movementTween?.Kill();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowJurekGloomyDialogue(() =>
            {
                WalkOut(() =>
                {
                    ShowEndScreenWithFade(gloomyFailReason, false);
                });
            });
        }
        else
        {
            WalkOut(() =>
            {
                ShowEndScreenWithFade(gloomyFailReason, false);
            });
        }
    }

    private void ShowEndScreenWithFade(string reason, bool isVictory)
    {
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeOut(0.4f, () =>
            {
                if (EndSummaryUI.Instance != null)
                {
                    EndSummaryUI.Instance.ShowEndScreen(reason, isVictory);
                }
                ScreenFader.Instance.FadeIn(0.3f);
            });
        }
        else
        {
            if (EndSummaryUI.Instance != null)
            {
                EndSummaryUI.Instance.ShowEndScreen(reason, isVictory);
            }
        }
    }

    private void OnDialogueCompleted()
    {
        Debug.Log("[CustomerJurek] Zakończono rozmowę z Jurkiem. Jurek idzie po schodkach do stanowiska fryzjerskiego!");
        onDialogueCompleted?.Invoke();

        WalkToBarberChair();
    }

    /// <summary>
    /// Krok 6: Marsz po wewnętrznych schodkach na podest do fotela fryzjerskiego.
    /// </summary>
    public void WalkToBarberChair()
    {
        List<Transform> route = new List<Transform>();
        if (toChairWaypoints != null && toChairWaypoints.Length > 0)
        {
            foreach (var wp in toChairWaypoints)
            {
                if (wp != null) route.Add(wp);
            }
        }

        if (chairSpot != null && (route.Count == 0 || route[route.Count - 1] != chairSpot))
        {
            route.Add(chairSpot);
        }

        if (route.Count > 0)
        {
            float duration = GetMovementDuration(route.ToArray(), chairWalkDuration);
            WalkAlongWaypoints(route.ToArray(), duration, OnFullyReachedChair);
        }
        else
        {
            OnFullyReachedChair();
        }
    }

    private void OnFullyReachedChair()
    {
        _hasReachedChair = true;
        SetWalkingAnimation(false);

        if (chairSpot != null)
        {
            transform.DORotateQuaternion(Quaternion.Euler(0f, chairSpot.rotation.eulerAngles.y, 0f), 0.35f)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        Debug.Log("[CustomerJurek] Jurek dotarł na stanowisko fryzjerskie i jest gotowy do golenia!");
        onReachedBarberChair?.Invoke();
    }

    private void PlayFootstepSound()
    {
        if (customFootstepClips != null && customFootstepClips.Length > 0)
        {
            AudioClip clip = customFootstepClips[UnityEngine.Random.Range(0, customFootstepClips.Length)];
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, footstepVolume);
                return;
            }
        }

        if (!string.IsNullOrEmpty(footstepSoundGroup) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(footstepSoundGroup);
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

        if (waitingSpot != null)
        {
            transform.position = waitingSpot.position;
            transform.rotation = waitingSpot.rotation;
        }

        if (frontDoor != null)
        {
            frontDoor.Unlock();
        }

        OnFullyReachedWaitingSpot();
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
        _isWaitingForPlayer = false;
        _hasInteractedWithPlayer = false;
        _hasReachedChair = false;
        _patienceRemaining = patienceDuration;

        SetWalkingAnimation(false);

        if (jurekVisual != null && jurekVisual != gameObject)
        {
            jurekVisual.SetActive(false);
        }

        Transform startTransform = spawnPoint != null ? spawnPoint : (approachWaypoints != null && approachWaypoints.Length > 0 ? approachWaypoints[0] : (stairsWaypoints != null && stairsWaypoints.Length > 0 ? stairsWaypoints[0] : null));
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

        // 2. Trasa 1: Od auta do schodków (Kolor pomarańczowy)
        if (approachWaypoints != null && approachWaypoints.Length > 0)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            Vector3 prev = spawnPoint != null ? spawnPoint.position : transform.position;
            for (int i = 0; i < approachWaypoints.Length; i++)
            {
                if (approachWaypoints[i] != null)
                {
                    Gizmos.DrawSphere(approachWaypoints[i].position, 0.2f);
                    Gizmos.DrawLine(prev, approachWaypoints[i].position);
                    prev = approachWaypoints[i].position;
                }
            }
        }

        // 3. Trasa 2: Schodki zewnętrzne do drzwi (Kolor żółty)
        if (stairsWaypoints != null && stairsWaypoints.Length > 0)
        {
            Gizmos.color = Color.yellow;
            Vector3 prev = approachWaypoints != null && approachWaypoints.Length > 0 && approachWaypoints[approachWaypoints.Length - 1] != null
                ? approachWaypoints[approachWaypoints.Length - 1].position
                : (spawnPoint != null ? spawnPoint.position : transform.position);

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

        // 4. Miejsce Oczekiwania w salonie (Waiting Spot) - Kolor fioletowy / Magenta
        if (waitingSpot != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(waitingSpot.position, 0.4f);
            Gizmos.DrawRay(waitingSpot.position, waitingSpot.forward * 0.9f);
        }

        // 5. Trasa 3: Po wewnętrznych schodkach do fotela (Kolor zielony)
        if (toChairWaypoints != null && toChairWaypoints.Length > 0)
        {
            Gizmos.color = Color.green;
            Vector3 prev = waitingSpot != null ? waitingSpot.position : transform.position;

            for (int i = 0; i < toChairWaypoints.Length; i++)
            {
                if (toChairWaypoints[i] != null)
                {
                    Gizmos.DrawSphere(toChairWaypoints[i].position, 0.22f);
                    Gizmos.DrawLine(prev, toChairWaypoints[i].position);
                    prev = toChairWaypoints[i].position;
                }
            }
        }

        // 6. Stanowisko / Fotel Fryzjerski (Chair Spot) - Kolor niebieski / Cyan
        if (chairSpot != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f);
            Gizmos.DrawWireSphere(chairSpot.position, 0.45f);
            Gizmos.DrawRay(chairSpot.position, chairSpot.forward * 1.0f);
        }

        // 7. Punkt ucieczki (Exit Destination) - Kolor czerwony
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
