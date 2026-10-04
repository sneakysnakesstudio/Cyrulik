using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cyrulik.PostProcessing
{
    [AddComponentMenu("Cyrulik/Post Processing Runtime Switcher")]
    public class CyrulikPostProcessingRuntimeSwitcher : MonoBehaviour
    {
        public enum PresetType
        {
            MrocznyCyrulik,
            RetroPSXHorror,
            CieplyVintage,
            FilmNoir,
            Clean
        }

        [Header("Target Volume")]
        [Tooltip("Volume to modify. If empty, will look for Global Volume in scene.")]
        [SerializeField] private Volume _targetVolume;

        [Header("Settings")]
        [SerializeField] private PresetType _startPreset = PresetType.MrocznyCyrulik;
        [SerializeField] private bool _enableHotkeys = true;
        [SerializeField] private bool _showOnScreenHint = false;

        private PresetType _currentPreset;

        private void Awake()
        {
            if (_targetVolume == null)
            {
                _targetVolume = FindAnyObjectByType<Volume>();
            }

            if (_targetVolume != null && _targetVolume.profile != null)
            {
                ApplyPreset(_startPreset);
            }
        }

        private void Update()
        {
            if (!_enableHotkeys) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                ApplyPreset(PresetType.MrocznyCyrulik);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                ApplyPreset(PresetType.RetroPSXHorror);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                ApplyPreset(PresetType.CieplyVintage);
            }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            {
                ApplyPreset(PresetType.FilmNoir);
            }
            else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame)
            {
                ApplyPreset(PresetType.Clean);
            }
        }

        public void ApplyPreset(PresetType preset)
        {
            if (_targetVolume == null || _targetVolume.profile == null)
            {
                _targetVolume = FindAnyObjectByType<Volume>();
                if (_targetVolume == null || _targetVolume.profile == null) return;
            }

            VolumeProfile profile = _targetVolume.profile;
            _currentPreset = preset;

            switch (preset)
            {
                case PresetType.MrocznyCyrulik:
                    ApplyMrocznyCyrulik(profile);
                    break;
                case PresetType.RetroPSXHorror:
                    ApplyRetroPSXHorror(profile);
                    break;
                case PresetType.CieplyVintage:
                    ApplyCieplyVintage(profile);
                    break;
                case PresetType.FilmNoir:
                    ApplyFilmNoir(profile);
                    break;
                case PresetType.Clean:
                    ApplyClean(profile);
                    break;
            }
        }

        public static void ApplyMrocznyCyrulik(VolumeProfile profile)
        {
            GetOrAdd(profile, out ColorAdjustments ca);
            ca.active = true;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = -0.35f;
            ca.contrast.overrideState = true;
            ca.contrast.value = 35f;
            ca.saturation.overrideState = true;
            ca.saturation.value = -20f;
            ca.colorFilter.overrideState = true;
            ca.colorFilter.value = new Color(0.96f, 0.92f, 0.84f, 1f);

            GetOrAdd(profile, out Bloom bloom);
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.85f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.65f;
            bloom.tint.overrideState = true;
            bloom.tint.value = new Color(1f, 0.88f, 0.65f, 1f);

            GetOrAdd(profile, out Vignette vig);
            vig.active = true;
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.35f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.5f;
            vig.rounded.overrideState = true;
            vig.rounded.value = true;

            GetOrAdd(profile, out FilmGrain grain);
            grain.active = true;
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.42f;
            grain.response.overrideState = true;
            grain.response.value = 0.8f;

            GetOrAdd(profile, out ChromaticAberration caEff);
            caEff.active = true;
            caEff.intensity.overrideState = true;
            caEff.intensity.value = 0.22f;

            GetOrAdd(profile, out Tonemapping tone);
            tone.active = true;
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            GetOrAdd(profile, out SplitToning split);
            split.active = true;
            split.shadows.overrideState = true;
            split.shadows.value = new Color(0.42f, 0.46f, 0.50f, 1f);
            split.highlights.overrideState = true;
            split.highlights.value = new Color(0.56f, 0.52f, 0.44f, 1f);
            split.balance.overrideState = true;
            split.balance.value = -10f;
        }

        public static void ApplyRetroPSXHorror(VolumeProfile profile)
        {
            GetOrAdd(profile, out ColorAdjustments ca);
            ca.active = true;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = 0f;
            ca.contrast.overrideState = true;
            ca.contrast.value = 35f;
            ca.saturation.overrideState = true;
            ca.saturation.value = -5f;
            ca.colorFilter.overrideState = true;
            ca.colorFilter.value = new Color(0.88f, 0.95f, 0.88f, 1f);

            GetOrAdd(profile, out Bloom bloom);
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 1.2f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.75f;
            bloom.tint.overrideState = true;
            bloom.tint.value = new Color(0.9f, 1f, 0.9f, 1f);

            GetOrAdd(profile, out Vignette vig);
            vig.active = true;
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.45f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.6f;
            vig.rounded.overrideState = true;
            vig.rounded.value = true;

            GetOrAdd(profile, out FilmGrain grain);
            grain.active = true;
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Medium1;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.65f;
            grain.response.overrideState = true;
            grain.response.value = 0.9f;

            GetOrAdd(profile, out ChromaticAberration caEff);
            caEff.active = true;
            caEff.intensity.overrideState = true;
            caEff.intensity.value = 0.45f;

            GetOrAdd(profile, out Tonemapping tone);
            tone.active = true;
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            GetOrAdd(profile, out SplitToning split);
            split.active = true;
            split.shadows.overrideState = true;
            split.shadows.value = new Color(0.38f, 0.45f, 0.40f, 1f);
            split.highlights.overrideState = true;
            split.highlights.value = new Color(0.58f, 0.58f, 0.48f, 1f);
            split.balance.overrideState = true;
            split.balance.value = -15f;
        }

        public static void ApplyCieplyVintage(VolumeProfile profile)
        {
            GetOrAdd(profile, out ColorAdjustments ca);
            ca.active = true;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = -0.15f;
            ca.contrast.overrideState = true;
            ca.contrast.value = 18f;
            ca.saturation.overrideState = true;
            ca.saturation.value = -8f;
            ca.colorFilter.overrideState = true;
            ca.colorFilter.value = new Color(1f, 0.94f, 0.85f, 1f);

            GetOrAdd(profile, out Bloom bloom);
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.2f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.6f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.6f;
            bloom.tint.overrideState = true;
            bloom.tint.value = new Color(1f, 0.85f, 0.55f, 1f);

            GetOrAdd(profile, out Vignette vig);
            vig.active = true;
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.28f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.45f;
            vig.rounded.overrideState = true;
            vig.rounded.value = true;

            GetOrAdd(profile, out FilmGrain grain);
            grain.active = true;
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.3f;
            grain.response.overrideState = true;
            grain.response.value = 0.7f;

            GetOrAdd(profile, out ChromaticAberration caEff);
            caEff.active = true;
            caEff.intensity.overrideState = true;
            caEff.intensity.value = 0.12f;

            GetOrAdd(profile, out Tonemapping tone);
            tone.active = true;
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.Neutral;

            GetOrAdd(profile, out SplitToning split);
            split.active = true;
            split.shadows.overrideState = true;
            split.shadows.value = new Color(0.48f, 0.45f, 0.42f, 1f);
            split.highlights.overrideState = true;
            split.highlights.value = new Color(0.58f, 0.52f, 0.42f, 1f);
            split.balance.overrideState = true;
            split.balance.value = 0f;
        }

        public static void ApplyFilmNoir(VolumeProfile profile)
        {
            GetOrAdd(profile, out ColorAdjustments ca);
            ca.active = true;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = 0.2f;
            ca.contrast.overrideState = true;
            ca.contrast.value = 40f;
            ca.saturation.overrideState = true;
            ca.saturation.value = -85f;
            ca.colorFilter.overrideState = true;
            ca.colorFilter.value = new Color(0.9f, 0.92f, 0.95f, 1f);

            GetOrAdd(profile, out Bloom bloom);
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.1f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.9f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.7f;
            bloom.tint.overrideState = true;
            bloom.tint.value = Color.white;

            GetOrAdd(profile, out Vignette vig);
            vig.active = true;
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.5f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.65f;
            vig.rounded.overrideState = true;
            vig.rounded.value = true;

            GetOrAdd(profile, out FilmGrain grain);
            grain.active = true;
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Large01;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.55f;
            grain.response.overrideState = true;
            grain.response.value = 0.85f;

            GetOrAdd(profile, out ChromaticAberration caEff);
            caEff.active = true;
            caEff.intensity.overrideState = true;
            caEff.intensity.value = 0.15f;

            GetOrAdd(profile, out Tonemapping tone);
            tone.active = true;
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            GetOrAdd(profile, out SplitToning split);
            split.active = true;
            split.shadows.overrideState = true;
            split.shadows.value = new Color(0.35f, 0.38f, 0.45f, 1f);
            split.highlights.overrideState = true;
            split.highlights.value = new Color(0.55f, 0.55f, 0.55f, 1f);
            split.balance.overrideState = true;
            split.balance.value = -20f;
        }

        public static void ApplyClean(VolumeProfile profile)
        {
            if (profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca.postExposure.value = 0f;
                ca.contrast.value = 0f;
                ca.saturation.value = 0f;
                ca.colorFilter.value = Color.white;
            }

            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloom.intensity.value = 0f;
                bloom.active = false;
            }

            if (profile.TryGet<Vignette>(out var vig))
            {
                vig.intensity.value = 0f;
                vig.active = false;
            }

            if (profile.TryGet<FilmGrain>(out var grain))
            {
                grain.intensity.value = 0f;
                grain.active = false;
            }

            if (profile.TryGet<ChromaticAberration>(out var caEff))
            {
                caEff.intensity.value = 0f;
                caEff.active = false;
            }

            if (profile.TryGet<Tonemapping>(out var tone))
            {
                tone.mode.value = TonemappingMode.None;
                tone.active = false;
            }

            if (profile.TryGet<SplitToning>(out var split))
            {
                split.active = false;
            }
        }

        private static void GetOrAdd<T>(VolumeProfile profile, out T component) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out component))
            {
                component = profile.Add<T>(true);
            }
        }

        private void OnGUI()
        {
            if (!_showOnScreenHint) return;

            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUILayout.BeginArea(new Rect(10, 10, 240, 160), GUI.skin.box);
            GUILayout.Label("<b>🎨 Cyrulik Post-Processing</b>");
            GUILayout.Label($"Styl: <b>{_currentPreset}</b>");
            GUILayout.Label("[1] Mroczny Cyrulik");
            GUILayout.Label("[2] Retro PSX Horror");
            GUILayout.Label("[3] Ciepły Vintage");
            GUILayout.Label("[4] Film Noir");
            GUILayout.Label("[5] Czysty Reset");
            GUILayout.EndArea();
        }
    }
}
