#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioDatabaseSO))]
public class AudioDatabaseEditor : Editor
{
    private AudioDatabaseSO _database;
    private SerializedProperty _playerSoundsProp;
    private SerializedProperty _lightSoundsProp;
    private SerializedProperty _minigameSoundsProp;
    private SerializedProperty _furnitureSoundsProp;
    private SerializedProperty _uiSoundsProp;
    private SerializedProperty _otherSoundsProp;
    private SerializedProperty _legacyAudioGroupsProp;

    private string _searchFilter = "";
    private int _selectedTab = 0;
    private readonly string[] _tabNames = new[]
    {
        "Wszystkie",
        "🧍 Player",
        "💡 Lights & Ambience",
        "🎮 MiniGames",
        "🚪 Furniture & Doors",
        "💬 UI & Dialogue",
        "📦 Inne"
    };

    private void OnEnable()
    {
        _database = (AudioDatabaseSO)target;
        _playerSoundsProp = serializedObject.FindProperty("playerSounds");
        _lightSoundsProp = serializedObject.FindProperty("lightAndAmbienceSounds");
        _minigameSoundsProp = serializedObject.FindProperty("minigameSounds");
        _furnitureSoundsProp = serializedObject.FindProperty("furnitureSounds");
        _uiSoundsProp = serializedObject.FindProperty("uiAndDialogueSounds");
        _otherSoundsProp = serializedObject.FindProperty("otherSounds");
        _legacyAudioGroupsProp = serializedObject.FindProperty("audioGroups");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. Nagłówek i statystyki
        DrawDatabaseHeader();

        // 2. Wyszukiwarka
        DrawSearchBar();

        // 3. Pasek zakładek / kategorii
        EditorGUILayout.Space(6);
        _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(28));
        EditorGUILayout.Space(8);

        // 4. Renderowanie zawartości w zależności od zakładki i filtra
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            DrawSearchResults();
        }
        else
        {
            switch (_selectedTab)
            {
                case 0: // Wszystkie kategorie w czytelnych sekcjach
                    DrawCategorySection("🧍 Player & Inventory", _playerSoundsProp, new Color(0.35f, 0.75f, 1f));
                    DrawCategorySection("💡 Lights & Ambience", _lightSoundsProp, new Color(1f, 0.85f, 0.35f));
                    DrawCategorySection("🎮 MiniGames & Razor", _minigameSoundsProp, new Color(0.4f, 0.95f, 0.45f));
                    DrawCategorySection("🚪 Furniture & Interactables", _furnitureSoundsProp, new Color(0.95f, 0.6f, 0.35f));
                    DrawCategorySection("💬 UI & Dialogues", _uiSoundsProp, new Color(0.85f, 0.5f, 0.95f));
                    if (_otherSoundsProp.arraySize > 0)
                        DrawCategorySection("📦 Pozostałe / Nieprzypisane", _otherSoundsProp, Color.gray);
                    break;

                case 1:
                    DrawCategoryList(_playerSoundsProp, "Dźwięki gracza, kroków, ubierania i ekwipunku");
                    break;
                case 2:
                    DrawCategoryList(_lightSoundsProp, "Przełączniki światła, lampki, szumy lodówki i tła");
                    break;
                case 3:
                    DrawCategoryList(_minigameSoundsProp, "Dźwięki minigier, ostrzenia brzytwy, trafień Good/Perfect/Miss");
                    break;
                case 4:
                    DrawCategoryList(_furnitureSoundsProp, "Dźwięki drzwi, szaf, lodówki, szuflad i klamek");
                    break;
                case 5:
                    DrawCategoryList(_uiSoundsProp, "Dźwięki dialogów, przewijania tekstu i kliknięć UI");
                    break;
                case 6:
                    DrawCategoryList(_otherSoundsProp, "Dźwięki pozostałe lub niesklasyfikowane");
                    break;
            }
        }

        // 5. Sekcja starych niesklasyfikowanych dźwięków (jeśli istnieją)
        if (_legacyAudioGroupsProp != null && _legacyAudioGroupsProp.arraySize > 0)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.HelpBox($"W bazie znajduje się {_legacyAudioGroupsProp.arraySize} niesklasyfikowanych dźwięków z poprzedniej wersji.", MessageType.Warning);
            if (GUILayout.Button("⚡ Automatycznie przenieś do odpowiednich kategorii", GUILayout.Height(30)))
            {
                _database.CategorizeAllLegacySounds();
                EditorUtility.SetDirty(_database);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDatabaseHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        
        int total = _database.TotalCategorizedCount + (_legacyAudioGroupsProp?.arraySize ?? 0);
        EditorGUILayout.LabelField($"🎵 Audio Database ({total} grup dźwięków)", EditorStyles.boldLabel);

        if (GUILayout.Button("⚡ Auto-Kategoryzuj", GUILayout.Width(130), GUILayout.Height(20)))
        {
            _database.CategorizeAllLegacySounds();
            EditorUtility.SetDirty(_database);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("🔍 Szukaj:", GUILayout.Width(55));
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
        {
            _searchFilter = "";
            GUIUtility.keyboardControl = 0;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCategorySection(string title, SerializedProperty listProp, Color headerColor)
    {
        Color oldColor = GUI.color;
        GUI.color = headerColor;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.color = oldColor;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{title} ({listProp.arraySize})", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Dodaj Dźwięk", EditorStyles.miniButton, GUILayout.Width(100)))
        {
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            SerializedProperty newElem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
            ResetNewElement(newElem);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);
        for (int i = 0; i < listProp.arraySize; i++)
        {
            DrawGroupElement(listProp.GetArrayElementAtIndex(i), listProp, i);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    private void DrawCategoryList(SerializedProperty listProp, string description)
    {
        EditorGUILayout.HelpBox(description, MessageType.None);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Grupy dźwięków ({listProp.arraySize})", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Dodaj nowy dźwięk do tej kategorii", GUILayout.Height(24)))
        {
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            SerializedProperty newElem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
            ResetNewElement(newElem);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        for (int i = 0; i < listProp.arraySize; i++)
        {
            DrawGroupElement(listProp.GetArrayElementAtIndex(i), listProp, i);
        }
    }

    private void DrawSearchResults()
    {
        EditorGUILayout.HelpBox($"Wyniki wyszukiwania dla: '{_searchFilter}'", MessageType.Info);
        EditorGUILayout.Space(4);

        DrawMatchingGroupsFromList(_playerSoundsProp, "Player");
        DrawMatchingGroupsFromList(_lightSoundsProp, "Lights");
        DrawMatchingGroupsFromList(_minigameSoundsProp, "MiniGames");
        DrawMatchingGroupsFromList(_furnitureSoundsProp, "Furniture");
        DrawMatchingGroupsFromList(_uiSoundsProp, "UI/Dialog");
        DrawMatchingGroupsFromList(_otherSoundsProp, "Inne");
        if (_legacyAudioGroupsProp != null)
            DrawMatchingGroupsFromList(_legacyAudioGroupsProp, "Legacy");
    }

    private void DrawMatchingGroupsFromList(SerializedProperty listProp, string categoryName)
    {
        if (listProp == null) return;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty elem = listProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = elem.FindPropertyRelative("groupName");
            if (nameProp != null && nameProp.stringValue.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"[{categoryName}]", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
                DrawGroupElement(elem, listProp, i);
            }
        }
    }

    private void DrawGroupElement(SerializedProperty groupProp, SerializedProperty parentList, int index)
    {
        if (groupProp == null) return;

        SerializedProperty nameProp = groupProp.FindPropertyRelative("groupName");
        SerializedProperty clipsProp = groupProp.FindPropertyRelative("clips");
        SerializedProperty volumeProp = groupProp.FindPropertyRelative("volume");

        string gName = string.IsNullOrEmpty(nameProp.stringValue) ? $"[Element {index}]" : nameProp.stringValue;
        int clipCount = clipsProp != null ? clipsProp.arraySize : 0;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        groupProp.isExpanded = EditorGUILayout.Foldout(groupProp.isExpanded, $"{gName}  ({clipCount} klipów)", true, EditorStyles.foldoutHeader);

        // Przycisk usuwania
        if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24)))
        {
            parentList.DeleteArrayElementAtIndex(index);
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (groupProp.isExpanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(nameProp, new GUIContent("Nazwa Grupy (ID)"));
            EditorGUILayout.PropertyField(volumeProp, new GUIContent("Głośność Bazowa"));
            EditorGUILayout.PropertyField(clipsProp, new GUIContent("Klipy Audio"), true);

            SerializedProperty randPitch = groupProp.FindPropertyRelative("randomizePitch");
            EditorGUILayout.PropertyField(randPitch, new GUIContent("Losuj Pitch (Wysokość)"));
            if (randPitch.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(groupProp.FindPropertyRelative("minPitch"), new GUIContent("Min Pitch"));
                EditorGUILayout.PropertyField(groupProp.FindPropertyRelative("maxPitch"), new GUIContent("Max Pitch"));
                EditorGUI.indentLevel--;
            }

            SerializedProperty randVol = groupProp.FindPropertyRelative("randomizeVolume");
            EditorGUILayout.PropertyField(randVol, new GUIContent("Losuj Głośność"));
            if (randVol.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(groupProp.FindPropertyRelative("minVolumeMultiplier"), new GUIContent("Min Mnożnik"));
                EditorGUILayout.PropertyField(groupProp.FindPropertyRelative("maxVolumeMultiplier"), new GUIContent("Max Mnożnik"));
                EditorGUI.indentLevel--;
            }

            SerializedProperty randRepeat = groupProp.FindPropertyRelative("preventImmediateRepeat");
            if (randRepeat != null)
                EditorGUILayout.PropertyField(randRepeat, new GUIContent("Nie powtarzaj pod rząd"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void ResetNewElement(SerializedProperty elem)
    {
        elem.FindPropertyRelative("groupName").stringValue = "new_audio_group";
        elem.FindPropertyRelative("volume").floatValue = 1f;
        SerializedProperty randRepeat = elem.FindPropertyRelative("preventImmediateRepeat");
        if (randRepeat != null) randRepeat.boolValue = true;
        elem.FindPropertyRelative("randomizePitch").boolValue = false;
        elem.FindPropertyRelative("minPitch").floatValue = 0.95f;
        elem.FindPropertyRelative("maxPitch").floatValue = 1.05f;
        elem.FindPropertyRelative("randomizeVolume").boolValue = false;
        elem.FindPropertyRelative("minVolumeMultiplier").floatValue = 0.9f;
        elem.FindPropertyRelative("maxVolumeMultiplier").floatValue = 1.1f;
        elem.FindPropertyRelative("clips").arraySize = 0;
        elem.isExpanded = true;
    }
}
#endif
