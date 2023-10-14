using System.Collections.Concurrent;
using Common;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading;
using System;

namespace SimplegRPCCacheService.Services;

public class CacherService :Cacher.CacherBase,ICacherService
{
    private readonly ILogger<CacherService> _logger;
    private readonly IKeyValueStore _keyValueStore;

    private readonly ConcurrentDictionary<string, ClientInfo> _clients = new ConcurrentDictionary<string, ClientInfo>();
    
    internal record ServerStreamQueueItem(ManualResetEventSlim SignalEvent, CommandServerStreamResponse ServerResponse)
    {
        public Exception? CommadError { get; set; }
    }
    internal record ClientInfo(string HostName, string Ip, ManualResetEventSlim CommandReady,ConcurrentQueue<ServerStreamQueueItem> Commands);
    
    public CacherService(ILogger<CacherService> logger,IKeyValueStore keyValueStore )
    {
        _keyValueStore = keyValueStore;
        _logger = logger;
    }

    private void RegisterClient(string hostName, string ip, ManualResetEventSlim manualResetEvent, ConcurrentQueue<ServerStreamQueueItem> queue)
    {
        var key = $"{hostName}@{ip}";
        if (!_clients.TryAdd(key,
            new ClientInfo(hostName, ip, manualResetEvent, queue)))
        {
            throw new Exception($"RegisterClient() client {key} already registered");
        }
    }

    private void UnRegisterClient(string hostName, string ip)
    {
        _clients.TryRemove($"{hostName}@{ip}", out _);
    }

    public async Task BroadCastCommandAsync(CommandServerStreamResponse commandServerStreamResponse)
    {
        var clients = _clients.Select(x => new { Key = x.Key, CommandQueue = x.Value.Commands, x.Value.CommandReady }).ToArray();

        foreach (var item in clients)
        {
            using var commandExecuted = new ManualResetEventSlim(false);
            var serverStreamQueueItem = new ServerStreamQueueItem(commandExecuted, commandServerStreamResponse);
            item.CommandQueue.Enqueue(serverStreamQueueItem);
            item.CommandReady.Set();
            try
            {
                await WaitForSignalAsync(commandExecuted);
                if (serverStreamQueueItem.CommadError != null)
                {
                    throw serverStreamQueueItem.CommadError;
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"BroadCastCommandAsync() client={item.Key},error={e.Message}");
            }
        }
    }

    public async Task WaitForSignalAsync(ManualResetEventSlim manualResetEventSlim,CancellationToken cancellationToken = default)
    {
        await Task.Factory.StartNew(manualResetEventSlim.Wait, cancellationToken, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
    }

    public override async Task CommandServerStream(ClientRequest request, IServerStreamWriter<CommandServerStreamResponse> responseStream, ServerCallContext context)
    {
        using ManualResetEventSlim commandReady = new ManualResetEventSlim(false);
        using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        
        var queue = new ConcurrentQueue<ServerStreamQueueItem>();
        _logger.LogDebug($">>CommandServerStream() client={request.Hostname}@{request.Ip}");
        try
        {

            RegisterClient(request.Hostname, request.Ip, commandReady, queue);
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    await WaitForSignalAsync(commandReady,cancellationTokenSource.Token);
                    commandReady.Reset();
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }
                    if (queue.TryDequeue(out var serverStreamQueueItem))
                    {
                        var command = serverStreamQueueItem.ServerResponse;
                        var callerSignal = serverStreamQueueItem.SignalEvent;
                        try
                        {
                            await responseStream.WriteAsync(command, cancellationTokenSource.Token);
                        }
                        catch (Exception e)
                        {
                            serverStreamQueueItem.CommadError = e;
                            _logger.LogError($"CommandServerStream() client={request.Hostname}@{request.Ip} error={e.Message}");
                        }
                        finally
                        {
                            callerSignal.Set();
                        }
                    }

                }
            }
            finally
            {
                UnRegisterClient(request.Hostname, request.Ip);
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"CommandServerStream() client={request.Hostname}@{request.Ip} error={e.Message}");
        }

        _logger.LogDebug($"<<CommandServerStream() client={request.Hostname}@{request.Ip}");
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