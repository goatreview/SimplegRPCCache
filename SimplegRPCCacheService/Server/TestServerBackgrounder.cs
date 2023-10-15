using SimplegRPCCacheService.Client;
using SimplegRPCCacheService.Services;
using System.Runtime.Intrinsics.X86;

namespace SimplegRPCCacheService.Server
{
    public class TestServerBackgrounder : BackgroundService
    {
        private readonly ILogger<TestServerBackgrounder> _logger;
        private readonly CacherService _cacherService;

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
                var results  = await _cacherService.BroadCastCommandAsync(new CommandRequest()
                {
                    ServerTimestamp =  Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                    CommandType = CommandType.Idle
                }, stoppingToken);

                _logger.LogInformation($"TestBackgrounder: Idle results={string.Join(",",results.Select(x=>x.ClientId))}");
                await Task.Delay(1000, stoppingToken);
            }
            _logger.LogInformation("TestBackgrounder stopped");
        }
    }
}
