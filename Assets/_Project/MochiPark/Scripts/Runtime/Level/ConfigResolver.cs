using System.Collections.Generic;
using MochiPark.Runtime.Data;
using UnityEngine;

namespace MochiPark.Runtime.Level
{
    public static class ConfigResolver
    {
        public static ResolvedDayConfig Resolve(
            GameCatalogSO catalog,
            LevelDefinitionSO level,
            int stageIndex,
            int dayIndex)
        {
            GlobalGameConfigSO global = catalog.GlobalConfig;
            StageConfig stage = FindStage(level, stageIndex);
            DayConfig day = FindDay(stage, dayIndex);

            int visitorCount = Mathf.Max(0, global.DefaultVisitorCount);
            visitorCount = Mathf.Max(0, level.DefaultVisitorCount);
            if (stage != null)
            {
                visitorCount = stage.BaseVisitorCountOverride.Resolve(visitorCount);
            }

            if (day != null)
            {
                visitorCount = day.BaseVisitorCountOverride.Resolve(visitorCount);
            }

            float spawnDelay = global.DefaultSpawnDelay;
            float visitorSpeed = global.DefaultVisitorSpeed;
            int upgradeRollCount = global.DefaultUpgradeRollCount;
            int draftCardCount = global.DefaultDraftCardCount;
            int unlockCharges = 0;
            bool allowPlacementBeforeRun = global.DefaultAllowPlacementBeforeRun;
            bool allowPlacementDuringRun = global.DefaultAllowPlacementDuringRun;
            bool speedControlEnabled = global.DefaultSpeedControlEnabled;
            List<float> allowedMultipliers = new List<float>(global.DefaultAllowedGameSpeeds);
            float defaultSpeed = global.DefaultGameSpeedOnDayStart;
            UnitPoolRules poolRules = level.DefaultUnitPoolRules;
            FrenzyConfigSpec frenzyConfig = global.DefaultFrenzyConfig;

            if (level.GameSpeedConfigOverride != null)
            {
                speedControlEnabled = level.GameSpeedConfigOverride.SpeedControlEnabled.Resolve(speedControlEnabled);
                allowedMultipliers = level.GameSpeedConfigOverride.AllowedMultipliers.Resolve(allowedMultipliers);
                defaultSpeed = level.GameSpeedConfigOverride.DefaultMultiplierOnDayStart.Resolve(defaultSpeed);
            }

            if (stage != null)
            {
                visitorSpeed = stage.VisitorSpeedOverride.Resolve(visitorSpeed);
                unlockCharges = stage.UnlockCharges;
                if (HasPoolRules(stage.UnitPoolRulesOverride))
                {
                    poolRules = stage.UnitPoolRulesOverride;
                }
            }

            string specialDayEvents = string.Empty;
            string savePolicy = string.Empty;

            if (day != null)
            {
                spawnDelay = day.SpawnDelayOverride.Resolve(spawnDelay);
                visitorSpeed = day.VisitorSpeedOverride.Resolve(visitorSpeed);
                upgradeRollCount = day.UpgradeRollCountOverride.Resolve(upgradeRollCount);
                draftCardCount = day.DraftCardCountOverride.Resolve(draftCardCount);
                speedControlEnabled = day.SpeedControlEnabledOverride.Resolve(speedControlEnabled);
                allowedMultipliers = day.AllowedGameSpeedMultipliersOverride.Resolve(allowedMultipliers);
                defaultSpeed = day.DefaultGameSpeedOnDayStartOverride.Resolve(defaultSpeed);
                allowPlacementBeforeRun = day.AllowPlacementBeforeRun.Resolve(allowPlacementBeforeRun);
                allowPlacementDuringRun = day.AllowPlacementDuringRun.Resolve(allowPlacementDuringRun);
                specialDayEvents = day.SpecialDayEvents;
                savePolicy = day.SavePolicyOverride;

                if (HasPoolRules(day.UnitPoolRulesOverride))
                {
                    poolRules = day.UnitPoolRulesOverride;
                }
            }

            NormalizeMultipliers(allowedMultipliers);
            defaultSpeed = ClampMultiplier(defaultSpeed, allowedMultipliers);

            return new ResolvedDayConfig(
                level.LevelId,
                stage == null ? stageIndex : stage.StageIndex,
                day == null ? dayIndex : day.DayIndexInLevel,
                visitorCount,
                Mathf.Max(0.01f, spawnDelay),
                Mathf.Max(0.01f, visitorSpeed),
                speedControlEnabled,
                allowedMultipliers,
                defaultSpeed,
                Mathf.Max(0, upgradeRollCount),
                Mathf.Max(0, draftCardCount),
                Mathf.Max(0, unlockCharges),
                poolRules,
                allowPlacementBeforeRun,
                allowPlacementDuringRun,
                frenzyConfig,
                specialDayEvents,
                savePolicy);
        }

        public static float ClampMultiplier(float requestedMultiplier, IReadOnlyList<float> allowedMultipliers)
        {
            if (allowedMultipliers == null || allowedMultipliers.Count == 0)
            {
                return 1f;
            }

            float best = allowedMultipliers[0];
            float bestDistance = Mathf.Abs(requestedMultiplier - best);
            for (int i = 1; i < allowedMultipliers.Count; i++)
            {
                float distance = Mathf.Abs(requestedMultiplier - allowedMultipliers[i]);
                if (distance < bestDistance)
                {
                    best = allowedMultipliers[i];
                    bestDistance = distance;
                }
            }

            return Mathf.Max(1f, best);
        }

        private static StageConfig FindStage(LevelDefinitionSO level, int stageIndex)
        {
            IReadOnlyList<StageConfig> stages = level.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] != null && stages[i].StageIndex == stageIndex)
                {
                    return stages[i];
                }
            }

            return stages.Count > 0 ? stages[0] : null;
        }

        private static DayConfig FindDay(StageConfig stage, int dayIndex)
        {
            if (stage == null)
            {
                return null;
            }

            IReadOnlyList<DayConfig> days = stage.Days;
            for (int i = 0; i < days.Count; i++)
            {
                if (days[i] != null && days[i].DayIndexInLevel == dayIndex)
                {
                    return days[i];
                }
            }

            return days.Count > 0 ? days[0] : null;
        }

        private static bool HasPoolRules(UnitPoolRules rules)
        {
            if (rules == null)
            {
                return false;
            }

            return rules.AvailableFamilies.Count > 0
                || rules.UnitWhitelist.Count > 0
                || rules.UnitBlacklist.Count > 0
                || rules.RequiredTags.Count > 0
                || rules.GuaranteedTags.Count > 0;
        }

        private static void NormalizeMultipliers(List<float> multipliers)
        {
            if (multipliers == null)
            {
                return;
            }

            for (int i = multipliers.Count - 1; i >= 0; i--)
            {
                if (multipliers[i] < 1f)
                {
                    multipliers.RemoveAt(i);
                }
            }

            if (!multipliers.Contains(1f))
            {
                multipliers.Insert(0, 1f);
            }
        }
    }
}
