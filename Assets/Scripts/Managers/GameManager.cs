using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum FpsLimit
    {
        FPS60 = 60,
        FPS120 = 120
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

    private void Awake()
    {
        SetFpsLimit(fpsLimit);
        SpawnPlayer();
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