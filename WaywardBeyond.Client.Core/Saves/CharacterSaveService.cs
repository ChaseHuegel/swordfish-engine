using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Globalization;
using WaywardBeyond.Client.Core.UI.Layers;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class CharacterSaveService(in LocalizedFormatter localizedFormatter, in NotificationService notificationService )
{
    private const string CHARACTERS_FOLDER = "characters/";

    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly NotificationService _notificationService = notificationService;

    private readonly PathInfo _charactersDirectory = new(CHARACTERS_FOLDER);
    
    public CharacterSave[] GetSaves()
    {
        PathInfo[] characterFiles = _charactersDirectory.GetFiles(SearchOption.AllDirectories);
        var characterSaves = new CharacterSave[characterFiles.Length];
        
        for (var i = 0; i < characterFiles.Length; i++)
        {
            PathInfo characterFile = characterFiles[i];
            
            byte[] bytes = characterFile.ReadBytes();
            Character character = Character.Deserialize(bytes);
            characterSaves[i] = new CharacterSave(characterFile, character);
        }
        
        return characterSaves;
    }
    
    public Result<CharacterSave> CreateSave(Character character)
    {
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        character.LastPlayedMs = nowUtcMs;
        character.AgeMs = 0;
        
        PathInfo characterSavePath = _charactersDirectory.At($"{character.Guid}.dat");
        var characterSave = new CharacterSave(characterSavePath, character);

        Result saveResult = Save(characterSave);
        if (!saveResult)
        {
            _notificationService.Push(_localizedFormatter.GetString("notification.character.create.failed", character.Name));
            return new Result<CharacterSave>(success: false, default, saveResult.Message, saveResult.Exception);
        }
        
        _notificationService.Push(_localizedFormatter.GetString("notification.character.created", character.Name));
        return Result<CharacterSave>.FromSuccess(characterSave);
    }

    public Result Save(CharacterSave save)
    {
        try
        {
            Character character = save.Character;
            byte[] bytes = character.Serialize();
            using var stream = new MemoryStream(bytes);
            Directory.CreateDirectory(save.Path.GetDirectory());
            save.Path.Write(stream);

            _notificationService.Push(_localizedFormatter.GetString("notification.character.saved", save.Character.Name));
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            _notificationService.Push(_localizedFormatter.GetString("notification.character.saving.failed", save.Character.Name));
            return new Result(success: false, $"Unexpected error saving character \"{save.Character.Name}\".", ex);
        }
    }
}