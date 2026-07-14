using System.Collections.Generic;
using MochiPark.Runtime.Data;
using UnityEngine;

namespace MochiPark.Runtime.Core
{
    public static class GridRotationUtility
    {
        public static Vector2Int Rotate(Vector2Int cell, GridRotation rotation, PatternRotationMode rotationMode)
        {
            if (rotationMode == PatternRotationMode.None)
            {
                return cell;
            }

            switch (rotation)
            {
                case GridRotation.East:
                    return new Vector2Int(cell.y, -cell.x);
                case GridRotation.South:
                    return new Vector2Int(-cell.x, -cell.y);
                case GridRotation.West:
                    return new Vector2Int(-cell.y, cell.x);
                default:
                    return cell;
            }
        }

        public static List<Vector2Int> ResolveCells(PatternDefinitionSO pattern, Vector2Int anchor, GridRotation rotation)
        {
            List<Vector2Int> result = new List<Vector2Int>();

            if (pattern == null)
            {
                return result;
            }

            IReadOnlyList<GridCell> cells = pattern.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int offset = Rotate(cells[i].ToVector2Int(), rotation, pattern.RotationMode);
                result.Add(anchor + offset);
            }

            return result;
        }
    }
}
