using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MochiPark.Runtime.Audio;
using MochiPark.Runtime.Board;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using MochiPark.Runtime.Units;
using MochiPark.Runtime.Visitors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MochiPark.Runtime.Views
{
    public sealed class BoardController : MonoBehaviour
    {
        private const int MousePointerId = -1;
        private const float DefaultUnitHeightOffset = 0.42f;
        private const float BuildableSpriteSelectionFadeDuration = 0.18f;
        private const float UnitPickRayDistance = 1000f;
        private const float DragStartScreenDistance = 8f;
        private const float DefaultUnitDragSmoothTime = 0.02f;
        private const float DefaultVisitorHeightOffset = 0.55f;
        private const float VisitorMinimumMoveSpeed = 0.01f;
        private const float VisitorReachedTileCenterDistance = 0.01f;
        private const float VisitorExitFadeDuration = 0.4f;
        private const float MochiUnitTriggeredVfxLifetime = 8f;
        private const float CountdownIntroDuration = 5f;
        private const float FrenzyIntroVfxDuration = 4f;
        private const float ResultVfxDuration = 5f;
        private const float OverlayVfxDestroyLingerDuration = 10f;
        private const float FloatingScoreSpawnDelay = 0.2f;
        private const float VisibleJoyMultiplierEpsilon = 0.001f;
        private const string DefaultLobbySceneName = "Lobby";
        private const string ResultCanvasResourcePath = "MochiPark/UI/Result/Canvas_Result";
        private const string ResultVfxResourcePath = "MochiPark/UI/OverlayVFX/VFX_Result";
        private const string PathJoyIndicatorKeyPrefix = "path-joy:";
        private const string JoyMultiplierIndicatorKeyPrefix = "unit-joy-multiplier:";
        private const string DailyLimitIndicatorKeyPrefix = "unit-daily-limit:";
        private const float VisitorVisualYawOffset = 180f;
        private static readonly Vector2 JoyMultiplierIndicatorScreenOffset = new Vector2(0f, 42f);
        private static readonly Vector2 DailyLimitIndicatorScreenOffset = new Vector2(0f, -15f);
        private static readonly Vector2 EditPanelIndicatorScreenOffset = new Vector2(0f, -90f);
        private static readonly int MaterialBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MaterialColorId = Shader.PropertyToID("_Color");
        private readonly Dictionary<string, GameObject> visitorViews = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, float> visitorSurfaceOffsets = new Dictionary<string, float>();
        private readonly Dictionary<string, Coroutine> visitorMoveCoroutines = new Dictionary<string, Coroutine>();
        private readonly Dictionary<string, Queue<Vector2Int>> visitorMoveTargetQueues =
            new Dictionary<string, Queue<Vector2Int>>();
        private readonly Dictionary<string, List<PendingMochiUnitTriggeredVfx>> pendingMochiUnitTriggeredVfxByVisitor =
            new Dictionary<string, List<PendingMochiUnitTriggeredVfx>>();
        private readonly List<FloatingScoreSequenceState> pendingFloatingScoreSequences =
            new List<FloatingScoreSequenceState>();
        private readonly List<GameObject> unitViews = new List<GameObject>();
        private readonly Dictionary<GameObject, string> unitInstanceIdsByView = new Dictionary<GameObject, string>();
        private readonly Dictionary<string, GameObject> unitViewsByInstanceId = new Dictionary<string, GameObject>();
        private readonly HashSet<string> visibleJoyMultiplierIndicatorKeys = new HashSet<string>();
        private readonly HashSet<string> visibleDailyLimitIndicatorKeys = new HashSet<string>();
        private readonly List<string> joyMultiplierIndicatorRemovalBuffer = new List<string>();
        private readonly List<string> dailyLimitIndicatorRemovalBuffer = new List<string>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> buildableSpriteRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> lockedBuildableSpriteRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> previewSpriteRenderers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, Color> buildableSpriteBaseColors = new Dictionary<Vector2Int, Color>();
        private readonly Dictionary<Vector2Int, Color> previewSpriteBaseColors = new Dictionary<Vector2Int, Color>();
        private readonly HashSet<Vector2Int> previewFootprintCells = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, AffectedPreviewKind> previewAffectedCells =
            new Dictionary<Vector2Int, AffectedPreviewKind>();
        private LevelDirector director;
        private EventBus eventBus;
        private CameraController cameraController;
        private BoardTileSpriteOrdering tileSpriteOrdering;
        private Camera mainCamera;
        private Transform tileRoot;
        private Transform unitRoot;
        private Transform visitorRoot;
        private Transform fellowVisualRoot;
        private Transform vfxRoot;
        private FellowVisitorVisualManager fellowVisualManager;
        private FloatingScoreView floatingScoreView;
        private ScreenspaceIndicatorView screenspaceIndicatorView;
        private HudView hudView;
        private DraftView draftView;
        private ResultView resultView;
        private UIVFXCanvasView uiVfxCanvasView;
        private JoyFlyVFXController joyFlyVfxController;
        private ScreenOverlayCameraController screenOverlayCameraController;
        private GameObject activeFrenzyMomentVfx;
        private Coroutine countdownIntroCoroutine;
        private Material pathMaterial;
        private Material buildableMaterial;
        private Material lockedMaterial;
        private Material unitMaterial;
        private Material visitorMaterial;
        private GameObject draggedUnitView;
        private GameObject pressedUnitView;
        private GameObject selectedUnitView;
        private UnitOutlineSelectionState selectedOutlineState;
        private string selectedUnitInstanceId;
        private Vector2Int selectedUnitPreviewAnchorCell;
        private GridRotation selectedUnitPreviewRotation;
        private Vector2 pressScreenPosition;
        private Vector3 draggedUnitVisualVelocity;
        private Coroutine buildableSpriteFadeCoroutine;
        private float buildableSpriteFadeAlpha = 1f;
        private int activePointerId = MousePointerId;
        private int pressedPointerId = MousePointerId;
        private bool pressedUnitWasSelected;
        private bool hasSelectedUnitPreviewAnchor;
        private bool hasSelectedUnitPreviewRotation;
        private bool isShovelModeActive;
        private bool isShovelCursorApplied;
        private bool isOpeningPark;
        private bool isCountdownIntroPlaying;
        private bool hasPlayedCountdownIntro;
        private bool isDraftSuppressedForUnitDrag;

        [Header("Unit Drag")]
        [SerializeField, Min(0f)] private float unitDragSmoothTime = DefaultUnitDragSmoothTime;

        [Header("Scene Loading")]
        [SerializeField] private string lobbySceneName = DefaultLobbySceneName;
        [SerializeField] private GameObject transitionScreenPrefab;
        [SerializeField] private float minimumLoadingSeconds = 0.35f;
        [SerializeField] private float transitionEventTimeoutSeconds = 3f;
        
        public void Bind(LevelDirector boundDirector, EventBus boundEventBus)
        {
            director = boundDirector;
            eventBus = boundEventBus;
            cameraController = GetComponent<CameraController>();
            if (cameraController == null)
            {
                cameraController = gameObject.AddComponent<CameraController>();
            }

            cameraController.Initialize();
            tileSpriteOrdering = GetComponent<BoardTileSpriteOrdering>();
            if (tileSpriteOrdering == null)
            {
                tileSpriteOrdering = gameObject.AddComponent<BoardTileSpriteOrdering>();
            }

            mainCamera = Camera.main;
            CreateMaterials();
            CreateRoots();
            Subscribe();
        }

        private void Update()
        {
            if (director == null || director.State == null)
            {
                return;
            }

            if (!CanEditBoard())
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                RequestOpenParkWithIntro();
                return;
            }

            if (draggedUnitView == null &&
                pressedUnitView == null &&
                keyboard != null &&
                keyboard.jKey.wasPressedThisFrame)
            {
                director.TryRerollUpgrade();
                return;
            }

            if (draggedUnitView == null &&
                pressedUnitView == null &&
                keyboard != null &&
                keyboard.rKey.wasPressedThisFrame)
            {
                RotateSelectedUnit();
                return;
            }

            if (draggedUnitView == null &&
                pressedUnitView == null &&
                keyboard != null &&
                keyboard.vKey.wasPressedThisFrame)
            {
                ConfirmSelectedUnitEdit();
                return;
            }

            if (draggedUnitView == null &&
                pressedUnitView == null &&
                keyboard != null &&
                keyboard.cKey.wasPressedThisFrame)
            {
                CancelSelectedUnitEdit();
                return;
            }

            if (draggedUnitView == null &&
                pressedUnitView == null &&
                TryGetPressedDraftCardIndex(keyboard, out int draftCardIndex))
            {
                SelectDraftCard(draftCardIndex);
                return;
            }

            if (draggedUnitView == null &&
                pressedUnitView == null &&
                keyboard != null &&
                keyboard.zKey.wasPressedThisFrame)
            {
                ToggleShovelMode();
                return;
            }

            if (draggedUnitView == null)
            {
                if (pressedUnitView != null)
                {
                    UpdatePressedUnit();
                    return;
                }

                if (TryGetPointerDown(out Vector2 screenPosition, out int pointerId))
                {
                    HandlePointerDown(screenPosition, pointerId);
                }

                return;
            }

            if (TryGetPointerPosition(activePointerId, out Vector2 currentPosition))
            {
                PreviewDraggedUnit(currentPosition);
            }

            if (TryGetPointerUp(activePointerId, out Vector2 releasePosition))
            {
                EndUnitDrag(releasePosition);
            }
        }

        private void LateUpdate()
        {
            fellowVisualManager?.LateUpdateFellows();
            RefreshDailyLimitIndicators();
            RefreshJoyMultiplierIndicators();
            RefreshSelectedUnitEditPanel();
        }

        private void OnDestroy()
        {
            isOpeningPark = false;
            StopCountdownIntro(true);
            StopFrenzyMomentVfx(true);
            StopPendingFloatingScores();
            ClearJoyMultiplierIndicators();
            ClearDailyLimitIndicators();
            screenspaceIndicatorView?.HideEditPanelImmediate();
            joyFlyVfxController?.Unbind();
            if (draftView != null)
            {
                draftView.DraftCardSelected -= SelectDraftCard;
                draftView.DraftCardDragStarted -= OnDraftCardDragStarted;
                draftView.DraftCardDragged -= OnDraftCardDragged;
                draftView.DraftCardDragEnded -= OnDraftCardDragEnded;
                draftView.SkipRequested -= SkipDraftForDay;
            }

            draftView?.Unbind();
            if (hudView != null)
            {
                hudView.ShovelToggleRequested -= ToggleShovelMode;
                hudView.OpenParkRequested -= RequestOpenParkWithIntro;
                hudView.ExitRequested -= RequestExitToLobby;
            }

            hudView?.Unbind();
            if (resultView != null)
            {
                resultView.RetryRequested -= RequestRetryLevel;
                resultView.ExitRequested -= RequestExitToLobby;
            }

            ApplyShovelCursor(false);

            if (eventBus == null)
            {
                return;
            }

            eventBus.LevelStarted -= OnLevelStarted;
            eventBus.DayPrepared -= OnDayPrepared;
            eventBus.PlacementChanged -= OnPlacementChanged;
            eventBus.DeterministicJoyIncomeChanged -= OnDeterministicJoyIncomeChanged;
            eventBus.DraftOffered -= OnDraftOffered;
            eventBus.DraftPicked -= OnDraftPicked;
            eventBus.DraftCompletedForDay -= OnDraftCompletedForDay;
            eventBus.BoardCellUnlocked -= OnBoardCellUnlocked;
            eventBus.ParkOpened -= OnParkOpened;
            eventBus.VisitorSpawned -= OnVisitorSpawned;
            eventBus.VisitorEnteredCell -= OnVisitorEnteredCell;
            eventBus.FellowAdded -= OnFellowAdded;
            eventBus.UnitTriggered -= OnUnitTriggered;
            eventBus.JoyCreated -= OnJoyCreated;
            eventBus.FrenzyStarted -= OnFrenzyStarted;
            eventBus.FrenzyEnded -= OnFrenzyEnded;
            eventBus.DayEnded -= OnDayEnded;
        }

        public void RequestOpenParkWithIntro()
        {
            if (!CanOpenPark())
            {
                return;
            }

            StartCoroutine(OpenParkAfterCameraIntro());
        }

        private static bool TryGetPressedDraftCardIndex(Keyboard keyboard, out int cardIndex)
        {
            cardIndex = -1;
            if (keyboard == null)
            {
                return false;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                cardIndex = 0;
                return true;
            }

            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                cardIndex = 1;
                return true;
            }

            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                cardIndex = 2;
                return true;
            }

            return false;
        }

        private void SelectDraftCard(int cardIndex)
        {
            TrySelectDraftCard(cardIndex, true, out _);
        }

        private bool TrySelectDraftCard(int cardIndex, bool closeDraft, out GameObject unitView)
        {
            unitView = null;
            if (!CanEditBoard() || director == null)
            {
                return false;
            }

            if (!director.TrySelectDraftOfferCard(cardIndex, out UnitInstanceState placedUnit) || placedUnit == null)
            {
                return false;
            }

            if (closeDraft)
            {
                draftView?.Close();
            }

            if (!unitViewsByInstanceId.TryGetValue(placedUnit.InstanceId, out unitView) || unitView == null)
            {
                RefreshUnits();
                unitViewsByInstanceId.TryGetValue(placedUnit.InstanceId, out unitView);
            }

            if (unitView != null)
            {
                SelectUnit(unitView, placedUnit.InstanceId);
                return true;
            }

            return false;
        }

        private void OnDraftCardDragStarted(int cardIndex, Vector2 screenPosition, int pointerId)
        {
            if (draggedUnitView != null || pressedUnitView != null)
            {
                return;
            }

            if (!TrySelectDraftCard(cardIndex, true, out GameObject unitView) || unitView == null)
            {
                return;
            }

            draggedUnitView = unitView;
            draggedUnitVisualVelocity = Vector3.zero;
            activePointerId = pointerId;
            ClearPressedUnitState();
            screenspaceIndicatorView?.HideEditPanel();
            PreviewDraggedUnit(screenPosition);
        }

        private void OnDraftCardDragged(int cardIndex, Vector2 screenPosition, int pointerId)
        {
            if (draggedUnitView == null || activePointerId != pointerId)
            {
                return;
            }

            PreviewDraggedUnit(screenPosition);
        }

        private void OnDraftCardDragEnded(int cardIndex, Vector2 screenPosition, int pointerId)
        {
            if (draggedUnitView == null || activePointerId != pointerId)
            {
                return;
            }

            EndUnitDrag(screenPosition);
        }

        private void SkipDraftForDay()
        {
            if (!CanEditBoard() || director == null || director.State == null)
            {
                return;
            }

            if (director.State.Draft.HasPendingPick)
            {
                CancelDraftSelection();
            }

            director.SkipDraftForDay();
            draftView?.Close();
        }

        private void ConfirmSelectedUnitEdit()
        {
            if (!CanEditBoard())
            {
                return;
            }

            if (TryConfirmPendingDraftPlacement(true) || HasPendingDraftSelection())
            {
                return;
            }

            if (!TryGetSelectedUnit(out UnitInstanceState unit) ||
                !TryCommitSelectedUnitPreview(unit, true))
            {
                return;
            }

            DeselectUnit();
        }

        private bool TryCommitSelectedUnitPreview(UnitInstanceState unit, bool showInvalidPopup)
        {
            if (director == null || director.PlacementSystem == null || unit == null)
            {
                return false;
            }

            Vector2Int targetAnchorCell = GetSelectedUnitPreviewAnchor(unit);
            GridRotation targetRotation = GetSelectedUnitPreviewRotation(unit);
            PlacementResult placement = director.PlacementSystem.CanPlace(
                unit.Definition,
                targetAnchorCell,
                targetRotation,
                unit.InstanceId);

            if (!placement.IsValid)
            {
                if (showInvalidPopup && eventBus != null)
                {
                    eventBus.Publish(new PopupRequestedEvent("Placement rejected", placement.Reason.ToString()));
                }

                RefreshSelectionPreviewSprites();
                return false;
            }

            if ((targetAnchorCell != unit.AnchorCell || targetRotation != unit.Rotation) &&
                !director.PlacementSystem.MoveUnit(unit.InstanceId, targetAnchorCell, targetRotation))
            {
                return false;
            }

            return true;
        }

        private bool TryConfirmPendingDraftPlacement(bool showInvalidPopup)
        {
            if (!CanEditBoard() ||
                director == null ||
                director.State == null ||
                director.PlacementSystem == null ||
                !director.State.Draft.HasPendingPick)
            {
                return false;
            }

            string pendingUnitInstanceId = director.State.Draft.PendingUnitInstanceId;
            UnitInstanceState pendingUnit = director.State.FindUnit(pendingUnitInstanceId);
            if (pendingUnit == null)
            {
                director.CancelDraftSelection();
                return false;
            }

            Vector2Int targetAnchorCell = selectedUnitInstanceId == pendingUnitInstanceId && hasSelectedUnitPreviewAnchor
                ? selectedUnitPreviewAnchorCell
                : pendingUnit.AnchorCell;
            GridRotation targetRotation = selectedUnitInstanceId == pendingUnitInstanceId && hasSelectedUnitPreviewRotation
                ? selectedUnitPreviewRotation
                : pendingUnit.Rotation;
            PlacementResult placement = director.PlacementSystem.CanPlace(
                pendingUnit.Definition,
                targetAnchorCell,
                targetRotation,
                pendingUnit.InstanceId);

            if (!placement.IsValid)
            {
                if (showInvalidPopup && eventBus != null)
                {
                    eventBus.Publish(new PopupRequestedEvent("Placement rejected", placement.Reason.ToString()));
                }

                RefreshSelectionPreviewSprites();
                return false;
            }

            if ((targetAnchorCell != pendingUnit.AnchorCell || targetRotation != pendingUnit.Rotation) &&
                !director.PlacementSystem.MoveUnit(pendingUnit.InstanceId, targetAnchorCell, targetRotation))
            {
                return false;
            }

            if (!director.ConfirmDraftPlacement())
            {
                return false;
            }

            DeselectUnit();
            return true;
        }

        private bool HasPendingDraftSelection()
        {
            return director != null &&
                director.State != null &&
                director.State.Draft.HasPendingPick;
        }

        private void CancelDraftSelection()
        {
            if (director == null || director.State == null || !director.State.Draft.HasPendingPick)
            {
                return;
            }

            bool wasSelected = selectedUnitInstanceId == director.State.Draft.PendingUnitInstanceId;
            if (director.CancelDraftSelection() && wasSelected)
            {
                DeselectUnit();
            }
        }

        private void CancelSelectedUnitEdit()
        {
            if (!CanEditBoard())
            {
                return;
            }

            if (HasPendingDraftSelection())
            {
                CancelDraftSelection();
                return;
            }

            if (selectedUnitView != null)
            {
                DeselectUnit();
            }
        }

        private void Subscribe()
        {
            eventBus.LevelStarted += OnLevelStarted;
            eventBus.DayPrepared += OnDayPrepared;
            eventBus.PlacementChanged += OnPlacementChanged;
            eventBus.DeterministicJoyIncomeChanged += OnDeterministicJoyIncomeChanged;
            eventBus.DraftOffered += OnDraftOffered;
            eventBus.DraftPicked += OnDraftPicked;
            eventBus.DraftCompletedForDay += OnDraftCompletedForDay;
            eventBus.BoardCellUnlocked += OnBoardCellUnlocked;
            eventBus.ParkOpened += OnParkOpened;
            eventBus.VisitorSpawned += OnVisitorSpawned;
            eventBus.VisitorEnteredCell += OnVisitorEnteredCell;
            eventBus.FellowAdded += OnFellowAdded;
            eventBus.UnitTriggered += OnUnitTriggered;
            eventBus.JoyCreated += OnJoyCreated;
            eventBus.FrenzyStarted += OnFrenzyStarted;
            eventBus.FrenzyEnded += OnFrenzyEnded;
            eventBus.DayEnded += OnDayEnded;
        }

        private void CreateRoots()
        {
            tileRoot = new GameObject("Tiles").transform;
            tileRoot.SetParent(transform, false);
            unitRoot = new GameObject("Units").transform;
            unitRoot.SetParent(transform, false);
            visitorRoot = new GameObject("Visitors").transform;
            visitorRoot.SetParent(transform, false);
            fellowVisualRoot = new GameObject("FellowVisualRoot").transform;
            fellowVisualRoot.SetParent(transform, false);
            vfxRoot = new GameObject("VFX").transform;
            vfxRoot.SetParent(transform, false);
            fellowVisualManager = new FellowVisitorVisualManager(
                fellowVisualRoot,
                GetMaxFellowCount(),
                GetFellowStackOffset(),
                GetFellowScale(),
                GetVisitorIdleStateName,
                GetVisitorAnimationCrossFadeDuration);
        }

        private void BuildBoard(RuntimeBoardState board)
        {
            StopCountdownIntro(true);
            StopFrenzyMomentVfx(true);
            ClearChildren(tileRoot);
            ClearChildren(vfxRoot);
            fellowVisualManager?.Clear();
            visibleJoyMultiplierIndicatorKeys.Clear();
            visibleDailyLimitIndicatorKeys.Clear();
            joyMultiplierIndicatorRemovalBuffer.Clear();
            dailyLimitIndicatorRemovalBuffer.Clear();
            floatingScoreView?.Clear();
            screenspaceIndicatorView?.Clear();
            buildableSpriteRenderers.Clear();
            lockedBuildableSpriteRenderers.Clear();
            previewSpriteRenderers.Clear();
            buildableSpriteBaseColors.Clear();
            previewSpriteBaseColors.Clear();
            selectedUnitView = null;
            selectedOutlineState = null;
            selectedUnitInstanceId = null;
            hasSelectedUnitPreviewAnchor = false;
            hasSelectedUnitPreviewRotation = false;
            buildableSpriteFadeAlpha = 0f;

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    BoardCellType type = board.GetCellType(cell);
                    GameObject tile = CreateTile(board, cell, type);
                    if (tile != null)
                    {
                        tile.transform.SetParent(tileRoot, false);
                    }

                    CreateBuildableSprite(board, cell, type);
                    CreateLockedBuildableSprite(board, cell, type);
                }
            }

            RefreshUnits();
            RefreshPathTileJoyDisplays();
            cameraController.FrameBoard(board);
        }

        private GameObject CreateTile(RuntimeBoardState board, Vector2Int cell, BoardCellType type)
        {
            BoardTilePrefabSelection prefabSelection = board.Definition.TilePrefabSet != null
                ? board.Definition.TilePrefabSet.GetPrefabSelection(type, board.Definition.GetPathEdgeMask(cell))
                : new BoardTilePrefabSelection(null, Quaternion.identity);
            GameObject prefab = prefabSelection.Prefab;

            GameObject tile = prefab != null
                ? Instantiate(prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            tile.name = $"Cell {cell.x},{cell.y} {type}";
            tile.transform.rotation = GetTileRotation(board, cell, type, prefabSelection) * tile.transform.rotation;

            if (prefab != null)
            {
                PositionPrefabTile(tile, board, cell);
            }
            else
            {
                tile.transform.position = new Vector3(cell.x * board.TileSizeXZ, board.TileHeightResolved * 0.5f, cell.y * board.TileSizeXZ);
                tile.transform.localScale = new Vector3(board.TileSizeXZ, board.TileHeightResolved, board.TileSizeXZ);
            }

            Renderer renderer = tile.GetComponentInChildren<Renderer>();
            if (renderer != null && prefab == null)
            {
                renderer.sharedMaterial = GetMaterial(type);
            }

            return tile;
        }

        private static void PositionPrefabTile(GameObject tile, RuntimeBoardState board, Vector2Int cell)
        {
            tile.transform.position = new Vector3(cell.x * board.TileSizeXZ, 0f, cell.y * board.TileSizeXZ);
            if (BoardPrefabBoundsUtility.TryGetBounds(tile, out Bounds bounds))
            {
                tile.transform.position += Vector3.up * (board.TileSurfaceY - bounds.max.y);
            }
        }

        private static Quaternion GetTileRotation(
            RuntimeBoardState board,
            Vector2Int cell,
            BoardCellType type,
            BoardTilePrefabSelection prefabSelection)
        {
            return TryGetPathEndpointRotation(board, cell, type, out Quaternion endpointRotation)
                ? endpointRotation
                : prefabSelection.Rotation;
        }

        private static bool TryGetPathEndpointRotation(
            RuntimeBoardState board,
            Vector2Int cell,
            BoardCellType type,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            IReadOnlyList<Vector2Int> pathCells = board.PathCells;
            if (pathCells.Count < 2)
            {
                return false;
            }

            Vector2Int direction;
            switch (type)
            {
                case BoardCellType.Start:
                    if (pathCells[0] != cell)
                    {
                        return false;
                    }

                    direction = pathCells[1] - pathCells[0];
                    break;
                case BoardCellType.Exit:
                    int lastPathIndex = pathCells.Count - 1;
                    if (pathCells[lastPathIndex] != cell)
                    {
                        return false;
                    }

                    Vector2Int incomingDirection = pathCells[lastPathIndex] - pathCells[lastPathIndex - 1];
                    direction = GetExitMoveOutDirection(board, cell, incomingDirection);
                    break;
                default:
                    return false;
            }

            Vector3 worldDirection = new Vector3(direction.x, 0f, direction.y);
            if (worldDirection.sqrMagnitude < Mathf.Epsilon)
            {
                return false;
            }

            Vector3 prefabDirection = type == BoardCellType.Exit ? Vector3.left : Vector3.right;
            rotation = Quaternion.FromToRotation(prefabDirection, worldDirection.normalized);
            return true;
        }

        private static Vector2Int GetExitMoveOutDirection(
            RuntimeBoardState board,
            Vector2Int exitCell,
            Vector2Int incomingDirection)
        {
            // At a corner, continuing straight takes precedence over the other board edge.
            if (exitCell.x == 0 && incomingDirection == Vector2Int.left)
            {
                return Vector2Int.left;
            }

            if (exitCell.x == board.Width - 1 && incomingDirection == Vector2Int.right)
            {
                return Vector2Int.right;
            }

            if (exitCell.y == 0 && incomingDirection == Vector2Int.down)
            {
                return Vector2Int.down;
            }

            if (exitCell.y == board.Height - 1 && incomingDirection == Vector2Int.up)
            {
                return Vector2Int.up;
            }

            if (exitCell.x == 0)
            {
                return Vector2Int.left;
            }

            if (exitCell.x == board.Width - 1)
            {
                return Vector2Int.right;
            }

            if (exitCell.y == 0)
            {
                return Vector2Int.down;
            }

            if (exitCell.y == board.Height - 1)
            {
                return Vector2Int.up;
            }

            // Interior exits have no board-edge move-out direction, so preserve the path heading.
            return incomingDirection;
        }

        private void CreateBuildableSprite(RuntimeBoardState board, Vector2Int cell, BoardCellType type)
        {
            Sprite sprite = GetConfiguredHighlightSprite(board, cell, type);
            if (sprite == null)
            {
                return;
            }

            SpriteRenderer spriteRenderer = CreateBuildableSpriteRenderer(board, cell, type, sprite);
            Color baseColor = GetConfiguredHighlightSpriteColor(board, cell, type);
            buildableSpriteBaseColors[cell] = baseColor;
            ApplyBuildableSpriteColor(spriteRenderer, baseColor);
            spriteRenderer.gameObject.SetActive(false);
        }

        private SpriteRenderer EnsureBuildableSpriteRenderer(RuntimeBoardState board, Vector2Int cell, Sprite sprite)
        {
            if (buildableSpriteRenderers.TryGetValue(cell, out SpriteRenderer spriteRenderer) && spriteRenderer != null)
            {
                return spriteRenderer;
            }

            BoardCellType type = board.GetCellType(cell);
            return CreateBuildableSpriteRenderer(board, cell, type, sprite);
        }

        private SpriteRenderer EnsurePreviewSpriteRenderer(RuntimeBoardState board, Vector2Int cell, Sprite sprite)
        {
            if (previewSpriteRenderers.TryGetValue(cell, out SpriteRenderer spriteRenderer) && spriteRenderer != null)
            {
                return spriteRenderer;
            }

            BoardCellType type = board.GetCellType(cell);
            return CreatePreviewSpriteRenderer(board, cell, type, sprite);
        }

        private void CreateLockedBuildableSprite(RuntimeBoardState board, Vector2Int cell, BoardCellType type)
        {
            Sprite sprite = GetLockedBuildableSprite(board, cell, type);
            if (sprite == null)
            {
                return;
            }

            SpriteRenderer spriteRenderer = CreateLockedBuildableSpriteRenderer(board, cell, type, sprite);
            spriteRenderer.gameObject.SetActive(true);
        }

        private SpriteRenderer CreateBuildableSpriteRenderer(
            RuntimeBoardState board,
            Vector2Int cell,
            BoardCellType type,
            Sprite sprite)
        {
            GameObject spriteObject = new GameObject($"Cell {cell.x},{cell.y} {type} Sprite");
            spriteObject.transform.SetParent(tileRoot, false);

            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            tileSpriteOrdering.ConfigureSprite(spriteRenderer, board, cell, sprite, BoardTileSpriteLayer.Buildable);
            spriteObject.SetActive(false);
            buildableSpriteRenderers[cell] = spriteRenderer;
            buildableSpriteBaseColors[cell] = Color.white;
            return spriteRenderer;
        }

        private SpriteRenderer CreatePreviewSpriteRenderer(
            RuntimeBoardState board,
            Vector2Int cell,
            BoardCellType type,
            Sprite sprite)
        {
            GameObject spriteObject = new GameObject($"Cell {cell.x},{cell.y} {type} Preview Sprite");
            spriteObject.transform.SetParent(tileRoot, false);

            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            tileSpriteOrdering.ConfigureSprite(spriteRenderer, board, cell, sprite, BoardTileSpriteLayer.Preview);
            spriteObject.SetActive(false);
            previewSpriteRenderers[cell] = spriteRenderer;
            previewSpriteBaseColors[cell] = Color.white;
            return spriteRenderer;
        }

        private SpriteRenderer CreateLockedBuildableSpriteRenderer(
            RuntimeBoardState board,
            Vector2Int cell,
            BoardCellType type,
            Sprite sprite)
        {
            GameObject spriteObject = new GameObject($"Cell {cell.x},{cell.y} {type} Lock Sprite");
            spriteObject.transform.SetParent(tileRoot, false);

            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            tileSpriteOrdering.ConfigureSprite(spriteRenderer, board, cell, sprite, BoardTileSpriteLayer.LockedBuildable);
            spriteRenderer.color = Color.white;
            lockedBuildableSpriteRenderers[cell] = spriteRenderer;
            return spriteRenderer;
        }

        private void RefreshBuildableSprite(RuntimeBoardState board, Vector2Int cell)
        {
            BoardCellType type = board.GetCellType(cell);
            Sprite sprite = GetConfiguredHighlightSprite(board, cell, type);
            if (buildableSpriteRenderers.TryGetValue(cell, out SpriteRenderer spriteRenderer) && spriteRenderer != null)
            {
                if (sprite == null)
                {
                    Destroy(spriteRenderer.gameObject);
                    buildableSpriteRenderers.Remove(cell);
                    buildableSpriteBaseColors.Remove(cell);
                    RefreshLockedBuildableSprite(board, cell, type);
                    RefreshSelectionPreviewSprites();
                    return;
                }

                tileSpriteOrdering.ConfigureSprite(spriteRenderer, board, cell, sprite, BoardTileSpriteLayer.Buildable);
                Color baseColor = GetConfiguredHighlightSpriteColor(board, cell, type);
                buildableSpriteBaseColors[cell] = baseColor;
                ApplyBuildableSpriteColor(spriteRenderer, baseColor);
                spriteRenderer.gameObject.SetActive(false);
                RefreshLockedBuildableSprite(board, cell, type);
                RefreshSelectionPreviewSprites();
                return;
            }

            CreateBuildableSprite(board, cell, type);
            RefreshLockedBuildableSprite(board, cell, type);
            RefreshSelectionPreviewSprites();
        }

        private void RefreshLockedBuildableSprite(RuntimeBoardState board, Vector2Int cell, BoardCellType type)
        {
            Sprite sprite = GetLockedBuildableSprite(board, cell, type);
            if (sprite == null)
            {
                if (lockedBuildableSpriteRenderers.TryGetValue(cell, out SpriteRenderer existingRenderer) &&
                    existingRenderer != null)
                {
                    Destroy(existingRenderer.gameObject);
                }

                lockedBuildableSpriteRenderers.Remove(cell);
                return;
            }

            if (!lockedBuildableSpriteRenderers.TryGetValue(cell, out SpriteRenderer spriteRenderer) ||
                spriteRenderer == null)
            {
                spriteRenderer = CreateLockedBuildableSpriteRenderer(board, cell, type, sprite);
            }

            tileSpriteOrdering.ConfigureSprite(spriteRenderer, board, cell, sprite, BoardTileSpriteLayer.LockedBuildable);
            spriteRenderer.gameObject.SetActive(true);
        }

        private static Sprite GetConfiguredHighlightSprite(RuntimeBoardState board, Vector2Int cell, BoardCellType type)
        {
            if (board.Definition.TilePrefabSet == null)
            {
                return null;
            }

            return board.Definition.TilePrefabSet.GetHighlightSprite(type);
        }

        private static Sprite GetLockedBuildableSprite(RuntimeBoardState board, Vector2Int cell, BoardCellType type)
        {
            if (board.Definition.TilePrefabSet == null ||
                type != BoardCellType.BuildableLocked ||
                board.IsBuildableUnlocked(cell))
            {
                return null;
            }

            return board.Definition.TilePrefabSet.GetBuildableLockedSprite();
        }

        private static Color GetConfiguredHighlightSpriteColor(RuntimeBoardState board, Vector2Int cell, BoardCellType type)
        {
            if (board.Definition.TilePrefabSet == null)
            {
                return Color.white;
            }

            return board.Definition.TilePrefabSet.GetHighlightSpriteColor(type);
        }

        private static Color GetConfiguredShovelHighlightSpriteColor(RuntimeBoardState board)
        {
            return board.Definition.TilePrefabSet != null
                ? board.Definition.TilePrefabSet.ShovelHighlightSpriteColor
                : Color.white;
        }

        private static Sprite GetFootprintPreviewSprite(RuntimeBoardState board)
        {
            if (board.Definition.TilePrefabSet == null)
            {
                return null;
            }

            return board.Definition.TilePrefabSet.GetFootprintPreviewSprite();
        }

        private static Sprite GetAffectedPreviewSprite(RuntimeBoardState board)
        {
            if (board.Definition.TilePrefabSet == null)
            {
                return null;
            }

            return board.Definition.TilePrefabSet.GetAffectedPreviewSprite();
        }

        private static Color GetFootprintPreviewColor(RuntimeBoardState board)
        {
            return board.Definition.TilePrefabSet != null
                ? board.Definition.TilePrefabSet.FootprintPreviewColor
                : Color.white;
        }

        private static Color GetAffectedPreviewColor(RuntimeBoardState board, AffectedPreviewKind previewKind)
        {
            if (board.Definition.TilePrefabSet == null)
            {
                return Color.white;
            }

            return previewKind == AffectedPreviewKind.Path
                ? board.Definition.TilePrefabSet.AffectedPathPreviewColor
                : board.Definition.TilePrefabSet.AffectedBuildablePreviewColor;
        }

        private static Color GetInvalidPreviewColor(RuntimeBoardState board)
        {
            return board.Definition.TilePrefabSet != null
                ? board.Definition.TilePrefabSet.InvalidPreviewColor
                : Color.red;
        }

        private bool ShouldShowBuildableSpritesForSelection()
        {
            return CanEditBoard() &&
                selectedUnitView != null;
        }

        private bool ShouldShowBuildableSpritesForShovel()
        {
            return CanEditBoard() &&
                isShovelModeActive;
        }

        private bool ShouldShowBuildableSprites()
        {
            return ShouldShowBuildableSpritesForSelection() || ShouldShowBuildableSpritesForShovel();
        }

        private bool CanEditBoard()
        {
            return director != null &&
                director.State != null &&
                director.State.Phase == GamePhase.Placement &&
                !isOpeningPark &&
                !isCountdownIntroPlaying;
        }

        private bool CanOpenPark()
        {
            return CanEditBoard() &&
                director.State.Draft != null &&
                director.State.Draft.IsCompletedForDay &&
                !director.State.Draft.HasPendingPick;
        }

        private void RefreshSelectionPreviewSprites()
        {
            previewFootprintCells.Clear();
            previewAffectedCells.Clear();

            if (ShouldShowBuildableSpritesForShovel())
            {
                HideAllBuildableSprites();
                ShowLockedBuildableHighlights(director.State.Board);
                return;
            }

            if (!ShouldShowBuildableSpritesForSelection() ||
                !TryGetSelectedUnit(out UnitInstanceState unit) ||
                director.PlacementSystem == null)
            {
                HideAllBuildableSprites();
                return;
            }

            RuntimeBoardState board = director.State.Board;
            Vector2Int anchorCell = GetSelectedUnitPreviewAnchor(unit);
            GridRotation rotation = GetSelectedUnitPreviewRotation(unit);
            PlacementResult placement = director.PlacementSystem.CanPlace(
                unit.Definition,
                anchorCell,
                rotation,
                unit.InstanceId);

            IReadOnlyList<Vector2Int> footprintCells = placement.FootprintCells;
            for (int i = 0; i < footprintCells.Count; i++)
            {
                previewFootprintCells.Add(footprintCells[i]);
            }

            AddAffectedPreviewCells(unit, anchorCell, rotation);
            HideAllBuildableSprites();
            ShowOpenBuildableHighlights(board);

            bool isInvalid = !placement.IsValid;
            foreach (Vector2Int cell in previewFootprintCells)
            {
                ApplyPreviewSpriteCell(board, cell, true, isInvalid);
            }

            foreach (KeyValuePair<Vector2Int, AffectedPreviewKind> entry in previewAffectedCells)
            {
                Vector2Int cell = entry.Key;
                if (!previewFootprintCells.Contains(cell))
                {
                    ApplyPreviewSpriteCell(board, cell, false, isInvalid, entry.Value);
                }
            }
        }

        private void ShowOpenBuildableHighlights(RuntimeBoardState board)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    BoardCellType cellType = board.GetCellType(cell);
                    if (!IsOpenBuildableHighlightCell(cellType) || !board.IsBuildableUnlocked(cell))
                    {
                        continue;
                    }

                    ApplyOpenBuildableHighlightCell(board, cell, cellType);
                }
            }
        }

        private void ShowLockedBuildableHighlights(RuntimeBoardState board)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    BoardCellType cellType = board.GetCellType(cell);
                    if (cellType != BoardCellType.BuildableLocked || board.IsBuildableUnlocked(cell))
                    {
                        continue;
                    }

                    ApplyLockedBuildableHighlightCell(board, cell, cellType);
                }
            }
        }

        private static bool IsOpenBuildableHighlightCell(BoardCellType cellType)
        {
            return cellType == BoardCellType.BuildableOpen ||
                cellType == BoardCellType.BuildableLocked;
        }

        private void ApplyOpenBuildableHighlightCell(RuntimeBoardState board, Vector2Int cell, BoardCellType cellType)
        {
            Sprite sprite = GetConfiguredHighlightSprite(board, cell, cellType);
            if (sprite == null)
            {
                return;
            }

            SpriteRenderer spriteRenderer = EnsureBuildableSpriteRenderer(board, cell, sprite);
            tileSpriteOrdering.ConfigureSprite(spriteRenderer, board, cell, sprite, BoardTileSpriteLayer.Buildable);

            Color baseColor = GetConfiguredHighlightSpriteColor(board, cell, cellType);
            buildableSpriteBaseColors[cell] = baseColor;
            ApplyBuildableSpriteColor(spriteRenderer, baseColor);
            spriteRenderer.gameObject.SetActive(true);
        }

        private void ApplyLockedBuildableHighlightCell(RuntimeBoardState board, Vector2Int cell, BoardCellType cellType)
        {
            Sprite sprite = GetConfiguredHighlightSprite(board, cell, cellType);
            if (sprite == null)
            {
                return;
            }

            SpriteRenderer spriteRenderer = EnsureBuildableSpriteRenderer(board, cell, sprite);
            tileSpriteOrdering.ConfigureSprite(spriteRenderer, board, cell, sprite, BoardTileSpriteLayer.Buildable);

            Color baseColor = GetConfiguredShovelHighlightSpriteColor(board);
            buildableSpriteBaseColors[cell] = baseColor;
            ApplyBuildableSpriteColor(spriteRenderer, baseColor);
            spriteRenderer.gameObject.SetActive(true);
        }

        private void AddAffectedPreviewCells(UnitInstanceState unit, Vector2Int anchorCell, GridRotation rotation)
        {
            IReadOnlyList<InfluencePatternSpec> influencePatterns = unit.Definition.InfluencePatterns;
            for (int i = 0; i < influencePatterns.Count; i++)
            {
                InfluencePatternSpec influence = influencePatterns[i];
                if (influence == null || influence.Pattern == null)
                {
                    continue;
                }

                AffectedPreviewKind previewKind = ResolveAffectedPreviewKind(unit.Definition, influence.Pattern);
                List<Vector2Int> cells = GridRotationUtility.ResolveCells(
                    influence.Pattern,
                    anchorCell,
                    rotation);

                for (int j = 0; j < cells.Count; j++)
                {
                    AddAffectedPreviewCell(cells[j], previewKind);
                }
            }
        }

        private void AddAffectedPreviewCell(Vector2Int cell, AffectedPreviewKind previewKind)
        {
            if (previewAffectedCells.TryGetValue(cell, out AffectedPreviewKind existingKind) &&
                existingKind == AffectedPreviewKind.Buildable)
            {
                return;
            }

            previewAffectedCells[cell] = previewKind;
        }

        private static AffectedPreviewKind ResolveAffectedPreviewKind(
            UnitDefinitionSO unitDefinition,
            PatternDefinitionSO pattern)
        {
            if (pattern != null)
            {
                switch (pattern.TargetType)
                {
                    case PatternTargetType.MochiPath:
                        return AffectedPreviewKind.Path;
                    case PatternTargetType.UnitBuff:
                        return AffectedPreviewKind.Buildable;
                }
            }

            if (HasBuildableTargetedEffect(unitDefinition))
            {
                return AffectedPreviewKind.Buildable;
            }

            return HasMochiPathTrigger(unitDefinition)
                ? AffectedPreviewKind.Path
                : AffectedPreviewKind.Buildable;
        }

        private static bool HasMochiPathTrigger(UnitDefinitionSO unitDefinition)
        {
            if (unitDefinition == null)
            {
                return false;
            }

            IReadOnlyList<TriggerRuleSpec> triggerRules = unitDefinition.TriggerRules;
            for (int i = 0; i < triggerRules.Count; i++)
            {
                TriggerRuleSpec rule = triggerRules[i];
                if (rule != null && rule.TriggerType == TriggerType.OnMochiEnterArea)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBuildableTargetedEffect(UnitDefinitionSO unitDefinition)
        {
            if (unitDefinition == null)
            {
                return false;
            }

            IReadOnlyList<EffectSpec> effects = unitDefinition.EffectPayloads;
            for (int i = 0; i < effects.Count; i++)
            {
                EffectSpec effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                switch (effect.EffectType)
                {
                    case EffectType.ApplyJoyModifier:
                    case EffectType.ApplyTriggerChanceModifier:
                    case EffectType.ApplyIntervalModifier:
                    case EffectType.AddDailyCharge:
                    case EffectType.FillStorage:
                    case EffectType.CashOutStorage:
                    case EffectType.TransformUnit:
                        return true;
                }

                if (effect.SourceCategory == SourceCategory.Buff)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyPreviewSpriteCell(
            RuntimeBoardState board,
            Vector2Int cell,
            bool isFootprint,
            bool isInvalid,
            AffectedPreviewKind previewKind = AffectedPreviewKind.Path)
        {
            if (!board.IsInBounds(cell))
            {
                return;
            }

            BoardCellType cellType = board.GetCellType(cell);
            Sprite sprite = isFootprint
                ? GetFootprintPreviewSprite(board)
                : GetAffectedPreviewSprite(board);
            if (sprite == null)
            {
                sprite = GetConfiguredHighlightSprite(board, cell, cellType);
            }

            if (sprite == null)
            {
                return;
            }

            SpriteRenderer spriteRenderer = EnsurePreviewSpriteRenderer(board, cell, sprite);
            tileSpriteOrdering.ConfigureSprite(spriteRenderer, board, cell, sprite, BoardTileSpriteLayer.Preview);

            Color baseColor = isInvalid
                ? GetInvalidPreviewColor(board)
                : isFootprint
                    ? GetFootprintPreviewColor(board)
                    : GetAffectedPreviewColor(board, previewKind);
            previewSpriteBaseColors[cell] = baseColor;
            ApplyBuildableSpriteColor(spriteRenderer, baseColor);
            spriteRenderer.gameObject.SetActive(true);
        }

        private void HideAllBuildableSprites()
        {
            foreach (SpriteRenderer spriteRenderer in buildableSpriteRenderers.Values)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.gameObject.SetActive(false);
                }
            }

            foreach (SpriteRenderer spriteRenderer in previewSpriteRenderers.Values)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.gameObject.SetActive(false);
                }
            }
        }

        private bool TryGetSelectedUnit(out UnitInstanceState unit)
        {
            unit = null;
            if (director == null ||
                director.State == null ||
                string.IsNullOrEmpty(selectedUnitInstanceId))
            {
                return false;
            }

            unit = director.State.FindUnit(selectedUnitInstanceId);
            return unit != null;
        }

        private Vector2Int GetSelectedUnitPreviewAnchor(UnitInstanceState unit)
        {
            return hasSelectedUnitPreviewAnchor
                ? selectedUnitPreviewAnchorCell
                : unit.AnchorCell;
        }

        private GridRotation GetSelectedUnitPreviewRotation(UnitInstanceState unit)
        {
            return hasSelectedUnitPreviewRotation
                ? selectedUnitPreviewRotation
                : unit.Rotation;
        }

        private void RotateSelectedUnit()
        {
            if (!CanEditBoard() ||
                director == null ||
                director.PlacementSystem == null ||
                !TryGetSelectedUnit(out UnitInstanceState unit) ||
                !unit.Definition.RotationAllowed)
            {
                return;
            }

            Vector2Int anchorCell = GetSelectedUnitPreviewAnchor(unit);
            GridRotation nextRotation = GetNextRotation(GetSelectedUnitPreviewRotation(unit));
            selectedUnitPreviewAnchorCell = anchorCell;
            selectedUnitPreviewRotation = nextRotation;
            hasSelectedUnitPreviewAnchor = true;
            hasSelectedUnitPreviewRotation = true;
            if (selectedUnitView != null && director.State != null)
            {
                selectedUnitView.transform.rotation = Quaternion.Euler(0f, (int)nextRotation * 90f, 0f);
                PositionUnitForFootprint(
                    selectedUnitView,
                    director.State.Board,
                    unit.Definition,
                    anchorCell,
                    nextRotation);
            }

            PlacementResult placement = director.PlacementSystem.CanPlace(
                unit.Definition,
                anchorCell,
                nextRotation,
                unit.InstanceId);

            if (placement.IsValid &&
                !director.PlacementSystem.MoveUnit(unit.InstanceId, anchorCell, nextRotation))
            {
                RefreshUnits();
                return;
            }

            RefreshSelectionPreviewSprites();
        }

        private static GridRotation GetNextRotation(GridRotation rotation)
        {
            return (GridRotation)(((int)rotation + 1) % 4);
        }

        private bool TryApplySmartPreviewRotation(UnitInstanceState unit, Vector2Int anchorCell, RuntimeBoardState board)
        {
            if (unit == null || !HasPathTargetedInfluence(unit.Definition))
            {
                return false;
            }

            if (!TryFindBestValidRotation(
                unit,
                anchorCell,
                board,
                GetSelectedUnitPreviewRotation(unit),
                out GridRotation bestRotation))
            {
                return false;
            }

            selectedUnitPreviewRotation = bestRotation;
            hasSelectedUnitPreviewRotation = true;
            return true;
        }

        private bool TryFindBestValidRotation(
            UnitInstanceState unit,
            Vector2Int anchorCell,
            RuntimeBoardState board,
            GridRotation preferredRotation,
            out GridRotation bestRotation)
        {
            bestRotation = preferredRotation;
            if (unit == null ||
                unit.Definition == null ||
                board == null ||
                director == null ||
                director.PlacementSystem == null)
            {
                return false;
            }

            int bestAffectedPathCells = -1;
            int rotationCount = unit.Definition.RotationAllowed ? 4 : 1;
            for (int i = 0; i < rotationCount; i++)
            {
                GridRotation candidateRotation = unit.Definition.RotationAllowed
                    ? (GridRotation)(((int)preferredRotation + i) % 4)
                    : GridRotation.North;

                if (!director.PlacementSystem.CanPlace(
                    unit.Definition,
                    anchorCell,
                    candidateRotation,
                    unit.InstanceId).IsValid)
                {
                    continue;
                }

                int affectedPathCells = CountAffectedPathCells(unit.Definition, anchorCell, candidateRotation, board);
                if (affectedPathCells > bestAffectedPathCells)
                {
                    bestAffectedPathCells = affectedPathCells;
                    bestRotation = candidateRotation;
                }
            }

            return bestAffectedPathCells >= 0;
        }

        private static int CountAffectedPathCells(
            UnitDefinitionSO unitDefinition,
            Vector2Int anchorCell,
            GridRotation rotation,
            RuntimeBoardState board)
        {
            HashSet<Vector2Int> affectedPathCells = new HashSet<Vector2Int>();
            IReadOnlyList<InfluencePatternSpec> influencePatterns = unitDefinition.InfluencePatterns;
            for (int i = 0; i < influencePatterns.Count; i++)
            {
                InfluencePatternSpec influence = influencePatterns[i];
                if (influence == null ||
                    influence.Pattern == null ||
                    !influence.Pattern.TargetsMochiPath)
                {
                    continue;
                }

                List<Vector2Int> cells = GridRotationUtility.ResolveCells(influence.Pattern, anchorCell, rotation);
                for (int j = 0; j < cells.Count; j++)
                {
                    Vector2Int cell = cells[j];
                    if (board.IsInBounds(cell) && board.IsPathCell(cell))
                    {
                        affectedPathCells.Add(cell);
                    }
                }
            }

            return affectedPathCells.Count;
        }

        private static bool HasPathTargetedInfluence(UnitDefinitionSO unitDefinition)
        {
            if (unitDefinition == null)
            {
                return false;
            }

            IReadOnlyList<InfluencePatternSpec> influencePatterns = unitDefinition.InfluencePatterns;
            for (int i = 0; i < influencePatterns.Count; i++)
            {
                InfluencePatternSpec influence = influencePatterns[i];
                if (influence != null &&
                    influence.Pattern != null &&
                    influence.Pattern.TargetsMochiPath)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetBuildableSpritesVisible(bool isVisible)
        {
            if (buildableSpriteFadeCoroutine != null)
            {
                StopCoroutine(buildableSpriteFadeCoroutine);
                buildableSpriteFadeCoroutine = null;
            }

            buildableSpriteFadeAlpha = isVisible ? 1f : 0f;
            if (isVisible)
            {
                RefreshSelectionPreviewSprites();
            }
            else
            {
                SetBuildableSpriteObjectsActive(false);
            }

            ApplyBuildableSpriteFadeAlpha();
        }

        private Coroutine StartBuildableSpriteFade(float targetAlpha, float duration)
        {
            if (buildableSpriteFadeCoroutine != null)
            {
                StopCoroutine(buildableSpriteFadeCoroutine);
            }

            buildableSpriteFadeCoroutine = StartCoroutine(FadeBuildableSprites(targetAlpha, duration));
            return buildableSpriteFadeCoroutine;
        }

        private IEnumerator FadeBuildableSprites(float targetAlpha, float duration)
        {
            targetAlpha = Mathf.Clamp01(targetAlpha);
            float startAlpha = buildableSpriteFadeAlpha;

            if (targetAlpha > 0f)
            {
                RefreshSelectionPreviewSprites();
            }

            if (duration <= 0f)
            {
                SetBuildableSpriteFadeAlpha(targetAlpha);
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    SetBuildableSpriteFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, t)));
                    yield return null;
                }

                SetBuildableSpriteFadeAlpha(targetAlpha);
            }

            if (Mathf.Approximately(targetAlpha, 0f))
            {
                SetBuildableSpriteObjectsActive(false);
            }

            buildableSpriteFadeCoroutine = null;
        }

        private void SetBuildableSpriteFadeAlpha(float alpha)
        {
            buildableSpriteFadeAlpha = Mathf.Clamp01(alpha);
            ApplyBuildableSpriteFadeAlpha();
        }

        private void ApplyBuildableSpriteFadeAlpha()
        {
            foreach (KeyValuePair<Vector2Int, SpriteRenderer> entry in buildableSpriteRenderers)
            {
                SpriteRenderer spriteRenderer = entry.Value;
                if (spriteRenderer != null)
                {
                    Color baseColor = buildableSpriteBaseColors.TryGetValue(entry.Key, out Color cachedColor)
                        ? cachedColor
                        : spriteRenderer.color;
                    ApplyBuildableSpriteColor(spriteRenderer, baseColor);
                }
            }

            foreach (KeyValuePair<Vector2Int, SpriteRenderer> entry in previewSpriteRenderers)
            {
                SpriteRenderer spriteRenderer = entry.Value;
                if (spriteRenderer != null)
                {
                    Color baseColor = previewSpriteBaseColors.TryGetValue(entry.Key, out Color cachedColor)
                        ? cachedColor
                        : spriteRenderer.color;
                    ApplyBuildableSpriteColor(spriteRenderer, baseColor);
                }
            }
        }

        private void SetBuildableSpriteObjectsActive(bool isActive)
        {
            if (isActive)
            {
                RefreshSelectionPreviewSprites();
                return;
            }

            foreach (SpriteRenderer spriteRenderer in buildableSpriteRenderers.Values)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.gameObject.SetActive(isActive);
                }
            }

            foreach (SpriteRenderer spriteRenderer in previewSpriteRenderers.Values)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.gameObject.SetActive(isActive);
                }
            }
        }

        private void ApplyBuildableSpriteColor(SpriteRenderer spriteRenderer, Color baseColor)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            baseColor.a *= buildableSpriteFadeAlpha;
            spriteRenderer.color = baseColor;
        }

        private void RefreshUnits()
        {
            RestoreSelectedOutline();

            for (int i = 0; i < unitViews.Count; i++)
            {
                if (unitViews[i] != null)
                {
                    Destroy(unitViews[i]);
                }
            }

            unitViews.Clear();
            unitInstanceIdsByView.Clear();
            unitViewsByInstanceId.Clear();
            selectedUnitView = null;
            selectedOutlineState = null;
            hasSelectedUnitPreviewAnchor = false;
            hasSelectedUnitPreviewRotation = false;
            ClearPressedUnitState();

            if (director.State == null)
            {
                selectedUnitInstanceId = null;
                hasSelectedUnitPreviewAnchor = false;
                hasSelectedUnitPreviewRotation = false;
                HideUnitDetailPanel();
                SetBuildableSpritesVisible(false);
                ClearJoyMultiplierIndicators();
                ClearDailyLimitIndicators();
                return;
            }

            RuntimeBoardState board = director.State.Board;
            bool rebuiltSelectedUnit = false;
            for (int i = 0; i < director.State.Units.Count; i++)
            {
                UnitInstanceState unit = director.State.Units[i];
                GameObject unitView = unit.Definition.ViewPrefab != null
                    ? Instantiate(unit.Definition.ViewPrefab)
                    : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

                unitView.name = $"{unit.InstanceId} {unit.Definition.UnitTypeId}";
                unitView.transform.SetParent(unitRoot, false);
                unitView.transform.rotation = Quaternion.Euler(0f, (int)unit.Rotation * 90f, 0f);
                if (unit.Definition.ViewPrefab == null)
                {
                    unitView.transform.localScale = new Vector3(1.15f, 0.45f, 1.15f);
                }

                PositionUnitForFootprint(
                    unitView,
                    board,
                    unit.Definition,
                    unit.AnchorCell,
                    unit.Rotation);

                Renderer renderer = unitView.GetComponentInChildren<Renderer>();
                if (renderer != null && unit.Definition.ViewPrefab == null)
                {
                    renderer.sharedMaterial = unitMaterial;
                }

                unitInstanceIdsByView[unitView] = unit.InstanceId;
                unitViewsByInstanceId[unit.InstanceId] = unitView;
                EnsurePickCollider(unitView);
                unitViews.Add(unitView);

                if (!string.IsNullOrEmpty(selectedUnitInstanceId) && selectedUnitInstanceId == unit.InstanceId)
                {
                    selectedUnitView = unitView;
                    selectedOutlineState = ApplySelectedOutline(unitView);
                    selectedUnitPreviewAnchorCell = unit.AnchorCell;
                    selectedUnitPreviewRotation = unit.Rotation;
                    hasSelectedUnitPreviewAnchor = true;
                    hasSelectedUnitPreviewRotation = true;
                    rebuiltSelectedUnit = true;
                }
            }

            if (!rebuiltSelectedUnit)
            {
                selectedUnitInstanceId = null;
                hasSelectedUnitPreviewAnchor = false;
                hasSelectedUnitPreviewRotation = false;
                HideUnitDetailPanel();
            }
            else
            {
                ShowUnitDetailForSelection();
            }

            SetBuildableSpritesVisible(ShouldShowBuildableSprites());
            RefreshDailyLimitIndicators();
            RefreshJoyMultiplierIndicators();
        }

        private void HandlePointerDown(Vector2 screenPosition, int pointerId)
        {
            if (!CanEditBoard())
            {
                return;
            }

            if (IsPointerOverUi(pointerId))
            {
                return;
            }

            if (isShovelModeActive)
            {
                TryUnlockLockedCellAtScreenPosition(screenPosition);
                return;
            }

            if (!TryGetUnitAtScreenPosition(screenPosition, out GameObject unitView))
            {
                if (TryConfirmPendingDraftPlacement(true) || HasPendingDraftSelection())
                {
                    return;
                }

                DeselectUnit();
                return;
            }

            if (!unitInstanceIdsByView.TryGetValue(unitView, out string unitInstanceId))
            {
                return;
            }

            if (HasPendingDraftSelection() && unitInstanceId != director.State.Draft.PendingUnitInstanceId)
            {
                TryConfirmPendingDraftPlacement(true);
                return;
            }

            bool wasSelected = selectedUnitView == unitView;
            SelectUnit(unitView, unitInstanceId);

            UnitInstanceState unit = director.State.FindUnit(unitInstanceId);
            if (unit == null || unit.IsLockedByTutorial)
            {
                return;
            }

            pressedUnitView = unitView;
            pressedPointerId = pointerId;
            pressScreenPosition = screenPosition;
            pressedUnitWasSelected = wasSelected;
        }

        private void ToggleShovelMode()
        {
            Debug.Log($"ToggleShovelMode called. Current state: isShovelModeActive={isShovelModeActive}");
            if (isShovelModeActive)
            {
                SetShovelModeActive(false);
                return;
            }

            if (!CanActivateShovelMode())
            {
                if (eventBus != null)
                {
                    eventBus.Publish(new PopupRequestedEvent("Shovel unavailable", "No shovel uses remaining."));
                }

                return;
            }

            SetShovelModeActive(true);
        }

        private bool CanActivateShovelMode()
        {
            return CanEditBoard() &&
                director.State.RemainingShovelUses > 0;
        }

        private void SetShovelModeActive(bool isActive)
        {
            bool wasShovelModeActive = isShovelModeActive;
            isShovelModeActive = isActive && CanActivateShovelMode();
            if (isShovelModeActive)
            {
                DeselectUnit();
                RefreshSelectionPreviewSprites();
                StartBuildableSpriteFade(1f, BuildableSpriteSelectionFadeDuration);
                RefreshHudShovelState();
                ApplyShovelCursor(true);
                return;
            }

            if (wasShovelModeActive)
            {
                StartBuildableSpriteFade(ShouldShowBuildableSprites() ? 1f : 0f, BuildableSpriteSelectionFadeDuration);
            }

            RefreshHudShovelState();
            ApplyShovelCursor(false);
        }

        private bool TryUnlockLockedCellAtScreenPosition(Vector2 screenPosition)
        {
            if (!CanEditBoard() ||
                director.State.Board == null ||
                cameraController == null ||
                !cameraController.TryScreenToBoardCell(screenPosition, director.State.Board, out Vector2Int cell, out _))
            {
                return false;
            }

            RuntimeBoardState board = director.State.Board;
            if (board.GetCellType(cell) != BoardCellType.BuildableLocked || board.IsBuildableUnlocked(cell))
            {
                return false;
            }

            bool unlocked = director.TryUnlockBoardCell(cell);
            if (director.State.RemainingShovelUses <= 0)
            {
                SetShovelModeActive(false);
            }
            else
            {
                RefreshHudShovelState();
            }

            return unlocked;
        }

        private void UpdatePressedUnit()
        {
            if (pressedUnitView == null)
            {
                return;
            }

            if (!CanEditBoard())
            {
                ClearPressedUnitState();
                return;
            }

            if (TryGetPointerPosition(pressedPointerId, out Vector2 currentPosition) &&
                (currentPosition - pressScreenPosition).sqrMagnitude >= DragStartScreenDistance * DragStartScreenDistance)
            {
                BeginPreparedUnitDrag(currentPosition);
                return;
            }

            if (TryGetPointerUp(pressedPointerId, out _))
            {
                if (pressedUnitWasSelected)
                {
                    DeselectUnit();
                }

                ClearPressedUnitState();
            }
        }

        private void BeginPreparedUnitDrag(Vector2 screenPosition)
        {
            if (pressedUnitView == null || !CanEditBoard())
            {
                return;
            }

            draggedUnitView = pressedUnitView;
            draggedUnitVisualVelocity = Vector3.zero;
            activePointerId = pressedPointerId;
            ClearPressedUnitState();
            SetDraftSuppressedForUnitDrag(true);
            screenspaceIndicatorView?.HideEditPanel();
            PreviewDraggedUnit(screenPosition);
        }

        private void PreviewDraggedUnit(Vector2 screenPosition)
        {
            if (draggedUnitView == null || !CanEditBoard())
            {
                return;
            }

            RuntimeBoardState board = director.State.Board;
            if (!cameraController.TryScreenToBoardCell(
                    screenPosition,
                    board,
                    out Vector2Int cell,
                    out Vector3 surfacePoint))
            {
                return;
            }

            Vector2Int clampedCell = ClampCellToBoard(board, cell);
            bool isPointerOutsideBoard = cell != clampedCell;
            bool anchorChanged = !hasSelectedUnitPreviewAnchor || selectedUnitPreviewAnchorCell != clampedCell;
            selectedUnitPreviewAnchorCell = clampedCell;
            hasSelectedUnitPreviewAnchor = true;
            UnitInstanceState unit = null;
            if (anchorChanged && unitInstanceIdsByView.TryGetValue(draggedUnitView, out string unitInstanceId))
            {
                unit = director.State.FindUnit(unitInstanceId);
                if (unit != null && TryApplySmartPreviewRotation(unit, clampedCell, board))
                {
                    draggedUnitView.transform.rotation = Quaternion.Euler(
                        0f,
                        (int)selectedUnitPreviewRotation * 90f,
                        0f);
                }
            }

            if (unit == null && unitInstanceIdsByView.TryGetValue(draggedUnitView, out string draggedUnitInstanceId))
            {
                unit = director.State.FindUnit(draggedUnitInstanceId);
            }

            RefreshSelectionPreviewSprites();
            if (!isPointerOutsideBoard)
            {
                SmoothPositionDraggedUnitOnSurface(draggedUnitView, surfacePoint);
            }
            else if (unit != null)
            {
                draggedUnitVisualVelocity = Vector3.zero;
                PositionUnitForFootprint(
                    draggedUnitView,
                    board,
                    unit.Definition,
                    clampedCell,
                    GetSelectedUnitPreviewRotation(unit));
            }
            else
            {
                draggedUnitVisualVelocity = Vector3.zero;
                PositionUnitOnSurface(draggedUnitView, board.CellToWorldSurface(clampedCell));
            }
        }

        private void EndUnitDrag(Vector2 screenPosition)
        {
            if (draggedUnitView == null || !CanEditBoard())
            {
                ClearDragState();
                RefreshUnits();
                return;
            }

            RuntimeBoardState board = director.State.Board;
            GameObject releasedUnitView = draggedUnitView;
            int pointerId = activePointerId;
            ClearDragState();

            if (IsPointerOverUi(pointerId) ||
                !cameraController.TryScreenToBoardCell(screenPosition, board, out Vector2Int targetCell, out _))
            {
                RefreshUnits();
                return;
            }

            if (releasedUnitView == null || !unitInstanceIdsByView.TryGetValue(releasedUnitView, out string unitInstanceId))
            {
                RefreshUnits();
                return;
            }

            UnitInstanceState unit = director.State.FindUnit(unitInstanceId);
            if (unit == null)
            {
                RefreshUnits();
                return;
            }

            Vector2Int clampedTargetCell = ClampCellToBoard(board, targetCell);
            bool anchorChanged = !hasSelectedUnitPreviewAnchor || selectedUnitPreviewAnchorCell != clampedTargetCell;
            selectedUnitPreviewAnchorCell = clampedTargetCell;
            hasSelectedUnitPreviewAnchor = true;
            if (anchorChanged)
            {
                TryApplySmartPreviewRotation(unit, clampedTargetCell, board);
            }

            GridRotation targetRotation = GetSelectedUnitPreviewRotation(unit);
            PositionUnitForFootprint(
                releasedUnitView,
                board,
                unit.Definition,
                clampedTargetCell,
                targetRotation);

            PlacementResult placement = director.PlacementSystem.CanPlace(
                unit.Definition,
                clampedTargetCell,
                targetRotation,
                unit.InstanceId);
            if (!placement.IsValid)
            {
                RefreshSelectionPreviewSprites();
                return;
            }

            if (!director.PlacementSystem.MoveUnit(unit.InstanceId, clampedTargetCell, targetRotation))
            {
                RefreshUnits();
                return;
            }

            MochiParkAudioManager.PlaySfx(MochiParkAudioManager.UnitPlacedSfxCueId);
        }

        private static Vector2Int ClampCellToBoard(RuntimeBoardState board, Vector2Int cell)
        {
            return new Vector2Int(
                Mathf.Clamp(cell.x, 0, board.Width - 1),
                Mathf.Clamp(cell.y, 0, board.Height - 1));
        }

        private void ClearDragState()
        {
            draggedUnitView = null;
            draggedUnitVisualVelocity = Vector3.zero;
            activePointerId = MousePointerId;
            SetDraftSuppressedForUnitDrag(false);
        }

        private void SetDraftSuppressedForUnitDrag(bool suppressed)
        {
            if (isDraftSuppressedForUnitDrag == suppressed)
            {
                return;
            }

            isDraftSuppressedForUnitDrag = suppressed;
            draftView?.SetSuppressed(suppressed);
        }

        private void ClearPressedUnitState()
        {
            pressedUnitView = null;
            pressedPointerId = MousePointerId;
            pressedUnitWasSelected = false;
            pressScreenPosition = default;
        }

        private void SelectUnit(GameObject unitView, string unitInstanceId)
        {
            if (selectedUnitView == unitView)
            {
                RefreshSelectionPreviewSprites();
                ShowUnitDetailForSelection();
                return;
            }

            RestoreSelectedUnitPreviewPlacement();
            RestoreSelectedOutline();

            selectedUnitView = unitView;
            selectedUnitInstanceId = unitInstanceId;
            if (director != null && director.State != null)
            {
                UnitInstanceState unit = director.State.FindUnit(unitInstanceId);
                if (unit != null)
                {
                    selectedUnitPreviewAnchorCell = unit.AnchorCell;
                    selectedUnitPreviewRotation = unit.Rotation;
                    hasSelectedUnitPreviewAnchor = true;
                    hasSelectedUnitPreviewRotation = true;
                }
            }

            selectedOutlineState = ApplySelectedOutline(unitView);
            RefreshSelectionPreviewSprites();
            StartBuildableSpriteFade(ShouldShowBuildableSprites() ? 1f : 0f, BuildableSpriteSelectionFadeDuration);
            ShowUnitDetailForSelection();
        }

        private void DeselectUnit()
        {
            RestoreSelectedUnitPreviewPlacement();
            RestoreSelectedOutline();
            selectedUnitView = null;
            selectedUnitInstanceId = null;
            selectedOutlineState = null;
            hasSelectedUnitPreviewAnchor = false;
            hasSelectedUnitPreviewRotation = false;
            screenspaceIndicatorView?.HideEditPanel();
            StartBuildableSpriteFade(0f, BuildableSpriteSelectionFadeDuration);
            HideUnitDetailPanel();
        }

        private void RestoreSelectedUnitPreviewPlacement()
        {
            if (!TryGetSelectedUnit(out UnitInstanceState unit) || director.State == null)
            {
                hasSelectedUnitPreviewAnchor = false;
                hasSelectedUnitPreviewRotation = false;
                return;
            }

            selectedUnitPreviewAnchorCell = unit.AnchorCell;
            selectedUnitPreviewRotation = unit.Rotation;
            hasSelectedUnitPreviewAnchor = true;
            hasSelectedUnitPreviewRotation = true;

            if (selectedUnitView != null)
            {
                selectedUnitView.transform.rotation = Quaternion.Euler(0f, (int)unit.Rotation * 90f, 0f);
                PositionUnitForFootprint(
                    selectedUnitView,
                    director.State.Board,
                    unit.Definition,
                    unit.AnchorCell,
                    unit.Rotation);
            }

            RefreshSelectionPreviewSprites();
        }

        private UnitOutlineSelectionState ApplySelectedOutline(GameObject unitView)
        {
            Material selectedMaterial = GetMochiUnitSelectedMaterial();
            if (selectedMaterial == null)
            {
                return new UnitOutlineSelectionState(System.Array.Empty<MeshRenderer>(), System.Array.Empty<Material[]>());
            }

            MeshRenderer[] renderers = unitView.GetComponentsInChildren<MeshRenderer>(true);
            List<MeshRenderer> selectedRenderers = new List<MeshRenderer>();
            List<Material[]> originalMaterials = new List<Material[]>();

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                Material[] materials = renderer.sharedMaterials;
                if (ContainsMaterial(materials, selectedMaterial))
                {
                    continue;
                }

                Material[] updatedMaterials = new Material[materials.Length + 1];
                materials.CopyTo(updatedMaterials, 0);
                updatedMaterials[updatedMaterials.Length - 1] = selectedMaterial;
                renderer.sharedMaterials = updatedMaterials;

                selectedRenderers.Add(renderer);
                originalMaterials.Add(materials);
            }

            return new UnitOutlineSelectionState(selectedRenderers.ToArray(), originalMaterials.ToArray());
        }

        private void RestoreSelectedOutline()
        {
            if (selectedOutlineState == null)
            {
                return;
            }

            for (int i = 0; i < selectedOutlineState.Renderers.Length; i++)
            {
                MeshRenderer renderer = selectedOutlineState.Renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sharedMaterials = selectedOutlineState.OriginalMaterials[i];
            }
        }

        private Material GetMochiUnitSelectedMaterial()
        {
            return director != null && director.State != null && director.State.GlobalConfig != null
                ? director.State.GlobalConfig.MochiUnitSelectedMaterial
                : null;
        }

        private static bool ContainsMaterial(Material[] materials, Material material)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == material)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator OpenParkAfterCameraIntro()
        {
            if (!CanOpenPark())
            {
                yield break;
            }

            ResolvePendingDraftBeforeOpenPark();
            isOpeningPark = true;
            ClearPressedUnitState();
            ClearDragState();
            draftView?.Close();
            DeselectUnit();
            SetShovelModeActive(false);
            SetBuildableSpritesVisible(false);
            screenspaceIndicatorView?.HideAll(PathJoyIndicatorKeyPrefix);
            if (cameraController != null)
            {
                yield return cameraController.TweenToParkOpenView(director.State.Board);
            }

            director.OpenPark();
            isOpeningPark = false;
        }

        private void ResolvePendingDraftBeforeOpenPark()
        {
            if (!HasPendingDraftSelection())
            {
                return;
            }

            string pendingUnitInstanceId = director.State.Draft.PendingUnitInstanceId;
            if (TryConfirmPendingDraftPlacement(false))
            {
                return;
            }

            bool wasSelected = selectedUnitInstanceId == pendingUnitInstanceId;
            bool hadPendingPick = HasPendingDraftSelection();
            if (hadPendingPick)
            {
                director.CancelDraftSelection();
            }

            if (wasSelected)
            {
                DeselectUnit();
            }
        }

        private void StartCountdownIntro(RuntimeBoardState board)
        {
            hasPlayedCountdownIntro = true;
            StopCountdownIntro(true);
            countdownIntroCoroutine = StartCoroutine(PlayCountdownIntroRoutine(board));
        }

        private IEnumerator PlayCountdownIntroRoutine(RuntimeBoardState board)
        {
            isCountdownIntroPlaying = true;
            ClearPressedUnitState();
            ClearDragState();
            draftView?.SetSuppressed(true);
            DeselectUnit();
            SetShovelModeActive(false);
            SetBuildableSpritesVisible(false);
            screenspaceIndicatorView?.HideAll(PathJoyIndicatorKeyPrefix);
            RefreshHudShovelState();

            if (cameraController != null)
            {
                cameraController.FrameParkOpenView(board);
            }

            PlayCountdownVfx();
            yield return new WaitForSecondsRealtime(CountdownIntroDuration);

            if (cameraController != null)
            {
                yield return cameraController.TweenToEditView(board);
            }

            isCountdownIntroPlaying = false;
            countdownIntroCoroutine = null;
            draftView?.SetSuppressed(false);
            SetBuildableSpritesVisible(ShouldShowBuildableSprites());
            RefreshHudShovelState();
        }

        private void StopCountdownIntro(bool destroyOverlayVfx)
        {
            if (countdownIntroCoroutine != null)
            {
                StopCoroutine(countdownIntroCoroutine);
                countdownIntroCoroutine = null;
            }

            isCountdownIntroPlaying = false;
            draftView?.SetSuppressed(false);
            RefreshHudShovelState();
            if (destroyOverlayVfx)
            {
                screenOverlayCameraController?.ClearEffects(true);
            }
        }

        private void PlayCountdownVfx()
        {
            GameObject prefab = GetCountdownVfxPrefab();
            if (prefab == null)
            {
                return;
            }

            ScreenOverlayCameraController overlayController = GetOrCreateScreenOverlayCameraController();
            overlayController?.PlayTimed(
                prefab,
                CountdownIntroDuration,
                OverlayVfxDestroyLingerDuration,
                true);
        }

        private bool TryGetUnitAtScreenPosition(Vector2 screenPosition, out GameObject unitView)
        {
            unitView = null;

            if (!cameraController.TryGetScreenRay(screenPosition, out Ray ray))
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(ray, UnitPickRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Debug.Log(hits[i]);
                if (TryGetUnitViewForCollider(hits[i].collider, out GameObject hitUnitView))
                {
                    unitView = hitUnitView;
                    return true;
                }
            }

            if (director.State != null &&
                cameraController.TryScreenToBoardCell(screenPosition, director.State.Board, out Vector2Int cell, out _) &&
                director.State.Board.TryGetOccupant(cell, out string unitInstanceId) &&
                unitViewsByInstanceId.TryGetValue(unitInstanceId, out GameObject cellUnitView))
            {
                unitView = cellUnitView;
                return true;
            }

            return false;
        }

        private bool TryGetUnitViewForCollider(Collider hitCollider, out GameObject unitView)
        {
            Transform hitTransform = hitCollider.transform;
            for (int i = 0; i < unitViews.Count; i++)
            {
                GameObject candidate = unitViews[i];
                if (candidate != null && hitTransform.IsChildOf(candidate.transform))
                {
                    unitView = candidate;
                    return true;
                }
            }

            unitView = null;
            return false;
        }

        private static void PositionUnitOnSurface(GameObject unitView, Vector3 surfacePosition)
        {
            if (unitView == null)
            {
                return;
            }

            unitView.transform.position = surfacePosition;
            unitView.transform.position += Vector3.up * GetUnitSurfaceOffset(unitView, surfacePosition.y);
        }

        private void SmoothPositionDraggedUnitOnSurface(GameObject unitView, Vector3 surfacePosition)
        {
            if (unitView == null)
            {
                return;
            }

            Vector3 targetPosition;
            if (TryGetUnitVisualBounds(unitView, out Bounds bounds))
            {
                Vector3 visualBaseCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                targetPosition = unitView.transform.position + surfacePosition - visualBaseCenter;
            }
            else
            {
                targetPosition = surfacePosition + Vector3.up * DefaultUnitHeightOffset;
            }

            if (unitDragSmoothTime <= 0f)
            {
                draggedUnitVisualVelocity = Vector3.zero;
                unitView.transform.position = targetPosition;
                return;
            }

            unitView.transform.position = Vector3.SmoothDamp(
                unitView.transform.position,
                targetPosition,
                ref draggedUnitVisualVelocity,
                unitDragSmoothTime);
        }

        private static void PositionUnitForFootprint(
            GameObject unitView,
            RuntimeBoardState board,
            UnitDefinitionSO unitDefinition,
            Vector2Int anchorCell,
            GridRotation rotation)
        {
            if (unitView == null || board == null || unitDefinition == null)
            {
                return;
            }

            if (!TryGetFootprintWorldArea(
                    board,
                    unitDefinition.FootprintPattern,
                    anchorCell,
                    rotation,
                    out Vector3 footprintCenter,
                    out _))
            {
                PositionUnitOnSurface(unitView, board.CellToWorldSurface(anchorCell));
                return;
            }

            if (!TryGetUnitVisualBounds(unitView, out Bounds bounds))
            {
                unitView.transform.position = footprintCenter + Vector3.up * DefaultUnitHeightOffset;
                return;
            }

            Vector3 visualBaseCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            unitView.transform.position += footprintCenter - visualBaseCenter;
        }

        private static bool TryGetFootprintWorldArea(
            RuntimeBoardState board,
            PatternDefinitionSO footprintPattern,
            Vector2Int anchorCell,
            GridRotation rotation,
            out Vector3 center,
            out Vector2Int tileDimensions)
        {
            center = default;
            tileDimensions = Vector2Int.zero;
            if (board == null || footprintPattern == null)
            {
                return false;
            }

            List<Vector2Int> footprintCells = GridRotationUtility.ResolveCells(
                footprintPattern,
                anchorCell,
                rotation);
            if (footprintCells.Count == 0)
            {
                return false;
            }

            int minX = footprintCells[0].x;
            int maxX = minX;
            int minY = footprintCells[0].y;
            int maxY = minY;
            for (int i = 1; i < footprintCells.Count; i++)
            {
                Vector2Int cell = footprintCells[i];
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            center = new Vector3(
                (minX + maxX) * 0.5f * board.TileSizeXZ,
                board.TileSurfaceY,
                (minY + maxY) * 0.5f * board.TileSizeXZ);
            tileDimensions = new Vector2Int(maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static float GetUnitSurfaceOffset(GameObject unitView, float surfaceY)
        {
            if (TryGetUnitVisualBounds(unitView, out Bounds bounds))
            {
                return surfaceY - bounds.min.y;
            }

            return DefaultUnitHeightOffset;
        }

        private static bool TryGetUnitVisualBounds(GameObject unitView, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            Renderer[] renderers = unitView.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds || BoardPrefabBoundsUtility.TryGetBounds(unitView, out bounds);
        }

        private static bool IsPointerOverUi(int pointerId)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            return pointerId == MousePointerId
                ? eventSystem.IsPointerOverGameObject()
                : eventSystem.IsPointerOverGameObject(pointerId);
        }

        private static bool TryGetPointerDown(out Vector2 screenPosition, out int pointerId)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                pointerId = MousePointerId;
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                pointerId = touchscreen.primaryTouch.touchId.ReadValue();
                return true;
            }

            screenPosition = default;
            pointerId = MousePointerId;
            return false;
        }

        private static bool TryGetPointerPosition(int pointerId, out Vector2 screenPosition)
        {
            if (pointerId == MousePointerId)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    screenPosition = mouse.position.ReadValue();
                    return true;
                }
            }
            else
            {
                Touchscreen touchscreen = Touchscreen.current;
                if (touchscreen != null)
                {
                    screenPosition = touchscreen.primaryTouch.position.ReadValue();
                    return true;
                }
            }

            screenPosition = default;
            return false;
        }

        private static bool TryGetPointerUp(int pointerId, out Vector2 screenPosition)
        {
            if (pointerId == MousePointerId)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    screenPosition = mouse.position.ReadValue();
                    return mouse.leftButton.wasReleasedThisFrame;
                }
            }
            else
            {
                Touchscreen touchscreen = Touchscreen.current;
                if (touchscreen != null)
                {
                    screenPosition = touchscreen.primaryTouch.position.ReadValue();
                    return touchscreen.primaryTouch.press.wasReleasedThisFrame;
                }
            }

            screenPosition = default;
            return false;
        }

        private static void EnsurePickCollider(GameObject unitView)
        {
            if (unitView.GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            BoxCollider collider = unitView.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(1.2f, 1f, 1.2f);
        }

        private enum AffectedPreviewKind
        {
            Path,
            Buildable
        }

        private sealed class PendingMochiUnitTriggeredVfx
        {
            public PendingMochiUnitTriggeredVfx(
                Vector2Int triggerCell,
                string unitInstanceId,
                Vector2Int unitAnchorCell,
                GameObject vfxPrefab)
            {
                TriggerCell = triggerCell;
                UnitInstanceId = unitInstanceId;
                UnitAnchorCell = unitAnchorCell;
                VfxPrefab = vfxPrefab;
            }

            public Vector2Int TriggerCell { get; }
            public string UnitInstanceId { get; }
            public Vector2Int UnitAnchorCell { get; }
            public GameObject VfxPrefab { get; }
        }

        private sealed class FloatingScoreSequenceState
        {
            public Coroutine Coroutine { get; set; }
            public bool IsRunning { get; set; }
        }

        private sealed class UnitOutlineSelectionState
        {
            public UnitOutlineSelectionState(MeshRenderer[] renderers, Material[][] originalMaterials)
            {
                Renderers = renderers;
                OriginalMaterials = originalMaterials;
            }

            public MeshRenderer[] Renderers { get; }
            public Material[][] OriginalMaterials { get; }
        }

        private void OnLevelStarted(LevelStartedEvent evt)
        {
            hasPlayedCountdownIntro = false;
            StopCountdownIntro(true);
            StopFrenzyMomentVfx(true);
            BuildBoard(evt.State.Board);
            GetOrCreateFloatingScoreView();
            GetOrCreateScreenspaceIndicatorView();
            GetOrCreateDraftView();
            resultView?.HideImmediate();
            ConfigureJoyFlyVfx();
            RefreshHudShovelState();
        }

        private void OnDayPrepared(DayPreparedEvent evt)
        {
            if (director.State == null || evt.State != director.State)
            {
                return;
            }

            SetShovelModeActive(false);
            StopFrenzyMomentVfx(true);
            if (!hasPlayedCountdownIntro)
            {
                StartCountdownIntro(evt.State.Board);
            }

            SetBuildableSpritesVisible(ShouldShowBuildableSprites());
            RefreshDailyLimitIndicators();
            RefreshJoyMultiplierIndicators();
            RefreshPathTileJoyDisplays();
        }

        private void OnPlacementChanged(PlacementChangedEvent evt)
        {
            RefreshUnits();
            RefreshPathTileJoyDisplays();
        }

        private void OnDeterministicJoyIncomeChanged(DeterministicJoyIncomeChangedEvent evt)
        {
            if (director == null || director.State == null || evt.State != director.State)
            {
                return;
            }

            RefreshJoyMultiplierIndicators();
            RefreshPathTileJoyDisplays();
        }

        private void OnDraftOffered(DraftOfferedEvent evt)
        {
            if (director.State == null || evt.State != director.State || evt.Offer == null)
            {
                return;
            }

            List<string> labels = new List<string>();
            IReadOnlyList<string> unitTypeIds = evt.Offer.UnitTypeIds;
            for (int i = 0; i < unitTypeIds.Count; i++)
            {
                labels.Add($"{i + 1}:{unitTypeIds[i]}");
                Debug.Log($"Draft offer option {i + 1}: {unitTypeIds[i]}");
            }

            Debug.Log($"Draft offer {evt.Offer.RollIndex + 1}/{director.State.Draft.TotalRollsToday} - {string.Join(", ", labels)}");
        }

        private void OnDraftPicked(DraftPickedEvent evt)
        {
            if (director.State == null || evt.State != director.State)
            {
                return;
            }

            Debug.Log($"Draft picked {evt.Pick.PickedUnitTypeId} as {evt.Pick.CreatedUnitInstanceId}.");
        }

        private void OnDraftCompletedForDay(DraftCompletedForDayEvent evt)
        {
            if (director.State == null || evt.State != director.State)
            {
                return;
            }

            Debug.Log("Draft completed for day. Press X to open park.");
        }

        private void OnBoardCellUnlocked(BoardCellUnlockedEvent evt)
        {
            if (director.State == null || evt.State != director.State)
            {
                return;
            }

            RefreshBuildableSprite(evt.State.Board, evt.Cell);
            if (director.State.RemainingShovelUses <= 0)
            {
                SetShovelModeActive(false);
            }
        }

        private void OnParkOpened(ParkOpenedEvent evt)
        {
            if (director.State == null || evt.State != director.State)
            {
                return;
            }

            DeselectUnit();
            SetShovelModeActive(false);
            SetBuildableSpritesVisible(false);
            screenspaceIndicatorView?.HideAllImmediate(PathJoyIndicatorKeyPrefix);
            RefreshDailyLimitIndicators();
        }

        private void OnVisitorSpawned(VisitorSpawnedEvent evt)
        {
            if (director.State == null)
            {
                return;
            }

            GameObject visitorView = CreateVisitorView();
            visitorView.name = evt.Visitor.VisitorId;
            visitorView.transform.SetParent(visitorRoot, false);

            Renderer renderer = visitorView.GetComponentInChildren<Renderer>();
            if (renderer != null && !HasConfiguredVisitorPrefab())
            {
                renderer.sharedMaterial = visitorMaterial;
            }

            visitorViews[evt.Visitor.VisitorId] = visitorView;
            float surfaceOffset = PositionVisitorOnSurface(
                visitorView,
                director.State.Board.CellToWorldSurface(evt.Visitor.Cell));
            visitorSurfaceOffsets[evt.Visitor.VisitorId] = surfaceOffset;
            CrossFadeVisitorAnimation(visitorView, GetVisitorIdleStateName());
            fellowVisualManager?.SetFellowCount(evt.Visitor.VisitorId, visitorView, evt.Visitor.FellowMochiCount);
        }

        private void OnFellowAdded(FellowAddedEvent evt)
        {
            if (director == null ||
                director.State == null ||
                evt.Visitor == null ||
                !visitorViews.TryGetValue(evt.Visitor.VisitorId, out GameObject visitorView))
            {
                return;
            }

            fellowVisualManager?.Configure(GetMaxFellowCount(), GetFellowStackOffset(), GetFellowScale());
            fellowVisualManager?.SetFellowCount(
                evt.Visitor.VisitorId,
                visitorView,
                evt.Visitor.FellowMochiCount);
        }

        private GameObject CreateVisitorView()
        {
            GameObject visitorView = new GameObject("Visitor");
            Transform visualAnchor = new GameObject("Visual Anchor").transform;
            visualAnchor.SetParent(visitorView.transform, false);

            // Fix prefab facing backward.
            visualAnchor.localRotation = Quaternion.Euler(0f, VisitorVisualYawOffset, 0f);

            GameObject prefab = GetRandomVisitorViewPrefab();

            if (prefab != null)
            {
                GameObject visual = Instantiate(prefab, visualAnchor, false);
                visual.name = prefab.name;
                DisableVisitorRootMotion(visual);
                NormalizeVisitorVisual(visitorView);
                return visitorView;
            }

            GameObject fallbackVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallbackVisual.name = "Fallback Visual";
            fallbackVisual.transform.SetParent(visualAnchor, false);
            fallbackVisual.transform.localScale = Vector3.one * 0.55f;
            NormalizeVisitorVisual(visitorView);
            return visitorView;
        }

        private static void NormalizeVisitorVisual(GameObject visitorView)
        {
            if (visitorView == null || !TryGetUnitVisualBounds(visitorView, out Bounds bounds))
            {
                return;
            }

            Vector3 footprintWorldPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 localOffset = visitorView.transform.InverseTransformPoint(footprintWorldPosition);
            if (localOffset.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            for (int i = 0; i < visitorView.transform.childCount; i++)
            {
                Transform child = visitorView.transform.GetChild(i);
                child.localPosition -= localOffset;
            }
        }

        private static void DisableVisitorRootMotion(GameObject visitorView)
        {
            Animator[] animators = visitorView.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].applyRootMotion = false;
            }
        }

        private GameObject GetRandomVisitorViewPrefab()
        {
            GlobalGameConfigSO globalConfig = director.State.GlobalConfig;
            if (globalConfig == null)
            {
                return null;
            }

            IReadOnlyList<GameObject> prefabs = globalConfig.DefaultVisitorViewPrefabs;
            int configuredPrefabCount = prefabs != null ? prefabs.Count : 0;
            int validPrefabCount = 0;

            for (int i = 0; i < configuredPrefabCount; i++)
            {
                if (prefabs[i] != null)
                {
                    validPrefabCount++;
                }
            }

            if (validPrefabCount == 0)
            {
                return null;
            }

            int selectedIndex = Random.Range(0, validPrefabCount);
            for (int i = 0; i < configuredPrefabCount; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return prefab;
                }

                selectedIndex--;
            }

            return null;
        }

        private bool HasConfiguredVisitorPrefab()
        {
            if (director.State == null || director.State.GlobalConfig == null)
            {
                return false;
            }

            GlobalGameConfigSO globalConfig = director.State.GlobalConfig;
            IReadOnlyList<GameObject> prefabs = globalConfig.DefaultVisitorViewPrefabs;
            if (prefabs == null)
            {
                return false;
            }

            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnVisitorEnteredCell(VisitorEnteredCellEvent evt)
        {
            Debug.Log($"[Visitor Move Target] id={evt.Visitor.VisitorId}, cell={evt.Cell}");

            MoveVisitorView(evt.Visitor, evt.Cell);
        }

        private void OnUnitTriggered(UnitTriggeredEvent evt)
        {
            RefreshDailyLimitIndicators();

            if (director.State == null ||
                evt.Visitor == null ||
                evt.Unit == null ||
                evt.TriggerRule == null ||
                evt.TriggerRule.TriggerType != TriggerType.OnMochiEnterArea)
            {
                return;
            }

            GameObject vfxPrefab = director.State.GlobalConfig != null
                ? director.State.GlobalConfig.MochiUnitTriggeredVfxPrefab
                : null;
            if (vfxPrefab == null)
            {
                return;
            }

            QueueMochiUnitTriggeredVfx(
                evt.Visitor.VisitorId,
                evt.TriggerCell,
                evt.Unit.InstanceId,
                evt.Unit.AnchorCell,
                vfxPrefab);
        }

        private void QueueMochiUnitTriggeredVfx(
            string visitorId,
            Vector2Int triggerCell,
            string unitInstanceId,
            Vector2Int unitAnchorCell,
            GameObject vfxPrefab)
        {
            if (TryGetVisitorAtTileCenter(visitorId, triggerCell))
            {
                PlayMochiUnitTriggeredVfx(unitInstanceId, unitAnchorCell, vfxPrefab);
                return;
            }

            if (!pendingMochiUnitTriggeredVfxByVisitor.TryGetValue(
                    visitorId,
                    out List<PendingMochiUnitTriggeredVfx> pendingVfx))
            {
                pendingVfx = new List<PendingMochiUnitTriggeredVfx>();
                pendingMochiUnitTriggeredVfxByVisitor[visitorId] = pendingVfx;
            }

            pendingVfx.Add(new PendingMochiUnitTriggeredVfx(
                triggerCell,
                unitInstanceId,
                unitAnchorCell,
                vfxPrefab));
        }

        private void HandleVisitorReachedTileCenter(VisitorState visitor, Vector2Int cell)
        {
            if (visitor == null || director == null)
            {
                return;
            }

            PlayPendingMochiUnitTriggeredVfx(visitor.VisitorId, cell);
        }

        private bool TryGetVisitorAtTileCenter(string visitorId, Vector2Int cell)
        {
            if (!visitorViews.TryGetValue(visitorId, out GameObject visitorView) || visitorView == null)
            {
                return false;
            }

            Vector3 targetPosition = GetVisitorWorldPosition(visitorId, cell);
            return (visitorView.transform.position - targetPosition).sqrMagnitude <=
                VisitorReachedTileCenterDistance * VisitorReachedTileCenterDistance;
        }

        private void PlayPendingMochiUnitTriggeredVfx(string visitorId, Vector2Int reachedCell)
        {
            if (!pendingMochiUnitTriggeredVfxByVisitor.TryGetValue(
                    visitorId,
                    out List<PendingMochiUnitTriggeredVfx> pendingVfx))
            {
                return;
            }

            for (int i = pendingVfx.Count - 1; i >= 0; i--)
            {
                PendingMochiUnitTriggeredVfx pending = pendingVfx[i];
                if (pending.TriggerCell != reachedCell)
                {
                    continue;
                }

                PlayMochiUnitTriggeredVfx(pending.UnitInstanceId, pending.UnitAnchorCell, pending.VfxPrefab);
                pendingVfx.RemoveAt(i);
            }

            if (pendingVfx.Count == 0)
            {
                pendingMochiUnitTriggeredVfxByVisitor.Remove(visitorId);
            }
        }

        private void PlayMochiUnitTriggeredVfx(
            string unitInstanceId,
            Vector2Int unitAnchorCell,
            GameObject vfxPrefab)
        {
            if (director == null || director.State == null || vfxPrefab == null)
            {
                return;
            }

            RuntimeBoardState board = director.State.Board;
            Vector3 vfxPosition = board.CellToWorldSurface(unitAnchorCell);
            Vector2Int footprintDimensions = Vector2Int.one;
            UnitInstanceState unit = director.State.FindUnit(unitInstanceId);
            if (unit != null &&
                TryGetFootprintWorldArea(
                    board,
                    unit.Definition.FootprintPattern,
                    unitAnchorCell,
                    unit.Rotation,
                    out Vector3 footprintCenter,
                    out Vector2Int resolvedDimensions))
            {
                vfxPosition = footprintCenter;
                footprintDimensions = resolvedDimensions;
            }

            GameObject vfx = Instantiate(vfxPrefab, vfxPosition, Quaternion.identity);
            vfx.name = $"{unitInstanceId} Trigger VFX";
            Vector3 prefabScale = vfx.transform.localScale;
            vfx.transform.localScale = new Vector3(
                prefabScale.x * Mathf.Max(1, footprintDimensions.x),
                prefabScale.y,
                prefabScale.z * Mathf.Max(1, footprintDimensions.y));
            vfx.transform.SetParent(vfxRoot, true);
            Destroy(vfx, MochiUnitTriggeredVfxLifetime);
        }

        private void OnFrenzyStarted(FrenzyStartedEvent evt)
        {
            if (director == null ||
                director.State == null ||
                evt.State != director.State)
            {
                return;
            }

            PlayFrenzyStartVfx();
            PlayFrenzyMomentVfx();
        }

        private void OnFrenzyEnded(FrenzyEndedEvent evt)
        {
            if (director == null ||
                director.State == null ||
                evt.State != director.State)
            {
                return;
            }

            StopFrenzyMomentVfx();
        }

        private void PlayFrenzyStartVfx()
        {
            GameObject prefab = GetFrenzyVfxPrefab();
            if (prefab == null)
            {
                return;
            }

            ScreenOverlayCameraController overlayController = GetOrCreateScreenOverlayCameraController();
            overlayController?.PlayTimed(
                prefab,
                FrenzyIntroVfxDuration,
                OverlayVfxDestroyLingerDuration,
                true);
        }

        private void PlayFrenzyMomentVfx()
        {
            if (activeFrenzyMomentVfx != null)
            {
                ScreenOverlayVfxController activeOverlayVfx =
                    activeFrenzyMomentVfx.GetComponent<ScreenOverlayVfxController>();
                activeOverlayVfx?.BeginFrenzy();
                return;
            }

            GameObject prefab = GetFrenzyMomentVfxPrefab();
            if (prefab == null)
            {
                return;
            }

            ScreenOverlayCameraController overlayController = GetOrCreateScreenOverlayCameraController();
            if (overlayController == null || overlayController.OverlayCamera == null)
            {
                return;
            }

            activeFrenzyMomentVfx = overlayController.Spawn(prefab, true);
            if (activeFrenzyMomentVfx == null)
            {
                return;
            }

            ScreenOverlayVfxController overlayVfx =
                activeFrenzyMomentVfx.GetComponent<ScreenOverlayVfxController>();
            if (overlayVfx == null)
            {
                overlayVfx = activeFrenzyMomentVfx.AddComponent<ScreenOverlayVfxController>();
            }

            overlayVfx.Initialize(mainCamera, overlayController.OverlayCamera);
            overlayVfx.BeginFrenzy();
        }

        private void StopFrenzyMomentVfx()
        {
            StopFrenzyMomentVfx(false);
        }

        private void StopFrenzyMomentVfx(bool destroyImmediately)
        {
            if (activeFrenzyMomentVfx == null)
            {
                return;
            }

            ScreenOverlayVfxController overlayVfx =
                activeFrenzyMomentVfx.GetComponent<ScreenOverlayVfxController>();
            if (overlayVfx != null && !destroyImmediately)
            {
                overlayVfx.EndFrenzyAndDestroy(OverlayVfxDestroyLingerDuration);
                activeFrenzyMomentVfx = null;
                return;
            }

            if (overlayVfx != null)
            {
                overlayVfx.DestroyImmediately();
            }
            else
            {
                Destroy(activeFrenzyMomentVfx);
            }

            activeFrenzyMomentVfx = null;
        }

        private void RefreshSelectedUnitEditPanel()
        {
            if (!CanShowSelectedUnitEditPanel() ||
                !TryGetSelectedUnit(out UnitInstanceState unit) ||
                !TryGetSelectedUnitEditPanelWorldPosition(unit, out Vector3 worldPosition))
            {
                screenspaceIndicatorView?.HideEditPanel();
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            ScreenspaceIndicatorView view = GetOrCreateScreenspaceIndicatorView();
            if (view == null || mainCamera == null)
            {
                return;
            }

            bool canConfirm = CanConfirmSelectedUnitEdit(unit);
            view.ShowEditPanel(
                mainCamera,
                worldPosition,
                EditPanelIndicatorScreenOffset,
                CancelSelectedUnitEdit,
                RotateSelectedUnit,
                ConfirmSelectedUnitEdit,
                canConfirm);
        }

        private bool CanShowSelectedUnitEditPanel()
        {
            if (!CanEditBoard() || selectedUnitView == null || draggedUnitView != null || isShovelModeActive)
            {
                return false;
            }

            return TryGetSelectedUnit(out UnitInstanceState unit) &&
                unit != null &&
                !unit.IsLockedByTutorial;
        }

        private bool CanConfirmSelectedUnitEdit(UnitInstanceState unit)
        {
            if (director == null || director.PlacementSystem == null || unit == null)
            {
                return false;
            }

            return director.PlacementSystem.CanPlace(
                unit.Definition,
                GetSelectedUnitPreviewAnchor(unit),
                GetSelectedUnitPreviewRotation(unit),
                unit.InstanceId).IsValid;
        }

        private bool TryGetSelectedUnitEditPanelWorldPosition(UnitInstanceState unit, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (unit == null || director == null || director.State == null || director.State.Board == null)
            {
                return false;
            }

            RuntimeBoardState board = director.State.Board;
            Vector2Int anchorCell = GetSelectedUnitPreviewAnchor(unit);
            GridRotation rotation = GetSelectedUnitPreviewRotation(unit);
            if (!TryGetFootprintWorldArea(
                    board,
                    unit.Definition.FootprintPattern,
                    anchorCell,
                    rotation,
                    out worldPosition,
                    out _))
            {
                worldPosition = board.CellToWorldSurface(anchorCell);
            }

            worldPosition += Vector3.up * DefaultUnitHeightOffset;
            return true;
        }

        private void RefreshDailyLimitIndicators()
        {
            if (!CanShowDailyLimitIndicators())
            {
                ClearDailyLimitIndicators();
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            dailyLimitIndicatorRemovalBuffer.Clear();
            foreach (string key in visibleDailyLimitIndicatorKeys)
            {
                dailyLimitIndicatorRemovalBuffer.Add(key);
            }

            ScreenspaceIndicatorView view = null;
            IReadOnlyList<UnitInstanceState> units = director.State.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitInstanceState unit = units[i];
                if (!ShouldShowDailyLimitIndicator(unit) ||
                    !TryGetDailyLimitIndicatorPosition(unit, out Vector3 worldPosition))
                {
                    continue;
                }

                if (view == null)
                {
                    view = GetOrCreateScreenspaceIndicatorView();
                }

                if (view == null)
                {
                    continue;
                }

                string key = GetDailyLimitIndicatorKey(unit.InstanceId);
                if (view.SetDailyLimitText(
                        mainCamera,
                        key,
                        worldPosition,
                        FormatDailyLimitText(unit.RemainingDailyCharges),
                        DailyLimitIndicatorScreenOffset))
                {
                    visibleDailyLimitIndicatorKeys.Add(key);
                    dailyLimitIndicatorRemovalBuffer.Remove(key);
                }
            }

            for (int i = 0; i < dailyLimitIndicatorRemovalBuffer.Count; i++)
            {
                HideDailyLimitIndicator(dailyLimitIndicatorRemovalBuffer[i]);
            }

            dailyLimitIndicatorRemovalBuffer.Clear();
        }

        private bool CanShowDailyLimitIndicators()
        {
            if (director == null || director.State == null)
            {
                return false;
            }

            return director.State.Phase == GamePhase.Placement ||
                director.State.Phase == GamePhase.Running;
        }

        private static bool ShouldShowDailyLimitIndicator(UnitInstanceState unit)
        {
            return unit != null && !unit.HasUnlimitedDailyCharges;
        }

        private bool TryGetDailyLimitIndicatorPosition(UnitInstanceState unit, out Vector3 worldPosition)
        {
            return TryGetFloatingScoreStartPosition(unit, out worldPosition);
        }

        private void HideDailyLimitIndicator(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            screenspaceIndicatorView?.Hide(key);
            visibleDailyLimitIndicatorKeys.Remove(key);
        }

        private void ClearDailyLimitIndicators()
        {
            screenspaceIndicatorView?.Clear(DailyLimitIndicatorKeyPrefix);
            visibleDailyLimitIndicatorKeys.Clear();
            dailyLimitIndicatorRemovalBuffer.Clear();
        }

        private void RefreshJoyMultiplierIndicators()
        {
            if (director == null || director.State == null || !CanEditBoard())
            {
                ClearJoyMultiplierIndicators();
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            joyMultiplierIndicatorRemovalBuffer.Clear();
            foreach (string key in visibleJoyMultiplierIndicatorKeys)
            {
                joyMultiplierIndicatorRemovalBuffer.Add(key);
            }

            ScreenspaceIndicatorView view = null;
            IReadOnlyList<UnitInstanceState> units = director.State.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitInstanceState unit = units[i];
                float multiplier = JoyIncomeUtility.CalculateJoyMultiplier(unit);
                if (multiplier <= 1f + VisibleJoyMultiplierEpsilon ||
                    !JoyIncomeUtility.CanProduceJoy(unit) ||
                    !TryGetJoyMultiplierIndicatorPosition(unit, out Vector3 worldPosition))
                {
                    continue;
                }

                if (view == null)
                {
                    view = GetOrCreateScreenspaceIndicatorView();
                }

                if (view == null)
                {
                    continue;
                }

                string key = GetJoyMultiplierIndicatorKey(unit.InstanceId);
                if (view.SetJoyMultiplierText(
                        mainCamera,
                        key,
                        worldPosition,
                        FormatJoyMultiplierText(multiplier),
                        JoyMultiplierIndicatorScreenOffset))
                {
                    visibleJoyMultiplierIndicatorKeys.Add(key);
                    joyMultiplierIndicatorRemovalBuffer.Remove(key);
                }
            }

            for (int i = 0; i < joyMultiplierIndicatorRemovalBuffer.Count; i++)
            {
                HideJoyMultiplierIndicator(joyMultiplierIndicatorRemovalBuffer[i]);
            }

            joyMultiplierIndicatorRemovalBuffer.Clear();
        }

        private bool TryGetJoyMultiplierIndicatorPosition(UnitInstanceState unit, out Vector3 worldPosition)
        {
            return TryGetFloatingScoreStartPosition(unit, out worldPosition);
        }

        private void HideJoyMultiplierIndicator(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            screenspaceIndicatorView?.Hide(key);
            visibleJoyMultiplierIndicatorKeys.Remove(key);
        }

        private void ClearJoyMultiplierIndicators()
        {
            screenspaceIndicatorView?.Clear(JoyMultiplierIndicatorKeyPrefix);
            visibleJoyMultiplierIndicatorKeys.Clear();
            joyMultiplierIndicatorRemovalBuffer.Clear();
        }

        private static string GetPathJoyIndicatorKey(Vector2Int cell)
        {
            return $"{PathJoyIndicatorKeyPrefix}{cell.x},{cell.y}";
        }

        private static string GetJoyMultiplierIndicatorKey(string unitInstanceId)
        {
            return $"{JoyMultiplierIndicatorKeyPrefix}{unitInstanceId}";
        }

        private static string GetDailyLimitIndicatorKey(string unitInstanceId)
        {
            return $"{DailyLimitIndicatorKeyPrefix}{unitInstanceId}";
        }

        private static string FormatJoyMultiplierText(float multiplier)
        {
            float safeMultiplier = Mathf.Max(0f, multiplier);
            int rounded = Mathf.RoundToInt(safeMultiplier);
            string value = Mathf.Abs(safeMultiplier - rounded) <= 0.001f
                ? rounded.ToString(CultureInfo.InvariantCulture)
                : safeMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
            return $"x{value}";
        }

        private static string FormatDailyLimitText(int remaining)
        {
            return Mathf.Max(0, remaining).ToString(CultureInfo.InvariantCulture);
        }

        private GameObject GetFrenzyVfxPrefab()
        {
            UIConfigSO uiConfig = GetUIConfig();
            return uiConfig != null ? uiConfig.FrenzyVfxPrefab : null;
        }

        private GameObject GetFrenzyMomentVfxPrefab()
        {
            UIConfigSO uiConfig = GetUIConfig();
            return uiConfig != null ? uiConfig.FrenzyMomentVfxPrefab : null;
        }

        private GameObject GetCountdownVfxPrefab()
        {
            UIConfigSO uiConfig = GetUIConfig();
            return uiConfig != null ? uiConfig.CountdownVfxPrefab : null;
        }

        private GameObject GetResultVfxPrefab()
        {
            UIConfigSO uiConfig = GetUIConfig();
            if (uiConfig != null && uiConfig.ResultVfxPrefab != null)
            {
                return uiConfig.ResultVfxPrefab;
            }

            return Resources.Load<GameObject>(ResultVfxResourcePath);
        }

        private UIConfigSO GetUIConfig()
        {
            return director != null && director.State != null && director.State.GlobalConfig != null
                ? director.State.GlobalConfig.UIConfig
                : null;
        }

        private ScreenOverlayCameraController GetOrCreateScreenOverlayCameraController()
        {
            if (screenOverlayCameraController == null)
            {
                Transform existing = transform.Find("Screen Overlay VFX");
                if (existing != null)
                {
                    screenOverlayCameraController = existing.GetComponent<ScreenOverlayCameraController>();
                }
            }

            if (screenOverlayCameraController == null)
            {
                GameObject controllerObject = new GameObject("Screen Overlay VFX");
                controllerObject.transform.SetParent(transform, false);
                screenOverlayCameraController = controllerObject.AddComponent<ScreenOverlayCameraController>();
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            return screenOverlayCameraController.Initialize(mainCamera)
                ? screenOverlayCameraController
                : null;
        }

        private void OnJoyCreated(JoyCreatedEvent evt)
        {
            if (evt.Amount <= 0 ||
                director == null ||
                director.State == null ||
                string.IsNullOrEmpty(evt.Attribution.SourceUnitInstanceId))
            {
                return;
            }

            UnitInstanceState unit = director.State.FindUnit(evt.Attribution.SourceUnitInstanceId);
            if (unit == null || !TryGetFloatingScoreStartPosition(unit, out Vector3 worldPosition))
            {
                return;
            }

            PlayFloatingScore(worldPosition, evt.Amount, evt.FloatingScoreCount);
        }

        private bool TryGetFloatingScoreStartPosition(UnitInstanceState unit, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (unit == null || director == null || director.State == null)
            {
                return false;
            }

            if (unitViewsByInstanceId.TryGetValue(unit.InstanceId, out GameObject unitView) && unitView != null)
            {
                if (TryGetUnitVisualBounds(unitView, out Bounds bounds))
                {
                    worldPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                    return true;
                }

                worldPosition = unitView.transform.position + Vector3.up * DefaultUnitHeightOffset;
                return true;
            }

            worldPosition = director.State.Board.CellToWorldSurface(unit.AnchorCell) +
                Vector3.up * DefaultUnitHeightOffset;
            return true;
        }

        private bool TryResolveJoySourceWorldPosition(string unitInstanceId, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (director == null || director.State == null || string.IsNullOrEmpty(unitInstanceId))
            {
                return false;
            }

            UnitInstanceState unit = director.State.FindUnit(unitInstanceId);
            return unit != null && TryGetFloatingScoreStartPosition(unit, out worldPosition);
        }

        private void PlayFloatingScore(Vector3 worldPosition, int amount, int floatingScoreCount = 1)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                Debug.LogError($"{nameof(BoardController)} cannot show a floating score because the main camera is missing.", this);
                return;
            }

            FloatingScoreView view = GetOrCreateFloatingScoreView();
            if (view == null)
            {
                return;
            }

            int scoreCount = Mathf.Max(1, floatingScoreCount);
            if (scoreCount <= 1)
            {
                view.ShowScore(mainCamera, worldPosition, amount);
                return;
            }

            FloatingScoreSequenceState sequence = new FloatingScoreSequenceState();
            sequence.IsRunning = true;
            sequence.Coroutine = StartCoroutine(PlayFloatingScoreSequence(
                sequence,
                view,
                worldPosition,
                amount,
                scoreCount));
            pendingFloatingScoreSequences.Add(sequence);
        }

        private IEnumerator PlayFloatingScoreSequence(
            FloatingScoreSequenceState sequence,
            FloatingScoreView view,
            Vector3 worldPosition,
            int totalAmount,
            int scoreCount)
        {
            int remainingAmount = totalAmount;
            int remainingScores = scoreCount;

            while (remainingScores > 0 && view != null)
            {
                int amount = Mathf.CeilToInt(remainingAmount / (float)remainingScores);
                view.ShowScore(mainCamera, worldPosition, amount);
                remainingAmount -= amount;
                remainingScores--;

                if (remainingScores > 0)
                {
                    yield return new WaitForSecondsRealtime(FloatingScoreSpawnDelay);
                }
            }

            sequence.IsRunning = false;
            pendingFloatingScoreSequences.Remove(sequence);
        }

        private void StopPendingFloatingScores()
        {
            for (int i = pendingFloatingScoreSequences.Count - 1; i >= 0; i--)
            {
                FloatingScoreSequenceState sequence = pendingFloatingScoreSequences[i];
                if (sequence != null && sequence.IsRunning && sequence.Coroutine != null)
                {
                    StopCoroutine(sequence.Coroutine);
                    sequence.IsRunning = false;
                }
            }

            pendingFloatingScoreSequences.Clear();
        }

        private FloatingScoreView GetOrCreateFloatingScoreView()
        {
            if (floatingScoreView != null)
            {
                return floatingScoreView;
            }

            GlobalGameConfigSO globalConfig = director != null && director.State != null
                ? director.State.GlobalConfig
                : null;
            if (globalConfig == null)
            {
                Debug.LogError($"{nameof(BoardController)} cannot resolve {nameof(FloatingScoreView)} because {nameof(GlobalGameConfigSO)} is missing.", this);
                return null;
            }

            UIConfigSO uiConfig = globalConfig.UIConfig;
            if (uiConfig == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(FloatingScoreView)} because '{globalConfig.name}' has no {nameof(UIConfigSO)} assigned.",
                    globalConfig);
                return null;
            }

            GameObject prefab = uiConfig.FloatingScoreCanvasPrefab;
            if (prefab == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(FloatingScoreView)} because '{uiConfig.name}' has no Floating Score Canvas prefab assigned.",
                    uiConfig);
                return null;
            }


            return UIInstanceResolver.GetOrCreate(ref floatingScoreView, prefab);
        }

        private void RefreshPathTileJoyDisplays()
        {
            if (director == null || director.State == null)
            {
                return;
            }

            if (!CanEditBoard())
            {
                screenspaceIndicatorView?.HideAll(PathJoyIndicatorKeyPrefix);
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            ScreenspaceIndicatorView view = GetOrCreateScreenspaceIndicatorView();
            if (view == null || mainCamera == null)
            {
                return;
            }

            RuntimeBoardState board = director.State.Board;
            for (int i = 0; i < board.PathCells.Count; i++)
            {
                Vector2Int cell = board.PathCells[i];
                int amount = CalculatePredictableJoyForPathCell(board, cell);
                string key = GetPathJoyIndicatorKey(cell);
                if (amount <= 0)
                {
                    view.Hide(key);
                    continue;
                }

                view.SetText(
                    mainCamera,
                    key,
                    board.CellToWorldSurface(cell),
                    amount.ToString(CultureInfo.InvariantCulture));
            }
        }

        private int CalculatePredictableJoyForPathCell(RuntimeBoardState board, Vector2Int cell)
        {
            IReadOnlyList<InfluenceLink> links = board.GetInfluenceLinks(cell);
            HashSet<string> processedAreas = new HashSet<string>();
            int total = 0;

            for (int i = 0; i < links.Count; i++)
            {
                InfluenceLink link = links[i];
                string key = $"{link.UnitInstanceId}:{link.AreaId}";
                if (!processedAreas.Add(key))
                {
                    continue;
                }

                UnitInstanceState unit = director.State.FindUnit(link.UnitInstanceId);
                total += JoyIncomeUtility.CalculatePredictableJoyIncome(
                    director.State,
                    unit,
                    link.AreaId);
            }

            return total;
        }

        private ScreenspaceIndicatorView GetOrCreateScreenspaceIndicatorView()
        {
            if (screenspaceIndicatorView != null)
            {
                return screenspaceIndicatorView;
            }

            GlobalGameConfigSO globalConfig = director != null && director.State != null
                ? director.State.GlobalConfig
                : null;
            if (globalConfig == null)
            {
                Debug.LogError($"{nameof(BoardController)} cannot resolve {nameof(ScreenspaceIndicatorView)} because {nameof(GlobalGameConfigSO)} is missing.", this);
                return null;
            }

            UIConfigSO uiConfig = globalConfig.UIConfig;
            if (uiConfig == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(ScreenspaceIndicatorView)} because '{globalConfig.name}' has no {nameof(UIConfigSO)} assigned.",
                    globalConfig);
                return null;
            }

            GameObject prefab = uiConfig.ScreenspaceIndicatorCanvasPrefab;
            if (prefab == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(ScreenspaceIndicatorView)} because '{uiConfig.name}' has no Screenspace Indicator Canvas prefab assigned.",
                    uiConfig);
                return null;
            }

            return UIInstanceResolver.GetOrCreate(ref screenspaceIndicatorView, prefab);
        }

        private HudView GetOrCreateHudView()
        {
            if (hudView != null)
            {
                hudView.Bind(director, eventBus);
                ConfigureHudShovelControls();
                return hudView;
            }

            GlobalGameConfigSO globalConfig = director != null && director.State != null
                ? director.State.GlobalConfig
                : null;
            if (globalConfig == null)
            {
                Debug.LogError($"{nameof(BoardController)} cannot resolve {nameof(HudView)} because {nameof(GlobalGameConfigSO)} is missing.", this);
                return null;
            }

            UIConfigSO uiConfig = globalConfig.UIConfig;
            if (uiConfig == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(HudView)} because '{globalConfig.name}' has no {nameof(UIConfigSO)} assigned.",
                    globalConfig);
                return null;
            }

            GameObject prefab = uiConfig.HudCanvasPrefab;
            if (prefab == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(HudView)} because '{uiConfig.name}' has no HUD Canvas prefab assigned.",
                    uiConfig);
                return null;
            }

            hudView = UIInstanceResolver.GetOrCreate(ref hudView, prefab);
            if (hudView != null)
            {
                hudView.Bind(director, eventBus);
                ConfigureHudShovelControls();
            }

            return hudView;
        }

        private void ConfigureHudShovelControls()
        {
            if (hudView == null)
            {
                return;
            }

            hudView.ShovelToggleRequested -= ToggleShovelMode;
            hudView.ShovelToggleRequested += ToggleShovelMode;
            hudView.OpenParkRequested -= RequestOpenParkWithIntro;
            hudView.OpenParkRequested += RequestOpenParkWithIntro;
            hudView.ExitRequested -= RequestExitToLobby;
            hudView.ExitRequested += RequestExitToLobby;
            RefreshHudShovelState();
        }

        private void RequestExitToLobby()
        {
            SceneTransitionLoader.TryLoadScene(
                string.IsNullOrWhiteSpace(lobbySceneName) ? DefaultLobbySceneName : lobbySceneName,
                transitionScreenPrefab,
                minimumLoadingSeconds,
                transitionEventTimeoutSeconds);
        }

        private void RequestRetryLevel()
        {
            resultView?.HideImmediate();
            screenOverlayCameraController?.ClearEffects(true);
            StopFrenzyMomentVfx(true);
            director?.RestartLevel();
        }

        private ResultView GetOrCreateResultView()
        {
            if (resultView != null)
            {
                ConfigureResultControls();
                return resultView;
            }

            GameObject prefab = GetResultCanvasPrefab();
            if (prefab == null)
            {
                Debug.LogError($"{nameof(BoardController)} cannot show result UI because the Result Canvas prefab is missing.", this);
                return null;
            }

            ResultView existingResultView = FindFirstObjectByType<ResultView>(FindObjectsInactive.Include);
            if (existingResultView != null)
            {
                resultView = existingResultView;
            }
            else
            {
                GameObject instance = Instantiate(prefab);
                instance.name = prefab.name;
                instance.SetActive(true);
                resultView = instance.GetComponentInChildren<ResultView>(true);
                if (resultView == null)
                {
                    resultView = instance.AddComponent<ResultView>();
                }
            }

            ConfigureResultControls();
            return resultView;
        }

        private GameObject GetResultCanvasPrefab()
        {
            UIConfigSO uiConfig = GetUIConfig();
            if (uiConfig != null && uiConfig.ResultCanvasPrefab != null)
            {
                return uiConfig.ResultCanvasPrefab;
            }

            return Resources.Load<GameObject>(ResultCanvasResourcePath);
        }

        private void ConfigureResultControls()
        {
            if (resultView == null)
            {
                return;
            }

            resultView.RetryRequested -= RequestRetryLevel;
            resultView.RetryRequested += RequestRetryLevel;
            resultView.ExitRequested -= RequestExitToLobby;
            resultView.ExitRequested += RequestExitToLobby;
        }

        private DraftView GetOrCreateDraftView()
        {
            if (draftView != null)
            {
                draftView.Bind(director, eventBus);
                ConfigureDraftControls();
                return draftView;
            }

            GlobalGameConfigSO globalConfig = director != null && director.State != null
                ? director.State.GlobalConfig
                : null;
            if (globalConfig == null)
            {
                Debug.LogError($"{nameof(BoardController)} cannot resolve {nameof(DraftView)} because {nameof(GlobalGameConfigSO)} is missing.", this);
                return null;
            }

            UIConfigSO uiConfig = globalConfig.UIConfig;
            if (uiConfig == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(DraftView)} because '{globalConfig.name}' has no {nameof(UIConfigSO)} assigned.",
                    globalConfig);
                return null;
            }

            GameObject prefab = uiConfig.DraftCanvasPrefab;
            if (prefab == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(DraftView)} because '{uiConfig.name}' has no Draft Canvas prefab assigned.",
                    uiConfig);
                return null;
            }

            draftView = UIInstanceResolver.GetOrCreate(ref draftView, prefab);
            if (draftView != null)
            {
                draftView.Bind(director, eventBus);
                ConfigureDraftControls();
            }

            return draftView;
        }

        private void ConfigureDraftControls()
        {
            if (draftView == null)
            {
                return;
            }

            draftView.DraftCardSelected -= SelectDraftCard;
            draftView.DraftCardSelected += SelectDraftCard;
            draftView.DraftCardDragStarted -= OnDraftCardDragStarted;
            draftView.DraftCardDragStarted += OnDraftCardDragStarted;
            draftView.DraftCardDragged -= OnDraftCardDragged;
            draftView.DraftCardDragged += OnDraftCardDragged;
            draftView.DraftCardDragEnded -= OnDraftCardDragEnded;
            draftView.DraftCardDragEnded += OnDraftCardDragEnded;
            draftView.SkipRequested -= SkipDraftForDay;
            draftView.SkipRequested += SkipDraftForDay;
        }

        private void RefreshHudShovelState()
        {
            if (hudView == null || director == null || director.State == null)
            {
                return;
            }

            hudView.SetShovelState(
                director.State.RemainingShovelUses,
                isShovelModeActive,
                CanEditBoard());
        }

        private void ShowUnitDetailForSelection()
        {
            if (!TryGetSelectedUnit(out UnitInstanceState unit))
            {
                HideUnitDetailPanel();
                return;
            }

            HudView hud = hudView != null ? hudView : GetOrCreateHudView();
            hud?.ShowUnitDetail(unit.Definition);
        }

        private void HideUnitDetailPanel()
        {
            hudView?.HideUnitDetail();
        }

        private void ApplyShovelCursor(bool isActive)
        {
            if (!isActive)
            {
                if (isShovelCursorApplied)
                {
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    isShovelCursorApplied = false;
                }

                return;
            }

            HudView hud = GetOrCreateHudView();
            Texture2D cursorTexture = hud != null ? hud.ShovelCursorTexture : null;
            if (cursorTexture == null)
            {
                return;
            }

            Vector2 normalizedHotspot = hud.ShovelCursorHotspot;
            Vector2 hotSpot = new Vector2(
                cursorTexture.width * normalizedHotspot.x,
                cursorTexture.height * normalizedHotspot.y);
            Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
            isShovelCursorApplied = true;
        }

        private void ConfigureJoyFlyVfx()
        {
            HudView hud = GetOrCreateHudView();
            JoyFlyVFXController controller = GetOrCreateJoyFlyVfxController();
            bool delayJoyScore = false;

            if (hud != null && controller != null)
            {
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                }

                delayJoyScore = controller.Bind(
                    eventBus,
                    mainCamera,
                    hud,
                    TryResolveJoySourceWorldPosition);
            }

            hud?.SetDelayJoyScoreChanged(delayJoyScore);
        }

        private JoyFlyVFXController GetOrCreateJoyFlyVfxController()
        {
            if (joyFlyVfxController != null)
            {
                return joyFlyVfxController;
            }

            GlobalGameConfigSO globalConfig = director != null && director.State != null
                ? director.State.GlobalConfig
                : null;
            if (globalConfig == null)
            {
                Debug.LogError($"{nameof(BoardController)} cannot resolve {nameof(UIVFXCanvasView)} because {nameof(GlobalGameConfigSO)} is missing.", this);
                return null;
            }

            UIConfigSO uiConfig = globalConfig.UIConfig;
            if (uiConfig == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(UIVFXCanvasView)} because '{globalConfig.name}' has no {nameof(UIConfigSO)} assigned.",
                    globalConfig);
                return null;
            }

            if (uiConfig.UIVfxCanvasPrefab == null)
            {
                Debug.LogError(
                    $"{nameof(BoardController)} cannot resolve {nameof(UIVFXCanvasView)} because '{uiConfig.name}' has no UI VFX Canvas prefab assigned.",
                    uiConfig);
                return null;
            }

            uiVfxCanvasView = UIInstanceResolver.GetOrCreate(ref uiVfxCanvasView, uiConfig.UIVfxCanvasPrefab);
            if (uiVfxCanvasView == null)
            {
                return null;
            }

            joyFlyVfxController = uiVfxCanvasView.GetComponent<JoyFlyVFXController>();
            if (joyFlyVfxController == null)
            {
                joyFlyVfxController = uiVfxCanvasView.gameObject.AddComponent<JoyFlyVFXController>();
            }

            return joyFlyVfxController;
        }

        private void OnDayEnded(DayEndedEvent evt)
        {
            StopFrenzyMomentVfx(true);
            screenOverlayCameraController?.ClearEffects(true);
            joyFlyVfxController?.Clear(true);
            StopPendingFloatingScores();
            ClearVisitors();
            ClearChildren(vfxRoot);
            floatingScoreView?.Clear();
            ClearJoyMultiplierIndicators();
            ClearDailyLimitIndicators();
            screenspaceIndicatorView?.Clear();
            SetShovelModeActive(false);
            SetBuildableSpritesVisible(false);

            if (evt.State != null && IsFinalDay(evt.State))
            {
                ShowResult(evt.State);
                return;
            }

            if (evt.State != null)
            {
                StartCoroutine(AdvanceToNextDay(evt.State));
            }
        }

        private IEnumerator AdvanceToNextDay(RuntimeLevelState completedState)
        {
            if (cameraController != null)
            {
                yield return cameraController.TweenToEditView(completedState.Board);
            }

            if (director == null ||
                director.State != completedState ||
                completedState.Definition == null)
            {
                yield break;
            }

            int nextDayIndex = completedState.CurrentDayIndex + 1;
            if (!completedState.Definition.TryGetStageIndexForDay(nextDayIndex, out int nextStageIndex))
            {
                ShowResult(completedState);
                yield break;
            }

            director.StartDay(nextStageIndex, nextDayIndex);
        }

        private void ShowResult(RuntimeLevelState state)
        {
            PlayResultVfx();
            ResultView view = GetOrCreateResultView();
            view?.Show(state);
        }

        private void PlayResultVfx()
        {
            GameObject prefab = GetResultVfxPrefab();
            if (prefab == null)
            {
                return;
            }

            ScreenOverlayCameraController overlayController = GetOrCreateScreenOverlayCameraController();
            overlayController?.PlayTimed(
                prefab,
                ResultVfxDuration,
                OverlayVfxDestroyLingerDuration,
                true);
        }

        private static bool IsFinalDay(RuntimeLevelState state)
        {
            if (state == null || state.Definition == null)
            {
                return false;
            }

            return state.CurrentDayIndex >= state.Definition.TotalDayCount - 1;
        }

        private void MoveVisitorView(VisitorState visitor, Vector2Int targetCell)
        {
            if (visitor == null ||
                director.State == null ||
                !visitorViews.TryGetValue(visitor.VisitorId, out GameObject visitorView))
            {
                return;
            }

            Vector3 targetPosition = GetVisitorWorldPosition(visitor.VisitorId, targetCell);
            if ((visitorView.transform.position - targetPosition).sqrMagnitude <=
                VisitorReachedTileCenterDistance * VisitorReachedTileCenterDistance &&
                !visitorMoveCoroutines.ContainsKey(visitor.VisitorId))
            {
                visitorView.transform.position = targetPosition;
                HandleVisitorReachedTileCenter(visitor, targetCell);
                return;
            }

            if (!visitorMoveTargetQueues.TryGetValue(visitor.VisitorId, out Queue<Vector2Int> targetQueue))
            {
                targetQueue = new Queue<Vector2Int>();
                visitorMoveTargetQueues[visitor.VisitorId] = targetQueue;
            }

            targetQueue.Enqueue(targetCell);

            if (!visitorMoveCoroutines.ContainsKey(visitor.VisitorId))
            {
                visitorMoveCoroutines[visitor.VisitorId] = null;

                Coroutine coroutine = StartCoroutine(ProcessVisitorMoveQueue(
                    visitor,
                    visitor.VisitorId,
                    visitorView,
                    targetQueue));

                if (visitorMoveCoroutines.ContainsKey(visitor.VisitorId))
                {
                    visitorMoveCoroutines[visitor.VisitorId] = coroutine;
                }
            }
        }

        private IEnumerator ProcessVisitorMoveQueue(
            VisitorState visitor,
            string visitorId,
            GameObject visitorView,
            Queue<Vector2Int> targetQueue)
        {
            Vector2Int lastReachedCell = default;
            bool hasReachedCell = false;

            while (visitorView != null && targetQueue.Count > 0)
            {
                Vector2Int targetCell = targetQueue.Dequeue();
                Vector3 targetPosition = GetVisitorWorldPosition(visitorId, targetCell);

                if (!visitorView.activeInHierarchy ||
                    (visitorView.transform.position - targetPosition).sqrMagnitude < Mathf.Epsilon)
                {
                    visitorView.transform.position = targetPosition;
                    HandleVisitorReachedTileCenter(visitor, targetCell);
                    lastReachedCell = targetCell;
                    hasReachedCell = true;
                    continue;
                }

                RotateVisitorTowards(visitorView, targetPosition - visitorView.transform.position);
                CrossFadeVisitorAnimation(visitorView, GetVisitorRunStateName());
                while (visitorView != null &&
                       (visitorView.transform.position - targetPosition).sqrMagnitude > Mathf.Epsilon)
                {
                    RotateVisitorTowards(visitorView, targetPosition - visitorView.transform.position);
                    visitorView.transform.position = Vector3.MoveTowards(
                        visitorView.transform.position,
                        targetPosition,
                        GetVisitorMoveSpeed() * Time.unscaledDeltaTime);
                    yield return null;
                }

                if (visitorView == null)
                {
                    break;
                }

                visitorView.transform.position = targetPosition;
                HandleVisitorReachedTileCenter(visitor, targetCell);
                lastReachedCell = targetCell;
                hasReachedCell = true;
            }

            if (visitorView != null)
            {
                CrossFadeVisitorAnimation(visitorView, GetVisitorIdleStateName());
                if (hasReachedCell && IsVisitorExitCell(lastReachedCell))
                {
                    yield return FadeOutVisitorView(visitorView);
                    fellowVisualManager?.RemoveVisitor(visitorId);
                    visitorViews.Remove(visitorId);
                    visitorSurfaceOffsets.Remove(visitorId);
                    pendingMochiUnitTriggeredVfxByVisitor.Remove(visitorId);
                }
            }

            visitorMoveCoroutines.Remove(visitorId);
            if (targetQueue.Count == 0)
            {
                visitorMoveTargetQueues.Remove(visitorId);
            }
        }

        private bool IsVisitorExitCell(Vector2Int cell)
        {
            if (director == null || director.State == null || director.State.Board.PathCells.Count == 0)
            {
                return false;
            }

            IReadOnlyList<Vector2Int> pathCells = director.State.Board.PathCells;
            return pathCells[pathCells.Count - 1] == cell;
        }

        private static IEnumerator FadeOutVisitorView(GameObject visitorView)
        {
            Renderer[] renderers = visitorView.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Destroy(visitorView);
                yield break;
            }

            List<Material> materials = new List<Material>();
            List<int> colorPropertyIds = new List<int>();
            List<Color> baseColors = new List<Color>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] rendererMaterials = renderers[i].materials;
                for (int j = 0; j < rendererMaterials.Length; j++)
                {
                    Material material = rendererMaterials[j];
                    if (material == null || !TryGetMaterialColorPropertyId(material, out int colorPropertyId))
                    {
                        continue;
                    }

                    ConfigureMaterialForFade(material);
                    materials.Add(material);
                    colorPropertyIds.Add(colorPropertyId);
                    baseColors.Add(material.GetColor(colorPropertyId));
                }
            }

            if (materials.Count == 0)
            {
                Destroy(visitorView);
                yield break;
            }

            float elapsed = 0f;
            while (visitorView != null && elapsed < VisitorExitFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alphaMultiplier = 1f - Mathf.Clamp01(elapsed / VisitorExitFadeDuration);
                for (int i = 0; i < materials.Count; i++)
                {
                    Color color = baseColors[i];
                    color.a *= alphaMultiplier;
                    materials[i].SetColor(colorPropertyIds[i], color);
                }

                yield return null;
            }

            if (visitorView != null)
            {
                Destroy(visitorView);
            }
        }

        private static bool TryGetMaterialColorPropertyId(Material material, out int colorPropertyId)
        {
            if (material.HasProperty(MaterialBaseColorId))
            {
                colorPropertyId = MaterialBaseColorId;
                return true;
            }

            if (material.HasProperty(MaterialColorId))
            {
                colorPropertyId = MaterialColorId;
                return true;
            }

            colorPropertyId = 0;
            return false;
        }

        private static void ConfigureMaterialForFade(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 2f);
            }

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void CrossFadeVisitorAnimation(GameObject visitorView, string stateName)
        {
            if (visitorView == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            Animator animator = visitorView.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            animator.CrossFade(stateName, GetVisitorAnimationCrossFadeDuration());
        }

        private string GetVisitorIdleStateName()
        {
            GlobalGameConfigSO globalConfig = director != null && director.State != null ? director.State.GlobalConfig : null;
            return globalConfig != null ? globalConfig.DefaultVisitorIdleStateName : "Idle";
        }

        private string GetVisitorRunStateName()
        {
            GlobalGameConfigSO globalConfig = director != null && director.State != null ? director.State.GlobalConfig : null;
            return globalConfig != null ? globalConfig.DefaultVisitorRunStateName : "Run";
        }

        private float GetVisitorAnimationCrossFadeDuration()
        {
            GlobalGameConfigSO globalConfig = director != null && director.State != null ? director.State.GlobalConfig : null;
            return globalConfig != null ? globalConfig.DefaultVisitorAnimationCrossFadeDuration : 0.1f;
        }

        private int GetMaxFellowCount()
        {
            GlobalGameConfigSO globalConfig = director != null && director.State != null ? director.State.GlobalConfig : null;
            return globalConfig != null ? globalConfig.DefaultMaxFellowCount : 4;
        }

        private float GetFellowStackOffset()
        {
            GlobalGameConfigSO globalConfig = director != null && director.State != null ? director.State.GlobalConfig : null;
            return globalConfig != null ? globalConfig.DefaultFellowStackOffset : 0.45f;
        }

        private float GetFellowScale()
        {
            GlobalGameConfigSO globalConfig = director != null && director.State != null ? director.State.GlobalConfig : null;
            return globalConfig != null ? globalConfig.DefaultFellowScale : 0.75f;
        }

        private static void RotateVisitorTowards(GameObject visitorView, Vector3 moveDirection)
        {
            moveDirection.y = 0f;
            if (moveDirection.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            visitorView.transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
        }

        private Vector3 GetVisitorWorldPosition(string visitorId, Vector2Int cell)
        {
            Vector3 surfacePosition = director.State.Board.CellToWorldSurface(cell);
            float surfaceOffset = visitorSurfaceOffsets.TryGetValue(visitorId, out float storedSurfaceOffset)
                ? storedSurfaceOffset
                : DefaultVisitorHeightOffset;
            return surfacePosition + Vector3.up * surfaceOffset;
        }

        private static float PositionVisitorOnSurface(GameObject visitorView, Vector3 surfacePosition)
        {
            if (visitorView == null)
            {
                return DefaultVisitorHeightOffset;
            }

            visitorView.transform.position = surfacePosition;
            float surfaceOffset = GetVisitorSurfaceOffset(visitorView);
            visitorView.transform.position += Vector3.up * surfaceOffset;
            return surfaceOffset;
        }

        private static float GetVisitorSurfaceOffset(GameObject visitorView)
        {
            if (TryGetUnitVisualBounds(visitorView, out Bounds bounds))
            {
                return visitorView.transform.position.y - bounds.min.y;
            }

            return DefaultVisitorHeightOffset;
        }

        private float GetVisitorMoveSpeed()
        {
            float visitorSpeed = director.State.CurrentDayConfig != null
                ? director.State.CurrentDayConfig.VisitorSpeed
                : 1f;
            float gameSpeed = director.State.GameSpeed != null
                ? director.State.GameSpeed.EffectiveMultiplier
                : 1f;
            return Mathf.Max(
                VisitorMinimumMoveSpeed,
                director.State.Board.TileSizeXZ * visitorSpeed * Mathf.Max(0.01f, gameSpeed));
        }

        private void ClearVisitors()
        {
            foreach (Coroutine coroutine in visitorMoveCoroutines.Values)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }

            visitorMoveCoroutines.Clear();
            visitorMoveTargetQueues.Clear();
            visitorSurfaceOffsets.Clear();
            pendingMochiUnitTriggeredVfxByVisitor.Clear();
            fellowVisualManager?.Clear();

            foreach (GameObject view in visitorViews.Values)
            {
                if (view != null)
                {
                    Destroy(view);
                }
            }

            visitorViews.Clear();
        }

        private Material GetMaterial(BoardCellType type)
        {
            switch (type)
            {
                case BoardCellType.Path:
                case BoardCellType.Start:
                case BoardCellType.Exit:
                    return pathMaterial;
                case BoardCellType.BuildableOpen:
                    return buildableMaterial;
                case BoardCellType.BuildableLocked:
                    return lockedMaterial;
                default:
                    return lockedMaterial;
            }
        }

        private void CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            pathMaterial = CreateMaterial(shader, new Color(0.82f, 0.68f, 0.47f, 1f));
            buildableMaterial = CreateMaterial(shader, new Color(0.34f, 0.62f, 0.38f, 1f));
            lockedMaterial = CreateMaterial(shader, new Color(0.34f, 0.38f, 0.43f, 1f));
            unitMaterial = CreateMaterial(shader, new Color(0.95f, 0.58f, 0.72f, 1f));
            visitorMaterial = CreateMaterial(shader, new Color(0.78f, 0.94f, 1f, 1f));
        }

        private static Material CreateMaterial(Shader shader, Color color)
        {
            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }
    }
}
