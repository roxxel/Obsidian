using Obsidian.API;
using System.Collections.Generic;

namespace Obsidian.WorldData
{
    public abstract class WorldGenerator : IWorldGenerator
    {
        public List<Chunk> Chunks { get; }

        public string Id { get; }

        public WorldGenerator(string id)
        {
            this.Chunks = new List<Chunk>();
            this.Id = id;
        }

        public abstract IChunk GenerateChunk(int x, int z);
    }
}