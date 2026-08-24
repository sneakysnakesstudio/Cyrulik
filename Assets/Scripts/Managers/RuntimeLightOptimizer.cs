using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Optymalizator świateł runtime.
/// W buildzie wyłącza kosztowne real-time shadows z Point/Spot lightów — główna przyczyna
/// spadków FPS przy włączaniu świateł w URP (każde takie światło generuje osobną 1024x1024 shadowmapę).
/// Nie dotyka Directional Light (główne oświetlenie sceny).
/// Dodaj ten komponent raz do dowolnego GameObject w scenie.
/// </summary>
public class RuntimeLightOptimizer : MonoBehaviour
{
    [Header("Shadow Settings")]
    [Tooltip("Wyłącz cienie z Point/Spot lightów (największy zysk FPS w buildzie).")]
    [SerializeField] private bool disableAdditionalLightShadows = true;

    [Tooltip("Maksymalna odległość od gracza, przy której światło może rzucać cień (0 = wyłączone).")]
    [SerializeField] private float shadowCastingMaxDistance = 4f;

    [Tooltip("Jak często sprawdzać odległość do świateł (sekundy). Mniejsze = dokładniejsze, większe = szybsze.")]
    [SerializeField] private float updateInterval = 0.2f;

    [Header("Range Culling")]
    [Tooltip("Wyłącz światła całkowicie gdy gracz jest dalej niż ta odległość.")]
    [SerializeField] private float lightCullDistance = 15f;

    [Tooltip("Czy włączyć distance culling świateł.")]
    [SerializeField] private bool enableDistanceCulling = false;

    // ---

    private Light[] _allLights;
    private LightShadows[] _originalShadows;
    private Camera _playerCamera;
    private float _timer;

    private void Start()
    {
        _playerCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

        // Zbierz wszystkie światła w scenie
        _allLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _originalShadows = new LightShadows[_allLights.Length];

        for (int i = 0; i < _allLights.Length; i++)
        {
            _originalShadows[i] = _allLights[i].shadows;
        }

        // W buildzie aplikuj optymalizacje natychmiast
        ApplyOptimizations();
    }

    private void ApplyOptimizations()
    {
        if (_allLights == null) return;

        foreach (Light light in _allLights)
        {
            if (light == null) continue;

            // Directional light = główne oświetlenie sceny — nie ruszamy
            if (light.type == LightType.Directional) continue;

            // Point i Spot — wyłącz kosztowne real-time shadows
            if (disableAdditionalLightShadows)
            {
                light.shadows = LightShadows.None;
            }
        }

        Debug.Log($"[RuntimeLightOptimizer] Zoptymalizowano {_allLights.Length} świateł — wyłączono real-time shadows z Point/Spot lightów.");
    }

    private void Update()
    {
        // Distance culling — opcjonalne, dodatkowa oszczędność
        if (!enableDistanceCulling || _playerCamera == null || _allLights == null) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < updateInterval) return;
        _timer = 0f;

        Vector3 playerPos = _playerCamera.transform.position;

        for (int i = 0; i < _allLights.Length; i++)
        {
            Light light = _allLights[i];
            if (light == null) continue;
            if (light.type == LightType.Directional) continue;

            float dist = Vector3.Distance(playerPos, light.transform.position);

            // Światło włączone przez grę (intensity > 0) — sprawdź odległość
            if (light.enabled && light.intensity > 0.01f)
            {
                // Włącz shadow casting tylko gdy gracz jest blisko
                if (disableAdditionalLightShadows && shadowCastingMaxDistance > 0)
                {
                    light.shadows = dist <= shadowCastingMaxDistance
                        ? LightShadows.Hard
                        : LightShadows.None;
                }
            }
        }
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() { }
#endif
}
