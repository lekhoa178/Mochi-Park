using MochiPark.Runtime.Audio;
using MochiPark.Runtime.Board;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace MochiPark.Runtime.Views
{
    public sealed class GameplayBootstrap : MonoBehaviour
    {
        private const string SampleCatalogResourcePath = "MochiPark/Data/Catalogs/game_catalog_test";
        private const float BoardGizmoFillAlpha = 0.18f;
        private const float BoardGizmoWireAlpha = 0.75f;
        private static readonly Color PathTileGizmoColor = new Color(1f, 0.78f, 0.32f, 1f);
        private static readonly Color StartTileGizmoColor = new Color(0.25f, 0.95f, 0.45f, 1f);
        private static readonly Color ExitTileGizmoColor = new Color(1f, 0.35f, 0.35f, 1f);
        private static readonly Color BuildableTileGizmoColor = new Color(0.3f, 0.65f, 1f, 1f);
        private static readonly Color LockedBuildableTileGizmoColor = new Color(0.45f, 0.45f, 0.55f, 1f);

        [SerializeField] private GameCatalogSO catalog;
        [SerializeField] private string startLevelId = "level_l001_test_path_joy";
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool createBoardView = true;
        [SerializeField] private bool createHudView = true;
        [SerializeField] private PanelSettings hudPanelSettings;

        private bool initialized;
        private EventBus eventBus;
        private LevelDirector levelDirector;

        public LevelDirector LevelDirector => levelDirector;
        public EventBus EventBus => eventBus;

        public void Configure(GameCatalogSO configuredCatalog, string configuredStartLevelId, bool shouldAutoStart, bool shouldCreateViews)
        {
            catalog = configuredCatalog;
            startLevelId = string.IsNullOrWhiteSpace(configuredStartLevelId) ? startLevelId : configuredStartLevelId;
            autoStart = shouldAutoStart;
            createBoardView = shouldCreateViews;
            createHudView = shouldCreateViews;

            if (Application.isPlaying)
            {
                Initialize();
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            levelDirector?.Tick(Time.unscaledDeltaTime);
        }

        private void OnDrawGizmos()
        {
            if (!TryGetConfiguredLevel(out LevelDefinitionSO level) || level.Board == null)
            {
                return;
            }

            BoardDefinitionSO board = level.Board;
            float tileSize = RuntimeBoardState.DefaultTileSizeXZ;
            float tileHeight = board.ResolveTileHeight();
            Vector3 tileSizeVector = new Vector3(tileSize, tileHeight, tileSize);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Vector3 tileCenter = new Vector3(x * tileSize, tileHeight * 0.5f, y * tileSize);
                    Color tileColor = GetBoardTileGizmoColor(board.GetCellType(cell));

                    tileColor.a = BoardGizmoFillAlpha;
                    Gizmos.color = tileColor;
                    Gizmos.DrawCube(tileCenter, tileSizeVector);

                    tileColor.a = BoardGizmoWireAlpha;
                    Gizmos.color = tileColor;
                    Gizmos.DrawWireCube(tileCenter, tileSizeVector);
                }
            }

            Gizmos.matrix = previousMatrix;
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            if (catalog == null)
            {
                catalog = Resources.Load<GameCatalogSO>(SampleCatalogResourcePath);
            }

            if (catalog == null)
            {
                Debug.LogWarning("Mochi Park bootstrap could not find a catalog.");
                return;
            }

            initialized = true;
            eventBus = new EventBus();
            MochiParkAudioManager.BindEventBus(eventBus);
            levelDirector = new LevelDirector(catalog, eventBus);
            EnsureEventSystem();

            if (createBoardView)
            {
                GameObject boardViewObject = new GameObject("Board Controller");
                boardViewObject.transform.SetParent(transform, false);
                boardViewObject.AddComponent<BoardController>().Bind(levelDirector, eventBus);
            }

            if (createHudView)
            {
                GameObject hudViewObject = new GameObject("HUD Controller");
                hudViewObject.transform.SetParent(transform, false);
            }

            if (autoStart)
            {
                string levelId = ResolveStartLevelId();
                levelDirector.StartLevel(levelId);
            }
        }

        private string ResolveStartLevelId()
        {
            if (!string.IsNullOrWhiteSpace(startLevelId))
            {
                return startLevelId;
            }

            return catalog.Levels.Count > 0 && catalog.Levels[0] != null
                ? catalog.Levels[0].LevelId
                : string.Empty;
        }

        private bool TryGetConfiguredLevel(out LevelDefinitionSO level)
        {
            level = null;
            if (catalog == null)
            {
                return false;
            }

            string levelId = ResolveStartLevelId();
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return false;
            }

            return catalog.TryGetLevel(levelId, out level) && level != null && level.Board != null;
        }

        private static Color GetBoardTileGizmoColor(BoardCellType cellType)
        {
            switch (cellType)
            {
                case BoardCellType.Start:
                    return StartTileGizmoColor;
                case BoardCellType.Exit:
                    return ExitTileGizmoColor;
                case BoardCellType.Path:
                    return PathTileGizmoColor;
                case BoardCellType.BuildableOpen:
                    return BuildableTileGizmoColor;
                case BoardCellType.BuildableLocked:
                    return LockedBuildableTileGizmoColor;
                default:
                    return Color.clear;
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("Mochi Park Event System");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            BaseInputModule[] modules = eventSystem.GetComponents<BaseInputModule>();
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != inputModule)
                {
                    modules[i].enabled = false;
                }
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }

            inputModule.enabled = true;
        }
    }
}
