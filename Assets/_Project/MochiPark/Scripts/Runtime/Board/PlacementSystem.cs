using System.Collections.Generic;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using MochiPark.Runtime.Units;
using UnityEngine;

namespace MochiPark.Runtime.Board
{
    public sealed class PlacementSystem
    {
        private readonly RuntimeLevelState state;
        private readonly EventBus eventBus;

        public PlacementSystem(RuntimeLevelState state, EventBus eventBus)
        {
            this.state = state;
            this.eventBus = eventBus;
        }

        public PlacementResult CanPlace(
            UnitDefinitionSO unitDefinition,
            Vector2Int anchorCell,
            GridRotation rotation,
            string movingUnitInstanceId = null,
            bool ignorePhase = false)
        {
            if (unitDefinition == null || unitDefinition.FootprintPattern == null)
            {
                return PlacementResult.Fail(PlacementFailReason.MissingFootprint, anchorCell, new List<Vector2Int>());
            }

            if (!ignorePhase && !IsPlacementAllowedNow())
            {
                return PlacementResult.Fail(PlacementFailReason.PlacementPhaseClosed, anchorCell, new List<Vector2Int>());
            }

            GridRotation resolvedRotation = unitDefinition.RotationAllowed ? rotation : GridRotation.North;
            List<Vector2Int> footprintCells = GridRotationUtility.ResolveCells(
                unitDefinition.FootprintPattern,
                anchorCell,
                resolvedRotation);

            for (int i = 0; i < footprintCells.Count; i++)
            {
                Vector2Int cell = footprintCells[i];
                BoardCellType cellType = state.Board.GetCellType(cell);

                if (cellType == BoardCellType.OutOfBounds)
                {
                    return PlacementResult.Fail(PlacementFailReason.OutOfBounds, cell, footprintCells);
                }

                if (cellType == BoardCellType.Path || cellType == BoardCellType.Start || cellType == BoardCellType.Exit)
                {
                    return PlacementResult.Fail(PlacementFailReason.PathBlocked, cell, footprintCells);
                }

                if (cellType == BoardCellType.BuildableLocked && !state.Board.IsBuildableUnlocked(cell))
                {
                    return PlacementResult.Fail(PlacementFailReason.LockedCell, cell, footprintCells);
                }

                if (!state.Board.IsBuildableUnlocked(cell))
                {
                    return PlacementResult.Fail(PlacementFailReason.NotBuildable, cell, footprintCells);
                }

                if (state.Board.IsOccupied(cell, movingUnitInstanceId))
                {
                    return PlacementResult.Fail(PlacementFailReason.Occupied, cell, footprintCells);
                }
            }

            return PlacementResult.Success(footprintCells);
        }

        public bool PlaceInitialUnit(InitialUnitSpec spec)
        {
            if (spec == null || spec.UnitDefinition == null)
            {
                return false;
            }

            return PlaceUnit(
                spec.UnitDefinition,
                spec.AnchorCell.ToVector2Int(),
                spec.Rotation,
                spec.IsLockedByTutorial,
                true,
                out _);
        }

        public bool PlaceUnit(
            UnitDefinitionSO unitDefinition,
            Vector2Int anchorCell,
            GridRotation rotation,
            bool isLockedByTutorial,
            bool ignorePhase,
            out UnitInstanceState placedUnit)
        {
            PlacementResult result = CanPlace(unitDefinition, anchorCell, rotation, null, ignorePhase);
            if (!result.IsValid)
            {
                placedUnit = null;
                eventBus.Publish(new PopupRequestedEvent("Placement rejected", result.Reason.ToString()));
                return false;
            }

            GridRotation resolvedRotation = unitDefinition.RotationAllowed ? rotation : GridRotation.North;
            UnitInstanceState unit = new UnitInstanceState(
                state.NextUnitInstanceId(),
                unitDefinition,
                anchorCell,
                resolvedRotation,
                isLockedByTutorial);

            state.Units.Add(unit);
            state.Board.RegisterOccupancy(unit.InstanceId, result.FootprintCells);
            RebuildInfluenceCache();
            JoyIncomeUtility.RefreshDeterministicModifiers(state, eventBus);

            placedUnit = unit;
            eventBus.Publish(new PlacementChangedEvent(state, unit));
            eventBus.Publish(new SaveRequestedEvent(SaveRequestReason.PlacementChanged));
            return true;
        }

        public bool MoveUnit(string unitInstanceId, Vector2Int anchorCell, GridRotation rotation)
        {
            UnitInstanceState unit = state.FindUnit(unitInstanceId);
            if (unit == null || unit.IsLockedByTutorial)
            {
                eventBus.Publish(new PopupRequestedEvent("Move rejected", PlacementFailReason.TutorialLocked.ToString()));
                return false;
            }

            Vector2Int previousAnchor = unit.AnchorCell;
            GridRotation previousRotation = unit.Rotation;

            state.Board.ClearOccupancy(unit.InstanceId);
            PlacementResult result = CanPlace(unit.Definition, anchorCell, rotation, unit.InstanceId);

            if (!result.IsValid)
            {
                List<Vector2Int> previousFootprint = GridRotationUtility.ResolveCells(
                    unit.Definition.FootprintPattern,
                    previousAnchor,
                    previousRotation);
                state.Board.RegisterOccupancy(unit.InstanceId, previousFootprint);
                eventBus.Publish(new PopupRequestedEvent("Move rejected", result.Reason.ToString()));
                return false;
            }

            unit.SetPlacement(anchorCell, unit.Definition.RotationAllowed ? rotation : GridRotation.North);
            state.Board.RegisterOccupancy(unit.InstanceId, result.FootprintCells);
            RebuildInfluenceCache();
            JoyIncomeUtility.RefreshDeterministicModifiers(state, eventBus);

            eventBus.Publish(new PlacementChangedEvent(state, unit));
            eventBus.Publish(new SaveRequestedEvent(SaveRequestReason.PlacementChanged));
            return true;
        }

        public bool RemoveUnit(string unitInstanceId)
        {
            UnitInstanceState unit = state.FindUnit(unitInstanceId);
            if (unit == null || unit.IsLockedByTutorial)
            {
                return false;
            }

            state.Board.ClearOccupancy(unit.InstanceId);
            state.Units.Remove(unit);
            RebuildInfluenceCache();
            JoyIncomeUtility.RefreshDeterministicModifiers(state, eventBus);
            eventBus.Publish(new PlacementChangedEvent(state, unit));
            eventBus.Publish(new SaveRequestedEvent(SaveRequestReason.PlacementChanged));
            return true;
        }

        public bool UnlockCell(Vector2Int cell)
        {
            if (!state.Board.TryUnlockCell(cell))
            {
                return false;
            }

            state.SaveDirtyFlags.BoardLayout = true;
            eventBus.Publish(new BoardCellUnlockedEvent(state, cell));
            eventBus.Publish(new SaveRequestedEvent(SaveRequestReason.BoardUnlock));
            return true;
        }

        public void RebuildInfluenceCache()
        {
            state.Board.ClearInfluenceCache();

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstanceState unit = state.Units[i];
                IReadOnlyList<InfluencePatternSpec> influencePatterns = unit.Definition.InfluencePatterns;

                for (int j = 0; j < influencePatterns.Count; j++)
                {
                    InfluencePatternSpec influence = influencePatterns[j];
                    if (influence == null ||
                        influence.Pattern == null ||
                        !influence.Pattern.TargetsMochiPath)
                    {
                        continue;
                    }

                    List<Vector2Int> influencedCells = GridRotationUtility.ResolveCells(
                        influence.Pattern,
                        unit.AnchorCell,
                        unit.Rotation);

                    for (int k = 0; k < influencedCells.Count; k++)
                    {
                        Vector2Int cell = influencedCells[k];
                        if (state.Board.IsPathCell(cell))
                        {
                            state.Board.AddInfluence(cell, new InfluenceLink(unit.InstanceId, influence.AreaId));
                        }
                    }
                }
            }
        }

        private bool IsPlacementAllowedNow()
        {
            if (state.CurrentDayConfig == null)
            {
                return true;
            }

            if (state.Phase == GamePhase.Placement)
            {
                return state.CurrentDayConfig.AllowPlacementBeforeRun;
            }

            if (state.Phase == GamePhase.Running)
            {
                return state.CurrentDayConfig.AllowPlacementDuringRun;
            }

            return false;
        }
    }
}
