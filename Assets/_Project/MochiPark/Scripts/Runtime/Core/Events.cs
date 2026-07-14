using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using MochiPark.Runtime.Units;
using MochiPark.Runtime.Visitors;
using UnityEngine;

namespace MochiPark.Runtime.Core
{
    public readonly struct Attribution
    {
        public Attribution(
            string sourceUnitInstanceId,
            string sourceUnitTypeId,
            string sourceSystem,
            SourceCategory sourceCategory,
            EffectType effectType,
            bool isFrenzyAffected,
            bool isStorageDerived)
        {
            SourceUnitInstanceId = sourceUnitInstanceId;
            SourceUnitTypeId = sourceUnitTypeId;
            SourceSystem = sourceSystem;
            SourceCategory = sourceCategory;
            EffectType = effectType;
            IsFrenzyAffected = isFrenzyAffected;
            IsStorageDerived = isStorageDerived;
        }

        public string SourceUnitInstanceId { get; }
        public string SourceUnitTypeId { get; }
        public string SourceSystem { get; }
        public SourceCategory SourceCategory { get; }
        public EffectType EffectType { get; }
        public bool IsFrenzyAffected { get; }
        public bool IsStorageDerived { get; }
    }

    public readonly struct LevelStartedEvent
    {
        public LevelStartedEvent(RuntimeLevelState state)
        {
            State = state;
        }

        public RuntimeLevelState State { get; }
    }

    public readonly struct DayPreparedEvent
    {
        public DayPreparedEvent(RuntimeLevelState state, ResolvedDayConfig config)
        {
            State = state;
            Config = config;
        }

        public RuntimeLevelState State { get; }
        public ResolvedDayConfig Config { get; }
    }

    public readonly struct PlacementChangedEvent
    {
        public PlacementChangedEvent(RuntimeLevelState state, UnitInstanceState unit)
        {
            State = state;
            Unit = unit;
        }

        public RuntimeLevelState State { get; }
        public UnitInstanceState Unit { get; }
    }

    public readonly struct DeterministicJoyIncomeChangedEvent
    {
        public DeterministicJoyIncomeChangedEvent(RuntimeLevelState state)
        {
            State = state;
        }

        public RuntimeLevelState State { get; }
    }

    public readonly struct DraftOfferedEvent
    {
        public DraftOfferedEvent(RuntimeLevelState state, DraftOfferState offer)
        {
            State = state;
            Offer = offer;
        }

        public RuntimeLevelState State { get; }
        public DraftOfferState Offer { get; }
    }

    public readonly struct DraftPickedEvent
    {
        public DraftPickedEvent(RuntimeLevelState state, DraftPickSummary pick)
        {
            State = state;
            Pick = pick;
        }

        public RuntimeLevelState State { get; }
        public DraftPickSummary Pick { get; }
    }

    public readonly struct DraftCompletedForDayEvent
    {
        public DraftCompletedForDayEvent(RuntimeLevelState state)
        {
            State = state;
        }

        public RuntimeLevelState State { get; }
    }

    public readonly struct BoardCellUnlockedEvent
    {
        public BoardCellUnlockedEvent(RuntimeLevelState state, Vector2Int cell)
        {
            State = state;
            Cell = cell;
        }

        public RuntimeLevelState State { get; }
        public Vector2Int Cell { get; }
    }

    public readonly struct ParkOpenedEvent
    {
        public ParkOpenedEvent(RuntimeLevelState state)
        {
            State = state;
        }

        public RuntimeLevelState State { get; }
    }

    public readonly struct GameSpeedChangedEvent
    {
        public GameSpeedChangedEvent(float requestedMultiplier, float effectiveMultiplier)
        {
            RequestedMultiplier = requestedMultiplier;
            EffectiveMultiplier = effectiveMultiplier;
        }

        public float RequestedMultiplier { get; }
        public float EffectiveMultiplier { get; }
    }

    public readonly struct VisitorSpawnedEvent
    {
        public VisitorSpawnedEvent(VisitorState visitor)
        {
            Visitor = visitor;
        }

        public VisitorState Visitor { get; }
    }

    public readonly struct VisitorEnteredCellEvent
    {
        public VisitorEnteredCellEvent(VisitorState visitor, Vector2Int cell)
        {
            Visitor = visitor;
            Cell = cell;
        }

        public VisitorState Visitor { get; }
        public Vector2Int Cell { get; }
    }

    public readonly struct VisitorProgressChangedEvent
    {
        public VisitorProgressChangedEvent(RuntimeLevelState state, int remainingVisitors)
        {
            State = state;
            RemainingVisitors = remainingVisitors;
        }

        public RuntimeLevelState State { get; }
        public int RemainingVisitors { get; }
    }

    public readonly struct UnitTriggeredEvent
    {
        public UnitTriggeredEvent(UnitInstanceState unit, TriggerRuleSpec triggerRule, VisitorState visitor, Vector2Int triggerCell)
        {
            Unit = unit;
            TriggerRule = triggerRule;
            Visitor = visitor;
            TriggerCell = triggerCell;
        }

        public UnitInstanceState Unit { get; }
        public TriggerRuleSpec TriggerRule { get; }
        public VisitorState Visitor { get; }
        public Vector2Int TriggerCell { get; }
    }

    public readonly struct EffectAppliedEvent
    {
        public EffectAppliedEvent(EffectSpec effect, UnitInstanceState sourceUnit, int amount, Attribution attribution)
        {
            Effect = effect;
            SourceUnit = sourceUnit;
            Amount = amount;
            Attribution = attribution;
        }

        public EffectSpec Effect { get; }
        public UnitInstanceState SourceUnit { get; }
        public int Amount { get; }
        public Attribution Attribution { get; }
    }

    public readonly struct JoyCreatedEvent
    {
        public JoyCreatedEvent(int amount, int totalJoy, Attribution attribution, int floatingScoreCount = 1)
        {
            Amount = amount;
            TotalJoy = totalJoy;
            Attribution = attribution;
            FloatingScoreCount = Mathf.Max(1, floatingScoreCount);
        }

        public int Amount { get; }
        public int TotalJoy { get; }
        public Attribution Attribution { get; }
        public int FloatingScoreCount { get; }
    }

    public readonly struct RumbleAddedEvent
    {
        public RumbleAddedEvent(int amount, int currentRumble, Attribution attribution)
        {
            Amount = amount;
            CurrentRumble = currentRumble;
            Attribution = attribution;
        }

        public int Amount { get; }
        public int CurrentRumble { get; }
        public Attribution Attribution { get; }
    }

    public readonly struct VisitorCalledEvent
    {
        public VisitorCalledEvent(int amount, Attribution attribution)
        {
            Amount = amount;
            Attribution = attribution;
        }

        public int Amount { get; }
        public Attribution Attribution { get; }
    }

    public readonly struct FellowAddedEvent
    {
        public FellowAddedEvent(VisitorState visitor, int amount, Attribution attribution)
        {
            Visitor = visitor;
            Amount = amount;
            Attribution = attribution;
        }

        public VisitorState Visitor { get; }
        public int Amount { get; }
        public Attribution Attribution { get; }
    }

    public readonly struct FrenzyStartedEvent
    {
        public FrenzyStartedEvent(RuntimeLevelState state)
        {
            State = state;
        }

        public RuntimeLevelState State { get; }
    }

    public readonly struct FrenzyEndedEvent
    {
        public FrenzyEndedEvent(RuntimeLevelState state)
        {
            State = state;
        }

        public RuntimeLevelState State { get; }
    }

    public readonly struct StorageFilledEvent
    {
        public StorageFilledEvent(UnitInstanceState unit, int amountAdded, int storedAmount, int capacity, Attribution attribution)
        {
            Unit = unit;
            AmountAdded = amountAdded;
            StoredAmount = storedAmount;
            Capacity = capacity;
            Attribution = attribution;
        }

        public UnitInstanceState Unit { get; }
        public int AmountAdded { get; }
        public int StoredAmount { get; }
        public int Capacity { get; }
        public Attribution Attribution { get; }
    }

    public readonly struct StorageCashedOutEvent
    {
        public StorageCashedOutEvent(UnitInstanceState unit, int joyCreated, Attribution attribution)
        {
            Unit = unit;
            JoyCreated = joyCreated;
            Attribution = attribution;
        }

        public UnitInstanceState Unit { get; }
        public int JoyCreated { get; }
        public Attribution Attribution { get; }
    }

    public readonly struct ScoreChangedEvent
    {
        public ScoreChangedEvent(RuntimeLevelState state, JoyCreatedEvent sourceEvent)
        {
            State = state;
            SourceEvent = sourceEvent;
        }

        public RuntimeLevelState State { get; }
        public JoyCreatedEvent SourceEvent { get; }
    }

    public readonly struct LeaderboardUpdatedEvent
    {
        public LeaderboardUpdatedEvent(RuntimeLevelState state, UnitTypeScoreSummary summary)
        {
            State = state;
            Summary = summary;
        }

        public RuntimeLevelState State { get; }
        public UnitTypeScoreSummary Summary { get; }
    }

    public readonly struct DayEndedEvent
    {
        public DayEndedEvent(RuntimeLevelState state, int starsEarned)
        {
            State = state;
            StarsEarned = starsEarned;
        }

        public RuntimeLevelState State { get; }
        public int StarsEarned { get; }
    }

    public readonly struct SaveRequestedEvent
    {
        public SaveRequestedEvent(SaveRequestReason reason)
        {
            Reason = reason;
        }

        public SaveRequestReason Reason { get; }
    }

    public readonly struct PopupRequestedEvent
    {
        public PopupRequestedEvent(string title, string body)
        {
            Title = title;
            Body = body;
        }

        public string Title { get; }
        public string Body { get; }
    }
}
