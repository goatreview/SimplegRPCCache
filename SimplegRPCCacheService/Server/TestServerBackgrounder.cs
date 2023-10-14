using SimplegRPCCacheService.Client;
using SimplegRPCCacheService.Services;
using System.Runtime.Intrinsics.X86;

namespace SimplegRPCCacheService.Server
{
    public class TestServerBackgrounder : BackgroundService
    {
        private readonly ILogger<TestServerBackgrounder> _logger;
        private readonly ICacherService _cacherService;

        public TestServerBackgrounder(ILogger<TestServerBackgrounder> logger, CacherService cacherService)
        {
            _logger = logger;
            _cacherService = cacherService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TestBackgrounder started");
            while (!stoppingToken.IsCancellationRequested)
            {
                await _cacherService.BroadCastCommandAsync(new CommandServerStreamResponse()
                {
                    ServerRequest = ServerRequest.ForceFlushCache
                });
                await Task.Delay(1000, stoppingToken);
            }
            _logger.LogInformation("TestBackgrounder stopped");
        }
    }
}
