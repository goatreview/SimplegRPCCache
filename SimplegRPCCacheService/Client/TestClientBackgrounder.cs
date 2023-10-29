using Grpc.Core;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;

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

        private bool SetKeyInLocalCache(string key, byte[] bytes)
        {
            throw new NotImplementedException();
            //var cacherClientProxy = _serviceProvider.GetRequiredService<CacherClientProxy>();
            //cacherClientProxy.SetKey(key, value);
            //return true;
        }

        private (bool, byte[]) GetKeyFromLocalCache(string key)
        {
            throw new NotImplementedException();
            //var cacherClientProxy = _serviceProvider.GetRequiredService<CacherClientProxy>();
            //return cacherClientProxy.GetKey(key);
        }

        private bool ForceFlushCache()
        {
            throw new NotImplementedException();
            //var cacherClientProxy = _serviceProvider.GetRequiredService<CacherClientProxy>();
            //return cacherClientProxy.ForceFlushCache();
        }

        private IEnumerable<string> GetCurrentIpAddresses()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(ip => ip.ToString());
        }

        private (string ClientId,string ClientStats) Idle()
        {
            var clientStats = $"timestamp={DateTime.UtcNow.ToLongDateString()}";            
            return ($"{Dns.GetHostName()}@{string.Join(",", GetCurrentIpAddresses())}", clientStats);
        }

        private void GraceFullShutdown()
        {
            throw new NotImplementedException();
        }

        private async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
        {
            _logger.LogInformation("StartServerStreamAsync started");
            try
            {
                CacherClientProxy cacherClientProxy = serviceProvider.GetRequiredService<CacherClientProxy>();
                using var call = cacherClientProxy.ExchangeCommands(stoppingToken);
                while (await call.ResponseStream.MoveNext(stoppingToken))
                {
                    var command = call.ResponseStream.Current;
                    switch (command.CommandType)
                    {
                        case CommandType.SetKey:
                            var setResult = SetKeyInLocalCache(command.Key, command.KeyValue.ToByteArray());
                            if (setResult)
                            {
                                await call.RequestStream.WriteAsync(
                                    new CommandResponse { Result = CommandResult.Success }, stoppingToken);
                            }
                            else
                            {
                                await call.RequestStream.WriteAsync(
                                    new CommandResponse
                                        { Result = CommandResult.Error, ErrorMessage = "Failed to set key" },
                                    stoppingToken);
                            }
                            break;

                        case CommandType.GetKey:
                            var (notFound,getValue) = GetKeyFromLocalCache(command.Key);
                            if (notFound)
                            {
                                await call.RequestStream.WriteAsync(
                                    new CommandResponse
                                        { Result = CommandResult.NotFound, KeyValue = ByteString.Empty },
                                    stoppingToken);
                            }
                            else
                            {
                                await call.RequestStream.WriteAsync(
                                    new CommandResponse
                                        { Result = CommandResult.Success, KeyValue = ByteString.CopyFrom(getValue) },
                                    stoppingToken);
                            }
                            break;

                        case CommandType.ForceFlushCache:
                            var flushResult = ForceFlushCache();
                            if (flushResult)
                            {
                                await call.RequestStream.WriteAsync(
                                    new CommandResponse { Result = CommandResult.Success }, stoppingToken);
                            }
                            else
                            {
                                await call.RequestStream.WriteAsync(
                                    new CommandResponse
                                        { Result = CommandResult.Error, ErrorMessage = "Failed to flush cache" },
                                    stoppingToken);
                            }
                            break;
                        
                        case CommandType.Idle:
                            var (clientId,clientStats) = Idle();
                            await call.RequestStream.WriteAsync(
                                new CommandResponse
                                    { Result = CommandResult.Success, ClientId = clientId, ClientStats = clientStats },
                                stoppingToken);
                            break;

                        case CommandType.GraceFullShutdown:
                            GraceFullShutdown();
                            await call.RequestStream.WriteAsync(new CommandResponse { Result = CommandResult.Success },
                                stoppingToken);
                            await call.RequestStream.CompleteAsync();
                            return;
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
