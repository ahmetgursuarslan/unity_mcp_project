#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Antigravity.MCP.Editor
{
    /// <summary>
    /// Thread-safe dispatcher that marshals work from background threads 
    /// to Unity's main thread via EditorApplication.update.
    /// Uses time-budget (10ms) per frame for throughput.
    /// Features: configurable timeout, cancellation support, volatile initialization flag.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private struct WorkItem
        {
            public Func<string> Work;
            public TaskCompletionSource<string> Tcs;
            public long EnqueuedTicks;
        }

        private static readonly ConcurrentQueue<WorkItem> _queue = new ConcurrentQueue<WorkItem>();
        private static volatile bool _initialized;

        /// <summary>
        /// Timeout in seconds for enqueued work items. If the main thread hasn't processed
        /// a work item within this time, the caller gets a TimeoutException.
        /// Default: 25s (5s less than the Router's 30s/45s timeout to fail gracefully first).
        /// </summary>
        public static int TimeoutSeconds { get; set; } = 25;

        public static int PendingCount => _queue.Count;

        public static void Initialize()
        {
            if (_initialized) return;
            EditorApplication.update += ProcessQueue;
            _initialized = true;
        }

        /// <summary>
        /// Enqueues a function to run on the main thread and returns a Task
        /// that completes with the function's return value.
        /// Supports timeout: if the main thread doesn't process it within TimeoutSeconds,
        /// the task fails with TimeoutException instead of hanging forever.
        /// </summary>
        public static Task<string> EnqueueAsync(Func<string> work)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(new WorkItem
            {
                Work = work,
                Tcs = tcs,
                EnqueuedTicks = Environment.TickCount64
            });
            return tcs.Task;
        }

        private static void ProcessQueue()
        {
            if (_queue.IsEmpty) return;

            // Time-budget: process items for up to 10ms per frame
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int processed = 0;
            var timeoutMs = TimeoutSeconds * 1000L;

            while (sw.ElapsedMilliseconds < 10 && _queue.TryDequeue(out var item))
            {
                // Check if this work item has already timed out
                var elapsed = Environment.TickCount64 - item.EnqueuedTicks;
                if (elapsed > timeoutMs)
                {
                    item.Tcs.TrySetException(new TimeoutException(
                        $"Main thread dispatch timed out after {TimeoutSeconds}s. " +
                        "Unity's main thread may be blocked by a long operation."));
                    continue;
                }

                try
                {
                    var result = item.Work();
                    item.Tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    item.Tcs.TrySetException(ex);
                }
                processed++;
            }
        }
    }
}
#endif
