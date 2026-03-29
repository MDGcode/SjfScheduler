using SjfScheduler.Models;
using SjfScheduler.Scheduler;
using SjfScheduler.Workers;

namespace SjfScheduler.Kernel;

/// <summary>
/// Simulates a kernel that continuously spawns worker threads.
/// Each worker registers its job with the SJF scheduler and then
/// cooperatively waits for the scheduler's token before executing.
/// </summary>
public class KernelSimulator
{
    private readonly SJFScheduler _scheduler;
    private readonly Random _rng = new();
    private readonly List<ProcessJob> _allJobs = [];
    private readonly object _listLock = new();
    private volatile bool _stopSpawning;

    // Simulation parameters
    private readonly int _simulationSeconds;    // total wall-clock duration
    private readonly int _minSpawnDelay;        // seconds between new job arrivals
    private readonly int _maxSpawnDelay;
    private readonly int _minBurst;
    private readonly int _maxBurst;

    private DateTime _simStart;

    public KernelSimulator(SJFScheduler scheduler,
        int simulationSeconds = 90,
        int minSpawnDelay = 2, int maxSpawnDelay = 6,
        int minBurst = 3, int maxBurst = 12)
    {
        _scheduler = scheduler;
        _simulationSeconds = simulationSeconds;
        _minSpawnDelay = minSpawnDelay;
        _maxSpawnDelay = maxSpawnDelay;
        _minBurst = minBurst;
        _maxBurst = maxBurst;

        _scheduler.OnLog += PrintLive;
    }

    public void Run()
    {
        _simStart = DateTime.Now;
        Console.Clear();
        PrintHeader();

        // Spawn a dedicated thread that continuously creates new jobs
        var spawner = new Thread(SpawnLoop) { IsBackground = true, Name = "Spawner" };
        spawner.Start();

        // Run for the configured duration, then stop generating new jobs.
        // Existing workers are allowed to finish naturally.
        Thread.Sleep(_simulationSeconds * 1000);
        _stopSpawning = true;
        spawner.Join();

        // Wait for all worker threads to finish
        List<ProcessJob> snapshot;
        lock (_listLock) { snapshot = [.. _allJobs]; }
        foreach (var job in snapshot)
        {
            while (job.Status != JobStatus.Completed)
                Thread.Sleep(200);
        }

        PrintStats();
    }

    // ── Spawner ───────────────────────────────────────────────────────────────

    private void SpawnLoop()
    {
        while (!_stopSpawning)
        {
            int delay = _rng.Next(_minSpawnDelay, _maxSpawnDelay + 1);
            Thread.Sleep(delay * 1000);

            if (_stopSpawning) break;

            int burst = _rng.Next(_minBurst, _maxBurst + 1);
            string workType = WorkerFunctions.WorkTypes[_rng.Next(WorkerFunctions.WorkTypes.Length)];

            var job = new ProcessJob { BurstTime = burst, WorkType = workType };

            lock (_listLock) { _allJobs.Add(job); }
            _scheduler.Enqueue(job);

            // Spawn the worker thread for this job
            var thread = new Thread(() => WorkerThread(job))
            {
                IsBackground = true,
                Name = $"Worker-{job.Name}"
            };
            thread.Start();
        }
    }

    // ── Worker thread logic ───────────────────────────────────────────────────

    private void WorkerThread(ProcessJob job)
    {
        // ── Phase 1: Cooperative wait (self-aware) ──────────────────────────
        // The thread spins (yields CPU with short sleeps) until the SJF
        // scheduler grants it the execution token.
        while (!_scheduler.TryAcquireToken(job))
        {
            Thread.Sleep(50); // cooperative yield – not busy spinning hard
        }

        // ── Phase 2: Execute work ───────────────────────────────────────────
        // The yieldCallback is invoked after every simulated second.
        // In non-preemptive SJF the scheduler does NOT pause the running job,
        // but the callback is the hook point where it COULD (e.g. for SRTF).
        WorkerFunctions.Execute(job.WorkType, job.BurstTime,
            yieldCallback: () => job.TickSecond(),
            ct: CancellationToken.None);

        // ── Phase 3: Release token & mark done ─────────────────────────────
        _scheduler.Release(job);
    }

    // ── Output helpers ────────────────────────────────────────────────────────

    private string Elapsed() =>
        $"[{(DateTime.Now - _simStart):mm\\:ss}]";

    private void PrintLive(string msg)
    {
        Console.ForegroundColor = msg.StartsWith("▶") ? ConsoleColor.Green
            : msg.StartsWith("✅") ? ConsoleColor.Cyan
            : msg.StartsWith("➕") ? ConsoleColor.Yellow
            : ConsoleColor.White;
        Console.WriteLine($"{Elapsed()} {msg}");
        Console.ResetColor();
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║      SJF (Shortest Job First) Kernel Simulator - C#          ║");
        Console.WriteLine("║  Threads are self-aware and cooperate with the scheduler     ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private void PrintStats()
    {
        List<ProcessJob> completed;
        lock (_listLock)
        {
            completed = _allJobs
                .Where(j => j.Status == JobStatus.Completed)
                .OrderBy(j => j.FinishTime)
                .ToList();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║               SJF Simulation - Final Statistics                  ║");
        Console.WriteLine("╠════════╦═══════════════════╦═══════╦══════════╦═══════╦══════════╣");
        Console.WriteLine("║  Job   ║ WorkType           ║ Burst ║ Arrival  ║ Wait  ║   TAT    ║");
        Console.WriteLine("╠════════╬═══════════════════╬═══════╬══════════╬═══════╬══════════╣");
        Console.ResetColor();

        foreach (var job in completed)
        {
            string arrival = job.ArrivalTime.ToString("mm:ss");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                $"║ {job.Name,-6} ║ {job.WorkType,-19}║ {job.BurstTime,3}s  ║  {arrival,-6}  ║ {job.WaitingTime,3:F0}s  ║ {job.TurnaroundTime,5:F0}s   ║");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("╚════════╩═══════════════════╩═══════╩══════════╩═══════╩══════════╝");
        Console.ResetColor();

        if (completed.Count > 0)
        {
            double avgWait = completed.Average(j => j.WaitingTime);
            double avgTat  = completed.Average(j => j.TurnaroundTime);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  Avg Waiting Time   : {avgWait:F2}s");
            Console.WriteLine($"  Avg Turnaround Time: {avgTat:F2}s");
            Console.WriteLine($"  Total Jobs Completed: {completed.Count}");
            Console.ResetColor();
        }
    }
}
