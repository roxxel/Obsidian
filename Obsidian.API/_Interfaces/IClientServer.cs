using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;

namespace Obsidian.API._Interfaces
{
    public interface IClientServer
    {
        public IMinecraftEventHandler Events { get; }

        public IWorld World { get; }

        public IConfig Config { get; }

        public ILogger Logger { get; }

        public ConcurrentDictionary<Guid, IPlayer> OnlinePlayers { get; }

        internal Task DisconnectIfConnectedAsync(string username, IChatMessage reason = null);
    }
}
