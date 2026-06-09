using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.KeyValueStore;
using NATS.Net;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Config;

namespace WaywardBeyond.Server.Core.Streaming;

public sealed class KeyValueStore : IDisposable
{
    private const string VAR_NATS_URL = "NATS_URL";
    private const string VAR_NATS_JWT = "NATS_JWT";
    private const string VAR_NATS_NKEY_SEED = "NATS_NKEY_SEED";
    
    private readonly NatsClient _natsClient;
    private readonly INatsKVContext _kv;
    
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, INatsKVStore> _stores = [];
    
    public KeyValueStore(in IConfiguration configuration)
    {
        string natsUrl = configuration.GetString(VAR_NATS_URL) ?? "nats://127.0.0.1:4222";
        string? natsJwt = configuration.GetString(VAR_NATS_JWT);
        string? natsNkeySeed = configuration.GetString(VAR_NATS_NKEY_SEED);

        NatsOpts natsOpts = NatsOpts.Default with
        {
            Url = natsUrl,
            AuthOpts = new NatsAuthOpts
            {
                Jwt = natsJwt,
                Seed = natsNkeySeed,
            },
        };
        
        _natsClient = new NatsClient(natsOpts);
        _kv = _natsClient.CreateKeyValueStoreContext();
    }
    
    public void Dispose()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        _cts.Dispose();

        Task.Run(_natsClient.DisposeAsync);
    }
    
    public Result Put<T>(string bucket, string key, T value)
    {
        TaskCompletionSource<Result> tcs = new();
        Task.Run(PutAsync).ContinueWith(OnFaulted, TaskContinuationOptions.OnlyOnFaulted);
        return tcs.Task.Result;
     
        async Task PutAsync()
        {
            if (!_stores.TryGetValue(bucket, out INatsKVStore? store))
            {
                store = await _kv.CreateStoreAsync(bucket, cancellationToken: _cts.Token);
                _stores.TryAdd(bucket, store);
            }
            
            await store.PutAsync(key, value, cancellationToken: _cts.Token);
            
            tcs.SetResult(Result.FromSuccess());
        }
        
        void OnFaulted(Task task, object? state)
        {
            Result result = new Result(success: false, message: $"Failed to put \"{key}\"=\"{value}\" in \"{bucket}\"", task.Exception);
            tcs.SetResult(result);
        }
    }
    
    public Result<T> Get<T>(string bucket, string key)
    {
        TaskCompletionSource<Result<T>> tcs = new();
        Task.Run(GetAsync).ContinueWith(OnFaulted, TaskContinuationOptions.OnlyOnFaulted);
        return tcs.Task.Result;
     
        async Task GetAsync()
        {
            if (!_stores.TryGetValue(bucket, out INatsKVStore? store))
            {
                store = await _kv.CreateStoreAsync(bucket, cancellationToken: _cts.Token);
                _stores.TryAdd(bucket, store);
            }

            NatsKVEntry<T> value = await store.GetEntryAsync<T>(key, cancellationToken: _cts.Token);
            tcs.SetResult(Result<T>.FromSuccess(value.Value!));
        }
        
        void OnFaulted(Task task, object? state)
        {
            var result = new Result<T>(success: false, value: default!, message: $"Failed to get \"{key}\" in \"{bucket}\"", task.Exception);
            tcs.SetResult(result);
        }
    }

    public Result<string[]> GetKeys(string bucket)
    {
        TaskCompletionSource<Result<string[]>> tcs = new();
        Task.Run(GetKeysAsync).ContinueWith(OnFaulted, TaskContinuationOptions.OnlyOnFaulted);
        return tcs.Task.Result;
     
        async Task GetKeysAsync()
        {
            if (!_stores.TryGetValue(bucket, out INatsKVStore? store))
            {
                store = await _kv.CreateStoreAsync(bucket, cancellationToken: _cts.Token);
                _stores.TryAdd(bucket, store);
            }

            var values = new List<string>();
            await foreach (string key in store.GetKeysAsync(cancellationToken: _cts.Token))
            {
                values.Add(key);
            }

            tcs.SetResult(Result<string[]>.FromSuccess(values.ToArray()));
        }
        
        void OnFaulted(Task task, object? state)
        {
            var result = new Result<string[]>(success: false, value: [], message: $"Failed to get all in \"{bucket}\"", task.Exception);
            tcs.SetResult(result);
        }
    }
    
    public Result Delete(string bucket, string key)
    {
        TaskCompletionSource<Result> tcs = new();
        Task.Run(DeleteAsync).ContinueWith(OnFaulted, TaskContinuationOptions.OnlyOnFaulted);
        return tcs.Task.Result;
     
        async Task DeleteAsync()
        {
            if (!_stores.TryGetValue(bucket, out INatsKVStore? store))
            {
                store = await _kv.CreateStoreAsync(bucket, cancellationToken: _cts.Token);
                _stores.TryAdd(bucket, store);
            }

            await store.DeleteAsync(key, cancellationToken: _cts.Token);

            tcs.SetResult(Result.FromSuccess());
        }
        
        void OnFaulted(Task task, object? state)
        {
            var result = new Result(success: false, message: $"Failed to get \"{key}\" in \"{bucket}\"", task.Exception);
            tcs.SetResult(result);
        }
    }

    public Result Delete(string bucket, string[] keys)
    {
        TaskCompletionSource<Result> tcs = new();
        Task.Run(DeleteAsync).ContinueWith(OnFaulted, TaskContinuationOptions.OnlyOnFaulted);
        return tcs.Task.Result;
     
        async Task DeleteAsync()
        {
            if (!_stores.TryGetValue(bucket, out INatsKVStore? store))
            {
                store = await _kv.CreateStoreAsync(bucket, cancellationToken: _cts.Token);
                _stores.TryAdd(bucket, store);
            }

            foreach (string key in keys)
            {
                await store.DeleteAsync(key, cancellationToken: _cts.Token);
            }

            tcs.SetResult(Result.FromSuccess());
        }
        
        void OnFaulted(Task task, object? state)
        {
            var result = new Result(success: false, message: $"Failed to delete keys in \"{bucket}\"", task.Exception);
            tcs.SetResult(result);
        }
    }
}
