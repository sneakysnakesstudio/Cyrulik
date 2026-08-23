using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Obsługuje poruszanie się uciekającej myszy po wyznaczonej trasie (Waypoints) przy użyciu DOTween.
/// Uruchamiane w przypadku braku uzbrojenia pułapki przed goleniem (mysz przebiega obok stanowiska i płoszy klienta).
/// </summary>
public class MouseRunner : MonoBehaviour
{
    [Header("Trasa Myszy (Waypoints)")]
    [Tooltip("Punkty w przestrzeni, po których mysz ma przebiec obok fotela / stanowiska golenia.")]
    [SerializeField] private Transform[] pathWaypoints;

    [Header("Prędkość i Ruch")]
    [Tooltip("Całkowity czas przebiegnięcia trasy w sekundach.")]
    [SerializeField] private float runDuration = 3.5f;

    [Tooltip("Typ ścieżki (CatmullRom dla płynnych łuków, Linear dla ostrych skrętów).")]
    [SerializeField] private PathType pathType = PathType.CatmullRom;

    [Tooltip("Obrót w stronę kierunku biegu (LookAhead 0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float lookAhead = 0.05f;

    [Header("Audio")]
    [Tooltip("Dźwięk pisków myszy w AudioManager.")]
    [SerializeField] private string squeakSound = "mouse_squeak";
    [SerializeField] private AudioClip customSqueakClip;
    [SerializeField] private AudioSource audioSource;

    [Header("Wizualia i Animacja")]
    [Tooltip("Wizualny model myszy (jeśli jest potomkiem).")]
    [SerializeField] private GameObject mouseVisual;

    [Tooltip("Czy włączyć drobne drganie w biegu (bobbing).")]
    [SerializeField] private bool enableWiggle = true;

    [Header("Zdarzenia")]
    [SerializeField] private UnityEvent onRunStarted;
    [SerializeField] private UnityEvent onRunFinished;

    private Tween _pathTween;
    private Tween _wiggleTween;
    private bool _isRunning = false;

    public bool IsRunning => _isRunning;

    private void Awake()
    {
        if (mouseVisual == null && transform.childCount > 0)
        {
            mouseVisual = transform.GetChild(0).gameObject;
        }

        // Domyślnie na starcie mysz ucieczkowa jest ukryta
        if (mouseVisual != null)
        {
            mouseVisual.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        _pathTween?.Kill();
        _wiggleTween?.Kill();
    }

    /// <summary>
    /// Uruchamia sekwencję przebiegnięcia myszy.
    /// </summary>
    public void StartRunning(Action onComplete = null)
    {
        if (_isRunning) return;

        if (pathWaypoints == null || pathWaypoints.Length == 0)
        {
            Debug.LogWarning("[MouseRunner] Brak przypisanych punktów trasy (pathWaypoints)!");
            onComplete?.Invoke();
            return;
        }

        _isRunning = true;

        // Ustaw pozycję startową
        transform.position = pathWaypoints[0].position;

        if (mouseVisual != null)
        {
            mouseVisual.SetActive(true);
        }

        // Dźwięk pisków
        PlaySqueakSound();

        // Przygotuj tablicę wektorów
        Vector3[] waypoints = new Vector3[pathWaypoints.Length];
        for (int i = 0; i < pathWaypoints.Length; i++)
        {
            waypoints[i] = pathWaypoints[i].position;
        }

        onRunStarted?.Invoke();

        // Efekt drobnego drgania / wigglowania w biegu
        if (enableWiggle && mouseVisual != null)
        {
            _wiggleTween = mouseVisual.transform
                .DOLocalRotate(new Vector3(0f, 15f, 0f), 0.08f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        // Ruch po ścieżce DOTween
        _pathTween = transform
            .DOPath(waypoints, runDuration, pathType, PathMode.Full3D)
            .SetLookAt(lookAhead)
            .SetEase(Ease.Linear)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                _wiggleTween?.Kill();
                _isRunning = false;

                if (mouseVisual != null)
                {
                    mouseVisual.SetActive(false);
                }

                onRunFinished?.Invoke();
                onComplete?.Invoke();

                Debug.Log("[MouseRunner] Mysz zakończyła bieg przez salon.");
            });
    }

    private void PlaySqueakSound()
    {
        if (customSqueakClip != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(customSqueakClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(customSqueakClip, transform.position);
            }
            return;
        }

        if (!string.IsNullOrEmpty(squeakSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(squeakSound);
        }
    }
}
