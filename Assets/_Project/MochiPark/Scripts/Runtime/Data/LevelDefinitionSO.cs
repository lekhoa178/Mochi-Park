using System;
using System.Collections.Generic;
using MochiPark.Runtime.Core;
using UnityEngine;

namespace MochiPark.Runtime.Data
{
    [Serializable]
    public sealed class StarThresholds
    {
        [SerializeField] private int oneStar = 100;
        [SerializeField] private int twoStar = 200;
        [SerializeField] private int threeStar = 300;

        public int OneStar => Mathf.Max(0, oneStar);
        public int TwoStar => Mathf.Max(OneStar, twoStar);
        public int ThreeStar => Mathf.Max(TwoStar, threeStar);

        public int CalculateStars(int totalJoy)
        {
            if (totalJoy >= ThreeStar)
            {
                return 3;
            }

            if (totalJoy >= TwoStar)
            {
                return 2;
            }

            return totalJoy >= OneStar ? 1 : 0;
        }
    }

    [Serializable]
    public sealed class InitialUnitSpec
    {
        [SerializeField] private UnitDefinitionSO unitDefinition;
        [SerializeField] private GridCell anchorCell;
        [SerializeField] private GridRotation rotation;
        [SerializeField] private bool isLockedByTutorial;

        public UnitDefinitionSO UnitDefinition => unitDefinition;
        public GridCell AnchorCell => anchorCell;
        public GridRotation Rotation => rotation;
        public bool IsLockedByTutorial => isLockedByTutorial;
    }

    [Serializable]
    public sealed class UnitPoolRules
    {
        [SerializeField] private List<UnitFamily> availableFamilies = new List<UnitFamily>();
        [SerializeField] private List<UnitDefinitionSO> unitWhitelist = new List<UnitDefinitionSO>();
        [SerializeField] private List<UnitDefinitionSO> unitBlacklist = new List<UnitDefinitionSO>();
        [SerializeField] private List<UnitTag> requiredTags = new List<UnitTag>();
        [SerializeField] private List<UnitTag> guaranteedTags = new List<UnitTag>();
        [SerializeField] private bool noDuplicateInDraft = true;
        [SerializeField] private int maxGoldCardsPerDraft;
        [SerializeField] private bool guaranteedOneBasicScorer = true;

        public IReadOnlyList<UnitFamily> AvailableFamilies => availableFamilies;
        public IReadOnlyList<UnitDefinitionSO> UnitWhitelist => unitWhitelist;
        public IReadOnlyList<UnitDefinitionSO> UnitBlacklist => unitBlacklist;
        public IReadOnlyList<UnitTag> RequiredTags => requiredTags;
        public IReadOnlyList<UnitTag> GuaranteedTags => guaranteedTags;
        public bool NoDuplicateInDraft => noDuplicateInDraft;
        public int MaxGoldCardsPerDraft => Mathf.Max(0, maxGoldCardsPerDraft);
        public bool GuaranteedOneBasicScorer => guaranteedOneBasicScorer;
    }

    [Serializable]
    public sealed class GameSpeedConfigOverride
    {
        [SerializeField] private OptionalBool speedControlEnabled;
        [SerializeField] private OptionalFloatList allowedMultipliers = new OptionalFloatList();
        [SerializeField] private OptionalFloat defaultMultiplierOnDayStart;

        public OptionalBool SpeedControlEnabled => speedControlEnabled;
        public OptionalFloatList AllowedMultipliers => allowedMultipliers;
        public OptionalFloat DefaultMultiplierOnDayStart => defaultMultiplierOnDayStart;
    }

    [Serializable]
    public sealed class DayConfig
    {
        [SerializeField] private int dayIndexInLevel;
        [SerializeField] private OptionalInt baseVisitorCountOverride;
        [SerializeField] private OptionalFloat spawnDelayOverride;
        [SerializeField] private OptionalInt upgradeRollCountOverride;
        [SerializeField] private OptionalInt rerollCountOverride;
        [SerializeField] private OptionalInt draftCardCountOverride;
        [SerializeField] private UnitPoolRules unitPoolRulesOverride = new UnitPoolRules();
        [SerializeField] private OptionalFloat visitorSpeedOverride;
        [SerializeField] private OptionalBool speedControlEnabledOverride;
        [SerializeField] private OptionalFloatList allowedGameSpeedMultipliersOverride = new OptionalFloatList();
        [SerializeField] private OptionalFloat defaultGameSpeedOnDayStartOverride;
        [SerializeField] private OptionalBool allowPlacementBeforeRun;
        [SerializeField] private OptionalBool allowPlacementDuringRun;
        [SerializeField] private string specialDayEvents;
        [SerializeField] private string savePolicyOverride;

        public int DayIndexInLevel => Mathf.Max(0, dayIndexInLevel);
        public OptionalInt BaseVisitorCountOverride => baseVisitorCountOverride;
        public OptionalFloat SpawnDelayOverride => spawnDelayOverride;
        public OptionalInt UpgradeRollCountOverride => upgradeRollCountOverride;
        public OptionalInt RerollCountOverride => rerollCountOverride;
        public OptionalInt DraftCardCountOverride => draftCardCountOverride;
        public UnitPoolRules UnitPoolRulesOverride => unitPoolRulesOverride;
        public OptionalFloat VisitorSpeedOverride => visitorSpeedOverride;
        public OptionalBool SpeedControlEnabledOverride => speedControlEnabledOverride;
        public OptionalFloatList AllowedGameSpeedMultipliersOverride => allowedGameSpeedMultipliersOverride;
        public OptionalFloat DefaultGameSpeedOnDayStartOverride => defaultGameSpeedOnDayStartOverride;
        public OptionalBool AllowPlacementBeforeRun => allowPlacementBeforeRun;
        public OptionalBool AllowPlacementDuringRun => allowPlacementDuringRun;
        public string SpecialDayEvents => specialDayEvents;
        public string SavePolicyOverride => savePolicyOverride;
    }

    [Serializable]
    public sealed class StageConfig
    {
        [SerializeField] private int stageIndex;
        [SerializeField] private int dayCount = 1;
        [SerializeField] private int unlockCharges;
        [SerializeField] private UnitPoolRules unitPoolRulesOverride = new UnitPoolRules();
        [SerializeField] private OptionalInt baseVisitorCountOverride;
        [SerializeField] private OptionalFloat visitorSpeedOverride;
        [SerializeField] private List<UnitFamily> newUnlockedFamilies = new List<UnitFamily>();
        [SerializeField] private List<DayConfig> days = new List<DayConfig>();

        public int StageIndex => Mathf.Max(0, stageIndex);
        public int DayCount => Mathf.Max(0, dayCount);
        public int UnlockCharges => Mathf.Max(0, unlockCharges);
        public UnitPoolRules UnitPoolRulesOverride => unitPoolRulesOverride;
        public OptionalInt BaseVisitorCountOverride => baseVisitorCountOverride;
        public OptionalFloat VisitorSpeedOverride => visitorSpeedOverride;
        public IReadOnlyList<UnitFamily> NewUnlockedFamilies => newUnlockedFamilies;
        public IReadOnlyList<DayConfig> Days => days;
    }

    [CreateAssetMenu(
        fileName = "level_new",
        menuName = "Mochi Park/Data/Level Definition")]
    public sealed class LevelDefinitionSO : ScriptableObject
    {
        [SerializeField] private string levelId;
        [SerializeField] private string displayName;
        [SerializeField] private BoardDefinitionSO board;
        [SerializeField] private StarThresholds starThresholds = new StarThresholds();
        [SerializeField] private List<InitialUnitSpec> initialUnits = new List<InitialUnitSpec>();
        [SerializeField] private List<StageConfig> stages = new List<StageConfig>();
        [SerializeField] private UnitPoolRules defaultUnitPoolRules = new UnitPoolRules();
        [SerializeField] private int defaultVisitorCount = 2;
        [SerializeField] private GameSpeedConfigOverride gameSpeedConfigOverride = new GameSpeedConfigOverride();
        [SerializeField] private string tutorialConfigId;
        [TextArea]
        [SerializeField] private string designNotes;

        public string LevelId => levelId;
        public string DisplayName => displayName;
        public BoardDefinitionSO Board => board;
        public StarThresholds StarThresholds => starThresholds;
        public IReadOnlyList<InitialUnitSpec> InitialUnits => initialUnits;
        public IReadOnlyList<StageConfig> Stages => stages;
        public int TotalDayCount
        {
            get
            {
                if (stages == null)
                {
                    return 1;
                }

                int totalByStageCounts = 0;
                int highestConfiguredDayNumber = 0;

                for (int i = 0; i < stages.Count; i++)
                {
                    StageConfig stage = stages[i];
                    if (stage == null)
                    {
                        continue;
                    }

                    IReadOnlyList<DayConfig> configuredDays = stage.Days;
                    int configuredDayCount = configuredDays != null ? configuredDays.Count : 0;
                    totalByStageCounts += Mathf.Max(stage.DayCount, configuredDayCount);
                    for (int dayIndex = 0; dayIndex < configuredDayCount; dayIndex++)
                    {
                        DayConfig day = configuredDays[dayIndex];
                        if (day != null)
                        {
                            highestConfiguredDayNumber = Mathf.Max(
                                highestConfiguredDayNumber,
                                day.DayIndexInLevel + 1);
                        }
                    }
                }

                return Mathf.Max(1, Mathf.Max(totalByStageCounts, highestConfiguredDayNumber));
            }
        }
        public UnitPoolRules DefaultUnitPoolRules => defaultUnitPoolRules;
        public int DefaultVisitorCount => Mathf.Max(0, defaultVisitorCount);
        public GameSpeedConfigOverride GameSpeedConfigOverride => gameSpeedConfigOverride;
        public string TutorialConfigId => tutorialConfigId;
        public string DesignNotes => designNotes;

        public bool TryGetStageIndexForDay(int dayIndexInLevel, out int stageIndex)
        {
            stageIndex = 0;
            if (dayIndexInLevel < 0)
            {
                return false;
            }

            if (stages == null)
            {
                return false;
            }

            for (int i = 0; i < stages.Count; i++)
            {
                StageConfig stage = stages[i];
                if (stage == null)
                {
                    continue;
                }

                IReadOnlyList<DayConfig> configuredDays = stage.Days;
                int configuredDayCount = configuredDays != null ? configuredDays.Count : 0;
                for (int dayIndex = 0; dayIndex < configuredDayCount; dayIndex++)
                {
                    DayConfig day = configuredDays[dayIndex];
                    if (day != null && day.DayIndexInLevel == dayIndexInLevel)
                    {
                        stageIndex = stage.StageIndex;
                        return true;
                    }
                }
            }

            int firstDayIndex = 0;
            for (int i = 0; i < stages.Count; i++)
            {
                StageConfig stage = stages[i];
                if (stage == null)
                {
                    continue;
                }

                int configuredDayCount = stage.Days != null ? stage.Days.Count : 0;
                int stageDayCount = Mathf.Max(stage.DayCount, configuredDayCount);
                if (dayIndexInLevel < firstDayIndex + stageDayCount)
                {
                    stageIndex = stage.StageIndex;
                    return true;
                }

                firstDayIndex += stageDayCount;
            }

            return false;
        }
    }
}
