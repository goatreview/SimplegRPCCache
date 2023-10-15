namespace SimplegRPCCacheService.Server
{
    public record ClientInfo(string Key, SemaphoreSlim ClientSemaphoreSlim,CancellationTokenSource CancellationTokenSource,WaitableQueue<CommandQueueItem> Commands);
}
