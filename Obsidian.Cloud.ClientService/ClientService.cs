using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;
using Obsidian.API;
using Obsidian.Concurrency;
using Obsidian.Entities;
using Obsidian.Events;
using Obsidian.Interfaces;
using Obsidian.Util;
using Obsidian.WorldData;

namespace Obsidian.Cloud.ClientService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class ClientService : StatelessService, IClientServer
    {
        public Config Config { get; }
        public IConfig Configuration => Config;
        public World World { get; private set; }

        MinecraftEventHandler IClientServer.Events => throw new NotImplementedException();

        World IClientServer.World => throw new NotImplementedException();

        Config IClientServer.Config => throw new NotImplementedException();

        ConcurrentDictionary<Guid, Player> OnlinePlayers;

        private readonly ConcurrentHashSet<Client> clients;
        private readonly TcpListener tcpListener;

        public ClientService(StatelessServiceContext context)
            : base(context)
        {
            this.clients = new ConcurrentHashSet<Client>();
            this.OnlinePlayers = new ConcurrentDictionary<Guid, Player>();
            this.tcpListener = new TcpListener(IPAddress.Any, 25565) { ExclusiveAddressUse = false };


            string serverDir = $"Server-69";
            Directory.CreateDirectory(serverDir);
            string configPath = Path.Combine(serverDir, "config.json");
            if (File.Exists(configPath))
            {
                Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
            }
            else
            {
                Config = new Config();
                File.WriteAllText(configPath, JsonConvert.SerializeObject(Config, Formatting.Indented));
                ServiceEventSource.Current.ServiceMessage(this.Context, $"Created new configuration file for Server-69");
            }
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            ServiceInstanceListener sil = new ServiceInstanceListener((serviceContext) =>
            {
                return new TcpCommunicationListener(serviceContext);
            });

            return new ServiceInstanceListener[] { sil };
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Config is null) { return; }

            ServiceEventSource.Current.ServiceMessage(this.Context, "Launching Obsidian Server.");
            this.tcpListener.Start();
            ServiceEventSource.Current.ServiceMessage(this.Context, "Listening for new clients...");

            while (true)
            {
                var tcp = await this.tcpListener.AcceptTcpClientAsync();
                ServiceEventSource.Current.ServiceMessage(this.Context, $"New connection from client with IP {tcp.Client.RemoteEndPoint}");
                var client = new Client(tcp, this.Config, this.clients.Count, this); //TODO: clients.Count * ClientServerID
                this.clients.Add(client);
                client.Disconnected += client => clients.TryRemove(client);
                _ = Task.Run(client.StartConnectionAsync);
            }

        }
    }
}
