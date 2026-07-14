using System.Collections.Generic;
using MochiPark.Runtime.Data;

namespace MochiPark.Runtime.Level
{
    public sealed class ResolvedDayConfig
    {
        public ResolvedDayConfig(
            string levelId,
            int stageIndex,
            int dayIndexInLevel,
            int visitorCount,
            float spawnDelay,
            float visitorSpeed,
            bool speedControlEnabled,
            List<float> allowedGameSpeedMultipliers,
            float defaultGameSpeedOnDayStart,
            int upgradeRollCount,
            int draftCardCount,
            int unlockCharges,
            UnitPoolRules unitPoolRules,
            bool allowPlacementBeforeRun,
            bool allowPlacementDuringRun,
            FrenzyConfigSpec frenzyConfig,
            string specialDayEvents,
            string savePolicy)
        {
            LevelId = levelId;
            StageIndex = stageIndex;
            DayIndexInLevel = dayIndexInLevel;
            VisitorCount = visitorCount;
            SpawnDelay = spawnDelay;
            VisitorSpeed = visitorSpeed;
            SpeedControlEnabled = speedControlEnabled;
            AllowedGameSpeedMultipliers = allowedGameSpeedMultipliers ?? new List<float> { 1f };
            DefaultGameSpeedOnDayStart = defaultGameSpeedOnDayStart;
            UpgradeRollCount = upgradeRollCount;
            DraftCardCount = draftCardCount;
            UnlockCharges = unlockCharges;
            UnitPoolRules = unitPoolRules;
            AllowPlacementBeforeRun = allowPlacementBeforeRun;
            AllowPlacementDuringRun = allowPlacementDuringRun;
            FrenzyConfig = frenzyConfig;
            SpecialDayEvents = specialDayEvents;
            SavePolicy = savePolicy;
        }

        public string LevelId { get; }
        public int StageIndex { get; }
        public int DayIndexInLevel { get; }
        public int VisitorCount { get; }
        public float SpawnDelay { get; }
        public float VisitorSpeed { get; }
        public bool SpeedControlEnabled { get; }
        public List<float> AllowedGameSpeedMultipliers { get; }
        public float DefaultGameSpeedOnDayStart { get; }
        public int UpgradeRollCount { get; }
        public int DraftCardCount { get; }
        public int UnlockCharges { get; }
        public UnitPoolRules UnitPoolRules { get; }
        public bool AllowPlacementBeforeRun { get; }
        public bool AllowPlacementDuringRun { get; }
        public FrenzyConfigSpec FrenzyConfig { get; }
        public string SpecialDayEvents { get; }
        public string SavePolicy { get; }
    }
}
