using DG.Tweening;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum FpsLimit
    {
        FPS60 = 60,
        FPS120 = 120,
        FPS144 = 144,
        Unlimited = -1
    }

    public enum PlayerSpawnPosition
    {
        Position1,
        Position2,
        Position3
    }

    [Header("Performance")]
    [SerializeField] private FpsLimit fpsLimit = FpsLimit.FPS60;

    [Header("Player Spawn")]
    [SerializeField] private Transform player;

    [SerializeField] private Transform spawnPosition1;
    [SerializeField] private Transform spawnPosition2;
    [SerializeField] private Transform spawnPosition3;

    [SerializeField]
    private PlayerSpawnPosition selectedSpawnPosition =
        PlayerSpawnPosition.Position1;

    [Header("Debug / Quick Start")]
    [Tooltip("Czy na starcie gry natychmiast wywołać spawn Jurka (pomija czekanie na czas lub klikanie F3)?")]
    [SerializeField] private bool autoSpawnJurekOnStart = true;
    [Tooltip("Opóźnienie w sekundach przed wywołaniem przyjścia Jurka na starcie.")]
    [SerializeField] private float autoSpawnJurekDelay = 0.5f;

    private void Awake()
    {
        SetFpsLimit(fpsLimit);
        SpawnPlayer();
    }

    private void Start()
    {
        if (autoSpawnJurekOnStart)
        {
            if (autoSpawnJurekDelay > 0.01f)
            {
                DG.Tweening.DOVirtual.DelayedCall(autoSpawnJurekDelay, SpawnJurekImmediately)
                    .SetLink(gameObject, DG.Tweening.LinkBehaviour.KillOnDestroy);
            }
            else
            {
                SpawnJurekImmediately();
            }
        }
    }

    private void SpawnJurekImmediately()
    {
        // OPTYMALIZACJA: Singleton zamiast drogiego FindAnyObjectByType
        CustomerJurek jurek = CustomerJurek.Instance;

        // Fallback tylko gdy singleton nie gotowy (np. Jurek na nieaktywnym obiekcie)
        if (jurek == null)
            jurek = FindAnyObjectByType<CustomerJurek>(FindObjectsInactive.Include);

        if (jurek != null)
        {
            jurek.TriggerArrival();
            Debug.Log("<color=#70FF70>[GameManager] [Debug Auto-Spawn] Natychmiast wywołano przyjście Jurka na starcie gry!</color>");
        }
    }

    private void SpawnPlayer()
    {
        if (player == null)
        {
            Debug.LogWarning("GameManager: Nie przypisano gracza!", this);
            return;
        }

        Transform selectedSpawn = GetSelectedSpawnPosition();

        if (selectedSpawn == null)
        {
            Debug.LogWarning(
                $"GameManager: Nie przypisano {selectedSpawnPosition}!",
                this
            );

            return;
        }

        CharacterController characterController =
            player.GetComponent<CharacterController>();

        if (characterController != null)
            characterController.enabled = false;

        player.SetPositionAndRotation(
            selectedSpawn.position,
            selectedSpawn.rotation
        );

        if (characterController != null)
            characterController.enabled = true;
    }

    private Transform GetSelectedSpawnPosition()
    {
        return selectedSpawnPosition switch
        {
            PlayerSpawnPosition.Position1 => spawnPosition1,
            PlayerSpawnPosition.Position2 => spawnPosition2,
            PlayerSpawnPosition.Position3 => spawnPosition3,
            _ => spawnPosition1
        };
    }

    public void SetFpsLimit(FpsLimit newLimit)
    {
        fpsLimit = newLimit;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = (int)fpsLimit;
    }

    public void Set60Fps()
    {
        SetFpsLimit(FpsLimit.FPS60);
    }

    public void Set120Fps()
    {
        SetFpsLimit(FpsLimit.FPS120);
    }
}