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
    [Header("Zadania (PreparationStateManager)")]
    [Tooltip("ID głównego zadania przygotowania ręcznika.")]
    [SerializeField] private string towelTaskId = "towel_prepared";

    [Tooltip("Opcjonalne ID zadania rozpalenia pieca (np. 'stove_lit').")]
    [SerializeField] private string stoveLitTaskId = "stove_lit";

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
    [Tooltip("Wizualny ogień w piecu.")]
    [SerializeField] private GameObject fireVisual;

    [Tooltip("Światło ognia.")]
    [SerializeField] private Light fireLight;

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
    [SerializeField] private UnityEvent onStoveLit;
    [SerializeField] private UnityEvent onPotPlaced;
    [SerializeField] private UnityEvent onWaterBoiling;
    [SerializeField] private UnityEvent onTowelInserted;
    [SerializeField] private UnityEvent onHotTowelTaken;

    [Header("Referencje")]
    [SerializeField] private PlayerHands playerHands;

    // Stany wewnętrzne
    private bool _isLit = false;
    private bool _potOnStove = false;
    private bool _isBoiling = false;
    private bool _towelInPot = false;
    private bool _towelReadyToTake = false;
    private bool _isCompleted = false;

    private Coroutine _boilingCoroutine;
    private Tween _fireLightFlicker;

    public bool IsLit => _isLit;
    public bool PotOnStove => _potOnStove;
    public bool IsBoiling => _isBoiling;
    public bool IsCompleted => _isCompleted;

    public bool CanInteract
    {
        get
        {
            if (_isCompleted) return false;

            // 1. Piec nierozpalony -> gracz może go rozpalić
            if (!_isLit) return true;

            // 2. Garnek nie stoi na piecu -> gracz może postawić garnek z wodą
            if (!_potOnStove)
            {
                return IsHoldingWaterPot();
            }

            // 3. Woda się jeszcze grzeje -> czekamy
            if (!_isBoiling) return false;

            // 4. Woda wrze, ręcznik jeszcze nie włożony -> gracz może włożyć ręcznik
            if (!_towelInPot)
            {
                return IsHoldingCleanTowel();
            }

            // 5. Ręcznik jest we wrzątku i gotowy do wyjęcia -> gracz musi mieć wolne ręce
            if (_towelReadyToTake)
            {
                if (playerHands == null) playerHands = FindAnyObjectByType<PlayerHands>();
                return playerHands != null && !playerHands.HasItem;
            }

            return false;
        }
    }

    public string InteractionName
    {
        get
        {
            if (_isCompleted)
                return promptCompleted;

            // Krok A: Rozpalenie pieca
            if (!_isLit)
                return promptLightStove;

            // Krok B: Postawienie garnka
            if (!_potOnStove)
            {
                if (IsHoldingWaterPot())
                    return promptPlacePot;
                if (IsHoldingEmptyPot())
                    return promptNeedWater;

                return "Stove is lit (Place pot with water)";
            }

            // Krok C: Woda się grzeje
            if (!_isBoiling)
                return promptWaitingBoil;

            // Krok D: Włożenie ręcznika
            if (!_towelInPot)
            {
                if (IsHoldingCleanTowel())
                    return promptPutTowel;

                return promptNeedTowel;
            }

            // Krok E: Wyjęcie gotowego gorącego ręcznika
            if (_towelReadyToTake)
                return promptTakeHotTowel;

            return promptCompleted;
        }
    }

    private void Awake()
    {
        if (playerHands == null)
        {
            playerHands = FindAnyObjectByType<PlayerHands>();
        }

        // Ukrywamy wizualia na starcie
        if (fireVisual != null) fireVisual.SetActive(false);
        if (fireLight != null) fireLight.enabled = false;
        if (potOnStoveVisual != null) potOnStoveVisual.SetActive(false);
        if (waterInPotVisual != null) waterInPotVisual.SetActive(false);
        if (steamVisual != null) steamVisual.SetActive(false);
        if (towelInPotVisual != null) towelInPotVisual.SetActive(false);
    }

    private void OnDestroy()
    {
        _fireLightFlicker?.Kill();
    }

    public void Interact()
    {
        if (playerHands == null)
            playerHands = FindAnyObjectByType<PlayerHands>();

        // 1. Rozpalenie pieca
        if (!_isLit)
        {
            LightStove();
            return;
        }

        // 2. Położenie garnka z wodą na piecu
        if (!_potOnStove && IsHoldingWaterPot())
        {
            PlacePotOnStove();
            return;
        }

        // 3. Włożenie ręcznika do wrzącej wody
        if (_isBoiling && !_towelInPot && IsHoldingCleanTowel())
        {
            InsertTowelIntoPot();
            return;
        }

        // 4. Wyjęcie gorącego ręcznika
        if (_towelReadyToTake && playerHands != null && !playerHands.HasItem)
        {
            TakeHotTowel();
            return;
        }
    }

    /// <summary>
    /// Krok 2: Rozpala ogień w piecu.
    /// </summary>
    public void LightStove()
    {
        if (_isLit) return;

        _isLit = true;

        if (fireVisual != null) fireVisual.SetActive(true);

        if (fireLight != null)
        {
            fireLight.enabled = true;
            // Drobne migotanie światła ognia
            _fireLightFlicker = fireLight
                .DOIntensity(fireLight.intensity * 1.25f, 0.15f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(fireLight.gameObject, LinkBehaviour.KillOnDestroy);
        }

        if (!string.IsNullOrEmpty(soundLightFire) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundLightFire);
        }

        if (!string.IsNullOrEmpty(stoveLitTaskId) && PreparationStateManager.Instance != null)
        {
            PreparationStateManager.Instance.SetTaskState(stoveLitTaskId, true);
        }

        onStoveLit?.Invoke();
        Debug.Log("[Stove] Piec został rozpalony!");

        // Jeśli garnek już wcześniej stał na piecu, rozpocznij gotowanie
        CheckStartBoiling();
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
    /// Krok 5: Gracz wrzuca ręcznik do wrzącej wody.
    /// </summary>
    private void InsertTowelIntoPot()
    {
        if (!_isBoiling || _towelInPot) return;

        _towelInPot = true;

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

        _towelReadyToTake = true;
        onTowelInserted?.Invoke();

        Debug.Log("[Stove] Ręcznik wrzucony do wrzącej wody! Gotowy do wyjęcia.");
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
            return string.Equals(pickup.ItemId, waterPotItemId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private bool IsHoldingEmptyPot()
    {
        if (playerHands == null || !playerHands.HasItem) return false;
        GameObject held = playerHands.HeldItem;
        if (held == null) return false;

        if (held.TryGetComponent<PickupItem>(out var pickup))
        {
            return string.Equals(pickup.ItemId, emptyPotItemId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pickup.ItemId, "pot", StringComparison.OrdinalIgnoreCase);
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
            return string.Equals(pickup.ItemId, cleanTowelItemId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pickup.ItemId, "clean_towel", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
