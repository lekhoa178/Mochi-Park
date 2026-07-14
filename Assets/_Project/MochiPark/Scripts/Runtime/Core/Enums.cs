namespace MochiPark.Runtime.Core
{
    public enum BoardCellType
    {
        OutOfBounds = 0,
        Path = 1,
        Start = 2,
        Exit = 3,
        BuildableOpen = 4,
        BuildableLocked = 5
    }

    public enum GridRotation
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public enum PatternTargetType
    {
        Footprint = 0,
        MochiPath = 1,
        UnitBuff = 2,
        Mixed = 3
    }

    public enum PatternRotationMode
    {
        None = 0,
        RotateWithUnit = 1,
        MirrorWithUnit = 2,
        Custom = 3
    }

    public enum UnitRarity
    {
        Blue = 0,
        Purple = 1,
        Gold = 2
    }

    public enum UnitFamily
    {
        BasicJoy = 0,
        CreatureSupport = 1,
        FoodFellow = 2,
        PlayFrenzy = 3,
        StorageJoy = 4
    }

    public enum UnitTag
    {
        Starter = 0,
        BasicScorer = 1,
        DirectJoy = 2,
        VisitorSupport = 3,
        Fellow = 4,
        Rumble = 5,
        Frenzy = 6,
        Storage = 7
    }

    public enum TriggerType
    {
        OnMochiEnterArea = 0,
        OnOpenPark = 1,
        OnInterval = 2,
        OnFrenzyStarted = 3,
        OnFrenzyEnded = 4,
        OnStorageFull = 5,
        OnNeighborTriggered = 6,
        OnDayEnded = 7
    }

    public enum EffectType
    {
        AddJoy = 0,
        AddRumble = 1,
        CallVisitor = 2,
        AddFellow = 3,
        ApplyLinger = 4,
        AddDailyCharge = 5,
        ApplyJoyModifier = 6,
        ApplyTriggerChanceModifier = 7,
        ApplyIntervalModifier = 8,
        FillStorage = 9,
        CashOutStorage = 10,
        TransformUnit = 11
    }

    public enum SourceCategory
    {
        Direct = 0,
        Buff = 1,
        Frenzy = 2,
        Storage = 3,
        Draft = 4,
        System = 5
    }

    public enum PlacementFailReason
    {
        Success = 0,
        OutOfBounds = 1,
        NotBuildable = 2,
        LockedCell = 3,
        BlockedCell = 4,
        Occupied = 5,
        PathBlocked = 6,
        TileHeightMismatch = 7,
        TutorialLocked = 8,
        PlacementPhaseClosed = 9,
        MissingFootprint = 10
    }

    public enum GamePhase
    {
        None = 0,
        LevelReady = 1,
        Placement = 2,
        Running = 3,
        DayEnded = 4,
        LevelCompleted = 5
    }

    public enum SaveRequestReason
    {
        LevelStarted = 0,
        DraftChoiceConfirmed = 1,
        PlacementChanged = 2,
        BoardUnlock = 3,
        DayEnded = 4,
        LevelCompleted = 5
    }
}
