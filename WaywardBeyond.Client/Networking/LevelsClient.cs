using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NsdRpc;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// Thin async wrapper over the generated server proxy for level management. Each call is correlated by
/// nsd-rpc, so request order never matters. A disconnected client faults pending calls, and the wrapper
/// maps every failure to the menu's safe default.
/// </summary>
internal sealed class LevelsClient(ClientRpcHost host, ILogger<LevelsClient> logger)
{
    private readonly ClientRpcHost _host = host;
    private readonly ILogger _logger = logger;

    public async Task<LevelListing> GetLevelsAsync(CancellationToken cancellationToken = default)
    {
        if (_host.Api == null)
        {
            return new LevelListing([], false);
        }

        RpcResult<ListLevelsResponse> result = await _host.Api
            .ListLevelsAsync(new ListLevelsRequest(), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to list levels: {message}.", result.ErrorMessage);
            return new LevelListing([], false);
        }

        return new LevelListing(result.Value.Levels ?? [], result.Value.CanCreateSave);
    }

    public async Task<bool> CreateLevelAsync(string name, string seed, GameMode gameMode, CancellationToken cancellationToken = default)
    {
        if (_host.Api == null)
        {
            return false;
        }

        RpcResult<NewLevelResponse> result = await _host.Api
            .CreateLevelAsync(new NewLevelRequest { Name = name, Seed = seed, GameMode = gameMode }, cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess && result.Value.Success;
    }

    public async Task<bool> DeleteLevelAsync(string levelGuid, CancellationToken cancellationToken = default)
    {
        if (_host.Api == null)
        {
            return false;
        }

        RpcResult<DeleteLevelResponse> result = await _host.Api
            .DeleteLevelAsync(new DeleteLevelRequest { LevelGuid = levelGuid }, cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess && result.Value.Success;
    }

    public async Task<bool> SaveLevelAsync(CancellationToken cancellationToken = default)
    {
        if (_host.Api == null)
        {
            return false;
        }

        RpcResult<SaveLevelResponse> result = await _host.Api
            .SaveLevelAsync(new SaveLevelRequest(), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess && result.Value.Success;
    }

    /// <summary>Fire-and-forget notification that the player is returning to the menu.</summary>
    public void SendLeaveGame()
    {
        _host.Api?.LeaveGame(new LeaveGameRequest());
    }
}
