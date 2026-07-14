using System.Collections.Generic;
using UnityEngine;

namespace MochiPark.Runtime.Data
{
    [System.Serializable]
    public sealed class FrenzyConfigSpec
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private int rumbleThreshold = 6;
        [SerializeField] private float durationSeconds = 8f;
        [SerializeField] private float joyMultiplier = 2f;
        [SerializeField] private int callVisitorBonus;
        [SerializeField] private bool resetRumbleOnStart = true;
        [SerializeField] private int maxFrenzyPerDay = 1;

        public bool Enabled => enabled;
        public int RumbleThreshold => Mathf.Max(1, rumbleThreshold);
        public float DurationSeconds => Mathf.Max(0.01f, durationSeconds);
        public float JoyMultiplier => Mathf.Max(1f, joyMultiplier);
        public int CallVisitorBonus => Mathf.Max(0, callVisitorBonus);
        public bool ResetRumbleOnStart => resetRumbleOnStart;
        public int MaxFrenzyPerDay => Mathf.Max(0, maxFrenzyPerDay);
    }

    [CreateAssetMenu(
        fileName = "global_game_config",
        menuName = "Mochi Park/Data/Global Game Config")]
    public sealed class GlobalGameConfigSO : ScriptableObject
    {
        [SerializeField] private int defaultVisitorCount = 2;
        [SerializeField] private float defaultSpawnDelay = 1f;
        [SerializeField] private float defaultVisitorSpeed = 1f;
        [SerializeField] private bool defaultSpeedControlEnabled = true;
        [SerializeField] private List<float> defaultAllowedGameSpeeds = new List<float> { 1f, 2f, 3f };
        [SerializeField] private float defaultGameSpeedOnDayStart = 1f;
        [SerializeField] private int defaultUpgradeRollCount = 1;
        [SerializeField] private int defaultDraftCardCount = 3;
        [SerializeField] private bool defaultAllowPlacementBeforeRun = true;
        [SerializeField] private bool defaultAllowPlacementDuringRun;
        [SerializeField] private FrenzyConfigSpec defaultFrenzyConfig = new FrenzyConfigSpec();
        [SerializeField] private List<GameObject> defaultVisitorViewPrefabs = new List<GameObject>();
        [SerializeField] private string defaultVisitorIdleStateName = "Idle";
        [SerializeField] private string defaultVisitorRunStateName = "Run";
        [SerializeField] private float defaultVisitorAnimationCrossFadeDuration = 0.1f;
        [SerializeField] private int defaultMaxFellowCount = 4;
        [SerializeField] private float defaultFellowStackOffset = 0.45f;
        [SerializeField] private float defaultFellowScale = 0.75f;
        [SerializeField] private GameObject mochiUnitTriggeredVfxPrefab;
        [SerializeField] private Material mochiUnitSelectedMaterial;
        [SerializeField] private UIConfigSO uiConfig;
        [SerializeField] private bool useFixedRandomSeed = true;
        [SerializeField] private int fixedRandomSeed = 12345;
        [SerializeField] private bool enableEditorAutoBootstrap = true;

        public int DefaultVisitorCount => Mathf.Max(0, defaultVisitorCount);
        public float DefaultSpawnDelay => Mathf.Max(0.01f, defaultSpawnDelay);
        public float DefaultVisitorSpeed => Mathf.Max(0.01f, defaultVisitorSpeed);
        public bool DefaultSpeedControlEnabled => defaultSpeedControlEnabled;
        public IReadOnlyList<float> DefaultAllowedGameSpeeds => defaultAllowedGameSpeeds;
        public float DefaultGameSpeedOnDayStart => Mathf.Max(1f, defaultGameSpeedOnDayStart);
        public int DefaultUpgradeRollCount => Mathf.Max(0, defaultUpgradeRollCount);
        public int DefaultDraftCardCount => Mathf.Max(0, defaultDraftCardCount);
        public bool DefaultAllowPlacementBeforeRun => defaultAllowPlacementBeforeRun;
        public bool DefaultAllowPlacementDuringRun => defaultAllowPlacementDuringRun;
        public FrenzyConfigSpec DefaultFrenzyConfig => defaultFrenzyConfig;
        public IReadOnlyList<GameObject> DefaultVisitorViewPrefabs => defaultVisitorViewPrefabs;
        public string DefaultVisitorIdleStateName => defaultVisitorIdleStateName;
        public string DefaultVisitorRunStateName => defaultVisitorRunStateName;
        public float DefaultVisitorAnimationCrossFadeDuration => Mathf.Max(0f, defaultVisitorAnimationCrossFadeDuration);
        public int DefaultMaxFellowCount => Mathf.Max(0, defaultMaxFellowCount);
        public float DefaultFellowStackOffset => defaultFellowStackOffset;
        public float DefaultFellowScale => Mathf.Max(0.01f, defaultFellowScale);
        public GameObject MochiUnitTriggeredVfxPrefab => mochiUnitTriggeredVfxPrefab;
        public Material MochiUnitSelectedMaterial => mochiUnitSelectedMaterial;
        public UIConfigSO UIConfig => uiConfig;
        public bool UseFixedRandomSeed => useFixedRandomSeed;
        public int FixedRandomSeed => fixedRandomSeed;
        public bool EnableEditorAutoBootstrap => enableEditorAutoBootstrap;
    }
}
