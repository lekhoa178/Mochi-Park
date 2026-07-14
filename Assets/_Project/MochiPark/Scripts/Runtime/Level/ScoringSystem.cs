using System;
using MochiPark.Runtime.Core;

namespace MochiPark.Runtime.Level
{
    public sealed class ScoringSystem : IDisposable
    {
        private readonly RuntimeLevelState state;
        private readonly EventBus eventBus;

        public ScoringSystem(RuntimeLevelState state, EventBus eventBus)
        {
            this.state = state;
            this.eventBus = eventBus;

            if (this.eventBus == null)
            {
                return;
            }

            this.eventBus.JoyCreated += OnJoyCreated;
            this.eventBus.UnitTriggered += OnUnitTriggered;
        }

        public void Dispose()
        {
            if (eventBus == null)
            {
                return;
            }

            eventBus.JoyCreated -= OnJoyCreated;
            eventBus.UnitTriggered -= OnUnitTriggered;
        }

        private void OnJoyCreated(JoyCreatedEvent evt)
        {
            if (state == null)
            {
                return;
            }

            UnitTypeScoreSummary summary = state.Scoring.RecordJoy(evt);
            state.Frenzy.RecordJoy(evt.Amount);
            eventBus.Publish(new ScoreChangedEvent(state, evt));

            if (summary != null)
            {
                eventBus.Publish(new LeaderboardUpdatedEvent(state, summary));
            }
        }

        private void OnUnitTriggered(UnitTriggeredEvent evt)
        {
            if (state == null)
            {
                return;
            }

            UnitTypeScoreSummary summary = state.Scoring.RecordTrigger(evt);
            eventBus.Publish(new LeaderboardUpdatedEvent(state, summary));
        }
    }
}
