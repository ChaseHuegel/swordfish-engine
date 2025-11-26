using Swordfish.Library.Util;

namespace Swordfish.Audio;

public interface IAudioService
{
    Result Play(string id, bool block = false);
    
    Result Play(AudioSource audioSource, bool block = false);
}