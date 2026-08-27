using UnityEngine;

/// <summary>
/// Prosty skrypt optymalizujący grę w zbuildowanej wersji (poza edytorem).
/// Ustawia sztywny limit FPS lub włącza VSync, aby zapobiec 100% obciążeniu GPU/CPU.
/// </summary>
public class GameOptimizer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoRunOptimizer()
    {
        GameObject optimizerObject = new GameObject("GameOptimizer_Auto");
        optimizerObject.AddComponent<GameOptimizer>();
        DontDestroyOnLoad(optimizerObject);
        Debug.Log("[GameOptimizer] Wymuszenie VSync uruchomione na start gry.");
    }

    private void Awake()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60; 
    }
}