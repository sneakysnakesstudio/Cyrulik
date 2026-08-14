using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioDatabase",
    menuName = "Audio/Audio Database"
)]
public class AudioDatabaseSO : ScriptableObject
{
    [SerializeField] private List<AudioClipData> audioGroups =
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

    [Header("Settings")]
    [Range(0f, 1f)]
    public float volume = 1f;

    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Length == 0)
            return null;

        return clips[
            UnityEngine.Random.Range(
                0,
                clips.Length
            )
        ];
    }
}