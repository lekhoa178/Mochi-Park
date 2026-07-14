using System.Collections.Generic;
using MochiPark.Runtime.Board;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using UnityEngine;

namespace MochiPark.Runtime.Units
{
    public static class JoyIncomeUtility
    {
        private const float PersistentModifierDuration = -1f;

        public static void RefreshDeterministicModifiers(RuntimeLevelState state, EventBus eventBus)
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0; i < state.Units.Count; i++)
            {
                state.Units[i].ClearModifiers(RuntimeModifier.JoyMultiplierModifierType);
            }

            for (int sourceIndex = 0; sourceIndex < state.Units.Count; sourceIndex++)
            {
                UnitInstanceState sourceUnit = state.Units[sourceIndex];
                IReadOnlyList<EffectSpec> effects = sourceUnit.Definition.EffectPayloads;
                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    EffectSpec effect = effects[effectIndex];
                    if (!IsDeterministicJoyModifier(effect))
                    {
                        continue;
                    }

                    ApplyModifierToOverlappingUnits(state, sourceUnit, effect);
                }
            }

            eventBus?.Publish(new DeterministicJoyIncomeChangedEvent(state));
        }

        public static int CalculatePredictableJoyIncome(
            RuntimeLevelState state,
            UnitInstanceState unit,
            string areaId)
        {
            if (state == null || unit == null)
            {
                return 0;
            }

            float total = 0f;
            IReadOnlyList<TriggerRuleSpec> rules = unit.Definition.TriggerRules;
            IReadOnlyList<EffectSpec> effects = unit.Definition.EffectPayloads;

            for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                TriggerRuleSpec rule = rules[ruleIndex];
                if (!IsPredictablePathJoyRule(rule, areaId))
                {
                    continue;
                }

                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    EffectSpec effect = effects[effectIndex];
                    if (IsPredictableJoyEffect(effect))
                    {
                        total += Mathf.Max(0, effect.BaseAmount);
                    }
                }
            }

            return Mathf.RoundToInt(ApplyJoyModifiers(unit, total));
        }

        public static float ApplyJoyModifiers(UnitInstanceState unit, float amount)
        {
            if (unit == null || amount <= 0f)
            {
                return amount;
            }

            float flat = 0f;
            float multiplier = 1f;
            IReadOnlyList<RuntimeModifier> modifiers = unit.ActiveModifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                RuntimeModifier modifier = modifiers[i];
                if (modifier.ModifierType != RuntimeModifier.JoyMultiplierModifierType)
                {
                    continue;
                }

                flat += modifier.FlatValue;
                multiplier *= Mathf.Max(0f, 1f + modifier.PercentValue);
            }

            return Mathf.Max(0f, amount + flat) * multiplier;
        }

        public static float CalculateJoyMultiplier(UnitInstanceState unit)
        {
            if (unit == null)
            {
                return 1f;
            }

            float multiplier = 1f;
            IReadOnlyList<RuntimeModifier> modifiers = unit.ActiveModifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                RuntimeModifier modifier = modifiers[i];
                if (modifier.ModifierType != RuntimeModifier.JoyMultiplierModifierType)
                {
                    continue;
                }

                multiplier *= Mathf.Max(0f, 1f + modifier.PercentValue);
            }

            return multiplier;
        }

        public static bool CanProduceJoy(UnitInstanceState unit)
        {
            return unit != null && CanProduceJoy(unit.Definition);
        }

        public static bool CanProduceJoy(UnitDefinitionSO unitDefinition)
        {
            if (unitDefinition == null)
            {
                return false;
            }

            IReadOnlyList<EffectSpec> effects = unitDefinition.EffectPayloads;
            for (int i = 0; i < effects.Count; i++)
            {
                if (IsJoyProducingEffect(effects[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyModifierToOverlappingUnits(
            RuntimeLevelState state,
            UnitInstanceState sourceUnit,
            EffectSpec effect)
        {
            IReadOnlyList<InfluencePatternSpec> patterns = sourceUnit.Definition.InfluencePatterns;
            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                InfluencePatternSpec influence = patterns[patternIndex];
                if (!IsUnitBuffInfluence(influence))
                {
                    continue;
                }

                HashSet<Vector2Int> affectedCells = new HashSet<Vector2Int>(
                    GridRotationUtility.ResolveCells(
                        influence.Pattern,
                        sourceUnit.AnchorCell,
                        sourceUnit.Rotation));

                for (int targetIndex = 0; targetIndex < state.Units.Count; targetIndex++)
                {
                    UnitInstanceState targetUnit = state.Units[targetIndex];
                    if (!FootprintOverlaps(targetUnit, affectedCells))
                    {
                        continue;
                    }

                    float multiplier = Mathf.Max(0f, effect.BaseAmount);
                    if (multiplier <= 0f)
                    {
                        continue;
                    }

                    targetUnit.AddModifier(new RuntimeModifier(
                        sourceUnit.InstanceId,
                        RuntimeModifier.JoyMultiplierModifierType,
                        0f,
                        multiplier - 1f,
                        PersistentModifierDuration));
                }
            }
        }

        private static bool FootprintOverlaps(UnitInstanceState unit, HashSet<Vector2Int> affectedCells)
        {
            if (unit == null || unit.Definition.FootprintPattern == null || affectedCells == null)
            {
                return false;
            }

            List<Vector2Int> footprintCells = GridRotationUtility.ResolveCells(
                unit.Definition.FootprintPattern,
                unit.AnchorCell,
                unit.Rotation);
            for (int i = 0; i < footprintCells.Count; i++)
            {
                if (affectedCells.Contains(footprintCells[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDeterministicJoyModifier(EffectSpec effect)
        {
            return effect != null &&
                effect.EffectType == EffectType.ApplyJoyModifier &&
                effect.DurationSeconds <= Mathf.Epsilon &&
                effect.BaseAmount > 0;
        }

        private static bool IsUnitBuffInfluence(InfluencePatternSpec influence)
        {
            return influence != null &&
                influence.Pattern != null &&
                influence.Pattern.TargetType == PatternTargetType.UnitBuff;
        }

        private static bool IsPredictablePathJoyRule(TriggerRuleSpec rule, string areaId)
        {
            // A daily limit constrains trigger frequency, not the Joy amount produced by a trigger.
            // Its remaining uses are communicated by the separate daily-limit indicator.
            if (rule == null ||
                rule.TriggerType != TriggerType.OnMochiEnterArea ||
                !string.Equals(rule.TargetAreaId, areaId) ||
                rule.Chance < 1f ||
                rule.CooldownSeconds > Mathf.Epsilon ||
                rule.RequiresFrenzy ||
                rule.RequiresNotFrenzy)
            {
                return false;
            }

            return rule.ConditionTags == null || rule.ConditionTags.Count == 0;
        }

        private static bool IsPredictableJoyEffect(EffectSpec effect)
        {
            return effect != null &&
                effect.EffectType == EffectType.AddJoy &&
                effect.SourceCategory != SourceCategory.Frenzy &&
                effect.SourceCategory != SourceCategory.Storage;
        }

        private static bool IsJoyProducingEffect(EffectSpec effect)
        {
            return effect != null && effect.EffectType == EffectType.AddJoy;
        }
    }
}
