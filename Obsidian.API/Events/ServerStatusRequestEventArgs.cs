using Obsidian.API._Interfaces;

namespace Obsidian.API.Events
{
    public class ServerStatusRequestEventArgs : BaseMinecraftEventArgs
    {
        public IServerStatus Status { get; }
        internal ServerStatusRequestEventArgs(IClientServer server, IServerStatus status) : base(server)
        {
            this.Status = status;
        }
    }
}
