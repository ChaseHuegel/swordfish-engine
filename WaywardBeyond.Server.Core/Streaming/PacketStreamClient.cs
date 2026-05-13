using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using Swordfish.Library.Util;
using Torches.Networking.Models;
using WaywardBeyond.Server.Core.Config;
using WaywardBeyond.Server.Core.Networking;
using WaywardBeyond.Server.Core.Serialization;

namespace WaywardBeyond.Server.Core.Streaming;

internal sealed class PacketStreamClient : IDisposable
{
    private const string VAR_NATS_URL = "NATS_URL";
    private const string VAR_SERVER_ID = "SERVER_ID";
    private const string STREAM_NAME = "WAYWARD_BEYOND";
    private const string SUBJECT_PREFIX = "wb.packets";
    
    private readonly ILogger<PacketStreamClient> _logger;
    private readonly ServerEnvironment _environment;
    private readonly IProtocol _protocol;

    private readonly NatsClient _natsClient;
    private readonly CancellationTokenSource _cts;
    private readonly TaskCompletionSource<INatsJSContext?> _jetStreamTCS;
    private readonly PacketNatsSerializer _packetSerializer;

    private string? _source;
    
    public PacketStreamClient(in ILogger<PacketStreamClient> logger, in ServerEnvironment environment, in IProtocol protocol)
    {
        _logger = logger;
        _environment = environment;
        _protocol = protocol;

        string natsUrl = environment.GetString(VAR_NATS_URL) ?? "nats://127.0.0.1:4222";
        _natsClient = new NatsClient(natsUrl);
        _cts = new CancellationTokenSource();
        _jetStreamTCS = new TaskCompletionSource<INatsJSContext?>();
        _packetSerializer = new PacketNatsSerializer();
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
    
    public Result Publish(string destination, Packet packet)
    {
        TaskCompletionSource<Result> tcs = new();
        Task.Run(PublishAsync);
        return tcs.Task.Result;
     
        async Task PublishAsync()
        {
            INatsJSContext? jetStream = await _jetStreamTCS.Task;
            if (jetStream == null)
            {
                tcs.SetResult(Result.FromFailure("Chat event streaming failed to start."));
                return;
            }
            
            NatsResult<PubAckResponse> publishResponse = await jetStream.TryPublishAsync(
                subject: $"{SUBJECT_PREFIX}.{destination}.{packet.Type}",
                data: packet,
                serializer: _packetSerializer
            );
            
            string? errorMessage = publishResponse.Success ? null : publishResponse.Error.ToString();
            var result = new Result(publishResponse.Success, errorMessage);
            tcs.SetResult(result);
        }
    }
    
    public Task StartAsync(string source)
    {
        if (_source != null)
        {
            return Task.CompletedTask;
        }
        
        _source = source;
        return TryRunTask(StartAsyncInternal);
    }

    private Task TryRunTask(Func<Task> task)
    {
        return Task.Run(task).ContinueWith(OnFaulted, TaskContinuationOptions.OnlyOnFaulted);
     
        void OnFaulted(Task obj)
        {
            _logger.LogError(obj.Exception, "Caught an exception while starting packet streaming.");
        }
    }
    
    private async Task StartAsyncInternal()
    {
        INatsJSContext jetStream = _natsClient.CreateJetStreamContext();
        INatsJSStream stream = await jetStream.CreateStreamAsync(
            new StreamConfig(
                STREAM_NAME,
                subjects: [$"{SUBJECT_PREFIX}.{_source}.>"]
            ),
            cancellationToken: _cts.Token
        );
        
        _jetStreamTCS.SetResult(jetStream);
        _ = TryRunTask(() => ConsumeAsync(stream));
    }
    
    private async Task ConsumeAsync(INatsJSStream stream)
    {
        string? consumerID = _environment.GetString(VAR_SERVER_ID);
        ConsumerConfig consumerConfig;
        if (consumerID == null)
        {
            _logger.LogWarning("Unable to find an ID to use for a durable consumer. Packets will be missed during any server restarts if this is in a cluster!");
            consumerConfig = new ConsumerConfig();
        }
        else
        {
            consumerConfig = new ConsumerConfig(consumerID);
        }
	    
        consumerConfig.DeliverPolicy = ConsumerConfigDeliverPolicy.New;
        
        INatsJSConsumer consumer = await stream.CreateOrUpdateConsumerAsync(consumerConfig, cancellationToken: _cts.Token);
	    
        await foreach (NatsJSMsg<Packet> msg in consumer.ConsumeAsync(_packetSerializer, cancellationToken: _cts.Token))
        {
            await msg.AckAsync(cancellationToken: _cts.Token);
            if (msg.Data.Data != null)
            {
                _logger.LogError("Received a null packet from subject: {subject}", msg.Subject);
                continue;
            }
            
            _logger.LogInformation("Recv packet from subject {subject}: {type}.", msg.Subject, msg.Data.Type);
            
            Result result = _protocol.Send(msg.Data);
            if (!result)
            {
                _logger.LogError("Failed to relay a packet from subject {subject}: {type}.", msg.Subject, msg.Data.Type);
            }
        }
    }
}