using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Obsidian.API
{
    public interface IWorld
    {
        public string Name { get; }
        public bool Loaded { get; }

        public long Time { get; }
        public Gamemode GameType { get; }

        public Task SpawnExperienceOrbs(VectorF position, short count);
        public Task SpawnPainting(Vector position, Painting painting, PaintingDirection direction, Guid uuid = default);
        public List<VectorF> Traverse(VectorF startPosition, VectorF lookDirection, int length = 8);
        public List<VectorF> Traverse(VectorF startPosition, VectorF endPosition);

    }
}
