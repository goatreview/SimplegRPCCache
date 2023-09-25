using Common;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace SimplegRPCCacheService.Services;

public class CacherService :Cacher.CacherBase
{
    private readonly ILogger<CacherService> _logger;
    private readonly IKeyValueStore _keyValueStore;
    public CacherService(ILogger<CacherService> logger,IKeyValueStore keyValueStore )
    {
        _keyValueStore = keyValueStore;
        _logger = logger;
    }

    public override Task<GetKeyResponse> GetKey(GetKeyRequest request, ServerCallContext context)
    {
        _logger.LogDebug($">>GetKey() key={request.Key}");
        var (isError, result) = _keyValueStore.GetKey(request.Key);
        if (isError)
        {
            _logger.LogDebug($"<<GetKey() key={request.Key} not found");
            return Task.FromResult(new GetKeyResponse()
            {
                NotFound = true,
                KeyValue = ByteString.Empty
            });
        }
        _logger.LogDebug($"<<GetKey() key={request.Key} found");
        return Task.FromResult(new GetKeyResponse()
        {
            KeyValue = result == null ? ByteString.Empty : ByteString.CopyFrom(result)
        });
    }

    public override Task<SetKeyResponse> SetKey(SetKeyRequest request, ServerCallContext context)
    {
        _logger.LogDebug($">>SetKey() key={request.Key},remove_key={request.RemoveKey}");
        var result = _keyValueStore.SetKey(request.Key, request.KeyValue.ToByteArray(), request.RemoveKey);
        _logger.LogDebug($"<<SetKey() key={request.Key},remove_key={request.RemoveKey},set_key_result={result}");
        return Task.FromResult(new SetKeyResponse()
        {
            SetKeyResult = result,
        });
    }

    public override Task<ListKeyResponse> ListKeys(ListKeyRequest request, ServerCallContext context)
    {
        _logger.LogDebug($">>ListKeys() key_filter={request.KeyFilter}");
        var result = _keyValueStore.ListKeys(request.KeyFilter);
        _logger.LogDebug($"<<ListKeys() key_filter={request.KeyFilter}");
        return Task.FromResult(new ListKeyResponse()
        {
            Keys = {result}
        });
    }
}