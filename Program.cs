using SjfScheduler.Kernel;
using SjfScheduler.Scheduler;

// ────────────────────────────────────────────────────────────────────────────
//  SJF Kernel Simulator – Entry Point
//
//  Simulates a kernel that spawns worker threads executing long-running tasks.
//  Threads are self-aware and cooperate with the SJF scheduler:
//    • Each thread checks TryAcquireToken() before running.
//    • Only the job with the shortest burst time is allowed to execute.
//    • Non-preemptive: once a job starts it runs to completion.
// ────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = System.Text.Encoding.UTF8;

var scheduler = new SJFScheduler();
var kernel    = new KernelSimulator(
    scheduler,
    simulationSeconds: 90,   // run the simulation for 90 real seconds
    minSpawnDelay: 3,        // new job arrives every 3–7 seconds
    maxSpawnDelay: 7,
    minBurst: 3,             // burst times between 3 and 12 simulated seconds
    maxBurst: 12
);

kernel.Run();
