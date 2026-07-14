using MochiPark.Runtime.Board;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Units;
using MochiPark.Runtime.Visitors;
using UnityEngine;

namespace MochiPark.Runtime.Level
{
    public sealed class LevelDirector
    {
        private readonly GameCatalogSO catalog;
        private readonly EventBus eventBus;
        private EffectSystem effectSystem;
        private DraftSystem draftSystem;
        private UnitTriggerSystem unitTriggerSystem;
        private VisitorSimulationSystem visitorSimulationSystem;
        private ScoringSystem scoringSystem;

        public LevelDirector(GameCatalogSO catalog, EventBus eventBus)
        {
            this.catalog = catalog;
            this.eventBus = eventBus;
        }

        public RuntimeLevelState State { get; private set; }
        public PlacementSystem PlacementSystem { get; private set; }
        public DraftSystem DraftSystem => draftSystem;

        public void StartLevel(string levelId)
        {
            if (catalog == null || catalog.GlobalConfig == null)
            {
                eventBus.Publish(new PopupRequestedEvent("Cannot start level", "Missing catalog or global config."));
                return;
            }

            if (!catalog.TryGetLevel(levelId, out LevelDefinitionSO level) || level.Board == null)
            {
                eventBus.Publish(new PopupRequestedEvent("Cannot start level", $"Level not found: {levelId}"));
                return;
            }

            RuntimeBoardState board = RuntimeBoardState.Build(level.Board);
            scoringSystem?.Dispose();
            State = new RuntimeLevelState(level, board, catalog.GlobalConfig)
            {
                Phase = GamePhase.LevelReady
            };

            effectSystem = new EffectSystem(eventBus);
            scoringSystem = new ScoringSystem(State, eventBus);
            unitTriggerSystem = new UnitTriggerSystem(State, eventBus, effectSystem);
            visitorSimulationSystem = new VisitorSimulationSystem(State, eventBus, HandleVisitorReachedTileCenter);
            PlacementSystem = new PlacementSystem(State, eventBus);
            draftSystem = new DraftSystem(State, catalog, eventBus, PlacementSystem);

            for (int i = 0; i < level.InitialUnits.Count; i++)
            {
                PlacementSystem.PlaceInitialUnit(level.InitialUnits[i]);
            }

            eventBus.Publish(new LevelStartedEvent(State));
            eventBus.Publish(new SaveRequestedEvent(SaveRequestReason.LevelStarted));
            StartDay(0, 0);
        }

        public void StartDay(int stageIndex, int dayIndex)
        {
            if (State == null)
            {
                return;
            }

            ResolvedDayConfig config = ConfigResolver.Resolve(catalog, State.Definition, stageIndex, dayIndex);
            State.CurrentStageIndex = config.StageIndex;
            State.CurrentDayIndex = config.DayIndexInLevel;
            State.CurrentDayConfig = config;
            State.SetRemainingShovelUses(config.UnlockCharges);
            State.ResetRumble();
            State.ClearCalledVisitors();
            State.SetRemainingMochiVisitorsToday(config.VisitorCount);
            State.Frenzy.PrepareDay(config.FrenzyConfig);
            State.Scoring.ResetDay();
            State.Phase = GamePhase.Placement;

            State.GameSpeed.SetRequested(config.DefaultGameSpeedOnDayStart);
            State.GameSpeed.SetEffective(1f);

            unitTriggerSystem.PrepareDay();
            JoyIncomeUtility.RefreshDeterministicModifiers(State, eventBus);
            visitorSimulationSystem.PrepareDay(config.VisitorCount);

            eventBus.Publish(new DayPreparedEvent(State, config));
            eventBus.Publish(new GameSpeedChangedEvent(State.GameSpeed.RequestedMultiplier, State.GameSpeed.EffectiveMultiplier));
            draftSystem.PrepareDay(config);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (State == null || State.Phase != GamePhase.Running)
            {
                return;
            }

            float scaledDeltaTime = unscaledDeltaTime * State.GameSpeed.EffectiveMultiplier;
            bool wasFrenzyActiveAtFrameStart = State.Frenzy.IsActive;
            unitTriggerSystem.Tick(scaledDeltaTime);
            visitorSimulationSystem.Tick(scaledDeltaTime, unscaledDeltaTime);

            if (wasFrenzyActiveAtFrameStart && State.Frenzy.Tick(scaledDeltaTime))
            {
                eventBus.Publish(new FrenzyEndedEvent(State));
            }

            if (visitorSimulationSystem.IsComplete)
            {
                EndDay();
            }
        }

        public void OpenPark()
        {
            if (State == null || State.Phase != GamePhase.Placement)
            {
                return;
            }

            if (State.Draft == null ||
                State.Draft.HasPendingPick ||
                !State.Draft.IsCompletedForDay)
            {
                return;
            }

            State.Phase = GamePhase.Running;
            ApplyEffectiveSpeed();
            eventBus.Publish(new ParkOpenedEvent(State));
            unitTriggerSystem.HandleParkOpened();
            visitorSimulationSystem.BeginRun(State.CurrentDayConfig);
        }

        public bool TrySelectDraftOfferCard(int cardIndex, out UnitInstanceState placedUnit)
        {
            placedUnit = null;
            return draftSystem != null && draftSystem.TrySelectOfferCard(cardIndex, out placedUnit);
        }

        public bool TryRerollUpgrade()
        {
            return draftSystem != null && draftSystem.TryRerollCurrentOffer();
        }

        public bool TryGetUnitDefinition(string unitTypeId, out UnitDefinitionSO unit)
        {
            unit = null;
            return catalog != null && catalog.TryGetUnit(unitTypeId, out unit);
        }

        public bool ConfirmDraftPlacement()
        {
            return draftSystem != null && draftSystem.ConfirmPendingPick();
        }

        public bool CancelDraftSelection()
        {
            return draftSystem != null && draftSystem.CancelPendingPick();
        }

        public void SkipDraftForDay()
        {
            draftSystem?.CompleteForDay();
        }

        public void RefreshDeterministicJoyIncome()
        {
            if (State == null)
            {
                return;
            }

            PlacementSystem?.RebuildInfluenceCache();
            JoyIncomeUtility.RefreshDeterministicModifiers(State, eventBus);
        }

        public void ChangeGameSpeed(float requestedMultiplier)
        {
            if (State == null || State.CurrentDayConfig == null)
            {
                return;
            }

            float selected = State.CurrentDayConfig.SpeedControlEnabled
                ? ConfigResolver.ClampMultiplier(requestedMultiplier, State.CurrentDayConfig.AllowedGameSpeedMultipliers)
                : 1f;

            State.GameSpeed.SetRequested(selected);
            ApplyEffectiveSpeed();
            eventBus.Publish(new GameSpeedChangedEvent(State.GameSpeed.RequestedMultiplier, State.GameSpeed.EffectiveMultiplier));
        }

        public void HandleVisitorReachedTileCenter(VisitorState visitor, Vector2Int cell)
        {
            if (State == null || State.Phase != GamePhase.Running || visitor == null)
            {
                return;
            }

            unitTriggerSystem.HandleVisitorReachedTileCenter(visitor, cell);
        }

        public bool TryPlaceUnit(string unitTypeId, Vector2Int anchorCell, GridRotation rotation)
        {
            if (State == null || !catalog.TryGetUnit(unitTypeId, out UnitDefinitionSO unit))
            {
                return false;
            }

            return PlacementSystem.PlaceUnit(unit, anchorCell, rotation, false, false, out _);
        }

        public bool TryUnlockBoardCell(Vector2Int cell)
        {
            if (State == null || State.Phase != GamePhase.Placement || PlacementSystem == null)
            {
                return false;
            }

            if (State.RemainingShovelUses <= 0)
            {
                eventBus.Publish(new PopupRequestedEvent("Shovel unavailable", "No shovel uses remaining."));
                return false;
            }

            if (!PlacementSystem.UnlockCell(cell))
            {
                return false;
            }

            State.TryConsumeShovelUse();
            return true;
        }

        public void RestartLevel()
        {
            if (State == null)
            {
                return;
            }

            StartLevel(State.Definition.LevelId);
        }

        private void EndDay()
        {
            if (State == null || State.Phase == GamePhase.DayEnded)
            {
                return;
            }

            State.Phase = GamePhase.DayEnded;
            State.GameSpeed.SetEffective(1f);
            int stars = State.Definition.StarThresholds.CalculateStars(State.TotalJoy);
            eventBus.Publish(new DayEndedEvent(State, stars));
            eventBus.Publish(new SaveRequestedEvent(SaveRequestReason.DayEnded));
        }

        private void ApplyEffectiveSpeed()
        {
            if (State.CurrentDayConfig == null || !State.CurrentDayConfig.SpeedControlEnabled)
            {
                State.GameSpeed.SetEffective(1f);
                return;
            }

            float effective = State.Phase == GamePhase.Running ? State.GameSpeed.RequestedMultiplier : 1f;
            State.GameSpeed.SetEffective(effective);
        }
    }
}
