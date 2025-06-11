using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Banana.Backtest.CryptoConverter.Scheduler;

/// <summary>
/// A task scheduler that executes each task on its own thread
/// affinitized to a specific processor core. Supports
/// Windows and Linux platforms.
/// </summary>
public sealed class AffinitizedThreadPoolTaskScheduler : TaskScheduler, IDisposable
{
    #region P/Invoke

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentThread();

    [DllImport("libc", SetLastError = true)]
#pragma warning disable SA1300
    private static extern int sched_setaffinity(int pid, IntPtr cpusetsize, IntPtr mask);
#pragma warning restore SA1300

    #endregion

    private readonly BlockingCollection<Task> _queue;
    private readonly Thread[] _workers;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;

    // OS detection flags
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Initializes a new scheduler with a thread pool equal to the number of logical cores.
    /// </summary>
    public AffinitizedThreadPoolTaskScheduler()
        : this(Environment.ProcessorCount)
    {
    }

    /// <summary>
    /// Initializes a new scheduler with the specified number of threads (≤ number of logical cores).
    /// </summary>
    /// <param name="threadCount">
    /// The number of threads (and cores) to which threads will be affinitized.
    /// Must be between 1 and <see cref="Environment.ProcessorCount"/>.
    /// </param>
    public AffinitizedThreadPoolTaskScheduler(int threadCount)
    {
        if (threadCount < 1 || threadCount > Environment.ProcessorCount)
            throw new ArgumentOutOfRangeException(nameof(threadCount),
                $"Expected a value between 1 and {Environment.ProcessorCount}.");

        _queue = new BlockingCollection<Task>();
        _cts = new CancellationTokenSource();
        _workers = new Thread[threadCount];

        for (var i = 0; i < _workers.Length; i++)
        {
            var coreIndex = i;
            var thread = new Thread(() => WorkerLoop(coreIndex, _cts.Token))
            {
                IsBackground = true,
                Name = $"AffinitizedScheduler_Core{coreIndex}"
            };
            _workers[i] = thread;
            thread.Start();
        }
    }

    /// <inheritdoc/>
    public override int MaximumConcurrencyLevel => _workers.Length;

    /// <inheritdoc/>
    protected override void QueueTask(Task task)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AffinitizedThreadPoolTaskScheduler));

        _queue.Add(task, _cts.Token);
    }

    /// <inheritdoc/>
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        // Prevent inlining; enforce execution via our pool
        return false;
    }

    /// <inheritdoc/>
    protected override IEnumerable<Task> GetScheduledTasks() => _queue.ToArray();

    private void WorkerLoop(int coreIndex, CancellationToken token)
    {
        // Affinitize this thread to the specific core
        if (IsWindows)
        {
            var mask = (UIntPtr)(1UL << coreIndex);
            SetThreadAffinityMask(GetCurrentThread(), mask);
        }
        else if (IsLinux)
        {
            SetAffinityLinux(coreIndex);
        }

        try
        {
            foreach (var task in _queue.GetConsumingEnumerable(token))
            {
                TryExecuteTask(task);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested, exit loop
        }
    }

    private static void SetAffinityLinux(int coreIndex)
    {
        var mask = 1UL << coreIndex;
        IntPtr size = Marshal.SizeOf<ulong>();
        var maskPtr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.WriteInt64(maskPtr, unchecked((long)mask));
            if (sched_setaffinity(0, size, maskPtr) != 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.FreeHGlobal(maskPtr);
        }
    }

    /// <summary>
    /// Releases resources and stops all worker threads.
    /// </summary>
    public void Dispose() => Dispose(disposing: true);

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        if (disposing)
        {
            _cts.Cancel();
            _queue.CompleteAdding();
            foreach (var thread in _workers)
                thread.Join();
            _queue.Dispose();
            _cts.Dispose();
        }
    }
}
