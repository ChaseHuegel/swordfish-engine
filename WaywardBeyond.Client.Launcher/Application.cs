using Swordfish;

namespace WaywardBeyond.Client.Launcher;

internal class Application(in string[] args)
{
    private readonly SwordfishEngine _engine = new(args);

    public int Run()
    {
        return _engine.Run();
    }
}