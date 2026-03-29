namespace SjfScheduler.Models;

public enum JobStatus
{
    Waiting,
    Running,
    Completed
}

/// <summary>
/// Represents a schedulable process/job with its burst time and lifecycle timestamps.
/// </summary>
public class ProcessJob
{
    private static int _idCounter = 0;

    public int Id { get; } = Interlocked.Increment(ref _idCounter);
    public string Name => $"J{Id}";

    /// <summary>Simulated CPU burst duration in seconds.</summary>
    public int BurstTime { get; init; }

    /// <summary>The type of work this job performs.</summary>
    public string WorkType { get; init; } = "Unknown";

    public DateTime ArrivalTime { get; init; } = DateTime.Now;
    public DateTime? StartTime { get; set; }
    public DateTime? FinishTime { get; set; }

    public volatile JobStatus Status = JobStatus.Waiting;

    // Elapsed time already "executed" (in simulated seconds)
    private int _elapsed = 0;
    public int Elapsed => _elapsed;
    public int RemainingBurst => BurstTime - _elapsed;

    public void TickSecond() => Interlocked.Increment(ref _elapsed);

    public double WaitingTime =>
        StartTime.HasValue
            ? (StartTime.Value - ArrivalTime).TotalSeconds
            : 0;

    public double TurnaroundTime =>
        FinishTime.HasValue
            ? (FinishTime.Value - ArrivalTime).TotalSeconds
            : 0;

    public override string ToString() => $"{Name}(burst={BurstTime}s)";
}
