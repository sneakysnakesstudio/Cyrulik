using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("References")]
    [SerializeField] private AudioDatabaseSO database;
    [SerializeField] private AudioSource sfxSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void Play(string groupName)
    {
        if (database == null)
        {
            Debug.LogWarning(
                "AudioManager: AudioDatabaseSO is not assigned!",
                this
            );

            return;
        }

        if (sfxSource == null)
        {
            Debug.LogWarning(
                "AudioManager: SFX AudioSource is not assigned!",
                this
            );

            return;
        }

        AudioClipData data = database.Get(groupName);

        if (data == null)
        {
            Debug.LogWarning(
                $"AudioManager: Audio group '{groupName}' not found!",
                this
            );

            return;
        }

        AudioClip clip = data.GetRandomClip();

        if (clip == null)
        {
            Debug.LogWarning(
                $"AudioManager: Audio group '{groupName}' has no clips!",
                this
            );

            return;
        }

        sfxSource.PlayOneShot(
            clip,
            data.volume
        );
    }
}