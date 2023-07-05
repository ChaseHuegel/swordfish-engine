using System.Collections;
using Swordfish.Library.Annotations;
using Swordfish.Library.IO;

namespace Swordfish.Graphics;

public class TextureArray : Texture
{
    public TextureArray([NotNull] string name, [NotNull] IPath source) : base(name, source) { }
}
