using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralny menedżer efektów cząsteczkowych (Particle Effects) w grze Cyrulik.
/// Umożliwia wygodne odpalanie drobinek przy obiektach interaktywnych, lampach, 
/// rozbłysków przy interakcji oraz podpinanie zapętlonych poświat.
/// Posiada wbudowany Object Pooling oraz procedury zapasowe (fallback),
/// dzięki czemu działa od razu nawet bez przypisanych prefabów w Inspectorze.
/// </summary>
public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance { get; private set; }

    [System.Serializable]
    public class ParticlePreset
    {
        [Tooltip("Unikalne ID efektu (np. 'interactive_glint', 'sparkles', 'dust_motes', 'pickup_burst', 'lamp_dust').")]
        public string effectId;

        [Tooltip("Prefab systemu cząsteczek. Jeśli pusty, menedżer wygeneruje proceduralny efekt.")]
        public ParticleSystem prefab;

        [Tooltip("Domyślny kolor / zabarwienie cząsteczek.")]
        public Color defaultColor = Color.white;

        [Tooltip("Domyślna skala cząsteczek.")]
        [Range(0.1f, 5f)]
        public float defaultScale = 1f;

        [Tooltip("Liczba instancji w puli początkowej.")]
        public int initialPoolSize = 3;
    }

    [Header("Presety Cząsteczek")]
    [Tooltip("Lista zarejestrowanych presetów cząsteczek.")]
    [SerializeField]
    private List<ParticlePreset> presets = new List<ParticlePreset>
    {
        new ParticlePreset { effectId = "interactive_glint", defaultColor = new Color(1f, 0.92f, 0.6f, 0.85f), defaultScale = 0.5f, initialPoolSize = 5 },
        new ParticlePreset { effectId = "sparkles",          defaultColor = new Color(0.9f, 0.95f, 1f, 0.9f),   defaultScale = 0.6f, initialPoolSize = 5 },
        new ParticlePreset { effectId = "dust_motes",        defaultColor = new Color(1f, 0.95f, 0.8f, 0.4f),   defaultScale = 0.8f, initialPoolSize = 3 },
        new ParticlePreset { effectId = "pickup_burst",      defaultColor = new Color(1f, 0.85f, 0.3f, 1f),     defaultScale = 0.7f, initialPoolSize = 4 },
        new ParticlePreset { effectId = "lamp_dust",         defaultColor = new Color(1f, 0.9f, 0.7f, 0.5f),    defaultScale = 1.0f, initialPoolSize = 3 }
    };

    [Header("Ustawienia Ogólne")]
    [Tooltip("Czy obiekt menedżera ma przetrwać przeładowanie scen (DontDestroyOnLoad).")]
    [SerializeField] private bool persistAcrossScenes = true;

    // Słowniki i struktury wewnętrzne
    private readonly Dictionary<string, ParticlePreset> _presetLookup = 
        new Dictionary<string, ParticlePreset>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Queue<ParticleSystem>> _poolLookup = 
        new Dictionary<string, Queue<ParticleSystem>>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<Transform, Dictionary<string, ParticleSystem>> _attachedEffects = 
        new Dictionary<Transform, Dictionary<string, ParticleSystem>>();

    private Transform _poolContainer;
    private Material _defaultParticleMaterial;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        InitializeManager();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitializeManager()
    {
        _presetLookup.Clear();
        _poolLookup.Clear();
        _attachedEffects.Clear();

        GameObject container = new GameObject("_ParticlePool");
        container.transform.SetParent(transform);
        _poolContainer = container.transform;

        foreach (var preset in presets)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.effectId))
                continue;

            _presetLookup[preset.effectId] = preset;
            _poolLookup[preset.effectId] = new Queue<ParticleSystem>();

            if (preset.prefab != null && preset.initialPoolSize > 0)
            {
                for (int i = 0; i < preset.initialPoolSize; i++)
                {
                    ParticleSystem instance = Instantiate(preset.prefab, _poolContainer);
                    instance.gameObject.SetActive(false);
                    _poolLookup[preset.effectId].Enqueue(instance);
                }
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    // METODY ODPALANIA EFEKTÓW (DO WYBORU)
    // ──────────────────────────────────────────────────────────

    #region 1. Efekty Jednorazowe (One-Shot Spawns)

    /// <summary>
    /// Odpala jednorazowy efekt cząsteczkowy w podanej pozycji w świecie z opcjonalnym kolorem i skalą.
    /// </summary>
    public ParticleSystem PlayEffect(string effectId, Vector3 position, Quaternion rotation = default, Transform parent = null, Color? tint = null, float scaleMultiplier = 1f)
    {
        ParticleSystem ps = GetOrCreateParticleInstance(effectId, position, rotation, parent);
        if (ps == null) return null;

        ApplySettingsToSystem(ps, effectId, tint, scaleMultiplier);
        ps.gameObject.SetActive(true);
        ps.Play(true);

        // Automatyczny powrót do puli lub zniszczenie po zakończeniu emisji
        StartCoroutine(RecycleOrDestroyRoutine(ps, effectId));

        return ps;
    }

    /// <summary>
    /// Odpala efekt w pozycji i z orientacją wskazanego obiektu.
    /// </summary>
    public ParticleSystem PlayEffect(string effectId, Transform target, Vector3 localOffset = default, Color? tint = null, float scaleMultiplier = 1f)
    {
        if (target == null) return null;
        Vector3 worldPos = target.TransformPoint(localOffset);
        return PlayEffect(effectId, worldPos, target.rotation, null, tint, scaleMultiplier);
    }

    /// <summary>
    /// Szybkie odpalenie iskierek/błysków (np. w miejscu podniesienia przedmiotu).
    /// </summary>
    public ParticleSystem PlaySparkles(Vector3 position, Color? tint = null, float scale = 1f)
    {
        return PlayEffect("sparkles", position, Quaternion.identity, null, tint, scale);
    }

    /// <summary>
    /// Szybkie odpalenie dynamicznego rozbłysku (burst) przy interakcji gracza.
    /// </summary>
    public ParticleSystem PlayBurst(Vector3 position, Color? tint = null, float scale = 1f)
    {
        return PlayEffect("pickup_burst", position, Quaternion.identity, null, tint, scale);
    }

    #endregion

    #region 2. Efekty Zapętlone i Podpięte do Obiektu (Attached Looping Effects)

    /// <summary>
    /// Podpina ciągły efekt cząsteczkowy pod dany obiekt (np. mieniący się przedmiot, lampa, zwisający pasek).
    /// </summary>
    public ParticleSystem AttachLoopingEffect(string effectId, Transform parent, Vector3 localOffset = default, string key = null, Color? tint = null, float scaleMultiplier = 1f)
    {
        if (parent == null) return null;

        string attachKey = string.IsNullOrEmpty(key) ? effectId : key;

        if (!_attachedEffects.TryGetValue(parent, out var targetDict))
        {
            targetDict = new Dictionary<string, ParticleSystem>(StringComparer.OrdinalIgnoreCase);
            _attachedEffects[parent] = targetDict;
        }

        // Jeśli efekt już jest podpięty pod ten klucz, wznawiamy go
        if (targetDict.TryGetValue(attachKey, out ParticleSystem existingPs) && existingPs != null)
        {
            existingPs.transform.localPosition = localOffset;
            ApplySettingsToSystem(existingPs, effectId, tint, scaleMultiplier);
            existingPs.gameObject.SetActive(true);
            if (!existingPs.isPlaying) existingPs.Play(true);
            return existingPs;
        }

        // Tworzymy nową instancję zapętloną
        ParticleSystem ps = CreateNewParticleInstance(effectId, parent.TransformPoint(localOffset), parent.rotation, parent);
        if (ps == null) return null;

        ps.transform.localPosition = localOffset;
        var main = ps.main;
        main.loop = true;

        ApplySettingsToSystem(ps, effectId, tint, scaleMultiplier);
        ps.gameObject.SetActive(true);
        ps.Play(true);

        targetDict[attachKey] = ps;
        return ps;
    }

    /// <summary>
    /// Zatrzymuje i odczepia zapętlony efekt z obiektu.
    /// </summary>
    public void DetachLoopingEffect(Transform parent, string key = null, bool stopImmediate = false)
    {
        if (parent == null) return;

        if (_attachedEffects.TryGetValue(parent, out var targetDict))
        {
            if (string.IsNullOrEmpty(key))
            {
                // Odczep wszystkie efekty z tego obiektu
                foreach (var kvp in targetDict)
                {
                    StopAndCleanupAttached(kvp.Value, stopImmediate);
                }
                targetDict.Clear();
                _attachedEffects.Remove(parent);
            }
            else if (targetDict.TryGetValue(key, out ParticleSystem ps))
            {
                StopAndCleanupAttached(ps, stopImmediate);
                targetDict.Remove(key);
                if (targetDict.Count == 0) _attachedEffects.Remove(parent);
            }
        }
    }

    private void StopAndCleanupAttached(ParticleSystem ps, bool stopImmediate)
    {
        if (ps == null) return;

        if (stopImmediate)
        {
            Destroy(ps.gameObject);
        }
        else
        {
            var emission = ps.emission;
            emission.enabled = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(ps.gameObject, ps.main.startLifetime.constantMax + 0.5f);
        }
    }

    #endregion

    #region 3. Wygodne Metody Podświetlania Interaktywności (Interactive Highlight)

    /// <summary>
    /// Włącza delikatną poświatę / drobinki na wskazanym obiekcie, informujące gracza o możliwości interakcji.
    /// </summary>
    public ParticleSystem EnableInteractiveGlow(GameObject target, Color? tint = null, Vector3 localOffset = default, float scale = 0.6f)
    {
        if (target == null) return null;
        return AttachLoopingEffect("interactive_glint", target.transform, localOffset, "interactive_hint", tint, scale);
    }

    /// <summary>
    /// Wyłącza poświatę interaktywności z obiektu.
    /// </summary>
    public void DisableInteractiveGlow(GameObject target, bool immediate = false)
    {
        if (target == null) return;
        DetachLoopingEffect(target.transform, "interactive_hint", immediate);
    }

    /// <summary>
    /// Przełącza stan poświaty interaktywnej na obiekcie (true = włącz, false = wyłącz).
    /// </summary>
    public void SetInteractiveGlowActive(GameObject target, bool active, Color? tint = null, Vector3 localOffset = default, float scale = 0.6f)
    {
        if (active)
            EnableInteractiveGlow(target, tint, localOffset, scale);
        else
            DisableInteractiveGlow(target);
    }

    #endregion

    #region 4. Zarządzanie Instancjami i Fallback Proceduralny

    private ParticleSystem GetOrCreateParticleInstance(string effectId, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (_poolLookup.TryGetValue(effectId, out var queue) && queue.Count > 0)
        {
            ParticleSystem pooled = queue.Dequeue();
            if (pooled != null)
            {
                pooled.transform.SetParent(parent);
                pooled.transform.position = position;
                pooled.transform.rotation = rotation == default ? Quaternion.identity : rotation;
                return pooled;
            }
        }

        return CreateNewParticleInstance(effectId, position, rotation, parent);
    }

    private ParticleSystem CreateNewParticleInstance(string effectId, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (_presetLookup.TryGetValue(effectId, out var preset) && preset.prefab != null)
        {
            ParticleSystem instance = Instantiate(preset.prefab, position, rotation == default ? Quaternion.identity : rotation, parent);
            return instance;
        }

        // Proceduralny fallback, jeśli prefab nie został jeszcze przypisany w edytorze
        return CreateProceduralParticleSystem(effectId, position, rotation, parent);
    }

    private ParticleSystem CreateProceduralParticleSystem(string effectId, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject go = new GameObject($"Particle_{effectId}_Procedural");
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = rotation == default ? Quaternion.identity : rotation;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

        if (_defaultParticleMaterial == null)
        {
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                                 ?? Shader.Find("Particles/Standard Unlit") 
                                 ?? Shader.Find("Sprites/Default");
            _defaultParticleMaterial = new Material(defaultShader);
        }

        renderer.material = _defaultParticleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        var main = ps.main;
        main.playOnAwake = false;
        var emission = ps.emission;
        var shape = ps.shape;
        var colorOverLifetime = ps.colorOverLifetime;
        var sizeOverLifetime = ps.sizeOverLifetime;
        var velocityOverLifetime = ps.velocityOverLifetime;

        colorOverLifetime.enabled = true;
        sizeOverLifetime.enabled = true;

        // Dopasowanie konfiguracji w zależności od typu efektu
        if (effectId.Contains("burst") || effectId.Contains("pickup"))
        {
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
        }
        else if (effectId.Contains("dust") || effectId.Contains("lamp"))
        {
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 7.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.025f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.022f); // Maleńkie, realistyczne pyłki kurzu
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            emission.rateOverTime = 12; // Gęstsza chmura drobnych pyłków
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.9f, 1.2f, 0.9f); // Objętość stożka światła pod lampą

            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.015f, 0.015f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.005f, 0.02f); // Powolny taniec i dryf w powietrzu
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.015f, 0.015f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.035f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.15f;
        }
        else // Domyślne: "interactive_glint" / "sparkles"
        {
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            emission.rateOverTime = 5;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        }

        // Krzywa zanikania (Fade in / Fade out)
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.2f);
        sizeCurve.AddKey(0.5f, 1.0f);
        sizeCurve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        return ps;
    }

    private void ApplySettingsToSystem(ParticleSystem ps, string effectId, Color? tint, float scaleMultiplier)
    {
        if (ps == null) return;

        Color targetColor = Color.white;
        float baseScale = 1f;

        if (_presetLookup.TryGetValue(effectId, out var preset))
        {
            targetColor = preset.defaultColor;
            baseScale = preset.defaultScale;
        }

        if (tint.HasValue)
        {
            targetColor = tint.Value;
        }

        var main = ps.main;
        main.startColor = targetColor;
        ps.transform.localScale = Vector3.one * (baseScale * scaleMultiplier);
    }

    private System.Collections.IEnumerator RecycleOrDestroyRoutine(ParticleSystem ps, string effectId)
    {
        if (ps == null) yield break;

        float maxLifetime = ps.main.startLifetime.constantMax;
        float duration = ps.main.duration + maxLifetime + 0.2f;
        yield return new WaitForSeconds(duration);

        if (ps != null && ps.gameObject != null)
        {
            if (_poolLookup.TryGetValue(effectId, out var queue) && queue.Count < 10)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.transform.SetParent(_poolContainer);
                ps.gameObject.SetActive(false);
                queue.Enqueue(ps);
            }
            else
            {
                Destroy(ps.gameObject);
            }
        }
    }

    #endregion
}
