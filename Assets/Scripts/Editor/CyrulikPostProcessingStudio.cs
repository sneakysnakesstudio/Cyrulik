#if UNITY_EDITOR
using Cyrulik.PostProcessing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CyrulikPostProcessingStudio : EditorWindow
{
    private Volume _activeVolume;
    private VolumeProfile _activeProfile;
    private Camera _mainCamera;
    private Vector2 _scrollPos;

    [MenuItem("Tools/Cyrulik/🎨 Post-Processing Studio & Presets", false, 1)]
    [MenuItem("Window/Cyrulik Post-Processing Studio", false, 200)]
    public static void OpenWindow()
    {
        var win = GetWindow<CyrulikPostProcessingStudio>("Post-Processing Studio");
        win.minSize = new Vector2(420, 560);
        win.Show();
    }

    [MenuItem("Tools/Cyrulik/Quick Presets/🔪 Mroczny Cyrulik (Zalecany)", false, 20)]
    public static void QuickApplyMrocznyCyrulik()
    {
        var profile = EnsureVolumeAndGetProfile();
        if (profile != null)
        {
            Undo.RecordObject(profile, "Apply Mroczny Cyrulik Preset");
            CyrulikPostProcessingRuntimeSwitcher.ApplyMrocznyCyrulik(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#4CAF50><b>[Cyrulik PP]</b></color> Zastosowano styl: <b>Mroczny Cyrulik</b>!");
        }
    }

    [MenuItem("Tools/Cyrulik/Quick Presets/📼 Retro PSX Horror", false, 21)]
    public static void QuickApplyRetroPSX()
    {
        var profile = EnsureVolumeAndGetProfile();
        if (profile != null)
        {
            Undo.RecordObject(profile, "Apply Retro PSX Horror Preset");
            CyrulikPostProcessingRuntimeSwitcher.ApplyRetroPSXHorror(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#4CAF50><b>[Cyrulik PP]</b></color> Zastosowano styl: <b>Retro PSX Horror</b>!");
        }
    }

    [MenuItem("Tools/Cyrulik/Quick Presets/🕯️ Ciepły Vintage", false, 22)]
    public static void QuickApplyCieplyVintage()
    {
        var profile = EnsureVolumeAndGetProfile();
        if (profile != null)
        {
            Undo.RecordObject(profile, "Apply Ciepły Vintage Preset");
            CyrulikPostProcessingRuntimeSwitcher.ApplyCieplyVintage(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#4CAF50><b>[Cyrulik PP]</b></color> Zastosowano styl: <b>Ciepły Vintage</b>!");
        }
    }

    [MenuItem("Tools/Cyrulik/Quick Presets/🎞️ Film Noir", false, 23)]
    public static void QuickApplyFilmNoir()
    {
        var profile = EnsureVolumeAndGetProfile();
        if (profile != null)
        {
            Undo.RecordObject(profile, "Apply Film Noir Preset");
            CyrulikPostProcessingRuntimeSwitcher.ApplyFilmNoir(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#4CAF50><b>[Cyrulik PP]</b></color> Zastosowano styl: <b>Film Noir</b>!");
        }
    }

    [MenuItem("Tools/Cyrulik/Quick Presets/🧼 Czysty Reset (Neutral)", false, 24)]
    public static void QuickApplyClean()
    {
        var profile = EnsureVolumeAndGetProfile();
        if (profile != null)
        {
            Undo.RecordObject(profile, "Reset Post Processing");
            CyrulikPostProcessingRuntimeSwitcher.ApplyClean(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#FFC107><b>[Cyrulik PP]</b></color> Zresetowano efekty post-processingu!");
        }
    }

    [MenuItem("Tools/Cyrulik/🛠️ Fix & Setup Scene Volume + Cameras", false, 10)]
    public static void SetupSceneVolumeAndCameras()
    {
        EnsureVolumeAndGetProfile();
        EnsureCamerasPostProcessing();
        Debug.Log("<color=#4CAF50><b>[Cyrulik PP]</b></color> Scena i kamery zostały pomyślnie skonfigurowane pod Post-Processing!");
    }

    private void OnEnable()
    {
        RefreshReferences();
    }

    private void OnFocus()
    {
        RefreshReferences();
    }

    private void RefreshReferences()
    {
        _activeVolume = FindAnyObjectByType<Volume>();
        if (_activeVolume != null && _activeVolume.profile != null)
        {
            _activeProfile = _activeVolume.profile;
        }
        else if (_activeProfile == null)
        {
            _activeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
        }

        _mainCamera = Camera.main;
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space(10);
        DrawHeader();

        EditorGUILayout.Space(10);
        DrawSceneStatus();

        EditorGUILayout.Space(15);
        DrawPresetButtons();

        EditorGUILayout.Space(15);
        DrawProfileTweakSection();

        EditorGUILayout.Space(20);
        DrawUtilities();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.95f, 0.75f, 0.35f) }
        };
        EditorGUILayout.LabelField("✂️ CYRULIK - POST-PROCESSING STUDIO ✂️", titleStyle);
        EditorGUILayout.LabelField("Szybka stylizacja graficzna i presety na prezentację", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawSceneStatus()
    {
        EditorGUILayout.LabelField("1. Status Sceny & Kamer", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Volume check
        if (_activeVolume != null)
        {
            EditorGUILayout.HelpBox($"✓ Znaleziono Global Volume: '{_activeVolume.gameObject.name}'", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("⚠️ Brak obiektu Volume w aktywnej scenie!", MessageType.Warning);
            if (GUILayout.Button("➕ Utwórz Global Volume w Scenie", GUILayout.Height(26)))
            {
                EnsureVolumeAndGetProfile();
                RefreshReferences();
            }
        }

        // Camera check
        if (_mainCamera != null)
        {
            var camData = _mainCamera.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null && camData.renderPostProcessing)
            {
                EditorGUILayout.HelpBox($"✓ Main Camera '{_mainCamera.name}' ma włączony Post-Processing", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"⚠️ Main Camera '{_mainCamera.name}' ma wyłączony Post-Processing!", MessageType.Warning);
                if (GUILayout.Button("🎥 Włącz Post-Processing na Kamerze", GUILayout.Height(24)))
                {
                    EnsureCamerasPostProcessing();
                    RefreshReferences();
                }
            }
        }

        EditorGUILayout.Space(5);
        _activeProfile = (VolumeProfile)EditorGUILayout.ObjectField("Aktywny Profil Volume:", _activeProfile, typeof(VolumeProfile), false);

        if (_activeVolume != null && _activeProfile != null && _activeVolume.profile != _activeProfile)
        {
            if (GUILayout.Button("Przypisz ten profil do Volume w scenie"))
            {
                Undo.RecordObject(_activeVolume, "Set Volume Profile");
                _activeVolume.profile = _activeProfile;
                EditorUtility.SetDirty(_activeVolume);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPresetButtons()
    {
        EditorGUILayout.LabelField("2. Gotowe Presety Stylów (1-Click)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUI.backgroundColor = new Color(0.9f, 0.5f, 0.2f);
        if (GUILayout.Button("🔪 Mroczny Cyrulik (Stylizowany Thriller / Domyślny)", GUILayout.Height(36)))
        {
            ApplyPresetAndSave(CyrulikPostProcessingRuntimeSwitcher.PresetType.MrocznyCyrulik);
        }

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.5f);
        if (GUILayout.Button("📼 Retro PSX / VHS Horror (Mocny Grain + Aberracja)", GUILayout.Height(30)))
        {
            ApplyPresetAndSave(CyrulikPostProcessingRuntimeSwitcher.PresetType.RetroPSXHorror);
        }

        GUI.backgroundColor = new Color(0.9f, 0.8f, 0.3f);
        if (GUILayout.Button("🕯️ Ciepły Vintage (Klimatyczny Salon Fryzjerski)", GUILayout.Height(30)))
        {
            ApplyPresetAndSave(CyrulikPostProcessingRuntimeSwitcher.PresetType.CieplyVintage);
        }

        GUI.backgroundColor = new Color(0.6f, 0.6f, 0.7f);
        if (GUILayout.Button("🎞️ Film Noir (Mroczny / Monochromatyczny)", GUILayout.Height(30)))
        {
            ApplyPresetAndSave(CyrulikPostProcessingRuntimeSwitcher.PresetType.FilmNoir);
        }

        GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
        if (GUILayout.Button("🧼 Czysty Reset (Neutralne ustawienia)", GUILayout.Height(24)))
        {
            ApplyPresetAndSave(CyrulikPostProcessingRuntimeSwitcher.PresetType.Clean);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();
    }

    private void DrawProfileTweakSection()
    {
        if (_activeProfile == null) return;

        EditorGUILayout.LabelField("3. Szybkie Dostrajanie Parametrów (Live Tweaks)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUI.BeginChangeCheck();

        // 1. Color Adjustments
        if (_activeProfile.TryGet<ColorAdjustments>(out var ca))
        {
            EditorGUILayout.LabelField("🎨 Barwa & Kontrast", EditorStyles.boldLabel);
            ca.postExposure.value = EditorGUILayout.Slider("Post Exposure", ca.postExposure.value, -2f, 2f);
            ca.contrast.value = EditorGUILayout.Slider("Contrast", ca.contrast.value, -50f, 50f);
            ca.saturation.value = EditorGUILayout.Slider("Saturation", ca.saturation.value, -100f, 50f);
            ca.colorFilter.value = EditorGUILayout.ColorField("Color Filter (Tint)", ca.colorFilter.value);
            EditorGUILayout.Space(6);
        }

        // 2. Bloom
        if (_activeProfile.TryGet<Bloom>(out var bloom))
        {
            EditorGUILayout.LabelField("✨ Bloom (Poświata świateł i ostrza)", EditorStyles.boldLabel);
            bloom.active = EditorGUILayout.Toggle("Włączony Bloom", bloom.active);
            if (bloom.active)
            {
                bloom.threshold.value = EditorGUILayout.Slider("Threshold (Światła > 1.0)", bloom.threshold.value, 0f, 3f);
                bloom.intensity.value = EditorGUILayout.Slider("Intensity", bloom.intensity.value, 0f, 5f);
                bloom.scatter.value = EditorGUILayout.Slider("Scatter", bloom.scatter.value, 0f, 1f);
                bloom.tint.value = EditorGUILayout.ColorField("Bloom Tint", bloom.tint.value);
            }
            EditorGUILayout.Space(6);
        }

        // 3. Vignette
        if (_activeProfile.TryGet<Vignette>(out var vig))
        {
            EditorGUILayout.LabelField("🌑 Winieta (Cienie po rogach)", EditorStyles.boldLabel);
            vig.active = EditorGUILayout.Toggle("Włączona Winieta", vig.active);
            if (vig.active)
            {
                vig.intensity.value = EditorGUILayout.Slider("Intensity", vig.intensity.value, 0f, 1f);
                vig.smoothness.value = EditorGUILayout.Slider("Smoothness", vig.smoothness.value, 0f, 1f);
            }
            EditorGUILayout.Space(6);
        }

        // 4. Film Grain
        if (_activeProfile.TryGet<FilmGrain>(out var grain))
        {
            EditorGUILayout.LabelField("📺 Film Grain (Ziarno / Szum retro)", EditorStyles.boldLabel);
            grain.active = EditorGUILayout.Toggle("Włączone Ziarno", grain.active);
            if (grain.active)
            {
                grain.type.value = (FilmGrainLookup)EditorGUILayout.EnumPopup("Typ Ziarna", grain.type.value);
                grain.intensity.value = EditorGUILayout.Slider("Intensity", grain.intensity.value, 0f, 1f);
            }
            EditorGUILayout.Space(6);
        }

        // 5. Chromatic Aberration
        if (_activeProfile.TryGet<ChromaticAberration>(out var caEff))
        {
            EditorGUILayout.LabelField("🌈 Aberracja Chromatyczna (Rozmycie RGB)", EditorStyles.boldLabel);
            caEff.active = EditorGUILayout.Toggle("Włączona Aberracja", caEff.active);
            if (caEff.active)
            {
                caEff.intensity.value = EditorGUILayout.Slider("Intensity", caEff.intensity.value, 0f, 1f);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(_activeProfile);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawUtilities()
    {
        EditorGUILayout.LabelField("4. Przydatne Narzędzia na Prezentację", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (GUILayout.Button("🎮 Dodaj Runtime Switcher do Sceny (Klawisze 1-5 w Playmode)", GUILayout.Height(28)))
        {
            AddRuntimeSwitcherToScene();
        }

        if (GUILayout.Button("💾 Zapisz Profil na Dysk (Save Asset)", GUILayout.Height(24)))
        {
            if (_activeProfile != null)
            {
                EditorUtility.SetDirty(_activeProfile);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=#4CAF50><b>[Cyrulik PP]</b></color> Profil został pomyślnie zapisany.");
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void ApplyPresetAndSave(CyrulikPostProcessingRuntimeSwitcher.PresetType preset)
    {
        var profile = _activeProfile != null ? _activeProfile : EnsureVolumeAndGetProfile();
        if (profile == null) return;

        Undo.RecordObject(profile, $"Apply {preset} Preset");

        switch (preset)
        {
            case CyrulikPostProcessingRuntimeSwitcher.PresetType.MrocznyCyrulik:
                CyrulikPostProcessingRuntimeSwitcher.ApplyMrocznyCyrulik(profile);
                break;
            case CyrulikPostProcessingRuntimeSwitcher.PresetType.RetroPSXHorror:
                CyrulikPostProcessingRuntimeSwitcher.ApplyRetroPSXHorror(profile);
                break;
            case CyrulikPostProcessingRuntimeSwitcher.PresetType.CieplyVintage:
                CyrulikPostProcessingRuntimeSwitcher.ApplyCieplyVintage(profile);
                break;
            case CyrulikPostProcessingRuntimeSwitcher.PresetType.FilmNoir:
                CyrulikPostProcessingRuntimeSwitcher.ApplyFilmNoir(profile);
                break;
            case CyrulikPostProcessingRuntimeSwitcher.PresetType.Clean:
                CyrulikPostProcessingRuntimeSwitcher.ApplyClean(profile);
                break;
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        RefreshReferences();
        Repaint();
        Debug.Log($"<color=#4CAF50><b>[Cyrulik PP]</b></color> Zastosowano preset: <b>{preset}</b>!");
    }

    private static VolumeProfile EnsureVolumeAndGetProfile()
    {
        Volume volume = FindAnyObjectByType<Volume>();
        if (volume == null)
        {
            GameObject volumeGo = new GameObject("Global Post-Process Volume", typeof(Volume));
            volume = volumeGo.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.weight = 1.0f;
            Undo.RegisterCreatedObjectUndo(volumeGo, "Create Global Volume");
        }

        if (volume.profile == null)
        {
            VolumeProfile existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            if (existing != null)
            {
                volume.profile = existing;
            }
            else
            {
                VolumeProfile newProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(newProfile, "Assets/Settings/Cyrulik_PostProcessing_Profile.asset");
                AssetDatabase.SaveAssets();
                volume.profile = newProfile;
            }
            EditorUtility.SetDirty(volume);
        }

        EnsureCamerasPostProcessing();
        return volume.profile;
    }

    private static void EnsureCamerasPostProcessing()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (var cam in cameras)
        {
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            if (!camData.renderPostProcessing)
            {
                Undo.RecordObject(camData, "Enable Post Processing on Camera");
                camData.renderPostProcessing = true;
                EditorUtility.SetDirty(camData);
            }
        }
    }

    private static void AddRuntimeSwitcherToScene()
    {
        var existing = FindAnyObjectByType<CyrulikPostProcessingRuntimeSwitcher>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("<color=#2196F3><b>[Cyrulik PP]</b></color> Runtime Switcher już istnieje w scenie.");
            return;
        }

        Volume volume = FindAnyObjectByType<Volume>();
        GameObject targetGo = volume != null ? volume.gameObject : new GameObject("PostProcessing_Manager");
        var switcher = targetGo.AddComponent<CyrulikPostProcessingRuntimeSwitcher>();
        Undo.RegisterCreatedObjectUndo(switcher, "Add PP Runtime Switcher");
        Selection.activeGameObject = targetGo;
        Debug.Log("<color=#4CAF50><b>[Cyrulik PP]</b></color> Dodano <b>CyrulikPostProcessingRuntimeSwitcher</b> do sceny! Podczas Playmode możesz używać klawiszy [1]-[5].");
    }
}
#endif
