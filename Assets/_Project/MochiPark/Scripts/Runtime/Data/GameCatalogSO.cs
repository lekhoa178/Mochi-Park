using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MochiPark.Runtime.Data
{
    [CreateAssetMenu(
        fileName = "game_catalog",
        menuName = "Mochi Park/Data/Game Catalog")]
    public sealed class GameCatalogSO : ScriptableObject
    {
        [SerializeField] private GlobalGameConfigSO globalConfig;
        [SerializeField] private List<BoardDefinitionSO> boards = new List<BoardDefinitionSO>();
        [SerializeField] private List<LevelDefinitionSO> levels = new List<LevelDefinitionSO>();
        [SerializeField] private List<UnitDefinitionSO> units = new List<UnitDefinitionSO>();
        [SerializeField] private List<PatternDefinitionSO> patterns = new List<PatternDefinitionSO>();
        [SerializeField] private string contentVersion = "0.1.0";

        public GlobalGameConfigSO GlobalConfig => globalConfig;
        public IReadOnlyList<BoardDefinitionSO> Boards => boards;
        public IReadOnlyList<LevelDefinitionSO> Levels => levels;
        public IReadOnlyList<UnitDefinitionSO> Units => units;
        public IReadOnlyList<PatternDefinitionSO> Patterns => patterns;
        public string ContentVersion => contentVersion;

        public bool TryGetLevel(string levelId, out LevelDefinitionSO level)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null && levels[i].LevelId == levelId)
                {
                    level = levels[i];
                    return true;
                }
            }

            level = null;
            return false;
        }

        public bool TryGetUnit(string unitTypeId, out UnitDefinitionSO unit)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].UnitTypeId == unitTypeId)
                {
                    unit = units[i];
                    return true;
                }
            }

            unit = null;
            return false;
        }

        public bool TryGetPattern(string patternId, out PatternDefinitionSO pattern)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                if (patterns[i] != null && patterns[i].PatternId == patternId)
                {
                    pattern = patterns[i];
                    return true;
                }
            }

            pattern = null;
            return false;
        }

        public string BuildValidationReport()
        {
            StringBuilder builder = new StringBuilder();

            if (globalConfig == null)
            {
                builder.AppendLine("Missing global config.");
            }

            AppendDuplicateReport(builder, "board", boards, item => item == null ? string.Empty : item.BoardId);
            AppendDuplicateReport(builder, "level", levels, item => item == null ? string.Empty : item.LevelId);
            AppendDuplicateReport(builder, "unit", units, item => item == null ? string.Empty : item.UnitTypeId);
            AppendDuplicateReport(builder, "pattern", patterns, item => item == null ? string.Empty : item.PatternId);

            return builder.ToString();
        }

        [ContextMenu("Validate Catalog")]
        private void ValidateCatalog()
        {
            string report = BuildValidationReport();
            if (string.IsNullOrWhiteSpace(report))
            {
                Debug.Log($"{name} validation passed.", this);
                return;
            }

            Debug.LogWarning(report, this);
        }

        private static void AppendDuplicateReport<T>(
            StringBuilder builder,
            string label,
            IReadOnlyList<T> items,
            System.Func<T, string> idGetter)
            where T : Object
        {
            HashSet<string> ids = new HashSet<string>();

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item == null)
                {
                    builder.AppendLine($"Null {label} reference at index {i}.");
                    continue;
                }

                string id = idGetter(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    builder.AppendLine($"Empty {label} id at index {i}.");
                    continue;
                }

                if (!ids.Add(id))
                {
                    builder.AppendLine($"Duplicate {label} id: {id}.");
                }
            }
        }
    }
}
