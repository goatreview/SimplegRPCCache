namespace SimplegRPCCacheService.Services
{
    public interface ICacherService
    {
        Task BroadCastCommandAsync(CommandServerStreamResponse commandServerStreamResponse);
    }
}