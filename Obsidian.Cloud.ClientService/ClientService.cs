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
using Obsidian.Util;
using Obsidian.WorldData;

namespace Obsidian.Cloud.ClientService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class ClientService : StatelessService
    {
        private static readonly Dictionary<int, Server> Servers = new();

        public ClientService(StatelessServiceContext context)
            : base(context)
        {

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

            string version = "0.1-DEV";
            Globals.BasePath = Path.GetTempPath();
            Directory.CreateDirectory(Globals.BasePath);
            string globalConfigFile = Path.Combine(Globals.BasePath, "global_config.json");
            if (File.Exists(globalConfigFile))
            {
                Globals.Config = JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(globalConfigFile));
            }
            else
            {
                Globals.Config = new GlobalConfig();
                File.WriteAllText(globalConfigFile, JsonConvert.SerializeObject(Globals.Config, Formatting.Indented));
                Console.WriteLine("Created new global configuration `file");
            }

            string configPath = Path.Combine(Globals.BasePath, "config.json");
            Config config;
            if (File.Exists(configPath))
            {
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
            }
            else
            {
                config = new Config();
                File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
                Console.WriteLine($"Created new configuration file for Server-0");
            }
            var server = new Server(config, version, 0);

            await server.StartServerAsync();
            

        }
    }
}
