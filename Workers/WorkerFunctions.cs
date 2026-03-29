namespace SjfScheduler.Workers;

/// <summary>
/// Collection of long-duration simulated work functions.
/// Each function runs for approximately `burstSeconds` real seconds using
/// a mix of CPU work and Thread.Sleep to simulate realistic workloads.
/// Workers are "self-aware": they accept a cooperative yield callback
/// that is called between each simulated second so the scheduler can
/// temporarily pause them via a ManualResetEventSlim.
/// </summary>
public static class WorkerFunctions
{
    // Scale factor: 1 simulated second = TICK_MS real milliseconds
    // Set to 1000 for real-time, lower for fast demo
    public const int TickMs = 1000;

    /// <summary>
    /// Executes the named work function for `burstSeconds` ticks.
    /// Between each tick, `yieldCallback` is called – the scheduler may
    /// block the thread there if it needs to pause it (cooperative yield).
    /// </summary>
    public static void Execute(string workType, int burstSeconds, Action yieldCallback,
        CancellationToken ct)
    {
        Action<int, Action, CancellationToken> fn = workType switch
        {
            "HeavySort"       => HeavySort,
            "MatrixMultiply"  => MatrixMultiply,
            "PrimeSearch"     => PrimeSearch,
            "FibonacciCalc"   => FibonacciCalc,
            "FileSimulation"  => FileSimulation,
            _                 => HeavySort
        };
        fn(burstSeconds, yieldCallback, ct);
    }

    // ── Work implementations ──────────────────────────────────────────────────

    private static void HeavySort(int burst, Action yield, CancellationToken ct)
    {
        var rng = new Random();
        for (int tick = 0; tick < burst && !ct.IsCancellationRequested; tick++)
        {
            // Real CPU work: sort a random array
            var arr = new int[5000];
            for (int i = 0; i < arr.Length; i++) arr[i] = rng.Next();
            Array.Sort(arr);
            Thread.Sleep(TickMs);
            yield();
        }
    }

    private static void MatrixMultiply(int burst, Action yield, CancellationToken ct)
    {
        var rng = new Random();
        for (int tick = 0; tick < burst && !ct.IsCancellationRequested; tick++)
        {
            // Real CPU work: 30x30 matrix multiply
            int n = 30;
            var a = new double[n, n]; var b = new double[n, n]; var c = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                { a[i, j] = rng.NextDouble(); b[i, j] = rng.NextDouble(); }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    for (int k = 0; k < n; k++)
                        c[i, j] += a[i, k] * b[k, j];
            Thread.Sleep(TickMs);
            yield();
        }
    }

    private static void PrimeSearch(int burst, Action yield, CancellationToken ct)
    {
        for (int tick = 0; tick < burst && !ct.IsCancellationRequested; tick++)
        {
            // Real CPU work: find primes up to 10000 (sieve)
            int limit = 10000;
            var sieve = new bool[limit + 1];
            Array.Fill(sieve, true);
            sieve[0] = sieve[1] = false;
            for (int i = 2; i * i <= limit; i++)
                if (sieve[i])
                    for (int j = i * i; j <= limit; j += i) sieve[j] = false;
            Thread.Sleep(TickMs);
            yield();
        }
    }

    private static void FibonacciCalc(int burst, Action yield, CancellationToken ct)
    {
        for (int tick = 0; tick < burst && !ct.IsCancellationRequested; tick++)
        {
            // Real CPU work: recursive fib (intentionally slow)
            _ = SlowFib(30);
            Thread.Sleep(TickMs);
            yield();
        }
    }

    private static long SlowFib(int n) => n <= 1 ? n : SlowFib(n - 1) + SlowFib(n - 2);

    private static void FileSimulation(int burst, Action yield, CancellationToken ct)
    {
        var rng = new Random();
        for (int tick = 0; tick < burst && !ct.IsCancellationRequested; tick++)
        {
            // Simulates IO: write/read in-memory stream
            using var ms = new System.IO.MemoryStream();
            using var sw = new System.IO.StreamWriter(ms);
            for (int line = 0; line < 500; line++)
                sw.WriteLine(rng.Next().ToString());
            sw.Flush();
            Thread.Sleep(TickMs);
            yield();
        }
    }

    public static readonly string[] WorkTypes =
        [ "HeavySort", "MatrixMultiply", "PrimeSearch", "FibonacciCalc", "FileSimulation" ];
}
