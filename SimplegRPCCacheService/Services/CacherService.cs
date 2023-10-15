using System.Collections.Concurrent;
using Common;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading;
using System;
using System.Data;
using SimplegRPCCacheService.Server;
using System.Linq;

namespace SimplegRPCCacheService.Services;

public class CacherService :Cacher.CacherBase
{
    private readonly ILogger<CacherService> _logger;
    private readonly IKeyValueStore _keyValueStore;

    private readonly Dictionary<string, ClientInfo> _clients = new ();
    private readonly object _lock = new object();
    public CacherService(ILogger<CacherService> logger,IKeyValueStore keyValueStore )
    {
        _keyValueStore = keyValueStore;
        _logger = logger;
    }

    private async Task<CommandResponse?> ExecuteCommand(
        SemaphoreSlim semaphoreSlim,
        CommandRequest commandRequest,
        IAsyncStreamReader<CommandResponse> responseStream,
        IServerStreamWriter<CommandRequest> requestStream,
        CancellationToken cancellationToken
    )
    {
        await semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            await requestStream.WriteAsync(commandRequest, cancellationToken);
            CommandResponse? responseFromClient = null;
            while (await responseStream.MoveNext(cancellationToken))
            {
                responseFromClient = responseStream.Current;
                break;
            }
            return responseFromClient;
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
    private ClientInfo RegisterClient(string key, SemaphoreSlim semaphoreSlim, CancellationTokenSource cancellationTokenSource)
    {
        lock (_lock)
        {
            var clientInfo = new ClientInfo(key, semaphoreSlim, cancellationTokenSource,
                new WaitableQueue<CommandQueueItem>());
            if (!_clients.TryAdd(key, clientInfo))
            {
                clientInfo.CancellationTokenSource.Cancel();
                InternalUnRegisterClient(key);
            }

            return clientInfo;
        }
    }

    private void InternalUnRegisterClient(string key)
    {
        if (_clients.TryGetValue(key, out var clientInfo))
        {
            FlushPendingCommands(clientInfo);
        }
        _clients.Remove(key);

    }
    private void UnRegisterClient(string key)
    {
        lock (_lock)
        {
            InternalUnRegisterClient(key);
        }
    }

    private void FlushPendingCommands(ClientInfo clientInfo)
    {
        while (clientInfo.Commands.TryDequeue(out var command))
        {
            command.Exception = new Exception("Client disconnected");
            command.ExecutionCompleted.Set();
        }
    }

    public async Task<IEnumerable<CommandResponse>> BroadCastCommandAsync(CommandRequest commandRequest, CancellationToken cancellationToken = default)
    {
        List<Task<CommandResponse>> tasks = new List<Task<CommandResponse>>();

        lock (_lock)
        {
            foreach (var clientEntry in _clients)
            {
                var clientInfo = clientEntry.Value;
                var commandQueueItem = new CommandQueueItem(commandRequest, new ManualResetEventSlim(false));

                clientInfo.Commands.Enqueue(commandQueueItem);

                tasks.Add(WaitForCommandCompletionAsync(commandQueueItem, cancellationToken));
            }
        }

        CommandResponse[] responses;
        try
        {
            responses = await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            throw new AggregateException("One or more clients failed to process the command.", ex);
        }

        return responses;
    }

    private async Task<CommandResponse> WaitForCommandCompletionAsync(CommandQueueItem commandQueueItem, CancellationToken cancellationToken)
    {
        await Task.Factory.StartNew(() => commandQueueItem.ExecutionCompleted.Wait(cancellationToken),
            cancellationToken,
            TaskCreationOptions.RunContinuationsAsynchronously,
            TaskScheduler.Default
        );

        if (commandQueueItem.IsError)
        {
            throw commandQueueItem.Exception!;
        }

        return commandQueueItem.Response!;
    }


    public override async Task ExchangeCommands(IAsyncStreamReader<CommandResponse> responseStream, IServerStreamWriter<CommandRequest> requestStream, ServerCallContext context)
    {
        using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        using SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        
        var commandResponse = await ExecuteCommand(
            semaphoreSlim,
            new CommandRequest()
            {
                ServerTimestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                CommandType = CommandType.Idle
            },
            responseStream,
            requestStream,
            context.CancellationToken
        );

        if (commandResponse == null)
        {
            throw new NullReferenceException("commandResponse must have a value");
        }

        var clientInfo = RegisterClient(
            commandResponse.ClientId ?? throw new NullReferenceException("ClientId must have a value"),
            semaphoreSlim, cancellationTokenSource);

        try
        {
            while (!cancellationTokenSource.IsCancellationRequested &&
                   !context.CancellationToken.IsCancellationRequested)
            {
                WaitHandle.WaitAny(new WaitHandle[]
                {
                    cancellationTokenSource.Token.WaitHandle,
                    context.CancellationToken.WaitHandle,
                    clientInfo.Commands.WaitHandle
                });

                if (cancellationTokenSource.IsCancellationRequested ||
                    context.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (clientInfo.Commands.TryDequeue(out CommandQueueItem commandQueueItem))
                {
                    try
                    {
                        commandResponse = await ExecuteCommand(semaphoreSlim,
                            commandQueueItem.Request,
                            responseStream,
                            requestStream,
                            context.CancellationToken
                        );
                        if (commandResponse == null)
                        {
                            throw new NullReferenceException("commandResponse must have a value");
                        }

                        commandQueueItem.Response = commandResponse;
                    }
                    catch (Exception e)
                    {
                        commandQueueItem.Exception = e;
                        _logger.LogError($"ExchangeCommands() client={clientInfo.Key},error={e.Message}");
                    }
                    finally
                    {
                        commandQueueItem.ExecutionCompleted.Set();
                    }

                    if (commandQueueItem.Exception != null)
                    {
                        throw commandQueueItem.Exception;
                    }
                }
            }
        }
        finally
        {
            UnRegisterClient(clientInfo.Key);
        }
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