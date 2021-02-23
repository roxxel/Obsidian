using Obsidian.API.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Obsidian.API._Interfaces
{
    public interface IMinecraftEventHandler
    {
        public Task InvokeServerTickAsync();

        public Task InvokePlayerJoinAsync(PlayerJoinEventArgs eventArgs);

        public Task InvokePlayerLeaveAsync(PlayerLeaveEventArgs eventArgs);

        public Task<ServerStatusRequestEventArgs> InvokeServerStatusRequest(ServerStatusRequestEventArgs eventargs);

        public Task<IncomingChatMessageEventArgs> InvokeIncomingChatMessageAsync(IncomingChatMessageEventArgs eventArgs);

        public event AsyncEventHandler<PlayerLeaveEventArgs> PlayerLeave;

        public event AsyncEventHandler<PlayerJoinEventArgs> PlayerJoin;

    }
}
