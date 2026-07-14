using System;

namespace MochiPark.Runtime.Core
{
    public sealed class EventBus
    {
        public event Action<LevelStartedEvent> LevelStarted;
        public event Action<DayPreparedEvent> DayPrepared;
        public event Action<PlacementChangedEvent> PlacementChanged;
        public event Action<DeterministicJoyIncomeChangedEvent> DeterministicJoyIncomeChanged;
        public event Action<DraftOfferedEvent> DraftOffered;
        public event Action<DraftPickedEvent> DraftPicked;
        public event Action<DraftCompletedForDayEvent> DraftCompletedForDay;
        public event Action<BoardCellUnlockedEvent> BoardCellUnlocked;
        public event Action<ParkOpenedEvent> ParkOpened;
        public event Action<GameSpeedChangedEvent> GameSpeedChanged;
        public event Action<VisitorSpawnedEvent> VisitorSpawned;
        public event Action<VisitorEnteredCellEvent> VisitorEnteredCell;
        public event Action<VisitorProgressChangedEvent> VisitorProgressChanged;
        public event Action<UnitTriggeredEvent> UnitTriggered;
        public event Action<EffectAppliedEvent> EffectApplied;
        public event Action<JoyCreatedEvent> JoyCreated;
        public event Action<RumbleAddedEvent> RumbleAdded;
        public event Action<VisitorCalledEvent> VisitorCalled;
        public event Action<FellowAddedEvent> FellowAdded;
        public event Action<FrenzyStartedEvent> FrenzyStarted;
        public event Action<FrenzyEndedEvent> FrenzyEnded;
        public event Action<StorageFilledEvent> StorageFilled;
        public event Action<StorageCashedOutEvent> StorageCashedOut;
        public event Action<ScoreChangedEvent> ScoreChanged;
        public event Action<LeaderboardUpdatedEvent> LeaderboardUpdated;
        public event Action<DayEndedEvent> DayEnded;
        public event Action<SaveRequestedEvent> SaveRequested;
        public event Action<PopupRequestedEvent> PopupRequested;

        public void Publish(LevelStartedEvent evt) => LevelStarted?.Invoke(evt);
        public void Publish(DayPreparedEvent evt) => DayPrepared?.Invoke(evt);
        public void Publish(PlacementChangedEvent evt) => PlacementChanged?.Invoke(evt);
        public void Publish(DeterministicJoyIncomeChangedEvent evt) => DeterministicJoyIncomeChanged?.Invoke(evt);
        public void Publish(DraftOfferedEvent evt) => DraftOffered?.Invoke(evt);
        public void Publish(DraftPickedEvent evt) => DraftPicked?.Invoke(evt);
        public void Publish(DraftCompletedForDayEvent evt) => DraftCompletedForDay?.Invoke(evt);
        public void Publish(BoardCellUnlockedEvent evt) => BoardCellUnlocked?.Invoke(evt);
        public void Publish(ParkOpenedEvent evt) => ParkOpened?.Invoke(evt);
        public void Publish(GameSpeedChangedEvent evt) => GameSpeedChanged?.Invoke(evt);
        public void Publish(VisitorSpawnedEvent evt) => VisitorSpawned?.Invoke(evt);
        public void Publish(VisitorEnteredCellEvent evt) => VisitorEnteredCell?.Invoke(evt);
        public void Publish(VisitorProgressChangedEvent evt) => VisitorProgressChanged?.Invoke(evt);
        public void Publish(UnitTriggeredEvent evt) => UnitTriggered?.Invoke(evt);
        public void Publish(EffectAppliedEvent evt) => EffectApplied?.Invoke(evt);
        public void Publish(JoyCreatedEvent evt) => JoyCreated?.Invoke(evt);
        public void Publish(RumbleAddedEvent evt) => RumbleAdded?.Invoke(evt);
        public void Publish(VisitorCalledEvent evt) => VisitorCalled?.Invoke(evt);
        public void Publish(FellowAddedEvent evt) => FellowAdded?.Invoke(evt);
        public void Publish(FrenzyStartedEvent evt) => FrenzyStarted?.Invoke(evt);
        public void Publish(FrenzyEndedEvent evt) => FrenzyEnded?.Invoke(evt);
        public void Publish(StorageFilledEvent evt) => StorageFilled?.Invoke(evt);
        public void Publish(StorageCashedOutEvent evt) => StorageCashedOut?.Invoke(evt);
        public void Publish(ScoreChangedEvent evt) => ScoreChanged?.Invoke(evt);
        public void Publish(LeaderboardUpdatedEvent evt) => LeaderboardUpdated?.Invoke(evt);
        public void Publish(DayEndedEvent evt) => DayEnded?.Invoke(evt);
        public void Publish(SaveRequestedEvent evt) => SaveRequested?.Invoke(evt);
        public void Publish(PopupRequestedEvent evt) => PopupRequested?.Invoke(evt);
    }
}
