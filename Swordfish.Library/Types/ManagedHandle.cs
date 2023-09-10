namespace Swordfish.Library.Types
{
    public abstract class ManagedHandle<TType> : Handle
    {
        public TType Handle
        {
            get
            {
                if (!handleCreated)
                {
                    handle = CreateHandle();
                    handleCreated = true;
                }

                return handle!;
            }
        }

        private TType handle;
        private bool handleCreated;

        protected abstract TType CreateHandle();

        protected abstract void FreeHandle();

        protected override void OnDisposed()
        {
            FreeHandle();
        }
    }
}