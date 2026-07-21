using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public sealed class CountdownTimer : MonoBehaviour
{
    [Header("Countdown")]
    [Min(0f)] public float countdownTime = 300f;
    [Tooltip("Use real time even if another UI changes Time.timeScale.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Display")]
    public TMP_Text timerText;

    [Header("Events")]
    [SerializeField, Tooltip("Optional Inspector event invoked once when the timer reaches zero.")]
    private UnityEvent onCountdownFinished;

    public event Action Completed;
    public float RemainingTime { get; private set; }
    public bool IsFinished { get; private set; }

    private void Awake()
    {
        if (timerText == null) timerText = GetComponent<TMP_Text>();
        if (timerText == null) Debug.LogError("CountdownTimer: Timer Text (TMP) is not assigned.", this);
    }

    private void Start()
    {
        RemainingTime = Mathf.Max(0f, countdownTime);
        IsFinished = false;
        RefreshDisplay();
        if (RemainingTime <= 0f) FinishCountdown();
    }

    private void Update()
    {
        if (IsFinished) return;
        RemainingTime = Mathf.Max(0f, RemainingTime - (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime));
        RefreshDisplay();
        if (RemainingTime <= 0f) FinishCountdown();
    }

    public void RestartCountdown()
    {
        RemainingTime = Mathf.Max(0f, countdownTime);
        IsFinished = false;
        enabled = true;
        RefreshDisplay();
    }

    public void Configure(TMP_Text display, float duration)
    {
        timerText = display;
        countdownTime = duration;
    }

    private void FinishCountdown()
    {
        if (IsFinished) return;
        IsFinished = true;
        RemainingTime = 0f;
        RefreshDisplay();
        Completed?.Invoke();
        onCountdownFinished?.Invoke();
    }

    private void RefreshDisplay()
    {
        if (timerText == null) return;
        int totalSeconds = Mathf.CeilToInt(RemainingTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
    }
}
