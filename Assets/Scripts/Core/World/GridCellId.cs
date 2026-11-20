using System;
using System.Globalization;

namespace Nightmare.Core.World
{
    /// <summary>Transport-independent identity: room + authored integer cell, never localized floats.</summary>
    public readonly struct GridCellId : IEquatable<GridCellId>
    {
        public string RoomId { get; }
        public int X { get; }
        public int Z { get; }
        public GridCellId(string roomId, int x, int z) { RoomId = roomId ?? throw new ArgumentNullException(nameof(roomId)); X = x; Z = z; }
        public bool Equals(GridCellId other) => RoomId == other.RoomId && X == other.X && Z == other.Z;
        public override bool Equals(object value) => value is GridCellId other && Equals(other);
        public override int GetHashCode() => ((RoomId.GetHashCode() * 397) ^ X) * 397 ^ Z;
        public override string ToString() => RoomId + ":" + X.ToString(CultureInfo.InvariantCulture) + ":" + Z.ToString(CultureInfo.InvariantCulture);
    }
}
