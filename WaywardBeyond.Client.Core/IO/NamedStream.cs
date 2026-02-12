using System.IO;

namespace WaywardBeyond.Client.Core.IO;

internal readonly record struct NamedStream(string Name, Stream Value);