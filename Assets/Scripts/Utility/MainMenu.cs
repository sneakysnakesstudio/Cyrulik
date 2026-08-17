using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "MainScene";

    public void StartGame()
    {
        ScreenFader.Instance.LoadScene(gameSceneName);
    }
}