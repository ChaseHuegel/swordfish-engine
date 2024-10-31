namespace Swordfish.Library.IO;

public class RootedPathService : PathService
{
    public RootedPathService(string root)
    {
        this.root = new Path(root);
    }
}