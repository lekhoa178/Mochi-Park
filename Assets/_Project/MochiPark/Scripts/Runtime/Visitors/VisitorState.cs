using UnityEngine;

namespace MochiPark.Runtime.Visitors
{
    public sealed class VisitorState
    {
        private int lastReachedTileCenterPathIndex = -1;

        public VisitorState(string visitorId, Vector2Int cell)
        {
            VisitorId = visitorId;
            Cell = cell;
        }

        public string VisitorId { get; }
        public Vector2Int Cell { get; private set; }
        public int PathIndex { get; private set; }
        public int FellowMochiCount { get; private set; }
        public float LingerRemaining { get; private set; }
        public float StepTimer { get; set; }
        public int EffectiveVisitorCount => 1 + Mathf.Max(0, FellowMochiCount);

        public void MoveTo(Vector2Int cell, int pathIndex)
        {
            Cell = cell;
            PathIndex = pathIndex;
        }

        public bool TryMarkTileCenterReached()
        {
            if (PathIndex <= lastReachedTileCenterPathIndex)
            {
                return false;
            }

            lastReachedTileCenterPathIndex = PathIndex;
            return true;
        }

        public int AddFellows(int amount, int maxFellowCount = int.MaxValue)
        {
            int previousCount = FellowMochiCount;
            int clampedMax = Mathf.Max(0, maxFellowCount);
            FellowMochiCount = Mathf.Clamp(FellowMochiCount + Mathf.Max(0, amount), 0, clampedMax);
            return FellowMochiCount - previousCount;
        }

        public int RemoveFellows(int amount)
        {
            int previousCount = FellowMochiCount;
            FellowMochiCount = Mathf.Max(0, FellowMochiCount - Mathf.Max(0, amount));
            return previousCount - FellowMochiCount;
        }

        public void ApplyLinger(float seconds)
        {
            LingerRemaining = Mathf.Max(0f, LingerRemaining + seconds);
        }

        public float ConsumeLinger(float deltaTime)
        {
            if (LingerRemaining <= 0f)
            {
                return deltaTime;
            }

            float consumed = Mathf.Min(LingerRemaining, Mathf.Max(0f, deltaTime));
            LingerRemaining -= consumed;
            return Mathf.Max(0f, deltaTime - consumed);
        }
    }
}
