using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Obsidian.API
{
    public interface IWorldGenerator
    {
        public string Id { get; }
        public IChunk GenerateChunk(int x, int z);
    }
}
