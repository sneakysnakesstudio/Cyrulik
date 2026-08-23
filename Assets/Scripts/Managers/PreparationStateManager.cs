using System;
using System.Collections.Generic;
using UnityEngine;

public class PreparationStateManager : MonoBehaviour
{
    public static PreparationStateManager Instance { get; private set; }

    [System.Serializable]
    public class PreparationTask
    {
        [Tooltip("Unikalny identyfikator zadania (np. lights_salon, stove_lit, razor_sharpened).")]
        public string taskId;

        [Tooltip("Tekst wyświetlany na ekranie podsumowania końcowego, jeśli zadanie zostało zrobione.")]
        public string displayName;

        [Tooltip("Ukryty stan - czy zadanie zostało poprawnie wykonane.")]
        public bool isCompleted;

        public PreparationTask(string id, string name, bool completed = false)
        {
            taskId = id;
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            isCompleted = completed;
        }
    }

    [Header("Tasks Configuration")]
    [Tooltip("Lista zadań przygotowawczych definiowanych w Inspectorze.")]
    [SerializeField]
    private List<PreparationTask> tasks = new List<PreparationTask>
    {
        new PreparationTask("proper_atmosphere", "Set the right mood"),
        new PreparationTask("stove_lit", "Lit the stove fire"),
        new PreparationTask("towel_prepared", "Prepared a hot towel"),
        new PreparationTask("razor_sharpened", "Sharpened the razor"),
        new PreparationTask("mouse_disposed", "Disposed of the mouse")
    };

    public event Action<string, bool> OnTaskStateChanged;

    public IReadOnlyList<PreparationTask> Tasks => tasks;

    private readonly Dictionary<string, PreparationTask> _taskLookup = 
        new Dictionary<string, PreparationTask>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildLookup();
    }

    private void RebuildLookup()
    {
        _taskLookup.Clear();
        foreach (PreparationTask task in tasks)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.taskId))
                continue;

            _taskLookup[task.taskId] = task;
        }
    }

    /// <summary>
    /// Ustawia stan zadania (true = zaliczone poprawnie, false = niezaliczone).
    /// </summary>
    public void SetTaskState(string taskId, bool completed)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return;

        if (_taskLookup.TryGetValue(taskId, out PreparationTask existingTask))
        {
            if (existingTask.isCompleted != completed)
            {
                existingTask.isCompleted = completed;
                OnTaskStateChanged?.Invoke(taskId, completed);
                Debug.Log($"[PreparationState] Zadanie '{taskId}' -> {(completed ? "ZALICZONE" : "NIEZALICZONE")}");
            }
        }
        else
        {
            // Dynamiczne dodanie zadania, jeśli nie było wcześniej na liście w Inspectorze
            PreparationTask newTask = new PreparationTask(taskId, taskId, completed);
            tasks.Add(newTask);
            _taskLookup[taskId] = newTask;
            OnTaskStateChanged?.Invoke(taskId, completed);
            Debug.Log($"[PreparationState] Nowe zadanie '{taskId}' -> {(completed ? "ZALICZONE" : "NIEZALICZONE")}");
        }
    }

    /// <summary>
    /// Oznacza zadanie jako poprawnie wykonane.
    /// </summary>
    public void CompleteTask(string taskId)
    {
        SetTaskState(taskId, true);
    }

    /// <summary>
    /// Oznacza zadanie jako niewykonane / cofnięte.
    /// </summary>
    public void InvalidateTask(string taskId)
    {
        SetTaskState(taskId, false);
    }

    /// <summary>
    /// Sprawdza, czy dane zadanie jest zaliczone.
    /// </summary>
    public bool IsTaskCompleted(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return false;

        if (_taskLookup.TryGetValue(taskId, out PreparationTask task))
        {
            return task.isCompleted;
        }

        return false;
    }

    /// <summary>
    /// Zwraca listę nazw wyświetlanych (displayName) TYLKO dla poprawnie wykonanych zadań.
    /// Wykorzystywane przez Ekran Podsumowania na koniec gry.
    /// </summary>
    public List<string> GetCompletedTaskSummaries()
    {
        List<string> completedList = new List<string>();
        foreach (PreparationTask task in tasks)
        {
            if (task != null && task.isCompleted)
            {
                completedList.Add(!string.IsNullOrWhiteSpace(task.displayName) ? task.displayName : task.taskId);
            }
        }
        return completedList;
    }

    /// <summary>
    /// Zwraca kopię wszystkich zadań oznaczonych jako ukończone.
    /// </summary>
    public List<PreparationTask> GetAllCompletedTasks()
    {
        List<PreparationTask> completedList = new List<PreparationTask>();
        foreach (PreparationTask task in tasks)
        {
            if (task != null && task.isCompleted)
            {
                completedList.Add(task);
            }
        }
        return completedList;
    }

    /// <summary>
    /// Resetuje wszystkie zadania do stanu false (np. przy nowym runie).
    /// </summary>
    public void ResetAllTasks()
    {
        foreach (PreparationTask task in tasks)
        {
            if (task != null)
            {
                task.isCompleted = false;
            }
        }
    }
}
