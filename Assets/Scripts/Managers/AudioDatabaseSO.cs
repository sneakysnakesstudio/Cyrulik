using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioDatabase",
    menuName = "Audio/Audio Database"
)]
public class AudioDatabaseSO : ScriptableObject
{
    [Header("🧍 1. PLAYER & INVENTORY")]
    [Tooltip("Dźwięki kroków gracza, ubierania, podnoszenia i wyrzucania przedmiotów.")]
    [SerializeField] private List<AudioClipData> playerSounds = new List<AudioClipData>();

    [Header("💡 2. LIGHTS & AMBIENCE")]
    [Tooltip("Przełączniki światła, lampki, szumy urządzeń, tło otoczenia.")]
    [SerializeField] private List<AudioClipData> lightAndAmbienceSounds = new List<AudioClipData>();

    [Header("🎮 3. MINIGAMES")]
    [Tooltip("Dźwięki minigier, ostrzenia brzytwy, trafień Perfect/Good/Miss itp.")]
    [SerializeField] private List<AudioClipData> minigameSounds = new List<AudioClipData>();

    [Header("🚪 4. FURNITURE & INTERACTABLES")]
    [Tooltip("Dźwięki drzwi, szaf, lodówki, szuflad, klamek itp.")]
    [SerializeField] private List<AudioClipData> furnitureSounds = new List<AudioClipData>();

    [Header("💬 5. UI & DIALOGUES")]
    [Tooltip("Dźwięki dialogów, przewijania tekstu, kliknięć interfejsu.")]
    [SerializeField] private List<AudioClipData> uiAndDialogueSounds = new List<AudioClipData>();

    [Header("📦 6. OTHER / UNCATEGORIZED")]
    [Tooltip("Pozostałe lub nieprzypisane dźwięki.")]
    [SerializeField] private List<AudioClipData> otherSounds = new List<AudioClipData>();

    // Zachowane dla migracji starych danych z pliku assetu
    [SerializeField] private List<AudioClipData> audioGroups = new List<AudioClipData>();

    public List<AudioClipData> PlayerSounds => playerSounds;
    public List<AudioClipData> LightAndAmbienceSounds => lightAndAmbienceSounds;
    public List<AudioClipData> MinigameSounds => minigameSounds;
    public List<AudioClipData> FurnitureSounds => furnitureSounds;
    public List<AudioClipData> UIAndDialogueSounds => uiAndDialogueSounds;
    public List<AudioClipData> OtherSounds => otherSounds;
    public List<AudioClipData> LegacyAudioGroups => audioGroups;

    private void OnEnable()
    {
        // Automatyczna migracja jeśli w starej liście są dane, a nowe kategorie są puste
        if (audioGroups != null && audioGroups.Count > 0 && TotalCategorizedCount == 0)
        {
            CategorizeAllLegacySounds();
        }
    }

    public int TotalCategorizedCount => 
        (playerSounds?.Count ?? 0) + 
        (lightAndAmbienceSounds?.Count ?? 0) + 
        (minigameSounds?.Count ?? 0) + 
        (furnitureSounds?.Count ?? 0) + 
        (uiAndDialogueSounds?.Count ?? 0) + 
        (otherSounds?.Count ?? 0);

    public AudioClipData Get(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return null;

        AudioClipData found = FindInList(playerSounds, groupName)
                           ?? FindInList(lightAndAmbienceSounds, groupName)
                           ?? FindInList(minigameSounds, groupName)
                           ?? FindInList(furnitureSounds, groupName)
                           ?? FindInList(uiAndDialogueSounds, groupName)
                           ?? FindInList(otherSounds, groupName)
                           ?? FindInList(audioGroups, groupName);

        return found;
    }

    private AudioClipData FindInList(List<AudioClipData> list, string name)
    {
        if (list == null) return null;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && string.Equals(list[i].groupName, name, StringComparison.OrdinalIgnoreCase))
                return list[i];
        }
        return null;
    }

    public List<AudioClipData> GetAllGroups()
    {
        var all = new List<AudioClipData>();
        if (playerSounds != null) all.AddRange(playerSounds);
        if (lightAndAmbienceSounds != null) all.AddRange(lightAndAmbienceSounds);
        if (minigameSounds != null) all.AddRange(minigameSounds);
        if (furnitureSounds != null) all.AddRange(furnitureSounds);
        if (uiAndDialogueSounds != null) all.AddRange(uiAndDialogueSounds);
        if (otherSounds != null) all.AddRange(otherSounds);
        if (audioGroups != null) all.AddRange(audioGroups);
        return all;
    }

    public void CategorizeAllLegacySounds()
    {
        if (audioGroups == null || audioGroups.Count == 0) return;

        foreach (var group in audioGroups)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.groupName)) continue;
            string n = group.groupName.ToLowerInvariant();

            if (n.Contains("player") || n.Contains("step") || n.Contains("dress") || n.Contains("pickup") || n.Contains("drop"))
            {
                if (!ContainsGroup(playerSounds, group.groupName)) playerSounds.Add(group);
            }
            else if (n.Contains("light") || n.Contains("lamp") || n.Contains("switch") || n.Contains("hum") || n.Contains("ambient"))
            {
                if (!ContainsGroup(lightAndAmbienceSounds, group.groupName)) lightAndAmbienceSounds.Add(group);
            }
            else if (n.Contains("sharpen") || n.Contains("ostrzen") || n.Contains("razor") || n.Contains("minigame") || n.Contains("game"))
            {
                if (!ContainsGroup(minigameSounds, group.groupName)) minigameSounds.Add(group);
            }
            else if (n.Contains("door") || n.Contains("drawer") || n.Contains("fridge") || n.Contains("wadrobe") || n.Contains("wardrobe") || n.Contains("handle"))
            {
                if (!ContainsGroup(furnitureSounds, group.groupName)) furnitureSounds.Add(group);
            }
            else if (n.Contains("dialog") || n.Contains("ui") || n.Contains("click") || n.Contains("text"))
            {
                if (!ContainsGroup(uiAndDialogueSounds, group.groupName)) uiAndDialogueSounds.Add(group);
            }
            else
            {
                if (!ContainsGroup(otherSounds, group.groupName)) otherSounds.Add(group);
            }
        }

        audioGroups.Clear();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private bool ContainsGroup(List<AudioClipData> list, string groupName)
    {
        if (list == null) return false;
        return list.Exists(g => g != null && string.Equals(g.groupName, groupName, StringComparison.OrdinalIgnoreCase));
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