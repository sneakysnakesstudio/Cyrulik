using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("References")]
    [SerializeField] private AudioDatabaseSO database;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Pool")]
    [SerializeField] private int initialPoolSize = 8;

    private readonly List<AudioSource> _sourcePool =
        new List<AudioSource>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        CreateAudioPool();
    }

    private void CreateAudioPool()
    {
        if (sfxSource == null)
            return;

        _sourcePool.Clear();

        _sourcePool.Add(sfxSource);

        for (int i = 1; i < initialPoolSize; i++)
        {
            CreatePooledSource();
        }
    }

    private AudioSource CreatePooledSource()
    {
        GameObject sourceObject =
            new GameObject(
                $"SFX Source {_sourcePool.Count + 1}"
            );

        sourceObject.transform.SetParent(
            transform
        );

        AudioSource source =
            sourceObject.AddComponent<AudioSource>();

        CopyAudioSourceSettings(
            sfxSource,
            source
        );

        _sourcePool.Add(source);

        return source;
    }

    private void CopyAudioSourceSettings(
        AudioSource from,
        AudioSource to
    )
    {
        if (from == null || to == null)
            return;

        to.outputAudioMixerGroup =
            from.outputAudioMixerGroup;

        to.mute =
            from.mute;

        to.bypassEffects =
            from.bypassEffects;

        to.bypassListenerEffects =
            from.bypassListenerEffects;

        to.bypassReverbZones =
            from.bypassReverbZones;

        to.priority =
            from.priority;

        to.volume =
            from.volume;

        to.pitch = 1f;

        to.panStereo =
            from.panStereo;

        // Dźwięki systemowe/SFX odtwarzamy w 2D (spatialBlend = 0), aby nie zanikały w przestrzeni 3D
        to.spatialBlend = 0f;

        to.reverbZoneMix =
            from.reverbZoneMix;

        to.dopplerLevel =
            from.dopplerLevel;

        to.spread =
            from.spread;

        to.rolloffMode =
            from.rolloffMode;

        to.minDistance =
            from.minDistance;

        to.maxDistance =
            from.maxDistance;

        to.playOnAwake = false;
        to.loop = false;
    }

    private AudioSource GetAvailableSource()
    {
        foreach (AudioSource source in _sourcePool)
        {
            if (source == null)
                continue;

            if (!source.isPlaying)
                return source;
        }

        return CreatePooledSource();
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

        AudioClipData data =
            database.Get(groupName);

        if (data == null)
        {
            Debug.LogWarning(
                $"AudioManager: Audio group '{groupName}' not found!",
                this
            );

            return;
        }

        AudioClip clip =
            data.GetRandomClip();

        if (clip == null)
        {
            Debug.LogWarning(
                $"AudioManager: Audio group '{groupName}' has no clips!",
                this
            );

            return;
        }

        AudioSource source =
            GetAvailableSource();

        source.pitch =
            data.GetRandomPitch();

        float volume =
            data.GetRandomVolume();

        source.PlayOneShot(
            clip,
            volume
        );
    }
}