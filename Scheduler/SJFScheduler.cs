using SjfScheduler.Models;

namespace SjfScheduler.Scheduler;

/// <summary>
/// Non-preemptive SJF (Shortest Job First) cooperative scheduler.
///
/// Threads call TryAcquireToken() in a spin-wait loop.
/// The scheduler only grants the token to the job with the smallest BurstTime
/// (ties broken by arrival order). Once a job acquires the token, it runs to
/// completion before another job can start.
/// </summary>
public class SJFScheduler
{
    private readonly object _lock = new();
    // Sorted by BurstTime asc, then by ArrivalTime asc
    private readonly SortedSet<ProcessJob> _readyQueue = new(Comparer<ProcessJob>.Create(
        (a, b) =>
        {
            int cmp = a.BurstTime.CompareTo(b.BurstTime);
            if (cmp != 0) return cmp;
            cmp = a.ArrivalTime.CompareTo(b.ArrivalTime);
            return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
        }));

    // The job currently holding the CPU token (null = CPU idle)
    private ProcessJob? _running = null;

    public event Action<string>? OnLog;

    /// <summary>Adds a newly arrived job to the ready queue.</summary>
    public void Enqueue(ProcessJob job)
    {
        lock (_lock)
        {
            _readyQueue.Add(job);
            LogQueue($"➕ {job.Name} arrived | Burst={job.BurstTime}s | Type={job.WorkType}");
        }
    }

    /// <summary>
    /// Called by a worker thread to determine if it may run.
    /// Returns true only if this job is the shortest in the queue AND
    /// no other job is currently running.
    /// </summary>
    public bool TryAcquireToken(ProcessJob job)
    {
        lock (_lock)
        {
            // CPU is busy with another job
            if (_running != null && _running != job)
                return false;

            // Already running – keep going
            if (_running == job)
                return true;

            // CPU is idle – check if this job is the shortest
            if (_readyQueue.Count == 0)
                return false;

            var shortest = _readyQueue.Min!;
            if (shortest.Id != job.Id)
                return false;

            // Grant token
            _readyQueue.Remove(job);
            _running = job;
            job.Status = JobStatus.Running;
            job.StartTime = DateTime.Now;
            LogQueue($"▶  {job.Name} RUNNING | Burst={job.BurstTime}s | " +
                     $"Waiting queue: [{string.Join(", ", _readyQueue)}]");
            return true;
        }
    }

    /// <summary>Called by a worker when its job has finished.</summary>
    public void Release(ProcessJob job)
    {
        lock (_lock)
        {
            if (_running == job)
                _running = null;

            job.Status = JobStatus.Completed;
            job.FinishTime = DateTime.Now;
            LogQueue($"✅ {job.Name} DONE   | Burst={job.BurstTime}s | " +
                     $"Wait={job.WaitingTime:F1}s | TAT={job.TurnaroundTime:F1}s");
        }
    }

    /// <summary>Snapshot of the current ready queue for display.</summary>
    public IEnumerable<ProcessJob> GetQueue()
    {
        lock (_lock) { return _readyQueue.ToList(); }
    }

    private void LogQueue(string msg) => OnLog?.Invoke(msg);
}
