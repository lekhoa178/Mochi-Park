using System;
using System.Collections.Generic;
using MochiPark.Runtime.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace MochiPark.Runtime.Data
{
    [Flags]
    public enum BoardPathEdgeMask
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }

    public struct BoardTilePrefabSelection
    {
        public BoardTilePrefabSelection(GameObject prefab, Quaternion rotation)
        {
            Prefab = prefab;
            Rotation = rotation;
        }

        public GameObject Prefab { get; }
        public Quaternion Rotation { get; }
    }

    public static class BoardPrefabBoundsUtility
    {
        public static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(collider.bounds);
            }

            return hasBounds;
        }

        public static float GetHeight(GameObject root)
        {
            return TryGetBounds(root, out Bounds bounds)
                ? Mathf.Max(0f, bounds.size.y)
                : 0f;
        }

    }

    [Serializable]
    public sealed class BoardTilePrefabSet
    {
        [SerializeField] private GameObject pathPrefab;
        [Tooltip("When disabled, every path cell uses Path Prefab. When enabled, exposed path edges select one of the variant prefabs below.")]
        [SerializeField] private bool useMultiplePathPrefabs;
        [Tooltip("Used when one side of the path cell has an exposed edge. Author the base prefab with its edge facing north (+Z).")]
        [SerializeField] private GameObject pathOneSideEdgePrefab;
        [Tooltip("Used when two adjacent sides of the path cell have exposed edges. Author the base prefab with north (+Z) and east (+X) edges.")]
        [SerializeField] private GameObject pathTwoSideEdgePrefab;
        [Tooltip("Used when two opposite sides of the path cell have exposed edges. Author the base prefab with north (+Z) and south (-Z) edges.")]
        [SerializeField] private GameObject pathTwoSideParallelEdgePrefab;
        [Tooltip("Used when three sides of the path cell have exposed edges. Author the base prefab with north (+Z), east (+X), and west (-X) edges.")]
        [SerializeField] private GameObject pathThreeSideEdgePrefab;
        [SerializeField] private GameObject startPrefab;
        [SerializeField] private GameObject exitPrefab;
        [FormerlySerializedAs("buildableOpenPrefab")]
        [SerializeField] private GameObject buildablePrefab;
        [FormerlySerializedAs("buildableOpenSprite")]
        [Tooltip("Sprite rendered as a highlight above buildable cells.")]
        [SerializeField] private Sprite highlightSprite;
        [FormerlySerializedAs("buildableOpenSpriteColor")]
        [Tooltip("Tint applied to the highlight when a Mochi Unit is selected.")]
        [SerializeField] private Color highlightSpriteColor = Color.white;
        [Tooltip("Tint applied to the highlight when the Shovel is selected.")]
        [SerializeField] private Color shovelHighlightSpriteColor = new Color(0.45f, 0.8f, 1f, 1f);
        [Tooltip("Prefab used by locked buildable cells before they are unlocked.")]
        [SerializeField] private GameObject buildableLockedPrefab;
        [Header("Selection Preview")]
        [Tooltip("Sprite rendered over cells covered by the selected unit footprint. Falls back to Highlight Sprite when empty.")]
        [SerializeField] private Sprite footprintPreviewSprite;
        [Tooltip("Sprite rendered over cells affected by the selected unit influence patterns. Falls back to Highlight Sprite when empty.")]
        [SerializeField] private Sprite affectedPreviewSprite;
        [Tooltip("Tint applied to selected unit footprint preview cells.")]
        [SerializeField] private Color footprintPreviewColor = new Color(0.35f, 0.82f, 1f, 1f);
        [Tooltip("Tint applied to affected preview cells that overlap the visitor movement path.")]
        [SerializeField] private Color affectedPathPreviewColor = new Color(1f, 0.86f, 0.25f, 1f);
        [Tooltip("Tint applied to affected preview cells that overlap another buildable tile.")]
        [SerializeField] private Color affectedBuildablePreviewColor = new Color(0.86f, 0.5f, 1f, 1f);
        [Tooltip("Tint applied to every selected-unit preview sprite when the current placement is invalid.")]
        [SerializeField] private Color invalidPreviewColor = new Color(1f, 0.18f, 0.18f, 1f);

        public GameObject GetPrefab(BoardCellType cellType)
        {
            return GetPrefabSelection(cellType, BoardPathEdgeMask.None).Prefab;
        }

        public BoardTilePrefabSelection GetPrefabSelection(BoardCellType cellType, BoardPathEdgeMask pathEdges)
        {
            switch (cellType)
            {
                case BoardCellType.Start:
                    return startPrefab != null
                        ? new BoardTilePrefabSelection(startPrefab, Quaternion.identity)
                        : GetPathPrefabSelection(pathEdges);
                case BoardCellType.Exit:
                    return exitPrefab != null
                        ? new BoardTilePrefabSelection(exitPrefab, Quaternion.identity)
                        : GetPathPrefabSelection(pathEdges);
                case BoardCellType.Path:
                    return GetPathPrefabSelection(pathEdges);
                case BoardCellType.BuildableOpen:
                    return new BoardTilePrefabSelection(buildablePrefab, Quaternion.identity);
                case BoardCellType.BuildableLocked:
                    return new BoardTilePrefabSelection(buildableLockedPrefab, Quaternion.identity);
                default:
                    return new BoardTilePrefabSelection(null, Quaternion.identity);
            }
        }

        public Sprite GetHighlightSprite(BoardCellType cellType)
        {
            return cellType == BoardCellType.BuildableOpen || cellType == BoardCellType.BuildableLocked
                ? highlightSprite
                : null;
        }

        public Color GetHighlightSpriteColor(BoardCellType cellType)
        {
            return cellType == BoardCellType.BuildableOpen || cellType == BoardCellType.BuildableLocked
                ? highlightSpriteColor
                : Color.white;
        }

        public Sprite GetFootprintPreviewSprite()
        {
            return footprintPreviewSprite != null ? footprintPreviewSprite : highlightSprite;
        }

        public Sprite GetAffectedPreviewSprite()
        {
            return affectedPreviewSprite != null ? affectedPreviewSprite : highlightSprite;
        }

        public Color ShovelHighlightSpriteColor => shovelHighlightSpriteColor;
        public Color FootprintPreviewColor => footprintPreviewColor;
        public Color AffectedPathPreviewColor => affectedPathPreviewColor;
        public Color AffectedBuildablePreviewColor => affectedBuildablePreviewColor;
        public Color InvalidPreviewColor => invalidPreviewColor;

        private BoardTilePrefabSelection GetPathPrefabSelection(BoardPathEdgeMask pathEdges)
        {
            if (!useMultiplePathPrefabs)
            {
                return new BoardTilePrefabSelection(pathPrefab, Quaternion.identity);
            }

            int edgeCount = CountEdges(pathEdges);
            switch (edgeCount)
            {
                case 1:
                    return SelectPathVariant(pathOneSideEdgePrefab, GetOneSideRotation(pathEdges));
                case 2:
                    return AreParallelEdges(pathEdges)
                        ? SelectPathVariant(pathTwoSideParallelEdgePrefab, GetParallelRotation(pathEdges))
                        : SelectPathVariant(pathTwoSideEdgePrefab, GetCornerRotation(pathEdges));
                case 3:
                    return SelectPathVariant(pathThreeSideEdgePrefab, GetThreeSideRotation(pathEdges));
                default:
                    return new BoardTilePrefabSelection(pathPrefab, Quaternion.identity);
            }
        }

        private BoardTilePrefabSelection SelectPathVariant(GameObject variantPrefab, Quaternion rotation)
        {
            return variantPrefab != null
                ? new BoardTilePrefabSelection(variantPrefab, rotation)
                : new BoardTilePrefabSelection(pathPrefab, Quaternion.identity);
        }

        private static int CountEdges(BoardPathEdgeMask edges)
        {
            int count = 0;
            if ((edges & BoardPathEdgeMask.North) != 0)
            {
                count++;
            }

            if ((edges & BoardPathEdgeMask.East) != 0)
            {
                count++;
            }

            if ((edges & BoardPathEdgeMask.South) != 0)
            {
                count++;
            }

            if ((edges & BoardPathEdgeMask.West) != 0)
            {
                count++;
            }

            return count;
        }

        private static bool AreParallelEdges(BoardPathEdgeMask edges)
        {
            return edges == (BoardPathEdgeMask.North | BoardPathEdgeMask.South) ||
                edges == (BoardPathEdgeMask.East | BoardPathEdgeMask.West);
        }

        private static Quaternion GetOneSideRotation(BoardPathEdgeMask edges)
        {
            if ((edges & BoardPathEdgeMask.East) != 0)
            {
                return Quaternion.Euler(0f, 90f, 0f);
            }

            if ((edges & BoardPathEdgeMask.South) != 0)
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }

            if ((edges & BoardPathEdgeMask.West) != 0)
            {
                return Quaternion.Euler(0f, 270f, 0f);
            }

            return Quaternion.identity;
        }

        private static Quaternion GetCornerRotation(BoardPathEdgeMask edges)
        {
            if (edges == (BoardPathEdgeMask.East | BoardPathEdgeMask.South))
            {
                return Quaternion.Euler(0f, 90f, 0f);
            }

            if (edges == (BoardPathEdgeMask.South | BoardPathEdgeMask.West))
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }

            if (edges == (BoardPathEdgeMask.West | BoardPathEdgeMask.North))
            {
                return Quaternion.Euler(0f, 270f, 0f);
            }

            return Quaternion.identity;
        }

        private static Quaternion GetParallelRotation(BoardPathEdgeMask edges)
        {
            return edges == (BoardPathEdgeMask.East | BoardPathEdgeMask.West)
                ? Quaternion.Euler(0f, 90f, 0f)
                : Quaternion.identity;
        }

        private static Quaternion GetThreeSideRotation(BoardPathEdgeMask edges)
        {
            if ((edges & BoardPathEdgeMask.West) == 0)
            {
                return Quaternion.Euler(0f, 90f, 0f);
            }

            if ((edges & BoardPathEdgeMask.North) == 0)
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }

            if ((edges & BoardPathEdgeMask.East) == 0)
            {
                return Quaternion.Euler(0f, 270f, 0f);
            }

            return Quaternion.identity;
        }
    }

    public static class BoardGridSymbols
    {
        public const char Start = 'S';
        public const char Exit = 'E';
        public const char Path = '*';
        public const char PathAlias = 'P';
        public const char BuildableOpen = 'O';
        public const char BuildableLocked = 'L';
    }

    [CreateAssetMenu(
        fileName = "board_new",
        menuName = "Mochi Park/Data/Board Definition")]
    public sealed class BoardDefinitionSO : ScriptableObject
    {
        [SerializeField] private string boardId;
        [TextArea(3, 16)]
        [Tooltip("Legend: S=start, E=exit, *=path, O=open buildable, L=locked buildable. First line is the top row. Legacy . reads as open buildable and X reads as locked buildable.")]
        [SerializeField] private string gridText = "OOOOO\nLOOOO\nS***E\nOOOOL\nOOOOO";
        [SerializeField] private BoardTilePrefabSet tilePrefabSet = new BoardTilePrefabSet();
        [SerializeField] private List<string> boardTags = new List<string>();
        [SerializeField] private string themeId = "default";
        [Header("Unit Outline")]
        [ColorUsage(true, true)]
        [SerializeField] private Color selectedUnitOutlineColor = new Color(0.969f, 1f, 0f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] private Color hoveredUnitOutlineColor = Color.white;

        private ParsedBoardGrid cachedGrid;
        private string cachedGridText;

        public string BoardId => boardId;
        public string GridText => gridText;
        public int Width => Grid.Width;
        public int Height => Grid.Height;
        public IReadOnlyList<GridCell> PathCells => Grid.PathCells;
        public GridCell StartCell => Grid.StartCell;
        public GridCell ExitCell => Grid.ExitCell;
        public IReadOnlyList<GridCell> OpenBuildableCells => Grid.OpenBuildableCells;
        public IReadOnlyList<GridCell> LockedBuildableCells => Grid.LockedBuildableCells;
        public BoardTilePrefabSet TilePrefabSet => tilePrefabSet;
        public IReadOnlyList<string> BoardTags => boardTags;
        public string ThemeId => themeId;
        public Color SelectedUnitOutlineColor => selectedUnitOutlineColor;
        public Color HoveredUnitOutlineColor => hoveredUnitOutlineColor;

        public float ResolveTileHeight()
        {
            float height = 0f;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    BoardCellType cellType = GetCellType(cell);
                    BoardTilePrefabSelection selection = tilePrefabSet != null
                        ? tilePrefabSet.GetPrefabSelection(cellType, GetPathEdgeMask(cell))
                        : new BoardTilePrefabSelection(null, Quaternion.identity);
                    height = Mathf.Max(
                        height,
                        BoardPrefabBoundsUtility.GetHeight(selection.Prefab));
                }
            }

            return Mathf.Max(0.01f, height > 0f ? height : 1f);
        }

        private ParsedBoardGrid Grid
        {
            get
            {
                if (cachedGrid == null || cachedGridText != gridText)
                {
                    cachedGridText = gridText;
                    cachedGrid = ParsedBoardGrid.Parse(gridText);
                }

                return cachedGrid;
            }
        }

        public BoardCellType GetCellType(Vector2Int cell)
        {
            return Grid.GetCellType(cell);
        }

        public BoardPathEdgeMask GetPathEdgeMask(Vector2Int cell)
        {
            return Grid.GetPathEdgeMask(cell);
        }

        private void OnValidate()
        {
            cachedGrid = null;
            cachedGridText = null;
        }

        private sealed class ParsedBoardGrid
        {
            private static readonly Vector2Int[] PathNeighborOffsets =
            {
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(-1, 0),
                new Vector2Int(0, -1)
            };

            private readonly BoardCellType[,] cellTypes;
            private readonly List<GridCell> pathCells = new List<GridCell>();
            private readonly List<GridCell> openBuildableCells = new List<GridCell>();
            private readonly List<GridCell> lockedBuildableCells = new List<GridCell>();

            private ParsedBoardGrid(int width, int height)
            {
                Width = Mathf.Max(1, width);
                Height = Mathf.Max(1, height);
                cellTypes = new BoardCellType[Width, Height];
                StartCell = new GridCell(0, 0);
                ExitCell = new GridCell(Width - 1, Height - 1);
            }

            public int Width { get; }
            public int Height { get; }
            public GridCell StartCell { get; private set; }
            public GridCell ExitCell { get; private set; }
            public IReadOnlyList<GridCell> PathCells => pathCells;
            public IReadOnlyList<GridCell> OpenBuildableCells => openBuildableCells;
            public IReadOnlyList<GridCell> LockedBuildableCells => lockedBuildableCells;

            public static ParsedBoardGrid Parse(string source)
            {
                List<string> rows = GetRows(source);
                int width = GetWidth(rows);
                ParsedBoardGrid grid = new ParsedBoardGrid(width, rows.Count);

                bool foundStart = false;
                bool foundExit = false;
                List<GridCell> unorderedPathCells = new List<GridCell>();

                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    string row = rows[rowIndex];
                    int y = rows.Count - 1 - rowIndex;

                    for (int x = 0; x < grid.Width; x++)
                    {
                        char symbol = x < row.Length ? row[x] : BoardGridSymbols.BuildableOpen;
                        GridCell cell = new GridCell(x, y);
                        BoardCellType cellType = SymbolToCellType(symbol);

                        grid.cellTypes[x, y] = cellType;
                        switch (cellType)
                        {
                            case BoardCellType.Start:
                                if (!foundStart)
                                {
                                    grid.StartCell = cell;
                                    foundStart = true;
                                }

                                unorderedPathCells.Add(cell);
                                break;
                            case BoardCellType.Exit:
                                if (!foundExit)
                                {
                                    grid.ExitCell = cell;
                                    foundExit = true;
                                }

                                unorderedPathCells.Add(cell);
                                break;
                            case BoardCellType.Path:
                                unorderedPathCells.Add(cell);
                                break;
                            case BoardCellType.BuildableOpen:
                                grid.openBuildableCells.Add(cell);
                                break;
                            case BoardCellType.BuildableLocked:
                                grid.lockedBuildableCells.Add(cell);
                                break;
                        }
                    }
                }

                if (!foundStart && unorderedPathCells.Count > 0)
                {
                    grid.StartCell = unorderedPathCells[0];
                }

                if (!foundExit && unorderedPathCells.Count > 0)
                {
                    grid.ExitCell = unorderedPathCells[unorderedPathCells.Count - 1];
                }

                grid.BuildOrderedPathCells(unorderedPathCells, foundStart, foundExit);
                return grid;
            }

            public BoardCellType GetCellType(Vector2Int cell)
            {
                return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height
                    ? cellTypes[cell.x, cell.y]
                    : BoardCellType.OutOfBounds;
            }

            public BoardPathEdgeMask GetPathEdgeMask(Vector2Int cell)
            {
                if (!IsPathLike(cell))
                {
                    return BoardPathEdgeMask.None;
                }

                BoardPathEdgeMask edges = BoardPathEdgeMask.None;
                if (!IsPathLike(cell + Vector2Int.up))
                {
                    edges |= BoardPathEdgeMask.North;
                }

                if (!IsPathLike(cell + Vector2Int.right))
                {
                    edges |= BoardPathEdgeMask.East;
                }

                if (!IsPathLike(cell + Vector2Int.down))
                {
                    edges |= BoardPathEdgeMask.South;
                }

                if (!IsPathLike(cell + Vector2Int.left))
                {
                    edges |= BoardPathEdgeMask.West;
                }

                return edges;
            }

            private static List<string> GetRows(string source)
            {
                List<string> rows = new List<string>();
                if (!string.IsNullOrEmpty(source))
                {
                    string[] splitRows = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                    for (int i = 0; i < splitRows.Length; i++)
                    {
                        string row = splitRows[i].TrimEnd();
                        if (!string.IsNullOrWhiteSpace(row))
                        {
                            rows.Add(row);
                        }
                    }
                }

                if (rows.Count == 0)
                {
                    rows.Add(BoardGridSymbols.BuildableOpen.ToString());
                }

                return rows;
            }

            private static int GetWidth(IReadOnlyList<string> rows)
            {
                int width = 1;
                for (int i = 0; i < rows.Count; i++)
                {
                    width = Mathf.Max(width, rows[i].Length);
                }

                return width;
            }

            private static BoardCellType SymbolToCellType(char symbol)
            {
                switch (symbol)
                {
                    case BoardGridSymbols.Start:
                        return BoardCellType.Start;
                    case BoardGridSymbols.Exit:
                        return BoardCellType.Exit;
                    case BoardGridSymbols.Path:
                    case BoardGridSymbols.PathAlias:
                        return BoardCellType.Path;
                    case BoardGridSymbols.BuildableOpen:
                        return BoardCellType.BuildableOpen;
                    case BoardGridSymbols.BuildableLocked:
                        return BoardCellType.BuildableLocked;
                    case 'X':
                        return BoardCellType.BuildableLocked;
                    case '.':
                    default:
                        return BoardCellType.BuildableOpen;
                }
            }

            private void BuildOrderedPathCells(IReadOnlyList<GridCell> unorderedPathCells, bool hasStart, bool hasExit)
            {
                pathCells.Clear();
                if (unorderedPathCells.Count == 0)
                {
                    return;
                }

                if (!hasStart || !hasExit || !TryBuildConnectedPath())
                {
                    for (int i = 0; i < unorderedPathCells.Count; i++)
                    {
                        pathCells.Add(unorderedPathCells[i]);
                    }
                }
            }

            private bool TryBuildConnectedPath()
            {
                Vector2Int start = StartCell.ToVector2Int();
                Vector2Int exit = ExitCell.ToVector2Int();
                Queue<Vector2Int> frontier = new Queue<Vector2Int>();
                Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

                frontier.Enqueue(start);
                cameFrom[start] = start;

                while (frontier.Count > 0)
                {
                    Vector2Int current = frontier.Dequeue();
                    if (current == exit)
                    {
                        break;
                    }

                    for (int i = 0; i < PathNeighborOffsets.Length; i++)
                    {
                        Vector2Int next = current + PathNeighborOffsets[i];
                        if (cameFrom.ContainsKey(next) || !IsPathLike(next))
                        {
                            continue;
                        }

                        frontier.Enqueue(next);
                        cameFrom[next] = current;
                    }
                }

                if (!cameFrom.ContainsKey(exit))
                {
                    return false;
                }

                List<Vector2Int> reversed = new List<Vector2Int>();
                Vector2Int step = exit;
                while (step != start)
                {
                    reversed.Add(step);
                    step = cameFrom[step];
                }

                reversed.Add(start);

                for (int i = reversed.Count - 1; i >= 0; i--)
                {
                    pathCells.Add(GridCell.FromVector2Int(reversed[i]));
                }

                return true;
            }

            private bool IsPathLike(Vector2Int cell)
            {
                BoardCellType type = GetCellType(cell);
                return type == BoardCellType.Path || type == BoardCellType.Start || type == BoardCellType.Exit;
            }
        }
    }
}
