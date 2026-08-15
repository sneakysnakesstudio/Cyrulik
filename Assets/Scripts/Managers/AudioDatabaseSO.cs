using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioDatabase",
    menuName = "Audio/Audio Database"
)]
public class AudioDatabaseSO : ScriptableObject
{
    [SerializeField]
    private List<AudioClipData> audioGroups =
        new List<AudioClipData>();

    public AudioClipData Get(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return null;

        foreach (AudioClipData group in audioGroups)
        {
            if (group == null)
                continue;

            if (string.Equals(
                    group.groupName,
                    groupName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return group;
            }
        }

        return null;
    }
}

[Serializable]
public class AudioClipData
{
    [Header("Group")]
    public string groupName;

    [Header("Clips")]
    public AudioClip[] clips;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Clip Randomization")]
    public bool preventImmediateRepeat = true;

    [Header("Pitch Randomization")]
    public bool randomizePitch = false;

    [Min(0.1f)]
    public float minPitch = 0.95f;

    [Min(0.1f)]
    public float maxPitch = 1.05f;

    [Header("Volume Randomization")]
    public bool randomizeVolume = false;

    [Range(0f, 2f)]
    public float minVolumeMultiplier = 0.9f;

    [Range(0f, 2f)]
    public float maxVolumeMultiplier = 1.1f;

    [NonSerialized]
    private int _lastClipIndex = -1;

    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
        {
            _lastClipIndex = 0;
            return clips[0];
        }

        int selectedIndex;

        if (preventImmediateRepeat)
        {
            do
            {
                selectedIndex =
                    UnityEngine.Random.Range(
                        0,
                        clips.Length
                    );
            }
            while (selectedIndex == _lastClipIndex);
        }
        else
        {
            selectedIndex =
                UnityEngine.Random.Range(
                    0,
                    clips.Length
                );
        }

        _lastClipIndex = selectedIndex;

        return clips[selectedIndex];
    }

    public float GetRandomPitch()
    {
        if (!randomizePitch)
            return 1f;

        float min = Mathf.Min(
            minPitch,
            maxPitch
        );

        float max = Mathf.Max(
            minPitch,
            maxPitch
        );

        return UnityEngine.Random.Range(
            min,
            max
        );
    }

    public float GetRandomVolume()
    {
        if (!randomizeVolume)
            return volume;

        float min = Mathf.Min(
            minVolumeMultiplier,
            maxVolumeMultiplier
        );

        float max = Mathf.Max(
            minVolumeMultiplier,
            maxVolumeMultiplier
        );

        float multiplier =
            UnityEngine.Random.Range(
                min,
                max
            );

        return Mathf.Clamp01(
            volume * multiplier
        );
    }
}