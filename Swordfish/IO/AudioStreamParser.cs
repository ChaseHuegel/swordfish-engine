using Swordfish.Audio;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class AudioStreamParser : IFileParser<AudioSource>
    {
        public string[] SupportedExtensions { get; } =
        [
            ".wav",
        ];

        object IFileParser.Parse(PathInfo file) => Parse(file);
        public AudioSource Parse(PathInfo file)
        {
            Stream stream = file.Open();
            return new AudioSource(stream);
        }
    }
}
