using System.Collections.Concurrent;

namespace SimplegRPCCacheService.Server
{
    public class WaitableQueue<T>
    {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim(false);
        public WaitHandle WaitHandle => _manualResetEventSlim.WaitHandle;

        public void Enqueue(T item)
        {
            _semaphoreSlim.Wait();
            try
            {
                _queue.Enqueue(item);
                _manualResetEventSlim.Set();
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public bool TryDequeue(out T item)
        {
            _semaphoreSlim.Wait();
            try
            {
                if (_queue.Count == 0)
                {
                    item = default!;
                    _manualResetEventSlim.Reset();
                    return false;
                }

                item = _queue.Dequeue();

                if (_queue.Count == 0)
                    _manualResetEventSlim.Reset();

                return true;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

    }
}
