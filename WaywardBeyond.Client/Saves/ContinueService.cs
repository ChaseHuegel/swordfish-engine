using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.Saves;

/// <summary>Resumes the last played server, save, and character.</summary>
internal sealed class ContinueService(
    ClientRpcHost rpcHost,
    NetworkingConfig networkingConfig,
    ProfileSettings profileSettings,
    GameSaveService gameSaveService,
    GameSaveManager gameSaveManager,
    CharacterSaveManager characterSaveManager,
    ICharacterStorage characterStorage,
    ILocalization localization,
    ILogger<ContinueService> logger
) {
    private const string TITLE_KEY = "ui.continue.failedTitle";
    private const string CONNECT_KEY = "ui.continue.connectFailed";
    private const string SAVE_KEY = "ui.continue.saveMissing";
    private const string CHARACTER_KEY = "ui.continue.characterMissing";
    private const string LOAD_KEY = "ui.continue.loadFailed";

    private readonly ConcurrentQueue<Failure> _failures = new();
    private readonly ILogger<ContinueService> _logger = logger;
    private int _running;

    /// <summary>True when a last played save and character are recorded.</summary>
    public bool HasLastSession =>
        !string.IsNullOrWhiteSpace(profileSettings.LastSaveGuid.Get())
        && !string.IsNullOrWhiteSpace(profileSettings.LastCharacterUuid.Get());

    /// <summary>Starts one Continue attempt. Repeated calls are ignored while one is running.</summary>
    public void Start()
    {
        if (!HasLastSession || Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(RunAsync);
    }

    /// <summary>Retrieves a failure raised by the last attempt.</summary>
    public bool TryDequeueFailure(out Failure failure)
    {
        return _failures.TryDequeue(out failure);
    }

    private async Task RunAsync()
    {
        try
        {
            WaywardBeyond.GameState.Set(GameState.Loading);

            LastServerMode mode = profileSettings.LastServerMode.Get();
            if (mode == LastServerMode.Local)
            {
                rpcHost.Disconnect();
            }
            else
            {
                string host = profileSettings.LastServerHost.Get();
                int port = profileSettings.LastServerPort.Get();
                Result result = rpcHost.ConnectRemote(host, port);
                if (!result)
                {
                    Fail(CONNECT_KEY);
                    return;
                }

                networkingConfig.RemoteHost.Set(host);
                networkingConfig.RemotePort.Set(port);
                networkingConfig.Save();
            }

            gameSaveService.ClearListing();
            await gameSaveService.RefreshLevelsAsync();

            if (!rpcHost.IsConnected)
            {
                Fail(CONNECT_KEY);
                return;
            }

            string saveGuid = profileSettings.LastSaveGuid.Get();
            GameSave? save = null;
            GameSave[] saves = gameSaveService.GetSaves();
            for (var i = 0; i < saves.Length; i++)
            {
                if (saves[i].Level.Guid == saveGuid)
                {
                    save = saves[i];
                    break;
                }
            }

            if (save == null)
            {
                Fail(SAVE_KEY);
                return;
            }

            string characterUuid = profileSettings.LastCharacterUuid.Get();
            Character? character = null;
            IReadOnlyList<Character> characters = characterStorage.GetAllCharacters();
            for (var i = 0; i < characters.Count; i++)
            {
                if (characters[i].Uuid.ToString() == characterUuid)
                {
                    character = characters[i];
                    break;
                }
            }

            if (character == null)
            {
                Fail(CHARACTER_KEY);
                return;
            }

            gameSaveManager.ActiveSave = save.Value;
            characterSaveManager.ActiveSave = character.Value;
            await gameSaveManager.Load();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume the last session.");
            Fail(LOAD_KEY);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private void Fail(string messageKey)
    {
        WaywardBeyond.GameState.Set(GameState.MainMenu);
        _failures.Enqueue(new Failure(
            localization.GetString(TITLE_KEY) ?? "Continue Failed",
            localization.GetString(messageKey) ?? messageKey
        ));
    }

    /// <summary>One Continue failure shown by the menu.</summary>
    internal readonly record struct Failure(string Title, string Body);
}
