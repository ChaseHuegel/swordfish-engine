using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// Client RPC wrapper for authoritative region cut and fragmented paste/spawn uploads.
/// </summary>
internal sealed class RegionClient(ClientRpcHost host, ILogger<RegionClient> logger, GameplayConfig? gameplayConfig = null)
{
    private readonly ClientRpcHost _host = host;
    private readonly ILogger _logger = logger;
    private readonly GameplayConfig? _gameplayConfig = gameplayConfig;
    private int _nextBatchId;

    /// <summary>Requests that the server clear the selected voxel box on a structure.</summary>
    public async Task<RpcResult<RegionOpResponse>> CutRegionAsync(
        CutRegionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        GameServerApiClient? api = _host.Api;
        if (api == null)
        {
            return RpcResult<RegionOpResponse>.Failure(GameRpcErrors.NOT_JOINED, "client is not connected");
        }

        return await api
            .CutRegionAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Requests that the server place the player's currently held blueprint placeable item.</summary>
    public async Task<RpcResult<RegionOpResponse>> PlaceBlueprintItemAsync(
        PlaceBlueprintItemRequest request,
        CancellationToken cancellationToken = default
    )
    {
        GameServerApiClient? api = _host.Api;
        if (api == null)
        {
            return RpcResult<RegionOpResponse>.Failure(GameRpcErrors.NOT_JOINED, "client is not connected");
        }

        return await api
            .PlaceBlueprintItemAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Fragments and uploads a region paste or spawn request sequentially to the server.</summary>
    public async Task<RpcResult<RegionOpResponse>> PasteRegionAsync(
        PasteRegionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        GameServerApiClient? api = _host.Api;
        if (api == null)
        {
            return RpcResult<RegionOpResponse>.Failure(GameRpcErrors.NOT_JOINED, "client is not connected");
        }

        byte[] payload = request.Serialize();
        int maxPayloadBytes = _gameplayConfig?.Regions.MaxPayloadBytes.Get() ?? RegionLimits.MAX_PAYLOAD_BYTES;
        if (!RegionLimits.IsValidPayloadSize(payload.Length, maxPayloadBytes) || payload.Length > PayloadFragmenter.MAX_PAYLOAD_BYTES)
        {
            return RpcResult<RegionOpResponse>.Success(new RegionOpResponse
            {
                Success = false,
                Completed = true,
                ErrorKey = "notification.region.invalidBounds",
            });
        }

        uint batchId = unchecked((uint)Interlocked.Increment(ref _nextBatchId));
        int partCount = PayloadFragmenter.PartCount(payload.Length);
        RpcResult<RegionOpResponse> lastResult = default;

        for (var partIndex = 0; partIndex < partCount; partIndex++)
        {
            if (!ReferenceEquals(_host.Api, api))
            {
                return RpcResult<RegionOpResponse>.Failure(
                    GameRpcErrors.NOT_JOINED, "client disconnected during region upload");
            }

            var part = new RegionUploadPart
            {
                BatchId = batchId,
                PartIndex = (uint)partIndex,
                PartCount = (uint)partCount,
                Payload = PayloadFragmenter.Slice(payload, partIndex),
            };

            lastResult = await api
                .PasteRegionPartAsync(part, cancellationToken)
                .ConfigureAwait(false);

            if (!lastResult.IsSuccess || !lastResult.Value.Success)
            {
                _logger.LogDebug(
                    "Paste region part {partIndex}/{partCount} did not succeed: {message}",
                    partIndex + 1, partCount, lastResult.ErrorMessage);
                return lastResult;
            }
        }

        return lastResult;
    }
}
