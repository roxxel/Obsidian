using Obsidian.API._Interfaces;

namespace Obsidian.API.Events
{
    public class BaseMinecraftEventArgs : AsyncEventArgs
    {
        /// <summary>
        /// Server this event took place in.
        /// </summary>
        public IClientServer Server { get; }

        /// <summary>
        /// Constructs a new BaseMinecraftEventArgs object.
        /// <param name="server">The server that's handling this event.</param>
        internal BaseMinecraftEventArgs(IClientServer server)
        {
            this.Server = server;
        }
    }
}
