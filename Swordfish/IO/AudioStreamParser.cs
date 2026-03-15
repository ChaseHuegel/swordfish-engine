using Swordfish.Audio;
using Swordfish.Library.IO;

namespace Swordfish.IO
{
    internal class AudioStreamParser : IFileParser<AudioStream>
    {
        public string[] SupportedExtensions { get; } =
        [
            ".wav",
        ];

        object IFileParser.Parse(PathInfo file) => Parse(file);
        public AudioStream Parse(PathInfo file)
        {
            Stream stream = file.Open();
            return new AudioStream(stream);
        }
    }
}
