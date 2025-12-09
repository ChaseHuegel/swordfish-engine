using Swordfish.Library.Util;

namespace Swordfish.Audio;

public interface IAudioService
{
    Result Play(string id, float volume = 1.0f, bool block = false);
    
    Result Play(AudioSource audioSource, float volume = 1.0f, bool block = false);
}