namespace Swordfish.Library.Types;

public abstract class ManagedHandle<TType> : Handle
{
    public TType Handle
    {
        get
        {
            if (_handleCreated)
            {
                return _handle!;
            }

            _handle = CreateHandle();
            _handleCreated = true;
            return _handle!;
        }
    }

    private TType _handle;
    private bool _handleCreated;

    protected abstract TType CreateHandle();

    protected abstract void FreeHandle();

    protected override void OnDisposed()
    {
        FreeHandle();
    }
}