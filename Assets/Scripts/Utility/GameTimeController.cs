using System;
using TMPro;
using UnityEngine;

public class GameTimeController : MonoBehaviour
{
    [Header("Start Time")]
    [SerializeField] private int startHour = 16;
    [SerializeField] private int startMinute = 57;
    [SerializeField] private int startSecond = 0;

    [Header("Opening Time")]
    [SerializeField] private int openingHour = 17;
    [SerializeField] private int openingMinute = 0;

    [Header("Time Settings")]
    [Tooltip("1 = jedna sekunda realna to jedna sekunda w grze.")]
    [SerializeField] private float timeScale = 1f;

    [Header("UI")]
    [SerializeField] private TMP_Text timeText;

    public event Action OnOpeningTimeReached;

    public int Hour => Mathf.FloorToInt(_currentTime / 3600f) % 24;
    public int Minute => Mathf.FloorToInt(_currentTime / 60f) % 60;
    public int Second => Mathf.FloorToInt(_currentTime) % 60;

    public bool OpeningTimeReached { get; private set; }

    private float _currentTime;
    private float _openingTime;

    private void Start()
    {
        _currentTime =
            startHour * 3600f +
            startMinute * 60f +
            startSecond;

        _openingTime =
            openingHour * 3600f +
            openingMinute * 60f;

        UpdateTimeUI();
    }

    private void Update()
    {
        _currentTime += Time.deltaTime * timeScale;

        // Pełna doba
        if (_currentTime >= 86400f)
            _currentTime -= 86400f;

        CheckOpeningTime();
        UpdateTimeUI();
    }

    private void CheckOpeningTime()
    {
        if (OpeningTimeReached)
            return;

        if (_currentTime >= _openingTime)
        {
            OpeningTimeReached = true;

            Debug.Log("Opening time reached!");

            OnOpeningTimeReached?.Invoke();
        }
    }

    private void UpdateTimeUI()
    {
        if (timeText == null)
            return;

        timeText.text = $"{Hour:00}:{Minute:00}:{Second:00}";
    }

    public void SetTime(int hour, int minute, int second = 0)
    {
        _currentTime =
            hour * 3600f +
            minute * 60f +
            second;

        OpeningTimeReached = _currentTime >= _openingTime;

        UpdateTimeUI();
    }
}