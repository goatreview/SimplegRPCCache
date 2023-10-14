using Google.Protobuf;
using Grpc.Core;
using System.Net;

namespace SimplegRPCCacheService.Client
{
    public class CacherClientProxy
    {
        private readonly Cacher.CacherClient _cacherClient;

        public CacherClientProxy(Cacher.CacherClient cacherClient)
        {
            _cacherClient = cacherClient;
        }

        public AsyncServerStreamingCall<CommandServerStreamResponse> CommandServerStream(CancellationToken cancellationToken = default)
        {
            var clientRequest = new ClientRequest()
            {
                Hostname = Dns.GetHostName(),
                Ip = Dns.GetHostAddresses(Dns.GetHostName()).First().ToString()
            };
            return _cacherClient.CommandServerStream(clientRequest,cancellationToken: cancellationToken);
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

        public (bool, string) GetKey(string key)
        {
            var getKeyRequest = new GetKeyRequest()
            {
                Key = key,
            };
            var getKeyResponse = _cacherClient.GetKey(getKeyRequest);
            if (getKeyResponse.NotFound)
                return (getKeyResponse.NotFound, string.Empty);

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
}
