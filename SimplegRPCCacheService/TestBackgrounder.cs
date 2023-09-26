

using System.Globalization;
using System.Reflection.Metadata;
using Google.Protobuf;
using System.Threading;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;

namespace SimplegRPCCacheService
{
    public class CacherClientProxy
    {
        private readonly Cacher.CacherClient _cacherClient;

        public CacherClientProxy(Cacher.CacherClient cacherClient)
        {
            _cacherClient = cacherClient;
        }

        public void RemoveKey(string key)
        {
            var setKeyRequest = new SetKeyRequest()
            {
                Key = key,
                RemoveKey = true
            };
            _ = _cacherClient.SetKey(setKeyRequest);
        }

        public (bool,string) GetKey(string key)
        {
            var getKeyRequest = new GetKeyRequest()
            {
                Key = key,
            };
            var getKeyResponse = _cacherClient.GetKey(getKeyRequest);
            if (getKeyResponse.NotFound)
                return (getKeyResponse.NotFound,string.Empty);

            return (getKeyResponse.NotFound, getKeyResponse.KeyValue.ToStringUtf8());
        }

        public void SetKey(string key, string value)
        {
            var setKeyRequest = new SetKeyRequest()
            {
                Key = key,
                KeyValue = ByteString.CopyFromUtf8(value),
                RemoveKey = false
            };
            _ = _cacherClient.SetKey(setKeyRequest);
        }

        public string[] ListKeys(string filter)
        {
            var listKeyRequest = new ListKeyRequest()
            {
                KeyFilter = filter
            };
            var listKeyResponse = _cacherClient.ListKeys(listKeyRequest);
            return listKeyResponse.Keys.ToArray();
        }
    }

    public class TestBackgrounder : BackgroundService
    {
        private readonly ILogger<TestBackgrounder> _logger;
        private readonly IServiceProvider _serviceProvider;

        public TestBackgrounder(ILogger<TestBackgrounder> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TestBackgrounder started");
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
                    var (newCacheKey,newCacheValue) = ($"{keyPrefix}/{keyId.ToString("000000",CultureInfo.InvariantCulture)}", DateTime.Now.ToString(CultureInfo.InvariantCulture));
                    cacherClientProxy.SetKey(newCacheKey, newCacheValue);
                    #endregion
                    
                    await Task.Delay(1000, stoppingToken);

                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error in SetKey()");
                    await Task.Delay(5000, stoppingToken);
                    cacherClientProxy = _serviceProvider.GetRequiredService<CacherClientProxy>();
                }
            }

            _logger.LogInformation("TestBackgrounder ended");
        }
    }
}
