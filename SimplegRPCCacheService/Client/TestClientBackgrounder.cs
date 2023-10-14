using System.Globalization;
using System.Net;

namespace SimplegRPCCacheService.Client
{
    public class TestClientBackgrounder : BackgroundService
    {
        private readonly ILogger<TestClientBackgrounder> _logger;
        private readonly IServiceProvider _serviceProvider;

        public TestClientBackgrounder(ILogger<TestClientBackgrounder> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        private async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
        {
            _logger.LogInformation("StartServerStreamAsync started");
            try
            {
                CacherClientProxy cacherClientProxy = serviceProvider.GetRequiredService<CacherClientProxy>();
                using var serverStreamResponse = cacherClientProxy.CommandServerStream(stoppingToken);
                while (await serverStreamResponse.ResponseStream.MoveNext(stoppingToken))
                {
                    var commandServerStreamResponse = serverStreamResponse.ResponseStream.Current;
                    var serverRequest = commandServerStreamResponse.ServerRequest;
                    _logger.LogInformation($"CommandServerStream() command={serverRequest}");
                    switch (serverRequest)
                    {
                        case ServerRequest.GetKey:
                            break;
                        case ServerRequest.GraceFullShutdown:
                            return;
                        case ServerRequest.ForceFlushCache:
                            //TODO
                            break;
                        case ServerRequest.ResetCache:
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in StartServerStreamAsync()");
            }
        }
        private Task StartServerStreamAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
        {

            //return Task.CompletedTask;
            return Task.Factory.StartNew(async () => await ExecuteAsync(serviceProvider, stoppingToken), stoppingToken,TaskCreationOptions.RunContinuationsAsynchronously,TaskScheduler.Default);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TestBackgrounder started");
            var task = StartServerStreamAsync(_serviceProvider, stoppingToken);
            CacherClientProxy cacherClientProxy = _serviceProvider.GetRequiredService<CacherClientProxy>();

            var keyPrefix = Dns.GetHostName();
            var keyId = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {

                    #region List all keys on server and get one randomly and delete it 
                    var cacheKeys = cacherClientProxy.ListKeys("");
                    cacheKeys = cacheKeys.OrderBy(x => Random.Shared.Next(cacheKeys.Length))
                        .ToArray();

                    foreach (var cacheKey in cacheKeys)
                    {
                        var (notFound, keyValue) = cacherClientProxy.GetKey(cacheKey);
                        if (notFound)
                            continue;
                        _logger.LogInformation($"Random ReadKey() key: {cacheKey} Value: {keyValue}");
                        cacherClientProxy.RemoveKey(cacheKey);
                        break;
                    }
                    #endregion

                    #region Create a new (key,value) and send it to the server
                    keyId = (keyId + 1) % 1000000;
                    var (newCacheKey, newCacheValue) = ($"{keyPrefix}/{keyId.ToString("000000", CultureInfo.InvariantCulture)}", DateTime.Now.ToString(CultureInfo.InvariantCulture));
                    cacherClientProxy.SetKey(newCacheKey, newCacheValue);
                    #endregion

                    await Task.Delay(1000, stoppingToken);

                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error in SetKey()");
                    await Task.Delay(5000, stoppingToken);
                    cacherClientProxy = _serviceProvider.GetRequiredService<CacherClientProxy>();
                    task = StartServerStreamAsync(_serviceProvider, stoppingToken);
                }
            }

            _logger.LogInformation("TestBackgrounder ended");
        }
    }
}
