using System.Collections.Generic;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using UnityEngine;

namespace MochiPark.Runtime.Board
{
    public sealed class RuntimeBoardState
    {
        public const float DefaultTileSizeXZ = 2f;
        private readonly BoardCellType[,] cellTypes;
        private readonly HashSet<Vector2Int> unlockedCells = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, string> cellToUnitInstanceId = new Dictionary<Vector2Int, string>();
        private readonly Dictionary<Vector2Int, List<InfluenceLink>> pathCellToInfluencingUnits = new Dictionary<Vector2Int, List<InfluenceLink>>();
        private readonly Dictionary<string, List<Vector2Int>> unitToInfluencedPathCells = new Dictionary<string, List<Vector2Int>>();
        private readonly Dictionary<Vector2Int, int> pathIndexByCell = new Dictionary<Vector2Int, int>();
        private readonly List<Vector2Int> pathCells = new List<Vector2Int>();

        private RuntimeBoardState(BoardDefinitionSO definition)
        {
            Definition = definition;
            Width = definition.Width;
            Height = definition.Height;
            TileSizeXZ = DefaultTileSizeXZ;
            TileHeightResolved = definition.ResolveTileHeight();
            TileSurfaceY = TileHeightResolved;
            cellTypes = new BoardCellType[Width, Height];
        }

        public BoardDefinitionSO Definition { get; }
        public int Width { get; }
        public int Height { get; }
        public float TileSizeXZ { get; }
        public float TileHeightResolved { get; }
        public float TileSurfaceY { get; }
        public IReadOnlyList<Vector2Int> PathCells => pathCells;
        public IReadOnlyDictionary<Vector2Int, string> CellToUnitInstanceId => cellToUnitInstanceId;
        public IReadOnlyDictionary<string, List<Vector2Int>> UnitToInfluencedPathCells => unitToInfluencedPathCells;

        public static RuntimeBoardState Build(BoardDefinitionSO definition)
        {
            RuntimeBoardState state = new RuntimeBoardState(definition);

            for (int x = 0; x < state.Width; x++)
            {
                for (int y = 0; y < state.Height; y++)
                {
                    state.cellTypes[x, y] = definition.GetCellType(new Vector2Int(x, y));
                }
            }

            Vector2Int start = definition.StartCell.ToVector2Int();
            Vector2Int exit = definition.ExitCell.ToVector2Int();
            if (state.IsInBounds(start))
            {
                state.cellTypes[start.x, start.y] = BoardCellType.Start;
            }

            if (state.IsInBounds(exit))
            {
                state.cellTypes[exit.x, exit.y] = BoardCellType.Exit;
            }

            state.BuildPathLookup();
            return state;
        }

        public bool IsInBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        public BoardCellType GetCellType(Vector2Int cell)
        {
            return IsInBounds(cell) ? cellTypes[cell.x, cell.y] : BoardCellType.OutOfBounds;
        }

        public bool IsPathCell(Vector2Int cell)
        {
            BoardCellType type = GetCellType(cell);
            return type == BoardCellType.Path || type == BoardCellType.Start || type == BoardCellType.Exit;
        }

        public bool IsBuildableUnlocked(Vector2Int cell)
        {
            BoardCellType type = GetCellType(cell);
            if (type == BoardCellType.BuildableOpen)
            {
                return true;
            }

            return type == BoardCellType.BuildableLocked && unlockedCells.Contains(cell);
        }

        public bool IsOccupied(Vector2Int cell, string ignoredUnitInstanceId = null)
        {
            if (!cellToUnitInstanceId.TryGetValue(cell, out string unitInstanceId))
            {
                return false;
            }

            return string.IsNullOrEmpty(ignoredUnitInstanceId) || unitInstanceId != ignoredUnitInstanceId;
        }

        public bool TryGetOccupant(Vector2Int cell, out string unitInstanceId)
        {
            return cellToUnitInstanceId.TryGetValue(cell, out unitInstanceId);
        }

        public bool TryUnlockCell(Vector2Int cell)
        {
            if (GetCellType(cell) != BoardCellType.BuildableLocked)
            {
                return false;
            }

            return unlockedCells.Add(cell);
        }

        public void RegisterOccupancy(string unitInstanceId, IReadOnlyList<Vector2Int> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                cellToUnitInstanceId[cells[i]] = unitInstanceId;
            }
        }

        public void ClearOccupancy(string unitInstanceId)
        {
            List<Vector2Int> toRemove = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, string> pair in cellToUnitInstanceId)
            {
                if (pair.Value == unitInstanceId)
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                cellToUnitInstanceId.Remove(toRemove[i]);
            }
        }

        public void ClearInfluenceCache()
        {
            pathCellToInfluencingUnits.Clear();
            unitToInfluencedPathCells.Clear();
        }

        public void AddInfluence(Vector2Int pathCell, InfluenceLink link)
        {
            if (!pathCellToInfluencingUnits.TryGetValue(pathCell, out List<InfluenceLink> links))
            {
                links = new List<InfluenceLink>();
                pathCellToInfluencingUnits[pathCell] = links;
            }

            links.Add(link);

            if (!unitToInfluencedPathCells.TryGetValue(link.UnitInstanceId, out List<Vector2Int> cells))
            {
                cells = new List<Vector2Int>();
                unitToInfluencedPathCells[link.UnitInstanceId] = cells;
            }

            if (!cells.Contains(pathCell))
            {
                cells.Add(pathCell);
            }
        }

        public IReadOnlyList<InfluenceLink> GetInfluenceLinks(Vector2Int pathCell)
        {
            return pathCellToInfluencingUnits.TryGetValue(pathCell, out List<InfluenceLink> links)
                ? links
                : System.Array.Empty<InfluenceLink>();
        }

        public Vector3 CellToWorldSurface(Vector2Int cell)
        {
            return new Vector3(cell.x * TileSizeXZ, TileSurfaceY, cell.y * TileSizeXZ);
        }

        public bool TryGetPathIndex(Vector2Int cell, out int pathIndex)
        {
            return pathIndexByCell.TryGetValue(cell, out pathIndex);
        }

        private void BuildPathLookup()
        {
            pathCells.Clear();
            pathIndexByCell.Clear();

            IReadOnlyList<GridCell> sourcePath = Definition.PathCells;
            for (int i = 0; i < sourcePath.Count; i++)
            {
                Vector2Int cell = sourcePath[i].ToVector2Int();
                if (!IsInBounds(cell))
                {
                    continue;
                }

                pathCells.Add(cell);
                pathIndexByCell[cell] = pathCells.Count - 1;
            }
        }
    }
}
