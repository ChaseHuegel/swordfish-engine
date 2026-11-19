using Swordfish;

namespace WaywardBeyond.Client.Launcher;

internal class Application(in string[] args)
{
    private const string COMPANY_NAME = "bitfish";

    private readonly SwordfishEngine _engine = new(args, companyName: COMPANY_NAME);

    public int Run()
    {
        return _engine.Run();
    }
}