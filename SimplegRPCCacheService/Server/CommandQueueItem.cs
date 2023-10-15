namespace SimplegRPCCacheService.Server
{
    public record CommandQueueItem(CommandRequest Request, ManualResetEventSlim ExecutionCompleted)
    {
        public bool IsError => Exception != null;
        public Exception? Exception { get; set; }
        public CommandResponse? Response { get; set; }
    }
}
