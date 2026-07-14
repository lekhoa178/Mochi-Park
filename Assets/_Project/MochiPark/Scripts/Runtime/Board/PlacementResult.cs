using System.Collections.Generic;
using MochiPark.Runtime.Core;
using UnityEngine;

namespace MochiPark.Runtime.Board
{
    public readonly struct PlacementResult
    {
        public PlacementResult(bool isValid, PlacementFailReason reason, Vector2Int failedCell, List<Vector2Int> footprintCells)
        {
            IsValid = isValid;
            Reason = reason;
            FailedCell = failedCell;
            FootprintCells = footprintCells ?? new List<Vector2Int>();
        }

        public bool IsValid { get; }
        public PlacementFailReason Reason { get; }
        public Vector2Int FailedCell { get; }
        public IReadOnlyList<Vector2Int> FootprintCells { get; }

        public static PlacementResult Success(List<Vector2Int> footprintCells)
        {
            return new PlacementResult(true, PlacementFailReason.Success, default, footprintCells);
        }

        public static PlacementResult Fail(PlacementFailReason reason, Vector2Int failedCell, List<Vector2Int> footprintCells)
        {
            return new PlacementResult(false, reason, failedCell, footprintCells);
        }
    }
}
