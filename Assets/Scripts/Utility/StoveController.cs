using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Zarządza piecem i pełnym procesem przygotowania gorącego ręcznika (Towel Prepared Quest):
/// 1. Nalej wody do garnka (SinkInteractable -> pot_water).
/// 2. Rozpal piec (Light the stove -> stove_lit).
/// 3. Połóż garnek z wodą na piecu (Place pot with water on stove).
/// 4. Poczekaj aż woda się zagotuje (Boiling timer -> para i dźwięk wrzenia).
/// 5. Wrzuć ręcznik do wrzącego garnka (Put towel into boiling water).
/// 6. Wyjmij gorący ręcznik (Take out hot towel -> zaliczenie zadania 'towel_prepared').
/// </summary>
public class StoveController : MonoBehaviour, IConditionalInteractable
{
    public enum MovementAxis
    {
        X,
        Y,
        Z
    }

    [Header("Zadania (PreparationStateManager)")]
    [Tooltip("ID głównego zadania przygotowania ręcznika.")]
    [SerializeField] private string towelTaskId = "towel_prepared";

    [Tooltip("Opcjonalne ID zadania rozpalenia pieca (np. 'stove_lit').")]
    [SerializeField] private string stoveLitTaskId = "stove_lit";

    [Header("Drzwiczki Pieca (Stove Door / Hatch)")]
    [Tooltip("Transform drzwiczek pieca z własnego modelu.")]
    [SerializeField] private Transform stoveDoor;

    [Tooltip("Czy drzwiczki muszą zostać najpierw otwarte, aby rozpalić ogień.")]
    [SerializeField] private bool requireDoorOpenToLight = true;

    [Tooltip("Oś obrotu drzwiczek.")]
    [SerializeField] private MovementAxis doorAxis = MovementAxis.Y;

    [Tooltip("Kąt otwarcia drzwiczek w stopniach (np. 90, -90, 80).")]
    [SerializeField] private float doorOpenAngle = 90f;

    [Tooltip("Czas trwania animacji otwierania/zamykania drzwiczek.")]
    [SerializeField] private float doorAnimationDuration = 0.45f;

    [SerializeField] private Ease doorEase = Ease.OutQuad;

    [Tooltip("Czy po rozpaleniu gracz może zamknąć drzwiczki.")]
    [SerializeField] private bool allowCloseDoorWhenLit = true;

    [Header("Dźwięki drzwiczek")]
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;
    [SerializeField] private string doorSoundGroup = "door_open";
    [SerializeField] private string doorCloseSoundGroup = "door_close";

    [Header("Item IDs")]
    [Tooltip("ID garnka z wodą (z PickupItem).")]
    [SerializeField] private string waterPotItemId = "pot_water";

    [Tooltip("ID pustego garnka (z PickupItem).")]
    [SerializeField] private string emptyPotItemId = "pot_empty";

    [Tooltip("ID czystego ręcznika.")]
    [SerializeField] private string cleanTowelItemId = "towel";

    [Tooltip("ID gorącego ręcznika przekazywanego graczowi.")]
    [SerializeField] private string hotTowelItemId = "hot_towel";

    [Header("Wizualia i Efekty Pieca")]
    [Tooltip("Wizualny ogień w piecu (GameObject / cząsteczki).")]
    [SerializeField] private GameObject fireVisual;

    [Tooltip("Światło ognia.")]
    [SerializeField] private Light fireLight;

    [Header("Rozpalanie Ognia (Powolne Rozpalanie)")]
    [Tooltip("Czas stopniowego rozpalania się ognia w sekundach (wzrost płomieni, światła i dźwięku).")]
    [SerializeField] private float fireIgniteDuration = 3.0f;

    [Tooltip("Docelowa intensywność światła ognia po pełnym rozpaleniu.")]
    [SerializeField] private float targetLightIntensity = 2.0f;

    [Tooltip("Docelowy zasięg światła ognia.")]
    [SerializeField] private float targetLightRange = 3.5f;

    [Tooltip("Krzywa rozpalania ognia.")]
    [SerializeField] private Ease fireIgniteEase = Ease.InOutSine;

    [Tooltip("Dźwięk inicjacji rozpalenia (np. zapałka / iskra / krzesiwo).")]
    [SerializeField] private AudioClip ignitionStartClip;

    [Tooltip("Dedykowany AudioSource dla dźwięku ognia do płynnego zgłaśniania.")]
    [SerializeField] private AudioSource fireAudioSource;

    [Header("Garnek na Piecu")]
    [Tooltip("Punkt na płycie pieca, do którego przyczepia się garnek.")]
    [SerializeField] private Transform potSnapPoint;

    [Tooltip("Model garnka na piecu (jeśli używasz dedykowanego w hierarchii pieca zamiast podczepiania z rąk).")]
    [SerializeField] private GameObject potOnStoveVisual;

    [Tooltip("Tafla wody w garnku na piecu.")]
    [SerializeField] private GameObject waterInPotVisual;

    [Tooltip("Efekt pary wodnej po zagotowaniu (ParticleSystem lub GameObject).")]
    [SerializeField] private GameObject steamVisual;

    [Tooltip("Model ręcznika zanurzonego w garnku.")]
    [SerializeField] private GameObject towelInPotVisual;

    [Tooltip("Prefab gorącego ręcznika trafiający do rąk gracza po wyjęciu.")]
    [SerializeField] private GameObject hotTowelPrefab;

    [Header("Czasy i Gotowanie")]
    [Tooltip("Czas gotowania wody na rozpalonym piecu w sekundach.")]
    [SerializeField] private float boilDuration = 6.0f;

    [Header("Interaction Prompts (English)")]
    [SerializeField] private string promptOpenDoor = "Open stove door";
    [SerializeField] private string promptCloseDoor = "Close stove door";
    [SerializeField] private string promptLightStove = "Light the stove";
    [SerializeField] private string promptPlacePot = "Place pot with water on stove";
    [SerializeField] private string promptNeedWater = "Fill pot with water at the sink first";
    [SerializeField] private string promptWaitingBoil = "Heating water... (Wait for it to boil)";
    [SerializeField] private string promptPutTowel = "Put towel into boiling water";
    [SerializeField] private string promptNeedTowel = "Boiling water ready (Bring a clean towel)";
    [SerializeField] private string promptTakeHotTowel = "Take out hot towel";
    [SerializeField] private string promptCompleted = "Hot stove";

    [Header("Audio")]
    [SerializeField] private string soundLightFire = "stove_fire";
    [SerializeField] private string soundBoiling = "water_boil";
    [SerializeField] private string soundCloth = "cloth_pickup";
    [SerializeField] private AudioSource boilingAudioSource;

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onDoorOpened;
    [SerializeField] private UnityEvent onDoorClosed;
    [SerializeField] private UnityEvent onStoveLit;
    [SerializeField] private UnityEvent onPotPlaced;
    [SerializeField] private UnityEvent onWaterBoiling;
    [SerializeField] private UnityEvent onTowelInserted;
    [SerializeField] private UnityEvent onHotTowelTaken;

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    // Stany wewnętrzne
    private bool _isDoorOpen = false;
    private bool _isLit = false;
    private bool _potOnStove = false;
    private bool _isBoiling = false;
    private bool _towelInPot = false;
    private bool _towelReadyToTake = false;
    private bool _isCompleted = false;

    private Vector3 _doorClosedRotation;
    private Vector3 _doorOpenRotation;
    private Vector3 _fireVisualOriginalScale = Vector3.one;
    private Tween _doorTween;
    private Tween _fireScaleTween;
    private Tween _fireLightTween;
    private Tween _fireLightFlicker;
    private Coroutine _boilingCoroutine;

    public bool IsDoorOpen => _isDoorOpen;
    public Transform StoveDoor => stoveDoor;
    public bool IsLit => _isLit;
    public bool PotOnStove => _potOnStove;
    public bool HasPot => _potOnStove;
    public bool IsBoiling => _isBoiling;
    public bool HasTowel => _towelInPot;
    public bool IsCompleted => _isCompleted;

    public void LightFire() => LightStove();
    public void PlacePot(bool withWater = true) => PlacePotOnStove();
    public void InstantBoil()
    {
        if (_boilingCoroutine != null) StopCoroutine(_boilingCoroutine);
        _isBoiling = true;
        _boilingCoroutine = null;

        if (steamVisual != null) steamVisual.SetActive(true);
        if (boilingAudioSource != null) boilingAudioSource.Play();
        else if (!string.IsNullOrEmpty(soundBoiling) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundBoiling);
        }

        onWaterBoiling?.Invoke();
        Debug.Log("[Stove] Woda natychmiast wrze!");
    }

    public bool CanInteract
    {
        get
        {
            if (_isCompleted)
            {
                if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen) return true;
                return false;
            }

            // 1. Gracz trzyma garnek z wodą a ten jeszcze nie stoi na piecu -> stawia garnek
            if (!_potOnStove && IsHoldingWaterPot()) return true;

            // 2. Drzwiczki pieca: jeśli piec ma drzwiczki i wymagane jest ich otwarcie przed rozpaleniem
            if (stoveDoor != null && requireDoorOpenToLight && !_isDoorOpen && !_isLit)
            {
                return true;
            }

            // 3. Piec nierozpalony:
            // Jeśli wymaga otwartych drzwiczek -> gracz może go rozpalić TYLKO gdy drzwiczki są otwarte
            // Jeśli nie wymaga lub brak drzwiczek -> gracz może go rozpalić
            if (!_isLit)
            {
                if (stoveDoor != null && requireDoorOpenToLight)
                    return _isDoorOpen;
                return true;
            }

            // 4. Garnek nie stoi na piecu -> sprawdź czy gracz ma garnek z wodą
            if (!_potOnStove)
            {
                if (IsHoldingWaterPot()) return true;
                if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen) return true;
                return false;
            }

            // 5. Woda się jeszcze grzeje -> czekamy na zagotowanie
            if (!_isBoiling)
            {
                if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen) return true;
                return false;
            }

            // 6. Woda wrze, ręcznik jeszcze nie włożony -> gracz może wrzucić ręcznik
            if (!_towelInPot)
            {
                if (IsHoldingCleanTowel()) return true;
                if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen) return true;
                return false;
            }

            // 7. Opcjonalne zamknięcie drzwiczek po zakończeniu
            if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen)
            {
                return true;
            }

            return false;
        }
    }

    public string InteractionName
    {
        get
        {
            // Krok: Postawienie garnka (gdy gracz go trzyma)
            if (!_potOnStove)
            {
                if (IsHoldingWaterPot())
                    return promptPlacePot;
                if (IsHoldingEmptyPot())
                    return promptNeedWater;

                // Jeśli piec wymaga otwarcia drzwiczek i są zamknięte
                if (stoveDoor != null && requireDoorOpenToLight && !_isDoorOpen && !_isLit)
                    return promptOpenDoor;

                if (!_isLit)
                    return promptLightStove;

                if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen)
                    return promptCloseDoor;

                return "Stove is lit (Place pot with water)";
            }

            // Krok: Otwarcie drzwiczek pieca (gdy drzwiczki zamknięte i piec zgaszony)
            if (stoveDoor != null && requireDoorOpenToLight && !_isDoorOpen && !_isLit)
            {
                return promptOpenDoor;
            }

            // Krok: Rozpalenie pieca (gdy drzwiczki otwarte lub brak wymogu)
            if (!_isLit)
            {
                return promptLightStove;
            }

            // Krok: Woda się podgrzewa
            if (!_isBoiling)
            {
                if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen)
                    return promptCloseDoor;
                return promptWaitingBoil;
            }

            // Krok: Wrząca woda - wrzucenie ręcznika
            if (!_towelInPot)
            {
                if (IsHoldingCleanTowel())
                    return promptPutTowel;

                if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen)
                    return promptCloseDoor;

                return promptNeedTowel;
            }

            // Krok: Gotowy do wyjęcia gorący ręcznik
            if (_towelReadyToTake)
            {
                return promptTakeHotTowel;
            }

            // Krok: Zakończono lub drzwiczki otwarte
            if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen)
            {
                return promptCloseDoor;
            }

            return promptCompleted;
        }
    }

    private void Awake()
    {
        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }

        // Ukrywamy wizualia na starcie i zapamiętujemy skalę ognia
        if (fireVisual != null)
        {
            _fireVisualOriginalScale = fireVisual.transform.localScale;
            fireVisual.SetActive(false);
        }

        if (fireLight != null) fireLight.enabled = false;
        if (potOnStoveVisual != null) potOnStoveVisual.SetActive(false);
        if (waterInPotVisual != null) waterInPotVisual.SetActive(false);
        if (steamVisual != null) steamVisual.SetActive(false);
        if (towelInPotVisual != null) towelInPotVisual.SetActive(false);

        // Inicjalizacja rotacji drzwiczek
        if (stoveDoor != null)
        {
            _doorClosedRotation = stoveDoor.localEulerAngles;
            _doorOpenRotation = _doorClosedRotation;

            switch (doorAxis)
            {
                case MovementAxis.X:
                    _doorOpenRotation.x += doorOpenAngle;
                    break;
                case MovementAxis.Y:
                    _doorOpenRotation.y += doorOpenAngle;
                    break;
                case MovementAxis.Z:
                    _doorOpenRotation.z += doorOpenAngle;
                    break;
            }
        }
    }

    private void OnDestroy()
    {
        _fireLightFlicker?.Kill();
        _fireLightTween?.Kill();
        _fireScaleTween?.Kill();
        _doorTween?.Kill();
    }

    private void OnDisable()
    {
        _fireLightFlicker?.Kill();
        _fireLightTween?.Kill();
        _fireScaleTween?.Kill();
        _doorTween?.Kill();
        _doorTween = null;
    }

    public void Interact()
    {
        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        // 1. Jeśli gracz trzyma garnek z wodą i jeszcze go nie postawił -> stawia garnek na piecu
        if (!_potOnStove && IsHoldingWaterPot())
        {
            PlacePotOnStove();
            return;
        }

        // 2. Jeśli piec ma drzwiczki, które trzeba otworzyć, a są zamknięte i piec nie jest rozpalony -> otwiera drzwiczki
        if (stoveDoor != null && requireDoorOpenToLight && !_isDoorOpen && !_isLit)
        {
            OpenDoor();
            return;
        }

        // 3. Jeśli piec nie jest rozpalony -> rozpala piec
        if (!_isLit)
        {
            if (stoveDoor != null && requireDoorOpenToLight && !_isDoorOpen)
            {
                OpenDoor();
                return;
            }

            LightStove();
            return;
        }

        // 4. Jeśli woda wrze i gracz trzyma czysty ręcznik -> wrzuca ręcznik i zalicza quest
        if (_isBoiling && !_towelInPot && IsHoldingCleanTowel())
        {
            InsertTowelIntoPot();
            return;
        }

        // 5. Opcjonalne zamykanie/otwieranie drzwiczek gdy piec już pali się / po zadaniu
        if (stoveDoor != null && allowCloseDoorWhenLit)
        {
            ToggleDoor();
            return;
        }
    }

    /// <summary>
    /// Otwiera drzwiczki pieca.
    /// </summary>
    public void OpenDoor()
    {
        if (stoveDoor == null || _isDoorOpen) return;

        _isDoorOpen = true;
        _doorTween?.Kill();

        _doorTween = stoveDoor
            .DOLocalRotate(_doorOpenRotation, doorAnimationDuration)
            .SetEase(doorEase)
            .SetLink(stoveDoor.gameObject, LinkBehaviour.KillOnDestroy);

        PlayDoorSound(doorOpenClip, doorSoundGroup);
        onDoorOpened?.Invoke();
        Debug.Log("[Stove] Otwarto drzwiczki pieca.");
    }

    /// <summary>
    /// Zamyka drzwiczki pieca.
    /// </summary>
    public void CloseDoor()
    {
        if (stoveDoor == null || !_isDoorOpen) return;

        _isDoorOpen = false;
        _doorTween?.Kill();

        _doorTween = stoveDoor
            .DOLocalRotate(_doorClosedRotation, doorAnimationDuration)
            .SetEase(doorEase)
            .SetLink(stoveDoor.gameObject, LinkBehaviour.KillOnDestroy);

        PlayDoorSound(doorCloseClip, doorCloseSoundGroup);
        onDoorClosed?.Invoke();
        Debug.Log("[Stove] Zamknięto drzwiczki pieca.");
    }

    /// <summary>
    /// Przełącza stan otwarcia drzwiczek pieca.
    /// </summary>
    public void ToggleDoor()
    {
        if (_isDoorOpen)
            CloseDoor();
        else
            OpenDoor();
    }

    private void PlayDoorSound(AudioClip directClip, string soundGroupName)
    {
        if (directClip != null)
        {
            AudioSource.PlayClipAtPoint(directClip, stoveDoor != null ? stoveDoor.position : transform.position);
            return;
        }

        if (!string.IsNullOrEmpty(soundGroupName) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundGroupName);
        }
    }

    /// <summary>
    /// Rozpala ogień w piecu z efektem powolnego, stopniowego rozpalania (wzrost płomieni, światła i dźwięku).
    /// </summary>
    public void LightStove()
    {
        if (_isLit) return;

        _isLit = true;

        // 1. Dźwięk startowy rozpalenia (np. zapałka / iskra / krzesiwo)
        if (ignitionStartClip != null)
        {
            AudioSource.PlayClipAtPoint(ignitionStartClip, fireVisual != null ? fireVisual.transform.position : transform.position);
        }

        // 2. Stopniowe rozrastanie się płomieni ognia od zera do docelowej skali
        if (fireVisual != null)
        {
            _fireScaleTween?.Kill();
            fireVisual.transform.localScale = Vector3.zero;
            fireVisual.SetActive(true);

            _fireScaleTween = fireVisual.transform
                .DOScale(_fireVisualOriginalScale, fireIgniteDuration)
                .SetEase(fireIgniteEase)
                .SetLink(fireVisual, LinkBehaviour.KillOnDestroy);

            // Jeśli ogień zawiera ParticleSystem, upewnij się, że zaczyna emitować
            var particleSystems = fireVisual.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                ps.Play();
            }
        }

        // 3. Stopniowe rozjaśnianie światła ognia
        if (fireLight != null)
        {
            _fireLightTween?.Kill();
            _fireLightFlicker?.Kill();

            fireLight.enabled = true;
            fireLight.intensity = 0f;
            fireLight.range = targetLightRange * 0.25f;

            // Płynny wzrost zasięgu światła
            DOTween.To(() => fireLight.range, x => fireLight.range = x, targetLightRange, fireIgniteDuration)
                .SetEase(fireIgniteEase)
                .SetLink(fireLight.gameObject, LinkBehaviour.KillOnDestroy);

            // Płynny wzrost intensywności, a po osiągnięciu pełnego rozpalenia - start migotania
            _fireLightTween = fireLight
                .DOIntensity(targetLightIntensity, fireIgniteDuration)
                .SetEase(fireIgniteEase)
                .SetLink(fireLight.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    StartFireFlicker();
                });
        }

        // 4. Dźwięk stałego płonięcia ognia (płynne zgłaśnianie lub fallback do AudioManager)
        if (fireAudioSource != null)
        {
            fireAudioSource.volume = 0f;
            fireAudioSource.loop = true;
            fireAudioSource.Play();
            fireAudioSource.DOFade(1f, fireIgniteDuration)
                .SetEase(Ease.InQuad)
                .SetLink(fireAudioSource.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else if (!string.IsNullOrEmpty(soundLightFire) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundLightFire);
        }

        if (!string.IsNullOrEmpty(stoveLitTaskId) && PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState(stoveLitTaskId, true);
        }

        onStoveLit?.Invoke();
        Debug.Log($"[Stove] Piec zaczyna się powoli rozpalać... (czas rozpalania: {fireIgniteDuration}s)");

        // Jeśli garnek już wcześniej stał na piecu, rozpocznij gotowanie
        CheckStartBoiling();
    }

    private void StartFireFlicker()
    {
        if (fireLight == null || !_isLit) return;

        _fireLightFlicker?.Kill();
        _fireLightFlicker = fireLight
            .DOIntensity(targetLightIntensity * 1.25f, 0.15f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(fireLight.gameObject, LinkBehaviour.KillOnDestroy);
    }

    /// <summary>
    /// Krok 3: Kładzie garnek z wodą na piecu.
    /// </summary>
    private void PlacePotOnStove()
    {
        if (_potOnStove) return;

        _potOnStove = true;

        // Zabierz garnek z rąk gracza
        if (playerHands != null && playerHands.HasItem)
        {
            if (potOnStoveVisual != null)
            {
                potOnStoveVisual.SetActive(true);
                if (waterInPotVisual != null) waterInPotVisual.SetActive(true);
                playerHands.DestroyHeldItem();
            }
            else
            {
                // Przypnij rzeczywisty obiekt garnka do potSnapPoint
                GameObject heldItem = playerHands.ReleaseHeldItem();
                if (heldItem != null)
                {
                    Transform targetPoint = potSnapPoint != null ? potSnapPoint : transform;
                    heldItem.transform.SetParent(targetPoint);
                    heldItem.transform.localPosition = Vector3.zero;
                    heldItem.transform.localRotation = Quaternion.identity;

                    if (heldItem.TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    }

                    if (heldItem.TryGetComponent<PickupItem>(out var pickup))
                    {
                        pickup.enabled = false;
                    }
                }
            }
        }
        else if (potOnStoveVisual != null)
        {
            potOnStoveVisual.SetActive(true);
            if (waterInPotVisual != null) waterInPotVisual.SetActive(true);
        }

        if (!string.IsNullOrEmpty(soundCloth) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundCloth);
        }

        onPotPlaced?.Invoke();
        Debug.Log("[Stove] Garnek z wodą został postawiony na piecu!");

        CheckStartBoiling();
    }

    private void CheckStartBoiling()
    {
        if (_isLit && _potOnStove && !_isBoiling && _boilingCoroutine == null)
        {
            _boilingCoroutine = StartCoroutine(BoilingRoutine());
        }
    }

    /// <summary>
    /// Krok 4: Odliczanie czasu gotowania wody.
    /// </summary>
    private IEnumerator BoilingRoutine()
    {
        Debug.Log($"[Stove] Woda zaczyna się podgrzewać... Gotowanie potrwa {boilDuration}s.");

        yield return new WaitForSeconds(boilDuration);

        _isBoiling = true;
        _boilingCoroutine = null;

        // Włącz parę
        if (steamVisual != null)
        {
            steamVisual.SetActive(true);
        }

        // Dźwięk wrzenia
        if (boilingAudioSource != null)
        {
            boilingAudioSource.loop = true;
            boilingAudioSource.Play();
        }
        else if (!string.IsNullOrEmpty(soundBoiling) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundBoiling);
        }

        onWaterBoiling?.Invoke();
        Debug.Log("[Stove] Woda W KOTLE WRZE! Można wrzucić ręcznik.");
    }

    /// <summary>
    /// Krok 4: Gracz wrzuca ręcznik do wrzącej wody -> ZALICZENIE ZADANIA 'Clean towel'!
    /// </summary>
    private void InsertTowelIntoPot()
    {
        if (!_isBoiling || _towelInPot) return;

        _towelInPot = true;
        _isCompleted = true;

        // Niszczymy suchy ręcznik z rąk
        if (playerHands != null && playerHands.HasItem)
        {
            playerHands.DestroyHeldItem();
        }

        // Włączamy ręcznik w garnku
        if (towelInPotVisual != null)
        {
            towelInPotVisual.SetActive(true);
        }

        if (!string.IsNullOrEmpty(soundCloth) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundCloth);
        }

        // ZALICZENIE GŁÓWNEGO ZADANIA "Clean towel"
        if (PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState("clean_towel", true);
        }

        // Rozbłysk cząsteczek przy sukcesie
        if (ParticleManager.Instance != null)
        {
            Vector3 burstPos = towelInPotVisual != null ? towelInPotVisual.transform.position : transform.position + Vector3.up * 0.4f;
            ParticleManager.Instance.PlayBurst(burstPos);
        }

        onTowelInserted?.Invoke();
        Debug.Log("[Stove] Ręcznik wrzucony do wrzącej wody! Zadanie 'Clean towel' ZALICZONE!");
    }

    /// <summary>
    /// Krok 6: Gracz wyjmuje gorący ręcznik do rąk i zalicza zadanie!
    /// </summary>
    private void TakeHotTowel()
    {
        if (!_towelReadyToTake || _isCompleted) return;

        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands == null || playerHands.HasItem)
            return;

        _isCompleted = true;
        _towelReadyToTake = false;

        // Ukryj ręcznik w garnku
        if (towelInPotVisual != null)
        {
            towelInPotVisual.SetActive(false);
        }

        // Stwórz instancję gorącego ręcznika do rąk gracza
        GameObject towelInstance = null;
        if (hotTowelPrefab != null)
        {
            towelInstance = Instantiate(hotTowelPrefab);
        }
        else
        {
            // Fallback: generujemy obiekt z PickupItem
            towelInstance = new GameObject("HotTowel_Item");
            var pickup = towelInstance.AddComponent<PickupItem>();
            pickup.ItemId = hotTowelItemId;
            pickup.InteractionName = "Hot Towel";
        }

        if (towelInstance != null)
        {
            var pickup = towelInstance.GetComponent<PickupItem>();
            if (pickup != null)
            {
                pickup.ItemId = hotTowelItemId;
                pickup.InteractionName = "Hot Towel";
            }

            playerHands.TryHold(towelInstance);
        }

        if (!string.IsNullOrEmpty(soundCloth) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundCloth);
        }

        // ZALICZENIE GŁÓWNEGO ZADANIA W PREPARATION MANAGERZE
        if (!string.IsNullOrEmpty(towelTaskId) && PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState(towelTaskId, true);
        }

        onHotTowelTaken?.Invoke();
        Debug.Log($"[Stove] Wyjęto gorący ręcznik ({hotTowelItemId})! Zadanie '{towelTaskId}' ZALICZONE!");
    }

    private bool IsHoldingWaterPot()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PotItem>(out var pot))
        {
            return pot.HasWater;
        }

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            if (string.IsNullOrEmpty(pickup.ItemId))
            {
                string objName = held.name.ToLowerInvariant();
                return objName.Contains("water") && (objName.Contains("pot") || objName.Contains("garnek"));
            }

            string id = pickup.ItemId.Trim().ToLowerInvariant();
            string expected = !string.IsNullOrEmpty(waterPotItemId) ? waterPotItemId.Trim().ToLowerInvariant() : "pot_water";
            return id == expected || id == "pot_water" || id == "water_pot" || id == "pot_with_water" || (id.Contains("pot") && id.Contains("water"));
        }

        return false;
    }

    private bool IsHoldingEmptyPot()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PotItem>(out var pot))
        {
            return !pot.HasWater;
        }

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            if (string.IsNullOrEmpty(pickup.ItemId))
            {
                string objName = held.name.ToLowerInvariant();
                return objName.Contains("pot") || objName.Contains("garnek");
            }

            string id = pickup.ItemId.Trim().ToLowerInvariant();
            string expected = !string.IsNullOrEmpty(emptyPotItemId) ? emptyPotItemId.Trim().ToLowerInvariant() : "pot_empty";
            return id == expected || id == "pot" || id == "pot_empty" || id == "empty_pot" || id == "garnek";
        }

        return false;
    }

    private bool IsHoldingCleanTowel()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            if (string.IsNullOrEmpty(pickup.ItemId))
            {
                string objName = held.name.ToLowerInvariant();
                return objName.Contains("towel") || objName.Contains("recznik");
            }

            string id = pickup.ItemId.Trim().ToLowerInvariant();
            string expected = !string.IsNullOrEmpty(cleanTowelItemId) ? cleanTowelItemId.Trim().ToLowerInvariant() : "towel";
            return id == expected || id == "towel" || id == "clean_towel" || id == "recznik" || id.Contains("towel") || id.Contains("recznik");
        }

        string name = held.name.ToLowerInvariant();
        return name.Contains("towel") || name.Contains("recznik");
    }
}
