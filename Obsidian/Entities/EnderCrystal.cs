﻿using Obsidian.API;

namespace Obsidian.Entities
{
    public class EnderCrystal : Entity
    {
        public VectorF BeamTarget { get; private set; }

        public bool ShowBottom { get; private set; } = true;
    }
}
