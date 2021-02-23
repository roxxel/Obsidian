using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Obsidian.API
{
    public interface ILevel
    {
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        public int SpawnZ { get; set; }

        public long Time { get; }

        public string GeneratorName { get; }

        public Gamemode GameType { get; }
    }
}
