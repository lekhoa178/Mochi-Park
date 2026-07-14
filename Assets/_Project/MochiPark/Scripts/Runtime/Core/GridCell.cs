using System;
using UnityEngine;

namespace MochiPark.Runtime.Core
{
    [Serializable]
    public struct GridCell : IEquatable<GridCell>
    {
        [SerializeField] private int x;
        [SerializeField] private int y;

        public GridCell(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public int X => x;
        public int Y => y;

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }

        public static GridCell FromVector2Int(Vector2Int value)
        {
            return new GridCell(value.x, value.y);
        }

        public bool Equals(GridCell other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ y;
            }
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }
    }
}
