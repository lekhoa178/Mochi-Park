using System;
using System.Collections.Generic;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using MochiPark.Runtime.Visitors;
using UnityEngine;

namespace MochiPark.Runtime.Units
{
    public sealed class EffectSystem
    {
        private readonly EventBus eventBus;

        public EffectSystem(EventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public bool ApplyEffects(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            TriggerRuleSpec triggerRule,
            VisitorState targetVisitor,
            Vector2Int triggerCell)
        {
            bool appliedAddFellow = ApplyEffectsMatching(
                state,
                sourceUnit,
                triggerRule,
                targetVisitor,
                triggerCell,
                true);
            bool appliedRemaining = ApplyEffectsMatching(
                state,
                sourceUnit,
                triggerRule,
                targetVisitor,
                triggerCell,
                false);
            return appliedAddFellow || appliedRemaining;
        }

        public bool ApplyAddFellowEffects(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            TriggerRuleSpec triggerRule,
            VisitorState targetVisitor,
            Vector2Int triggerCell)
        {
            return ApplyEffectsMatching(
                state,
                sourceUnit,
                triggerRule,
                targetVisitor,
                triggerCell,
                true);
        }

        public bool ApplyNonAddFellowEffects(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            TriggerRuleSpec triggerRule,
            VisitorState targetVisitor,
            Vector2Int triggerCell)
        {
            return ApplyEffectsMatching(
                state,
                sourceUnit,
                triggerRule,
                targetVisitor,
                triggerCell,
                false);
        }

        private bool ApplyEffectsMatching(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            TriggerRuleSpec triggerRule,
            VisitorState targetVisitor,
            Vector2Int triggerCell,
            bool applyAddFellow)
        {
            bool appliedAny = false;
            IReadOnlyList<EffectSpec> effects = sourceUnit.Definition.EffectPayloads;

            for (int i = 0; i < effects.Count; i++)
            {
                EffectSpec effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                if ((effect.EffectType == EffectType.AddFellow) != applyAddFellow)
                {
                    continue;
                }

                int amount = CalculateAmount(state, sourceUnit, effect, targetVisitor);
                Attribution attribution = new Attribution(
                    sourceUnit.InstanceId,
                    sourceUnit.Definition.UnitTypeId,
                    nameof(EffectSystem),
                    effect.SourceCategory,
                    effect.EffectType,
                    effect.AffectedByFrenzy && state.Frenzy.IsActive,
                    false);

                switch (effect.EffectType)
                {
                    case EffectType.AddJoy:
                        if (amount > 0)
                        {
                            CreateJoy(state, sourceUnit, effect, targetVisitor, amount, attribution);
                            appliedAny = true;
                        }
                        break;

                    case EffectType.ApplyJoyModifier:
                        break;

                    case EffectType.AddRumble:
                        if (amount > 0 && !state.Frenzy.IsActive)
                        {
                            state.AddRumble(amount);
                            sourceUnit.Telemetry.RecordRumble(amount);
                            eventBus.Publish(new EffectAppliedEvent(effect, sourceUnit, amount, attribution));
                            eventBus.Publish(new RumbleAddedEvent(amount, state.CurrentRumble, attribution));
                            TryStartFrenzy(state, attribution);
                            appliedAny = true;
                        }
                        break;

                    case EffectType.CallVisitor:
                        if (amount > 0)
                        {
                            state.CallVisitors(amount);
                            eventBus.Publish(new EffectAppliedEvent(effect, sourceUnit, amount, attribution));
                            eventBus.Publish(new VisitorCalledEvent(amount, attribution));
                            appliedAny = true;
                        }
                        break;

                    case EffectType.AddFellow:
                        if (targetVisitor != null && amount > 0)
                        {
                            int maxFellowCount = state.GlobalConfig != null
                                ? state.GlobalConfig.DefaultMaxFellowCount
                                : int.MaxValue;
                            int addedCount = targetVisitor.AddFellows(amount, maxFellowCount);
                            if (addedCount > 0)
                            {
                                eventBus.Publish(new EffectAppliedEvent(effect, sourceUnit, addedCount, attribution));
                                eventBus.Publish(new FellowAddedEvent(targetVisitor, addedCount, attribution));
                                appliedAny = true;
                            }
                        }
                        break;

                    case EffectType.ApplyLinger:
                        if (targetVisitor != null)
                        {
                            float lingerSeconds = effect.DurationSeconds > 0f
                                ? effect.DurationSeconds
                                : amount;
                            if (lingerSeconds > 0f)
                            {
                                targetVisitor.ApplyLinger(lingerSeconds);
                                eventBus.Publish(new EffectAppliedEvent(
                                    effect,
                                    sourceUnit,
                                    Mathf.RoundToInt(lingerSeconds),
                                    attribution));
                                appliedAny = true;
                            }
                        }
                        break;

                    case EffectType.FillStorage:
                        appliedAny |= TryFillStorage(state, sourceUnit, effect, targetVisitor, attribution);
                        break;

                    case EffectType.CashOutStorage:
                        appliedAny |= TryCashOutStorage(state, sourceUnit, effect, false);
                        break;

                    default:
                        break;
                }
            }

            return appliedAny;
        }

        private void CreateJoy(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            EffectSpec effect,
            VisitorState targetVisitor,
            int amount,
            Attribution attribution)
        {
            state.AddJoy(amount);
            sourceUnit.Telemetry.RecordJoy(amount);
            eventBus.Publish(new EffectAppliedEvent(effect, sourceUnit, amount, attribution));
            int floatingScoreCount = effect.ScalesWithFellows && targetVisitor != null
                ? targetVisitor.EffectiveVisitorCount
                : 1;
            eventBus.Publish(new JoyCreatedEvent(amount, state.TotalJoy, attribution, floatingScoreCount));
        }

        private void TryStartFrenzy(RuntimeLevelState state, Attribution sourceAttribution)
        {
            FrenzyConfigSpec config = state.CurrentDayConfig == null
                ? null
                : state.CurrentDayConfig.FrenzyConfig;
            if (config == null || !state.TryStartFrenzy(config))
            {
                return;
            }

            eventBus.Publish(new FrenzyStartedEvent(state));
            if (config.CallVisitorBonus <= 0)
            {
                return;
            }

            Attribution callAttribution = new Attribution(
                sourceAttribution.SourceUnitInstanceId,
                sourceAttribution.SourceUnitTypeId,
                nameof(EffectSystem),
                SourceCategory.Frenzy,
                EffectType.CallVisitor,
                false,
                false);
            eventBus.Publish(new VisitorCalledEvent(config.CallVisitorBonus, callAttribution));
        }

        private bool TryFillStorage(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            EffectSpec effect,
            VisitorState targetVisitor,
            Attribution attribution)
        {
            StorageDefinitionSpec storage = sourceUnit.Definition.StorageDefinition;
            if (storage == null || !storage.Enabled || storage.Capacity <= 0)
            {
                return false;
            }

            int amount = CalculateAmount(state, sourceUnit, effect, targetVisitor);
            int amountAdded = sourceUnit.Storage.Add(amount, storage.Capacity);
            if (amountAdded <= 0)
            {
                return false;
            }

            sourceUnit.Telemetry.RecordStorageFill(amountAdded);
            eventBus.Publish(new EffectAppliedEvent(effect, sourceUnit, amountAdded, attribution));
            eventBus.Publish(new StorageFilledEvent(
                sourceUnit,
                amountAdded,
                sourceUnit.Storage.CurrentAmount,
                storage.Capacity,
                attribution));

            if (sourceUnit.Storage.IsFull(storage.Capacity) && ShouldAutoCashOut(storage))
            {
                TryCashOutStorage(state, sourceUnit, effect, true);
            }

            return true;
        }

        private bool TryCashOutStorage(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            EffectSpec effect,
            bool fromAutoFull)
        {
            StorageDefinitionSpec storage = sourceUnit.Definition.StorageDefinition;
            if (storage == null || !storage.Enabled || sourceUnit.Storage.CurrentAmount <= 0)
            {
                return false;
            }

            if (!CanCashOut(storage, sourceUnit, fromAutoFull))
            {
                return false;
            }

            bool affectedByFrenzy = storage.AffectedByFrenzy && state.Frenzy.IsActive;
            int joyCreated = sourceUnit.Storage.CashOut(storage.CashOutMultiplier);
            if (affectedByFrenzy)
            {
                joyCreated = Mathf.RoundToInt(joyCreated * state.Frenzy.JoyMultiplier);
            }

            if (joyCreated <= 0)
            {
                return false;
            }

            Attribution attribution = new Attribution(
                sourceUnit.InstanceId,
                sourceUnit.Definition.UnitTypeId,
                nameof(EffectSystem),
                SourceCategory.Storage,
                EffectType.CashOutStorage,
                affectedByFrenzy,
                true);

            state.AddJoy(joyCreated);
            sourceUnit.Telemetry.RecordJoy(joyCreated);
            sourceUnit.Telemetry.RecordStorageCashOut();
            eventBus.Publish(new EffectAppliedEvent(effect, sourceUnit, joyCreated, attribution));
            eventBus.Publish(new StorageCashedOutEvent(sourceUnit, joyCreated, attribution));
            eventBus.Publish(new JoyCreatedEvent(joyCreated, state.TotalJoy, attribution));
            return true;
        }

        private static bool ShouldAutoCashOut(StorageDefinitionSpec storage)
        {
            return IsCondition(storage.CashOutCondition, "on_full") ||
                IsCondition(storage.CashOutCondition, "full") ||
                IsCondition(storage.CashOutCondition, "capacity");
        }

        private static bool CanCashOut(StorageDefinitionSpec storage, UnitInstanceState sourceUnit, bool fromAutoFull)
        {
            if (fromAutoFull)
            {
                return sourceUnit.Storage.IsFull(storage.Capacity);
            }

            string condition = storage.CashOutCondition;
            if (string.IsNullOrWhiteSpace(condition) ||
                IsCondition(condition, "manual") ||
                IsCondition(condition, "on_trigger") ||
                IsCondition(condition, "always"))
            {
                return true;
            }

            return ShouldAutoCashOut(storage) && sourceUnit.Storage.IsFull(storage.Capacity);
        }

        private static bool IsCondition(string condition, string expected)
        {
            return string.Equals(condition, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static int CalculateAmount(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            EffectSpec effect,
            VisitorState targetVisitor)
        {
            float amount = effect.BaseAmount;

            if (effect.ScalesWithFellows && targetVisitor != null)
            {
                amount *= targetVisitor.EffectiveVisitorCount;
            }

            if (effect.AffectedByFrenzy && state.Frenzy.IsActive)
            {
                amount *= state.Frenzy.JoyMultiplier;
            }

            if (effect.EffectType == EffectType.AddJoy)
            {
                amount = JoyIncomeUtility.ApplyJoyModifiers(sourceUnit, amount);
            }

            return Mathf.RoundToInt(Mathf.Max(0f, amount));
        }
    }
}
