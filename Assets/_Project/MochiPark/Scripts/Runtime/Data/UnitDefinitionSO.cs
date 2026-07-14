using System;
using System.Collections.Generic;
using MochiPark.Runtime.Core;
using UnityEngine;

namespace MochiPark.Runtime.Data
{
    [Serializable]
    public sealed class InfluencePatternSpec
    {
        [SerializeField] private string areaId = "main";
        [SerializeField] private PatternDefinitionSO pattern;

        public string AreaId => string.IsNullOrWhiteSpace(areaId) ? "main" : areaId;
        public PatternDefinitionSO Pattern => pattern;
    }

    [Serializable]
    public sealed class TriggerRuleSpec
    {
        [SerializeField] private TriggerType triggerType = TriggerType.OnMochiEnterArea;
        [SerializeField] private string targetAreaId = "main";
        [SerializeField, Range(0f, 1f)] private float chance = 1f;
        [SerializeField] private float cooldownSeconds;
        [SerializeField] private bool triggerOncePerVisitor = true;
        [SerializeField] private int dailyLimit;
        [SerializeField] private bool requiresFrenzy;
        [SerializeField] private bool requiresNotFrenzy;
        [SerializeField] private List<UnitTag> conditionTags = new List<UnitTag>();

        public TriggerType TriggerType => triggerType;
        public string TargetAreaId => string.IsNullOrWhiteSpace(targetAreaId) ? "main" : targetAreaId;
        public float Chance => Mathf.Clamp01(chance);
        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
        public bool TriggerOncePerVisitor => triggerOncePerVisitor;
        public int DailyLimit => Mathf.Max(0, dailyLimit);
        public bool RequiresFrenzy => requiresFrenzy;
        public bool RequiresNotFrenzy => requiresNotFrenzy;
        public IReadOnlyList<UnitTag> ConditionTags => conditionTags;
    }

    [Serializable]
    public sealed class EffectSpec
    {
        [SerializeField] private EffectType effectType = EffectType.AddJoy;
        [SerializeField] private int baseAmount = 1;
        [SerializeField] private bool scalesWithFellows;
        [SerializeField] private bool affectedByFrenzy = true;
        [SerializeField] private SourceCategory sourceCategory = SourceCategory.Direct;
        [SerializeField] private float durationSeconds;
        [SerializeField] private string customPayloadId;

        public EffectType EffectType => effectType;
        public int BaseAmount => baseAmount;
        public bool ScalesWithFellows => scalesWithFellows;
        public bool AffectedByFrenzy => affectedByFrenzy;
        public SourceCategory SourceCategory => sourceCategory;
        public float DurationSeconds => Mathf.Max(0f, durationSeconds);
        public string CustomPayloadId => customPayloadId;
    }

    [Serializable]
    public sealed class StorageDefinitionSpec
    {
        [SerializeField] private bool enabled;
        [SerializeField] private int capacity;
        [SerializeField] private string fillMode;
        [SerializeField] private string cashOutCondition;
        [SerializeField] private float cashOutMultiplier = 1f;
        [SerializeField] private bool affectedByFrenzy;

        public bool Enabled => enabled;
        public int Capacity => Mathf.Max(0, capacity);
        public string FillMode => fillMode;
        public string CashOutCondition => cashOutCondition;
        public float CashOutMultiplier => Mathf.Max(0f, cashOutMultiplier);
        public bool AffectedByFrenzy => affectedByFrenzy;
    }

    [CreateAssetMenu(
        fileName = "unit_new_mochi_stall",
        menuName = "Mochi Park/Data/Unit Definition")]
    public sealed class UnitDefinitionSO : ScriptableObject
    {
        [SerializeField] private string unitTypeId;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private UnitRarity rarity;
        [SerializeField] private UnitFamily family;
        [SerializeField] private List<UnitTag> tags = new List<UnitTag>();
        [SerializeField] private PatternDefinitionSO footprintPattern;
        [SerializeField] private List<InfluencePatternSpec> influencePatterns = new List<InfluencePatternSpec>();
        [SerializeField] private bool rotationAllowed = true;
        [SerializeField] private List<TriggerRuleSpec> triggerRules = new List<TriggerRuleSpec>();
        [SerializeField] private List<EffectSpec> effectPayloads = new List<EffectSpec>();
        [SerializeField] private int defaultDailyCharges;
        [SerializeField] private StorageDefinitionSpec storageDefinition = new StorageDefinitionSpec();
        [SerializeField] private GameObject viewPrefab;
        [SerializeField] private Sprite icon;
        [TextArea]
        [SerializeField] private string balanceNotes;

        public string UnitTypeId => unitTypeId;
        public string DisplayName => displayName;
        public string Description => description;
        public UnitRarity Rarity => rarity;
        public UnitFamily Family => family;
        public IReadOnlyList<UnitTag> Tags => tags;
        public PatternDefinitionSO FootprintPattern => footprintPattern;
        public IReadOnlyList<InfluencePatternSpec> InfluencePatterns => influencePatterns;
        public bool RotationAllowed => rotationAllowed;
        public IReadOnlyList<TriggerRuleSpec> TriggerRules => triggerRules;
        public IReadOnlyList<EffectSpec> EffectPayloads => effectPayloads;
        public StorageDefinitionSpec StorageDefinition => storageDefinition;
        public GameObject ViewPrefab => viewPrefab;
        public Sprite Icon => icon;
        public string BalanceNotes => balanceNotes;

        public int GetInitialDailyCharges()
        {
            if (defaultDailyCharges > 0)
            {
                return defaultDailyCharges;
            }

            int limit = 0;
            for (int i = 0; i < triggerRules.Count; i++)
            {
                int ruleLimit = triggerRules[i] == null ? 0 : triggerRules[i].DailyLimit;
                if (ruleLimit <= 0)
                {
                    continue;
                }

                limit = limit == 0 ? ruleLimit : Mathf.Min(limit, ruleLimit);
            }

            return limit > 0 ? limit : int.MaxValue;
        }
    }
}
