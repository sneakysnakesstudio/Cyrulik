using UnityEngine;

/// <summary>
/// Skrypt animujący realistyczne falowanie zasłony (Curtain Sway / Wind Effect).
/// Oblicza przesunięcie wierzchołków mesha (Vertex Displacement) z uwzględnieniem
/// wysokości Y — góra zasłony pozostaje przymocowana do karnisza, a dół łagodnie faluje na wietrze.
/// Posiada również opcjonalną reakcję na bliskość gracza (podmuch przy przechodzeniu!).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class CurtainSway : MonoBehaviour
{
    [Header("Ustawienia Falowania Wiatru")]
    [Tooltip("Szybkość falowania wiatru.")]
    [SerializeField] private float waveSpeed = 2.2f;

    [Tooltip("Siła / amplituda falowania przód-tył (amplituda Z).")]
    [SerializeField] private float waveAmplitudeZ = 0.07f;

    [Tooltip("Siła / amplituda falowania bocznego (amplituda X).")]
    [SerializeField] private float waveAmplitudeX = 0.025f;

    [Tooltip("Gęstość fal na długości zasłony (częstotliwość przestrzenna).")]
    [SerializeField] private float waveFrequency = 1.4f;

    [Header("Mocowanie Zasłony (Karnisz)")]
    [Tooltip("Ułamek wysokości górnej części zasłony, która pozostaje nieruchoma (0.15 = górne 15% sztywno przymocowane do drewna).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float topPinMargin = 0.15f;

    [Header("Reakcja na Gracza (Opcjonalnie)")]
    [Tooltip("Czy zasłona ma mocniej zafalować gdy gracz przechodzi blisko?")]
    [SerializeField] private bool reactToPlayer = true;

    [Tooltip("Zasięg reakcji na gracza (w metrach).")]
    [SerializeField] private float playerTriggerDistance = 1.3f;

    [Tooltip("Siła dodatkowego podmuchu przy przejściu gracza.")]
    [SerializeField] private float playerPushForce = 0.12f;

    private MeshFilter _meshFilter;
    private Mesh _originalMesh;
    private Mesh _clonedMesh;

    private Vector3[] _baseVertices;
    private Vector3[] _displacedVertices;

    private float _minY;
    private float _maxY;
    private float _meshHeight;

    private float _playerImpulse = 0f;
    private Transform _playerTransform;

    private void Start()
    {
        if (gameObject.isStatic)
        {
            Debug.LogError($"[CurtainSway] UWAGA! Obiekt {gameObject.name} jest ustawiony jako STATIC! Skrypty modyfikujące wierzchołki (Vertex Displacement) nie działają na statycznych obiektach w Buildzie. Odznacz 'Static' w Inspektorze!", this);
        }

        _meshFilter = GetComponent<MeshFilter>();
        if (_meshFilter == null || _meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("[CurtainSway] Brak MeshFilter lub sharedMesh na obiekcie!", this);
            enabled = false;
            return;
        }

        // Tworzymy kopię mesha w pamięci
        _originalMesh = _meshFilter.sharedMesh;
        _clonedMesh = Instantiate(_originalMesh);
        _clonedMesh.name = _originalMesh.name + "_SwayInstance";
        _clonedMesh.MarkDynamic(); // OPTYMALIZACJA: Mówimy Unity, że ten mesh będzie często zmieniany (szybsze przesyłanie do GPU)
        _meshFilter.mesh = _clonedMesh;

        _baseVertices = _originalMesh.vertices;
        _displacedVertices = new Vector3[_baseVertices.Length];

        // Wyznaczamy zakres Y
        _minY = float.MaxValue;
        _maxY = float.MinValue;

        for (int i = 0; i < _baseVertices.Length; i++)
        {
            float y = _baseVertices[i].y;
            if (y < _minY) _minY = y;
            if (y > _maxY) _maxY = y;
        }

        _meshHeight = Mathf.Max(0.01f, _maxY - _minY);

        // OPTYMALIZACJA: Zamiast przeliczać Bounds co klatkę, powiększamy je raz o maksymalną amplitudę
        Bounds newBounds = _clonedMesh.bounds;
        newBounds.Expand(new Vector3(waveAmplitudeX * 2f, 0f, waveAmplitudeZ * 2f + playerPushForce));
        _clonedMesh.bounds = newBounds;

        CharacterController playerCC = FindAnyObjectByType<CharacterController>();
        if (playerCC != null)
        {
            _playerTransform = playerCC.transform;
        }
    }

    private void Update()
    {
        if (_baseVertices == null || _clonedMesh == null) return;

        CheckPlayerInteraction();
        AnimateVertices();
    }

    private void CheckPlayerInteraction()
    {
        if (!reactToPlayer) return;

        if (_playerTransform == null)
        {
            CharacterController playerCC = FindAnyObjectByType<CharacterController>();
            if (playerCC != null) _playerTransform = playerCC.transform;
        }

        if (_playerTransform != null)
        {
            float dist = (_playerTransform.position - transform.position).sqrMagnitude;
            if (dist < playerTriggerDistance * playerTriggerDistance)
            {
                _playerImpulse = Mathf.Lerp(_playerImpulse, playerPushForce, Time.deltaTime * 5f);
            }
            else
            {
                _playerImpulse = Mathf.Lerp(_playerImpulse, 0f, Time.deltaTime * 2f);
            }
        }
    }

    private void AnimateVertices()
    {
        float time = Time.time * waveSpeed;

        for (int i = 0; i < _baseVertices.Length; i++)
        {
            Vector3 v = _baseVertices[i];
            float normalizedY = (v.y - _minY) / _meshHeight;
            float swayWeight = 1f - normalizedY;

            if (normalizedY > (1f - topPinMargin))
            {
                swayWeight = 0f;
            }
            else
            {
                swayWeight = Mathf.SmoothStep(0f, 1f, (1f - topPinMargin - normalizedY) / (1f - topPinMargin));
            }

            if (swayWeight > 0.001f)
            {
                float spatialFactor = (v.x + v.y) * waveFrequency;
                float noiseZ = (Mathf.PerlinNoise(v.x * 2f + time * 0.5f, v.y * 2f) - 0.5f) * 0.5f;
                float waveZ = (Mathf.Sin(time + spatialFactor) + noiseZ) * (waveAmplitudeZ + _playerImpulse);

                float noiseX = (Mathf.PerlinNoise(v.y * 2f + time * 0.3f, v.z * 2f) - 0.5f);
                float waveX = (Mathf.Sin(time * 0.7f + spatialFactor * 0.5f) + noiseX * 0.5f) * waveAmplitudeX;

                v.z += waveZ * swayWeight;
                v.x += waveX * swayWeight;
            }

            _displacedVertices[i] = v;
        }

        _clonedMesh.vertices = _displacedVertices;
        _clonedMesh.RecalculateNormals();
        // OPTYMALIZACJA: Usunięto _clonedMesh.RecalculateBounds() z Update, granice są statycznie powiększone w Start
    }

    private void OnDestroy()
    {
        if (_clonedMesh != null)
        {
            Destroy(_clonedMesh);
        }
    }
}
