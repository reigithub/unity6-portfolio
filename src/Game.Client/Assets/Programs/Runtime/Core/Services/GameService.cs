namespace Game.Core.Services
{
    public abstract class GameService
    {
        protected internal virtual void Startup()
        {
        }

        protected internal virtual void Shutdown()
        {
        }

        protected internal virtual bool AllowResidentOnMemory => false;
    }
}