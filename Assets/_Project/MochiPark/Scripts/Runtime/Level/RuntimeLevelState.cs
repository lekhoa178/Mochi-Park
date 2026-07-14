using System;
using System.Collections.Generic;
using MochiPark.Runtime.Board;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Units;
using MochiPark.Runtime.Visitors;

namespace MochiPark.Runtime.Level
{
    public sealed class GameSpeedRuntimeState
    {
        public float RequestedMultiplier { get; private set; } = 1f;
        public float EffectiveMultiplier { get; private set; } = 1f;

        public void SetRequested(float multiplier)
        {
            RequestedMultiplier = multiplier;
        }

        public void SetEffective(float multiplier)
        {
            EffectiveMultiplier = multiplier;
        }
    }

    public sealed class FrenzyRuntimeState
    {
        public bool IsActive { get; private set; }
        public float JoyMultiplier { get; private set; } = 2f;
        public float TimeRemaining { get; private set; }
        public int FrenzyCountToday { get; private set; }
        public int JoyCreatedDuringFrenzy { get; private set; }

        public void PrepareDay(FrenzyConfigSpec config)
        {
            IsActive = false;
            TimeRemaining = 0f;
            FrenzyCountToday = 0;
            JoyCreatedDuringFrenzy = 0;
            JoyMultiplier = config == null ? 2f : config.JoyMultiplier;
        }

        public bool CanStart(FrenzyConfigSpec config)
        {
            if (config == null || !config.Enabled || IsActive)
            {
                return false;
            }

            return config.MaxFrenzyPerDay <= 0 || FrenzyCountToday < config.MaxFrenzyPerDay;
        }

        public void Start(FrenzyConfigSpec config)
        {
            IsActive = true;
            TimeRemaining = config == null ? 0f : config.DurationSeconds;
            JoyMultiplier = config == null ? 2f : config.JoyMultiplier;
            FrenzyCountToday++;
            JoyCreatedDuringFrenzy = 0;
        }

        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            TimeRemaining -= Math.Max(0f, deltaTime);
            if (TimeRemaining > 0f)
            {
                return false;
            }

            TimeRemaining = 0f;
            IsActive = false;
            return true;
        }

        public void RecordJoy(int amount)
        {
            if (IsActive)
            {
                JoyCreatedDuringFrenzy += Math.Max(0, amount);
            }
        }
    }

    public sealed class UnitTypeScoreSummary
    {
        public UnitTypeScoreSummary(string unitTypeId)
        {
            UnitTypeId = unitTypeId;
        }

        public string UnitTypeId { get; }
        public int JoyDirect { get; private set; }
        public int JoyFromBuff { get; private set; }
        public int JoyFromFrenzy { get; private set; }
        public int JoyFromStorage { get; private set; }
        public int TriggerCount { get; private set; }
        public int FinalJoy => JoyDirect + JoyFromBuff + JoyFromFrenzy + JoyFromStorage;

        public void RecordJoy(SourceCategory sourceCategory, int amount)
        {
            int safeAmount = Math.Max(0, amount);
            switch (sourceCategory)
            {
                case SourceCategory.Buff:
                    JoyFromBuff += safeAmount;
                    break;
                case SourceCategory.Frenzy:
                    JoyFromFrenzy += safeAmount;
                    break;
                case SourceCategory.Storage:
                    JoyFromStorage += safeAmount;
                    break;
                default:
                    JoyDirect += safeAmount;
                    break;
            }
        }

        public void RecordTrigger()
        {
            TriggerCount++;
        }
    }

    public sealed class ScoringRuntimeState
    {
        private readonly Dictionary<string, int> joyByUnitType = new Dictionary<string, int>();
        private readonly Dictionary<string, int> joyByUnitInstance = new Dictionary<string, int>();
        private readonly Dictionary<SourceCategory, int> joyBySourceCategory = new Dictionary<SourceCategory, int>();
        private readonly Dictionary<string, UnitTypeScoreSummary> unitTypeSummaries =
            new Dictionary<string, UnitTypeScoreSummary>();

        public int TotalJoyLevel { get; private set; }
        public int JoyToday { get; private set; }
        public IReadOnlyDictionary<string, int> JoyByUnitType => joyByUnitType;
        public IReadOnlyDictionary<string, int> JoyByUnitInstance => joyByUnitInstance;
        public IReadOnlyDictionary<SourceCategory, int> JoyBySourceCategory => joyBySourceCategory;
        public IReadOnlyDictionary<string, UnitTypeScoreSummary> UnitTypeSummaries => unitTypeSummaries;

        public void ResetDay()
        {
            JoyToday = 0;
        }

        public UnitTypeScoreSummary RecordJoy(JoyCreatedEvent evt)
        {
            int amount = Math.Max(0, evt.Amount);
            if (amount <= 0)
            {
                return null;
            }

            TotalJoyLevel += amount;
            JoyToday += amount;
            AddToStringBucket(joyByUnitType, evt.Attribution.SourceUnitTypeId, amount);
            AddToStringBucket(joyByUnitInstance, evt.Attribution.SourceUnitInstanceId, amount);
            AddToSourceBucket(evt.Attribution.SourceCategory, amount);

            UnitTypeScoreSummary summary = GetOrCreateSummary(evt.Attribution.SourceUnitTypeId);
            summary.RecordJoy(evt.Attribution.SourceCategory, amount);
            return summary;
        }

        public UnitTypeScoreSummary RecordTrigger(UnitTriggeredEvent evt)
        {
            string unitTypeId = evt.Unit == null || evt.Unit.Definition == null
                ? string.Empty
                : evt.Unit.Definition.UnitTypeId;
            UnitTypeScoreSummary summary = GetOrCreateSummary(unitTypeId);
            summary.RecordTrigger();
            return summary;
        }

        private UnitTypeScoreSummary GetOrCreateSummary(string unitTypeId)
        {
            string safeUnitTypeId = string.IsNullOrWhiteSpace(unitTypeId) ? "unknown" : unitTypeId;
            if (!unitTypeSummaries.TryGetValue(safeUnitTypeId, out UnitTypeScoreSummary summary))
            {
                summary = new UnitTypeScoreSummary(safeUnitTypeId);
                unitTypeSummaries[safeUnitTypeId] = summary;
            }

            return summary;
        }

        private static void AddToStringBucket(Dictionary<string, int> bucket, string key, int amount)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = "unknown";
            }

            bucket.TryGetValue(key, out int current);
            bucket[key] = current + amount;
        }

        private void AddToSourceBucket(SourceCategory sourceCategory, int amount)
        {
            joyBySourceCategory.TryGetValue(sourceCategory, out int current);
            joyBySourceCategory[sourceCategory] = current + amount;
        }
    }

    public sealed class RunSaveDirtyFlags
    {
        public bool BoardLayout { get; set; }
        public bool UnitPlacement { get; set; }
        public bool DayProgress { get; set; }
    }

    public sealed class RuntimeLevelState
    {
        private int unitSerial;

        public RuntimeLevelState(
            LevelDefinitionSO definition,
            RuntimeBoardState board,
            GlobalGameConfigSO globalConfig)
        {
            Definition = definition;
            Board = board;
            GlobalConfig = globalConfig;
            GameSpeed = new GameSpeedRuntimeState();
            Frenzy = new FrenzyRuntimeState();
            Scoring = new ScoringRuntimeState();
            Draft = new DraftRuntimeState();
            SaveDirtyFlags = new RunSaveDirtyFlags();

            int seed = globalConfig != null && globalConfig.UseFixedRandomSeed
                ? globalConfig.FixedRandomSeed
                : Environment.TickCount;
            RunSeed = seed;
            Random = new Random(seed);
        }

        public LevelDefinitionSO Definition { get; }
        public RuntimeBoardState Board { get; }
        public GlobalGameConfigSO GlobalConfig { get; }
        public List<UnitInstanceState> Units { get; } = new List<UnitInstanceState>();
        public List<VisitorState> ActiveVisitors { get; } = new List<VisitorState>();
        public int CurrentStageIndex { get; set; }
        public int CurrentDayIndex { get; set; }
        public int TotalJoy { get; private set; }
        public int CurrentRumble { get; private set; }
        public int RemainingMochiVisitorsToday { get; private set; }
        public int PendingCalledVisitors { get; private set; }
        public int RemainingShovelUses { get; private set; }
        public GamePhase Phase { get; set; }
        public ResolvedDayConfig CurrentDayConfig { get; set; }
        public GameSpeedRuntimeState GameSpeed { get; }
        public FrenzyRuntimeState Frenzy { get; }
        public ScoringRuntimeState Scoring { get; }
        public DraftRuntimeState Draft { get; }
        public RunSaveDirtyFlags SaveDirtyFlags { get; }
        public int RunSeed { get; }
        public Random Random { get; }

        public string NextUnitInstanceId()
        {
            unitSerial++;
            return $"unit_{unitSerial:000}";
        }

        public UnitInstanceState FindUnit(string unitInstanceId)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                if (Units[i].InstanceId == unitInstanceId)
                {
                    return Units[i];
                }
            }

            return null;
        }

        public void AddJoy(int amount)
        {
            TotalJoy += Math.Max(0, amount);
        }

        public void AddRumble(int amount)
        {
            CurrentRumble += Math.Max(0, amount);
        }

        public void ResetRumble()
        {
            CurrentRumble = 0;
        }

        public void SetRemainingMochiVisitorsToday(int amount)
        {
            RemainingMochiVisitorsToday = Math.Max(0, amount);
        }

        public bool TryStartFrenzy(FrenzyConfigSpec config)
        {
            if (!Frenzy.CanStart(config) || CurrentRumble < config.RumbleThreshold)
            {
                return false;
            }

            Frenzy.Start(config);
            if (config.ResetRumbleOnStart)
            {
                ResetRumble();
            }

            CallVisitors(config.CallVisitorBonus);
            return true;
        }

        public void CallVisitors(int amount)
        {
            PendingCalledVisitors += Math.Max(0, amount);
        }

        public void ClearCalledVisitors()
        {
            PendingCalledVisitors = 0;
        }

        public int ConsumeCalledVisitors()
        {
            int calledVisitors = PendingCalledVisitors;
            PendingCalledVisitors = 0;
            return calledVisitors;
        }

        public void SetRemainingShovelUses(int amount)
        {
            RemainingShovelUses = Math.Max(0, amount);
        }

        public bool TryConsumeShovelUse()
        {
            if (RemainingShovelUses <= 0)
            {
                return false;
            }

            RemainingShovelUses--;
            return true;
        }
    }
}
