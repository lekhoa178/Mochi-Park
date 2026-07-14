using System;
using System.Collections.Generic;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Level;
using UnityEngine;

namespace MochiPark.Runtime.Visitors
{
    public sealed class VisitorSimulationSystem
    {
        private const float EndOfRunHoldSeconds = 2f;
        private readonly RuntimeLevelState state;
        private readonly EventBus eventBus;
        private readonly Action<VisitorState, Vector2Int> visitorReachedTileCenter;
        private int remainingVisitors;
        private int visitorSerial;
        private float spawnTimer;
        private float endOfRunHoldTimer;
        private int lastPublishedRemainingVisitors = -1;
        private bool isHoldingForEndOfRun;
        private bool isRunning;

        public VisitorSimulationSystem(RuntimeLevelState state, EventBus eventBus)
            : this(state, eventBus, null)
        {
        }

        public VisitorSimulationSystem(
            RuntimeLevelState state,
            EventBus eventBus,
            Action<VisitorState, Vector2Int> visitorReachedTileCenter)
        {
            this.state = state;
            this.eventBus = eventBus;
            this.visitorReachedTileCenter = visitorReachedTileCenter;
        }

        public bool IsComplete => isRunning && isHoldingForEndOfRun && endOfRunHoldTimer <= 0f;

        public void PrepareDay(int initialVisitorCount)
        {
            state.ActiveVisitors.Clear();
            remainingVisitors = 0;
            spawnTimer = 0f;
            endOfRunHoldTimer = 0f;
            isHoldingForEndOfRun = false;
            isRunning = false;
            lastPublishedRemainingVisitors = -1;
            SetRemainingVisitors(initialVisitorCount, false);
        }

        public void BeginRun(ResolvedDayConfig config)
        {
            state.ActiveVisitors.Clear();
            remainingVisitors = config.VisitorCount;
            spawnTimer = 0f;
            endOfRunHoldTimer = 0f;
            isHoldingForEndOfRun = false;
            isRunning = true;
            SetRemainingVisitors(GetRemainingVisitorCount(), true);
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!isRunning || state.CurrentDayConfig == null)
            {
                return;
            }

            TrySpawnVisitors(deltaTime);
            MoveVisitors(deltaTime);
            UpdateEndOfRunHold(unscaledDeltaTime);
            SetRemainingVisitors(GetRemainingVisitorCount(), true);
        }

        private void TrySpawnVisitors(float deltaTime)
        {
            remainingVisitors += state.ConsumeCalledVisitors();
            SetRemainingVisitors(GetRemainingVisitorCount(), true);

            if (remainingVisitors <= 0 || state.Board.PathCells.Count == 0)
            {
                return;
            }

            spawnTimer -= deltaTime;
            while (remainingVisitors > 0 && spawnTimer <= 0f)
            {
                SpawnVisitor();
                remainingVisitors--;
                SetRemainingVisitors(GetRemainingVisitorCount(), true);
                spawnTimer += state.CurrentDayConfig.SpawnDelay;
            }
        }

        private void SpawnVisitor()
        {
            visitorSerial++;
            Vector2Int startCell = state.Board.PathCells[0];
            VisitorState visitor = new VisitorState($"visitor_{visitorSerial:000}", startCell);
            visitor.MoveTo(startCell, 0);
            visitor.StepTimer = 0f;

            state.ActiveVisitors.Add(visitor);
            eventBus.Publish(new VisitorSpawnedEvent(visitor));
            float lingerBeforeTrigger = visitor.LingerRemaining;
            NotifyVisitorReachedTileCenter(visitor, startCell);
            eventBus.Publish(new VisitorEnteredCellEvent(visitor, startCell));
            if (visitor.LingerRemaining <= lingerBeforeTrigger)
            {
                TryBeginMoveToNextCell(visitor, state.Board.PathCells);
            }
        }

        private void MoveVisitors(float deltaTime)
        {
            IReadOnlyList<Vector2Int> pathCells = state.Board.PathCells;

            for (int i = state.ActiveVisitors.Count - 1; i >= 0; i--)
            {
                VisitorState visitor = state.ActiveVisitors[i];
                float movementDeltaTime = visitor.ConsumeLinger(deltaTime);
                if (movementDeltaTime <= 0f)
                {
                    continue;
                }

                while (movementDeltaTime > 0f)
                {
                    if (visitor.StepTimer <= 0f && !TryBeginMoveToNextCell(visitor, pathCells))
                    {
                        state.ActiveVisitors.RemoveAt(i);
                        SetRemainingVisitors(GetRemainingVisitorCount(), true);
                        break;
                    }

                    float consumedTime = Mathf.Min(movementDeltaTime, visitor.StepTimer);
                    visitor.StepTimer -= consumedTime;
                    movementDeltaTime -= consumedTime;
                    if (visitor.StepTimer > 0f)
                    {
                        break;
                    }

                    int reachedPathIndex = visitor.PathIndex + 1;
                    if (reachedPathIndex >= pathCells.Count)
                    {
                        state.ActiveVisitors.RemoveAt(i);
                        SetRemainingVisitors(GetRemainingVisitorCount(), true);
                        break;
                    }

                    Vector2Int reachedCell = pathCells[reachedPathIndex];
                    visitor.MoveTo(reachedCell, reachedPathIndex);
                    float lingerBeforeTrigger = visitor.LingerRemaining;
                    NotifyVisitorReachedTileCenter(visitor, reachedCell);

                    if (visitor.LingerRemaining > lingerBeforeTrigger)
                    {
                        visitor.StepTimer = 0f;
                        break;
                    }
                }
            }
        }

        private bool TryBeginMoveToNextCell(VisitorState visitor, IReadOnlyList<Vector2Int> pathCells)
        {
            int nextPathIndex = visitor.PathIndex + 1;
            if (nextPathIndex >= pathCells.Count)
            {
                return false;
            }

            visitor.StepTimer = GetStepInterval();
            eventBus.Publish(new VisitorEnteredCellEvent(visitor, pathCells[nextPathIndex]));
            return true;
        }

        private void NotifyVisitorReachedTileCenter(VisitorState visitor, Vector2Int cell)
        {
            if (visitor == null || !visitor.TryMarkTileCenterReached())
            {
                return;
            }

            visitorReachedTileCenter?.Invoke(visitor, cell);
        }

        private void UpdateEndOfRunHold(float deltaTime)
        {
            if (!HasNoPendingVisitorsAtExit())
            {
                ResetEndOfRunHold();
                return;
            }

            if (!isHoldingForEndOfRun)
            {
                isHoldingForEndOfRun = true;
                endOfRunHoldTimer = EndOfRunHoldSeconds;
            }

            endOfRunHoldTimer -= deltaTime;
        }

        private void ResetEndOfRunHold()
        {
            isHoldingForEndOfRun = false;
            endOfRunHoldTimer = EndOfRunHoldSeconds;
        }

        private bool HasNoPendingVisitorsAtExit()
        {
            if (remainingVisitors > 0)
            {
                return false;
            }

            IReadOnlyList<Vector2Int> pathCells = state.Board.PathCells;
            if (pathCells.Count == 0)
            {
                return state.ActiveVisitors.Count == 0;
            }

            int exitPathIndex = pathCells.Count - 1;
            for (int i = 0; i < state.ActiveVisitors.Count; i++)
            {
                if (state.ActiveVisitors[i].PathIndex < exitPathIndex)
                {
                    return false;
                }
            }

            return true;
        }

        private float GetStepInterval()
        {
            return 1f / Mathf.Max(0.01f, state.CurrentDayConfig.VisitorSpeed);
        }

        private int GetRemainingVisitorCount()
        {
            return Mathf.Max(0, remainingVisitors) + state.PendingCalledVisitors;
        }

        private void SetRemainingVisitors(int remaining, bool publishEvent)
        {
            int safeRemaining = Mathf.Max(0, remaining);
            state.SetRemainingMochiVisitorsToday(safeRemaining);
            if (!publishEvent || safeRemaining == lastPublishedRemainingVisitors)
            {
                return;
            }

            lastPublishedRemainingVisitors = safeRemaining;
            eventBus.Publish(new VisitorProgressChangedEvent(state, safeRemaining));
        }
    }
}
