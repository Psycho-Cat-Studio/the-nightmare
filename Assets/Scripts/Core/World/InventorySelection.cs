using System.Collections.Generic;

namespace Nightmare.Core.World
{
    /// <summary>Searches available inventory slots cyclically while preserving selection when none is available.</summary>
    public static class InventorySelection
    {
        /// <summary>Searches cyclically in either direction, returning the original slot when none is available.</summary>
        /// <param name="quantities">Inventory quantities indexed by catalog slot; nonzero means available.</param>
        /// <param name="start">Current slot, also returned if the inventory is empty.</param>
        /// <param name="direction">One for the next slot, minus one for the previous slot.</param>
        /// <param name="itemCount">Number of catalog entries participating in the search.</param>
        public static int FindAvailable(IReadOnlyList<int> quantities, int start, int direction, int itemCount)
        {
            int index = start;
            for (int visited = 0; visited < itemCount; visited++)
            {
                index += direction;
                if (index >= itemCount) index = 0;
                if (index < 0) index = itemCount - 1;
                if (quantities[index] != 0) return index;
            }
            return start;
        }
    }
}
