using System.Collections.Generic;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using UnityEngine;

namespace MochiPark.Runtime.Units
{
    public sealed class StorageRuntimeState
    {
        public int CurrentAmount { get; private set; }
        public int StoredCount { get; private set; }
        public int CashOutCountToday { get; private set; }
        public bool CashedOutThisDay { get; private set; }

        public void Reset()
        {
            CurrentAmount = 0;
            StoredCount = 0;
            CashOutCountToday = 0;
            CashedOutThisDay = false;
        }

        public int Add(int amount, int capacity)
        {
            int previousAmount = CurrentAmount;
            CurrentAmount = Mathf.Clamp(CurrentAmount + Mathf.Max(0, amount), 0, Mathf.Max(0, capacity));
            int amountAdded = Mathf.Max(0, CurrentAmount - previousAmount);
            if (amountAdded > 0)
            {
                StoredCount++;
            }

            return amountAdded;
        }

        public bool IsFull(int capacity)
        {
            return capacity > 0 && CurrentAmount >= capacity;
        }

        public int CashOut(float multiplier)
        {
            int joyCreated = Mathf.RoundToInt(CurrentAmount * Mathf.Max(0f, multiplier));
            CurrentAmount = 0;
            CashOutCountToday++;
            CashedOutThisDay = true;
            return Mathf.Max(0, joyCreated);
        }
    }

    public sealed class UnitTelemetryState
    {
        public int TriggerCount { get; private set; }
        public int JoyCreated { get; private set; }
        public int RumbleCreated { get; private set; }
        public int StorageFilled { get; private set; }
        public int StorageCashedOut { get; private set; }

        public void RecordTrigger()
        {
            TriggerCount++;
        }

        public void RecordJoy(int amount)
        {
            JoyCreated += amount;
        }

        public void RecordRumble(int amount)
        {
            RumbleCreated += amount;
        }

        public void RecordStorageFill(int amount)
        {
            StorageFilled += Mathf.Max(0, amount);
        }

        public void RecordStorageCashOut()
        {
            StorageCashedOut++;
        }
    }

    public sealed class RuntimeModifier
    {
        public const string JoyMultiplierModifierType = "joy_multiplier";

        public RuntimeModifier(string sourceUnitInstanceId, string modifierType, float flatValue, float percentValue, float durationRemaining)
        {
            SourceUnitInstanceId = sourceUnitInstanceId;
            ModifierType = modifierType;
            FlatValue = flatValue;
            PercentValue = percentValue;
            DurationRemaining = durationRemaining;
        }

        public string SourceUnitInstanceId { get; }
        public string ModifierType { get; }
        public float FlatValue { get; }
        public float PercentValue { get; }
        public float DurationRemaining { get; private set; }
        public bool IsPersistent => DurationRemaining < 0f;

        public void Tick(float deltaTime)
        {
            if (IsPersistent)
            {
                return;
            }

            DurationRemaining = Mathf.Max(0f, DurationRemaining - deltaTime);
        }
    }

    public sealed class UnitInstanceState
    {
        private readonly HashSet<string> triggeredVisitorKeys = new HashSet<string>();
        private readonly Dictionary<string, int> intCounters = new Dictionary<string, int>();
        private readonly Dictionary<string, float> floatCounters = new Dictionary<string, float>();
        private readonly List<RuntimeModifier> activeModifiers = new List<RuntimeModifier>();

        public UnitInstanceState(
            string instanceId,
            UnitDefinitionSO definition,
            Vector2Int anchorCell,
            GridRotation rotation,
            bool isLockedByTutorial)
        {
            InstanceId = instanceId;
            Definition = definition;
            AnchorCell = anchorCell;
            Rotation = rotation;
            IsLockedByTutorial = isLockedByTutorial;
            Storage = new StorageRuntimeState();
            Telemetry = new UnitTelemetryState();
            ResetDailyState();
        }

        public string InstanceId { get; }
        public UnitDefinitionSO Definition { get; }
        public Vector2Int AnchorCell { get; private set; }
        public GridRotation Rotation { get; private set; }
        public bool IsLockedByTutorial { get; }
        public int RemainingDailyCharges { get; private set; }
        public float CooldownRemaining { get; private set; }
        public StorageRuntimeState Storage { get; }
        public UnitTelemetryState Telemetry { get; }
        public IReadOnlyDictionary<string, int> IntCounters => intCounters;
        public IReadOnlyDictionary<string, float> FloatCounters => floatCounters;
        public IReadOnlyList<RuntimeModifier> ActiveModifiers => activeModifiers;
        public bool HasUnlimitedDailyCharges => RemainingDailyCharges == int.MaxValue;

        public void SetPlacement(Vector2Int anchorCell, GridRotation rotation)
        {
            AnchorCell = anchorCell;
            Rotation = rotation;
        }

        public void ResetDailyState()
        {
            RemainingDailyCharges = Definition.GetInitialDailyCharges();
            CooldownRemaining = 0f;
            triggeredVisitorKeys.Clear();
            Storage.Reset();
        }

        public void Tick(float deltaTime)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);

            for (int i = activeModifiers.Count - 1; i >= 0; i--)
            {
                activeModifiers[i].Tick(deltaTime);
                if (!activeModifiers[i].IsPersistent && activeModifiers[i].DurationRemaining <= 0f)
                {
                    activeModifiers.RemoveAt(i);
                }
            }
        }

        public void ClearModifiers(string modifierType)
        {
            for (int i = activeModifiers.Count - 1; i >= 0; i--)
            {
                if (activeModifiers[i].ModifierType == modifierType)
                {
                    activeModifiers.RemoveAt(i);
                }
            }
        }

        public void AddModifier(RuntimeModifier modifier)
        {
            if (modifier != null)
            {
                activeModifiers.Add(modifier);
            }
        }

        public bool CanSpendDailyCharge()
        {
            return HasUnlimitedDailyCharges || RemainingDailyCharges > 0;
        }

        public void SpendDailyCharge()
        {
            if (!HasUnlimitedDailyCharges)
            {
                RemainingDailyCharges = Mathf.Max(0, RemainingDailyCharges - 1);
            }
        }

        public void StartCooldown(float seconds)
        {
            CooldownRemaining = Mathf.Max(CooldownRemaining, seconds);
        }

        public bool HasTriggeredForVisitor(int ruleIndex, string visitorId)
        {
            return triggeredVisitorKeys.Contains(BuildVisitorKey(ruleIndex, visitorId));
        }

        public void RecordVisitorTrigger(int ruleIndex, string visitorId)
        {
            triggeredVisitorKeys.Add(BuildVisitorKey(ruleIndex, visitorId));
        }

        private static string BuildVisitorKey(int ruleIndex, string visitorId)
        {
            return $"{ruleIndex}:{visitorId}";
        }
    }
}
