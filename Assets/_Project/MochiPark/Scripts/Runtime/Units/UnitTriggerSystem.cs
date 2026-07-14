using System.Collections.Generic;
using MochiPark.Runtime.Board;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using MochiPark.Runtime.Visitors;
using UnityEngine;

namespace MochiPark.Runtime.Units
{
    public sealed class UnitTriggerSystem
    {
        private readonly RuntimeLevelState state;
        private readonly EventBus eventBus;
        private readonly EffectSystem effectSystem;

        public UnitTriggerSystem(RuntimeLevelState state, EventBus eventBus, EffectSystem effectSystem)
        {
            this.state = state;
            this.eventBus = eventBus;
            this.effectSystem = effectSystem;
        }

        public void PrepareDay()
        {
            for (int i = 0; i < state.Units.Count; i++)
            {
                state.Units[i].ResetDailyState();
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < state.Units.Count; i++)
            {
                state.Units[i].Tick(deltaTime);
            }
        }

        public void HandleParkOpened()
        {
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstanceState unit = state.Units[i];
                IReadOnlyList<TriggerRuleSpec> rules = unit.Definition.TriggerRules;

                for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    TriggerRuleSpec rule = rules[ruleIndex];
                    if (rule != null && rule.TriggerType == TriggerType.OnOpenPark)
                    {
                        TryTrigger(unit, rule, ruleIndex, null, unit.AnchorCell);
                    }
                }
            }
        }

        public void HandleVisitorReachedTileCenter(VisitorState visitor, Vector2Int cell)
        {
            IReadOnlyList<InfluenceLink> links = state.Board.GetInfluenceLinks(cell);
            List<PendingTrigger> pendingTriggers = new List<PendingTrigger>();
            HashSet<string> processed = new HashSet<string>();

            for (int i = 0; i < links.Count; i++)
            {
                InfluenceLink link = links[i];
                UnitInstanceState unit = state.FindUnit(link.UnitInstanceId);
                if (unit == null)
                {
                    continue;
                }

                IReadOnlyList<TriggerRuleSpec> rules = unit.Definition.TriggerRules;
                for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    TriggerRuleSpec rule = rules[ruleIndex];
                    if (rule == null || rule.TriggerType != TriggerType.OnMochiEnterArea)
                    {
                        continue;
                    }

                    if (rule.TargetAreaId != link.AreaId)
                    {
                        continue;
                    }

                    string processedKey = $"{unit.InstanceId}:{ruleIndex}:{cell.x}:{cell.y}";
                    if (processed.Add(processedKey) &&
                        TryCreatePendingTrigger(unit, rule, ruleIndex, visitor, cell, out PendingTrigger pendingTrigger))
                    {
                        pendingTriggers.Add(pendingTrigger);
                    }
                }
            }

            if (pendingTriggers.Count == 0)
            {
                return;
            }

            ApplyPendingPathTileTriggers(pendingTriggers);
        }

        private bool TryTrigger(
            UnitInstanceState unit,
            TriggerRuleSpec rule,
            int ruleIndex,
            VisitorState visitor,
            Vector2Int triggerCell)
        {
            if (!TryCreatePendingTrigger(unit, rule, ruleIndex, visitor, triggerCell, out PendingTrigger pendingTrigger))
            {
                return false;
            }

            bool applied = effectSystem.ApplyEffects(state, unit, rule, visitor, triggerCell);
            if (!applied)
            {
                return false;
            }

            CommitTrigger(pendingTrigger);
            return true;
        }

        private bool TryCreatePendingTrigger(
            UnitInstanceState unit,
            TriggerRuleSpec rule,
            int ruleIndex,
            VisitorState visitor,
            Vector2Int triggerCell,
            out PendingTrigger pendingTrigger)
        {
            pendingTrigger = new PendingTrigger();

            if (rule.RequiresFrenzy && !state.Frenzy.IsActive)
            {
                return false;
            }

            if (rule.RequiresNotFrenzy && state.Frenzy.IsActive)
            {
                return false;
            }

            if (unit.CooldownRemaining > 0f || !unit.CanSpendDailyCharge())
            {
                return false;
            }

            if (visitor != null && rule.TriggerOncePerVisitor && unit.HasTriggeredForVisitor(ruleIndex, visitor.VisitorId))
            {
                return false;
            }

            if (rule.Chance < 1f && state.Random.NextDouble() > rule.Chance)
            {
                return false;
            }

            pendingTrigger = new PendingTrigger(unit, rule, ruleIndex, visitor, triggerCell);
            return true;
        }

        private void ApplyPendingPathTileTriggers(List<PendingTrigger> pendingTriggers)
        {
            for (int i = 0; i < pendingTriggers.Count; i++)
            {
                PendingTrigger pendingTrigger = pendingTriggers[i];
                pendingTrigger.Applied = effectSystem.ApplyAddFellowEffects(
                    state,
                    pendingTrigger.Unit,
                    pendingTrigger.Rule,
                    pendingTrigger.Visitor,
                    pendingTrigger.TriggerCell);
                pendingTriggers[i] = pendingTrigger;
            }

            for (int i = 0; i < pendingTriggers.Count; i++)
            {
                PendingTrigger pendingTrigger = pendingTriggers[i];
                pendingTrigger.Applied |= effectSystem.ApplyNonAddFellowEffects(
                    state,
                    pendingTrigger.Unit,
                    pendingTrigger.Rule,
                    pendingTrigger.Visitor,
                    pendingTrigger.TriggerCell);
                pendingTriggers[i] = pendingTrigger;
            }

            for (int i = 0; i < pendingTriggers.Count; i++)
            {
                PendingTrigger pendingTrigger = pendingTriggers[i];
                if (!pendingTrigger.Applied)
                {
                    continue;
                }

                CommitTrigger(pendingTrigger);
            }
        }

        private void CommitTrigger(PendingTrigger pendingTrigger)
        {
            UnitInstanceState unit = pendingTrigger.Unit;
            TriggerRuleSpec rule = pendingTrigger.Rule;
            VisitorState visitor = pendingTrigger.Visitor;

            unit.Telemetry.RecordTrigger();
            unit.SpendDailyCharge();
            unit.StartCooldown(rule.CooldownSeconds);

            if (visitor != null && rule.TriggerOncePerVisitor)
            {
                unit.RecordVisitorTrigger(pendingTrigger.RuleIndex, visitor.VisitorId);
            }

            eventBus.Publish(new UnitTriggeredEvent(unit, rule, visitor, pendingTrigger.TriggerCell));
        }

        private struct PendingTrigger
        {
            public PendingTrigger(
                UnitInstanceState unit,
                TriggerRuleSpec rule,
                int ruleIndex,
                VisitorState visitor,
                Vector2Int triggerCell)
            {
                Unit = unit;
                Rule = rule;
                RuleIndex = ruleIndex;
                Visitor = visitor;
                TriggerCell = triggerCell;
                Applied = false;
            }

            public UnitInstanceState Unit { get; }
            public TriggerRuleSpec Rule { get; }
            public int RuleIndex { get; }
            public VisitorState Visitor { get; }
            public Vector2Int TriggerCell { get; }
            public bool Applied { get; set; }
        }
    }
}
