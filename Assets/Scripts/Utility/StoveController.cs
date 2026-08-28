using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Zarządza piecem i podgrzewaniem wody:
/// 1. Otwieranie/zamykanie drzwiczek i rozpalanie ognia w piecu.
/// 2. Postawienie garnka/czajnika (z wodą lub bez wody) na płycie pieca.
/// 3. Gotowanie wody na rozpalonym piecu (para wodna i dźwięk wrzenia).
/// 4. Zdejmowanie garnka z pieca do rąk gracza.
/// </summary>
public class StoveController : MonoBehaviour, IConditionalInteractable
{
    public enum MovementAxis
    {
        X,
        Y,
        Z
    }

    [Header("Zadanie (PreparationStateManager)")]
    [Tooltip("ID zadania rozpalenia pieca (np. 'stove_lit').")]
    [SerializeField] private string stoveLitTaskId = "stove_lit";

    [Header("Drzwiczki Pieca (Stove Door)")]
    [Tooltip("Transform drzwiczek pieca.")]
    [SerializeField] private Transform stoveDoor;

    [Tooltip("Czy drzwiczki muszą zostać najpierw otwarte, aby rozpalić ogień.")]
    [SerializeField] private bool requireDoorOpenToLight = true;

    [Tooltip("Oś obrotu drzwiczek.")]
    [SerializeField] private MovementAxis doorAxis = MovementAxis.Y;

    [Tooltip("Kąt otwarcia drzwiczek w stopniach.")]
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

    [Header("Wizualia i Efekty Pieca")]
    [Tooltip("Wizualny ogień w piecu (GameObject / cząsteczki).")]
    [SerializeField] private GameObject fireVisual;

    [Tooltip("Światło ognia.")]
    [SerializeField] private Light fireLight;

    [Header("Rozpalanie Ognia")]
    [Tooltip("Czas stopniowego rozpalania się ognia w sekundach.")]
    [SerializeField] private float fireIgniteDuration = 2.5f;

    [Tooltip("Docelowa intensywność światła ognia.")]
    [SerializeField] private float targetLightIntensity = 2.0f;

    [Tooltip("Docelowy zasięg światła ognia.")]
    [SerializeField] private float targetLightRange = 3.5f;

    [SerializeField] private Ease fireIgniteEase = Ease.InOutSine;

    [Tooltip("Dźwięk inicjacji rozpalenia (np. zapałka / iskra / krzesiwo).")]
    [SerializeField] private AudioClip ignitionStartClip;

    [Tooltip("Dedykowany AudioSource dla dźwięku ognia do płynnego zgłaśniania.")]
    [SerializeField] private AudioSource fireAudioSource;

    [Header("Garnek na Piecu")]
    [Tooltip("Punkt na płycie pieca, do którego przyczepia się garnek.")]
    [SerializeField] private Transform potSnapPoint;

    [Tooltip("Model garnka na piecu (opcjonalny fallback w hierarchii pieca).")]
    [SerializeField] private GameObject potOnStoveVisual;

    [Tooltip("Tafla wody w garnku na piecu.")]
    [SerializeField] private GameObject waterInPotVisual;

    [Tooltip("Efekt pary wodnej po zagotowaniu (ParticleSystem lub GameObject).")]
    [SerializeField] private GameObject steamVisual;

    [Header("Gotowanie Wody")]
    [Tooltip("Czas gotowania wody na rozpalonym piecu w sekundach.")]
    [SerializeField] private float boilDuration = 5.0f;

    [Header("Interaction Prompts (English)")]
    [SerializeField] private string promptOpenDoor = "Open stove door";
    [SerializeField] private string promptCloseDoor = "Close stove door";
    [SerializeField] private string promptLightStove = "Light the stove";
    [SerializeField] private string promptPlacePot = "Place pot on stove";
    [SerializeField] private string promptTakePot = "Pick up pot from stove";
    [SerializeField] private string promptTakeBoilingPot = "Pick up pot with boiling water";
    [SerializeField] private string promptWaitingBoil = "Heating water... (Wait for it to boil)";
    [SerializeField] private string promptStoveHot = "Hot stove";

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
    [SerializeField] private UnityEvent onPotTaken;
    [SerializeField] private UnityEvent onWaterBoiling;

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    // Stany wewnętrzne
    private bool _isDoorOpen = false;
    private bool _isLit = false;
    private bool _potOnStove = false;
    private bool _potHasWater = false;
    private bool _isBoiling = false;

    private Vector3 _originalPotWorldPosition;
    private Quaternion _originalPotWorldRotation;
    private Vector3 _originalPotScale = Vector3.one;
    private bool _hasRecordedOriginalPotTransform = false;
    private GameObject _physicalPotObject;

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
    public bool HasTowel => false; // Kompatybilność wsteczna z DebugOverlay
    public bool IsCompleted => _isBoiling;

    public void LightFire() => LightStove();
    public void PlacePot(bool withWater = true) => PlacePotOnStove();

    public void InstantBoil()
    {
        if (_boilingCoroutine != null) StopCoroutine(_boilingCoroutine);
        _isBoiling = true;
        _potHasWater = true;
        _boilingCoroutine = null;

        if (waterInPotVisual != null) waterInPotVisual.SetActive(true);
        if (steamVisual != null) steamVisual.SetActive(true);
        if (boilingAudioSource != null) boilingAudioSource.Play();
        else if (!string.IsNullOrEmpty(soundBoiling) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundBoiling);
        }

        onWaterBoiling?.Invoke();
        Debug.Log("[Stove] Woda natychmiast wrze!");
    }

    // ── IConditionalInteractable ──────────────────────────────────────────────

    public bool CanInteract
    {
        get
        {
            // 1. Gracz trzyma garnek (z wodą lub bez) i jeszcze go nie postawił -> może postawić
            if (!_potOnStove && IsHoldingAnyPot()) return true;

            // 2. Garnek stoi na piecu a gracz ma puste ręce -> może podnieść garnek z pieca
            if (_potOnStove)
            {
                if (playerHands == null) playerHands = FindAnyObjectByType<PlayerHands>();
                if (playerHands != null && !playerHands.HasItem) return true;
            }

            // 3. Drzwiczki zamknięte a piec wygaszony
            if (stoveDoor != null && requireDoorOpenToLight && !_isDoorOpen && !_isLit)
                return true;

            // 4. Rozpalenie pieca
            if (!_isLit)
            {
                if (stoveDoor != null && requireDoorOpenToLight)
                    return _isDoorOpen;
                return true;
            }

            // 5. Opcjonalne zamykanie/otwieranie drzwiczek gdy piec pali się
            if (stoveDoor != null && allowCloseDoorWhenLit)
                return true;

            return false;
        }
    }

    public string InteractionName
    {
        get
        {
            // Postawienie garnka
            if (!_potOnStove && IsHoldingAnyPot())
            {
                return promptPlacePot;
            }

            // Podniesienie garnka z pieca
            if (_potOnStove && playerHands != null && !playerHands.HasItem)
            {
                return _isBoiling ? promptTakeBoilingPot : promptTakePot;
            }

            // Otwarcie drzwiczek
            if (stoveDoor != null && requireDoorOpenToLight && !_isDoorOpen && !_isLit)
            {
                return promptOpenDoor;
            }

            // Rozpalenie pieca
            if (!_isLit)
            {
                return promptLightStove;
            }

            // Podgrzewanie wody
            if (_potOnStove && _potHasWater && !_isBoiling)
            {
                return promptWaitingBoil;
            }

            // Zamykanie/otwieranie drzwiczek
            if (stoveDoor != null && allowCloseDoorWhenLit && _isDoorOpen)
            {
                return promptCloseDoor;
            }

            return promptStoveHot;
        }
    }

    public string BlockedMessage => null;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }

        if (fireVisual != null)
        {
            _fireVisualOriginalScale = fireVisual.transform.localScale;
            fireVisual.SetActive(false);
        }

        if (fireLight != null) fireLight.enabled = false;
        if (potOnStoveVisual != null) potOnStoveVisual.SetActive(false);
        if (waterInPotVisual != null) waterInPotVisual.SetActive(false);
        if (steamVisual != null) steamVisual.SetActive(false);

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

    private void Start()
    {
        RecordInitialPotTransform();
    }

    private void RecordInitialPotTransform()
    {
        if (_hasRecordedOriginalPotTransform) return;

        PotItem potItem = FindAnyObjectByType<PotItem>();
        if (potItem != null)
        {
            _physicalPotObject = potItem.gameObject;
            _originalPotWorldPosition = potItem.transform.position;
            _originalPotWorldRotation = potItem.transform.rotation;
            _originalPotScale = potItem.transform.localScale;
            _hasRecordedOriginalPotTransform = true;
            return;
        }

        PickupItem[] allPickups = FindObjectsByType<PickupItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in allPickups)
        {
            if (p != null)
            {
                string id = p.ItemId != null ? p.ItemId.ToLowerInvariant() : "";
                string n = p.name.ToLowerInvariant();
                if (id == emptyPotItemId || id == waterPotItemId || id == "pot" || id == "pot_empty" || n.Contains("pot") || n.Contains("kettle") || n.Contains("czajnik") || n.Contains("garnek"))
                {
                    _physicalPotObject = p.gameObject;
                    _originalPotWorldPosition = p.transform.position;
                    _originalPotWorldRotation = p.transform.rotation;
                    _originalPotScale = p.transform.localScale;
                    _hasRecordedOriginalPotTransform = true;
                    break;
                }
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

        // 1. Postawienie garnka (z wodą lub bez)
        if (!_potOnStove && IsHoldingAnyPot())
        {
            PlacePotOnStove();
            return;
        }

        // 2. Podniesienie garnka z pieca
        if (_potOnStove && playerHands != null && !playerHands.HasItem)
        {
            TakePotFromStove();
            return;
        }

        // 3. Otwarcie drzwiczek przed rozpaleniem
        if (stoveDoor != null && requireDoorOpenToLight && !_isDoorOpen && !_isLit)
        {
            OpenDoor();
            return;
        }

        // 4. Rozpalenie pieca
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

        // 5. Zamykanie/otwieranie drzwiczek gdy piec już pali się
        if (stoveDoor != null && allowCloseDoorWhenLit)
        {
            ToggleDoor();
            return;
        }
    }

    // ── Drzwiczki ─────────────────────────────────────────────────────────────

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

    public void ToggleDoor()
    {
        if (_isDoorOpen) CloseDoor();
        else OpenDoor();
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

    // ── Rozpalanie ────────────────────────────────────────────────────────────

    public void LightStove()
    {
        if (_isLit) return;

        _isLit = true;

        if (ignitionStartClip != null)
        {
            AudioSource.PlayClipAtPoint(ignitionStartClip, fireVisual != null ? fireVisual.transform.position : transform.position);
        }

        if (fireVisual != null)
        {
            _fireScaleTween?.Kill();
            fireVisual.transform.localScale = Vector3.zero;
            fireVisual.SetActive(true);

            _fireScaleTween = fireVisual.transform
                .DOScale(_fireVisualOriginalScale, fireIgniteDuration)
                .SetEase(fireIgniteEase)
                .SetLink(fireVisual, LinkBehaviour.KillOnDestroy);

            var particleSystems = fireVisual.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                ps.Play();
            }
        }

        if (fireLight != null)
        {
            _fireLightTween?.Kill();
            _fireLightFlicker?.Kill();

            fireLight.enabled = true;
            fireLight.intensity = 0f;
            fireLight.range = targetLightRange * 0.25f;

            DOTween.To(() => fireLight.range, x => fireLight.range = x, targetLightRange, fireIgniteDuration)
                .SetEase(fireIgniteEase)
                .SetLink(fireLight.gameObject, LinkBehaviour.KillOnDestroy);

            _fireLightTween = fireLight
                .DOIntensity(targetLightIntensity, fireIgniteDuration)
                .SetEase(fireIgniteEase)
                .SetLink(fireLight.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    StartFireFlicker();
                });
        }

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
        Debug.Log($"[Stove] Piec rozpalony!");

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

    // ── Garnek / Czajnik na Piecu ─────────────────────────────────────────────

    private void PlacePotOnStove()
    {
        if (_potOnStove) return;

        _potOnStove = true;

        GameObject potToPlace = null;

        if (playerHands != null && playerHands.HasItem)
        {
            // Sprawdź czy garnek ma wodę
            _potHasWater = IsHoldingWaterPot();
            potToPlace = playerHands.ReleaseHeldItem();
        }
        else
        {
            _potHasWater = false;
        }

        if (potToPlace == null && _physicalPotObject != null)
        {
            potToPlace = _physicalPotObject;
        }

        if (potToPlace != null)
        {
            _physicalPotObject = potToPlace;

            potToPlace.SetActive(true);
            foreach (var r in potToPlace.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }

            if (_hasRecordedOriginalPotTransform)
            {
                potToPlace.transform.SetParent(potSnapPoint != null ? potSnapPoint : transform, true);
                potToPlace.transform.position = _originalPotWorldPosition;
                potToPlace.transform.rotation = _originalPotWorldRotation;
                potToPlace.transform.localScale = _originalPotScale;
            }
            else if (potSnapPoint != null)
            {
                potToPlace.transform.SetParent(potSnapPoint, false);
                potToPlace.transform.localPosition = Vector3.zero;
                potToPlace.transform.localRotation = Quaternion.identity;
            }
            else
            {
                potToPlace.transform.SetParent(transform, false);
                potToPlace.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                potToPlace.transform.localRotation = Quaternion.identity;
            }

            if (potToPlace.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Wyłączamy bezpośredni pickup – interakcja idzie przez piec
            if (potToPlace.TryGetComponent<PickupItem>(out var pickup))
            {
                pickup.enabled = false;
            }

            if (potToPlace.TryGetComponent<PotItem>(out var potItem))
            {
                potItem.SetWater(_potHasWater);
            }

            if (potOnStoveVisual != null)
            {
                potOnStoveVisual.SetActive(false);
            }

            if (waterInPotVisual != null)
            {
                waterInPotVisual.SetActive(_potHasWater);
            }

            if (steamVisual != null)
            {
                steamVisual.transform.position = potToPlace.transform.position + Vector3.up * 0.12f;
            }
        }
        else if (potOnStoveVisual != null)
        {
            potOnStoveVisual.SetActive(true);
            if (waterInPotVisual != null) waterInPotVisual.SetActive(_potHasWater);
        }

        if (!string.IsNullOrEmpty(soundCloth) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundCloth);
        }

        onPotPlaced?.Invoke();
        Debug.Log($"[Stove] Garnek postawiony na piecu (Zawiera wodę: {_potHasWater}).");

        CheckStartBoiling();
    }

    private void TakePotFromStove()
    {
        if (!_potOnStove) return;

        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        if (playerHands == null || playerHands.HasItem)
            return;

        // Zatrzymujemy gotowanie
        if (_boilingCoroutine != null)
        {
            StopCoroutine(_boilingCoroutine);
            _boilingCoroutine = null;
        }

        if (boilingAudioSource != null) boilingAudioSource.Stop();
        if (steamVisual != null) steamVisual.SetActive(false);
        if (waterInPotVisual != null) waterInPotVisual.SetActive(false);
        if (potOnStoveVisual != null) potOnStoveVisual.SetActive(false);

        _potOnStove = false;

        GameObject potObj = _physicalPotObject;
        if (potObj != null)
        {
            if (potObj.TryGetComponent<PickupItem>(out var pickup))
            {
                pickup.enabled = true;
            }

            if (potObj.TryGetComponent<PotItem>(out var potItem))
            {
                // Jeśli woda wrzała lub garnek miał wodę, zachowaj wodę
                potItem.SetWater(_potHasWater || _isBoiling);
            }

            playerHands.TryHold(potObj);
        }

        _isBoiling = false;

        if (!string.IsNullOrEmpty(soundCloth) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundCloth);
        }

        onPotTaken?.Invoke();
        Debug.Log("[Stove] Garnek zdjęty z pieca do rąk gracza.");
    }

    private void CheckStartBoiling()
    {
        if (_isLit && _potOnStove && _potHasWater && !_isBoiling && _boilingCoroutine == null)
        {
            _boilingCoroutine = StartCoroutine(BoilingRoutine());
        }
    }

    private IEnumerator BoilingRoutine()
    {
        Debug.Log($"[Stove] Woda zaczyna się podgrzewać... Gotowanie potrwa {boilDuration}s.");

        yield return new WaitForSeconds(boilDuration);

        _isBoiling = true;
        _boilingCoroutine = null;

        if (steamVisual != null)
        {
            steamVisual.SetActive(true);
        }

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
        Debug.Log("[Stove] WODA W GARNKU WRZE!");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsHoldingAnyPot()
    {
        return IsHoldingWaterPot() || IsHoldingEmptyPot();
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
            string id = pickup.ItemId != null ? pickup.ItemId.Trim().ToLowerInvariant() : "";
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
            string id = pickup.ItemId != null ? pickup.ItemId.Trim().ToLowerInvariant() : "";
            string expected = !string.IsNullOrEmpty(emptyPotItemId) ? emptyPotItemId.Trim().ToLowerInvariant() : "pot_empty";
            return id == expected || id == "pot" || id == "pot_empty" || id == "empty_pot" || id == "garnek" || id == "kettle" || id == "czajnik" || held.name.ToLowerInvariant().Contains("pot") || held.name.ToLowerInvariant().Contains("kettle");
        }

        return false;
    }
}
