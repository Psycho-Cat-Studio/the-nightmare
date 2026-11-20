using System;
using System.Collections.Generic;

namespace Nightmare.Core.World
{
    /// <summary>A generated horizontal position; the authoring component supplies height and scene objects.</summary>
    public readonly struct BuildingGridPosition
    {
        public float X { get; }
        public float Z { get; }
        public BuildingGridPosition(float x, float z) { X = x; Z = z; }
    }

    /// <summary>Preserves the legacy generator bounds, x-major order and repeated floating-point increments.</summary>
    public static class BuildingGridLayout
    {
        /// <summary>Enumerates world-space cells using the original integer half-extents and exclusive upper bounds.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Tile size is nonpositive or nonfinite.</exception>
        public static IEnumerable<BuildingGridPosition> Positions(float centerX, float centerZ, int width, int height, float tileSize)
        {
            if (tileSize <= 0f || float.IsNaN(tileSize) || float.IsInfinity(tileSize))
                throw new ArgumentOutOfRangeException(nameof(tileSize), "Grid tile size must be positive and finite.");
            int halfWidth = width / 2;
            int halfHeight = height / 2;
            // These two axes intentionally remain explicit; multiplication/LINQ would obscure legacy rounding/order.
            for (float x = centerX - halfWidth; x < centerX + halfWidth + 1; x += tileSize)
                for (float z = centerZ - halfHeight; z < centerZ + halfHeight + 1; z += tileSize)
                    yield return new BuildingGridPosition(x, z);
        }
    }
}
