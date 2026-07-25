using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum FpsLimit
    {
        FPS60 = 60,
        FPS120 = 120
    }

    [Header("Performance")]
    [SerializeField] private FpsLimit fpsLimit = FpsLimit.FPS60;

    private void Awake()
    {
        SetFpsLimit(fpsLimit);
    }

    public void SetFpsLimit(FpsLimit newLimit)
    {
        fpsLimit = newLimit;

        // Wyłączenie VSync, żeby limit FPS działał prawidłowo.
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