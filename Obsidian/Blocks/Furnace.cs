using Obsidian.Util.Registry.Enums;
using System;

namespace Obsidian.Blocks
{
    public struct Furnace
    {
        public bool Lit { get => ((state >> litShift) & litBits) == 0; set => state = (state & litFilter) | ((value ? 0 : 1) << litShift); }
        public Direction Face { get => (Direction)((state >> faceShift) & faceBits); set => state = (state & faceFilter) | ((int)value << faceShift); }

        public static readonly string UnlocalizedName = "Furnace";
        public static int Id => 154;
        public int StateId => baseId + state;
        public int State => state;
        public static int BaseId => baseId;

        private int state;

        #region Constants
        private const int baseId = 3373;

        private const int litFilter = 0b_1111_1110;
        private const int faceFilter = 0b_1111_1001;

        private const int litShift = 0;
        private const int faceShift = 1;

        private const int litBits = 0b_1;
        private const int faceBits = 0b_11;
        #endregion

        public Furnace(int state)
        {
            this.state = state;
        }

        public Furnace(Direction face, bool lit)
        {
            state = lit ? 0 : 1;
            state |= (int)face << faceShift;
        }

        public static implicit operator Block(Furnace furnace)
        {
            return new Block(baseId, furnace.state);
        }

        public static explicit operator Furnace(Block block)
        {
            if (block.BaseId == baseId)
                return new Furnace(block.StateId - baseId);
            throw new InvalidCastException($"Cannot cast {block.Name} to {UnlocalizedName}");
        }
    }
}
