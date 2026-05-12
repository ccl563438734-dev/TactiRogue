using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TactiRogue
{
    public sealed class BoardCell3DView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Collider _collider;
        private Renderer _renderer;

        public GridPosition Position { get; private set; }
        public Color CurrentColor { get; private set; }

        public event Action<GridPosition> Clicked;
        public event Action<GridPosition> Hovered;
        public event Action<GridPosition> HoverExited;

        public void Initialize(GridPosition position)
        {
            Position = position;
            _collider = GetComponent<Collider>();
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _renderer.material = BattleBoard3DController.CreateColorMaterial(Color.white);
            }
        }

        public void SetVisual(Color color, bool interactable)
        {
            CurrentColor = color;
            if (_renderer != null)
            {
                _renderer.material.color = color;
            }

            if (_collider != null)
            {
                _collider.enabled = interactable;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(Position);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered?.Invoke(Position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverExited?.Invoke(Position);
        }
    }

    public sealed class UnitCardPieceView
    {
        private readonly UnitPresentationView _presentation;
        private readonly HashSet<string> _warningKeys;
        private readonly List<Sequence> _activeMotions = new List<Sequence>();
        private Material _portraitMaterial;
        private Material _frameMaterial;
        private string _appliedFrameModelKey;
        private string _appliedFrameMaterialKey;
        private string _appliedCardArtKey;
        private CardPieceVisualRuntime _currentVisual = CardPieceVisualRuntime.Default;

        public UnitCardPieceView(int entityId, Transform parent, HashSet<string> warningKeys)
        {
            EntityId = entityId;
            _warningKeys = warningKeys;
            _presentation = UnitPresentationView.CreateGenerated(entityId, parent);
        }

        public int EntityId { get; }
        public Vector3 WorldPosition => _presentation.UnitRoot.position;
        public float IdleTiltAngle { get; private set; }
        public UnitPresentationView Presentation => _presentation;

        public void Refresh(Vector3 worldPosition, CardPieceVisualRuntime visual, bool selected)
        {
            _currentVisual = visual ?? CardPieceVisualRuntime.Default;
            _presentation.gameObject.SetActive(true);
            _presentation.UnitRoot.position = worldPosition + new Vector3(0f, _currentVisual.YOffset, 0f);
            IdleTiltAngle = _currentVisual.IdleTiltAngle;

            _presentation.ConfigureDefaultPose(_currentVisual.IdleTiltAngle, _currentVisual.DefaultScale);
            EnsureFrame(_currentVisual.FrameModelKey, _currentVisual.FrameMaterialKey, _currentVisual.ModelKey);
            EnsurePortraitMaterial();
            if (!string.Equals(_appliedCardArtKey, _currentVisual.CardArtKey, StringComparison.Ordinal))
            {
                ApplyPortrait(_currentVisual);
            }

            _presentation.SetSelected(selected);
        }

        public void Destroy()
        {
            KillActiveMotions();
            UnityEngine.Object.Destroy(_presentation.gameObject);
        }

        public void PlayMove(Vector3 startWorldPosition, Vector3 targetWorldPosition)
        {
            var context = BuildContext(startWorldPosition, targetWorldPosition, null);
            _presentation.UnitRoot.position = startWorldPosition;
            PlayMotion(MotionDefinitionCategory.Move, context, false, null);
        }

        public void PlaySpawn()
        {
            _presentation.ScaleRoot.localScale = Vector3.zero;
            PlayMotion(MotionDefinitionCategory.Spawn, MotionRuntimeContext.At(WorldPosition), false, null);
        }

        public void PlayAttack(Vector3 targetWorldPosition)
        {
            PlayMotion(MotionDefinitionCategory.Attack, BuildContext(WorldPosition, targetWorldPosition, null), false, null);
        }

        public void PlayHit(Vector3 sourceWorldPosition)
        {
            PlayMotion(MotionDefinitionCategory.Hit, BuildContext(sourceWorldPosition, WorldPosition, null), false, null);
        }

        public void PlayDeath(Action onComplete)
        {
            PlayMotion(MotionDefinitionCategory.Death, MotionRuntimeContext.At(WorldPosition), false, onComplete);
        }

        public bool HasStandardPresentationHierarchy()
        {
            return _presentation.MotionRoot != null
                   && _presentation.RotationRoot != null
                   && _presentation.ScaleRoot != null
                   && _presentation.VisualRoot != null
                   && _presentation.FrameRoot != null
                   && _presentation.PortraitRoot != null
                   && _presentation.GetSocket(UnitSocketType.Shadow) != null
                   && _presentation.GetSocket(UnitSocketType.VFX) != null
                   && _presentation.GetSocket(UnitSocketType.Selection) != null;
        }

        private void PlayMotion(MotionDefinitionCategory category, MotionRuntimeContext context, bool resetBefore, Action onComplete)
        {
            if (category == MotionDefinitionCategory.Death)
            {
                KillActiveMotions();
            }

            var definition = MotionPlayer.LoadOrFallback(_currentVisual.GetMotionKey(category), category);
            var motion = MotionPlayer.Play(_presentation, definition, context, resetBefore, category != MotionDefinitionCategory.Death);
            if (motion == null)
            {
                onComplete?.Invoke();
                return;
            }

            _activeMotions.Add(motion);
            motion.OnKill(() => _activeMotions.Remove(motion));
            if (onComplete != null)
            {
                motion.OnComplete(() =>
                {
                    onComplete();
                });
            }
        }

        private void KillActiveMotions()
        {
            foreach (var motion in _activeMotions.ToArray())
            {
                motion?.Kill();
            }

            _activeMotions.Clear();
        }

        private MotionRuntimeContext BuildContext(Vector3 startWorldPosition, Vector3 targetWorldPosition, Transform targetTransform)
        {
            var direction = targetWorldPosition - startWorldPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.forward;
            }

            return new MotionRuntimeContext
            {
                startWorldPosition = startWorldPosition,
                targetWorldPosition = targetWorldPosition,
                direction = direction.normalized,
                gridDistance = Mathf.Max(1, Mathf.RoundToInt(new Vector2(direction.x, direction.z).magnitude)),
                hasHit = true,
                targetTransform = targetTransform,
            };
        }

        private void EnsureFrame(string frameModelKey, string frameMaterialKey, string legacyModelKey)
        {
            var effectiveFrameModelKey = string.IsNullOrWhiteSpace(frameModelKey) ? CardPieceVisualRuntime.DefaultFrameModelKey : frameModelKey;
            var frameSignature = $"{effectiveFrameModelKey}|{legacyModelKey}";
            if (!string.Equals(_appliedFrameModelKey, frameSignature, StringComparison.Ordinal))
            {
                var framePrefab = Resources.Load<GameObject>(effectiveFrameModelKey);
                if (framePrefab == null)
                {
                    WarnOnce($"missing_frame:{effectiveFrameModelKey}", $"Card piece frame '{effectiveFrameModelKey}' was not found. Trying fallback frame assets.");
                    framePrefab = Resources.Load<GameObject>(CardPieceVisualRuntime.DefaultBaseFrameModelKey);
                }

                if (framePrefab == null && !string.IsNullOrWhiteSpace(legacyModelKey))
                {
                    framePrefab = Resources.Load<GameObject>(legacyModelKey);
                }

                if (framePrefab == null)
                {
                    WarnOnce($"missing_frame_fallback:{effectiveFrameModelKey}", $"Card piece frame fallback for '{effectiveFrameModelKey}' was not found. Using runtime fallback frame.");
                }

                _presentation.SetFramePrefab(framePrefab, ResolveFrameMaterial(frameMaterialKey));
                _appliedFrameModelKey = frameSignature;
                _appliedFrameMaterialKey = frameMaterialKey;
                return;
            }

            if (!string.Equals(_appliedFrameMaterialKey, frameMaterialKey, StringComparison.Ordinal))
            {
                _presentation.SetFrameMaterial(ResolveFrameMaterial(frameMaterialKey));
                _appliedFrameMaterialKey = frameMaterialKey;
            }
        }

        private void EnsurePortraitMaterial()
        {
            if (_presentation.PortraitRenderer == null)
            {
                return;
            }

            _portraitMaterial ??= BattleBoard3DController.CreateTextureMaterial(BattleBoard3DController.PlaceholderTexture);
            _presentation.PortraitRenderer.sharedMaterial = _portraitMaterial;
        }

        private void ApplyPortrait(CardPieceVisualRuntime visual)
        {
            _presentation.SetPortrait(LoadTexture(visual.CardArtKey, "portrait_art"));
            _appliedCardArtKey = visual.CardArtKey;
        }

        private Material ResolveFrameMaterial(string frameMaterialKey)
        {
            if (!string.IsNullOrWhiteSpace(frameMaterialKey))
            {
                var material = Resources.Load<Material>(frameMaterialKey);
                if (material != null)
                {
                    return material;
                }

                WarnOnce($"missing_frame_material:{frameMaterialKey}", $"Card piece frame material '{frameMaterialKey}' was not found. Using default frame material.");
            }

            _frameMaterial ??= BattleBoard3DController.CreateColorMaterial(new Color(0.78f, 0.74f, 0.68f, 1f));
            return _frameMaterial;
        }

        private Texture2D LoadTexture(string key, string label)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                var texture = Resources.Load<Texture2D>(key);
                if (texture != null)
                {
                    return texture;
                }

                WarnOnce($"missing_texture:{label}:{key}", $"Card piece {label} texture '{key}' was not found. Using placeholder texture.");
            }

            return BattleBoard3DController.PlaceholderTexture;
        }

        private void WarnOnce(string key, string message)
        {
            if (_warningKeys.Add(key))
            {
                Debug.LogWarning(message);
            }
        }
    }

    public sealed class BattleBoard3DController
    {
        private const float CellSize = 1f;
        private const float CellGap = 0.08f;

        private static Texture2D _placeholderTexture;
        private readonly Dictionary<GridPosition, BoardCell3DView> _cells = new Dictionary<GridPosition, BoardCell3DView>();
        private readonly Dictionary<int, UnitCardPieceView> _unitViews = new Dictionary<int, UnitCardPieceView>();
        private readonly HashSet<int> _dyingEntityIds = new HashSet<int>();
        private readonly HashSet<string> _warningKeys = new HashSet<string>(StringComparer.Ordinal);
        private Transform _root;
        private Transform _cellRoot;
        private Transform _unitRoot;
        private Camera _camera;
        private RectTransform _viewportPanel;
        private int _boardWidth = -1;
        private int _boardHeight = -1;

        public int BoardCellCount => _cells.Count;
        public int UnitCardCount => _unitViews.Count;

        public static Texture2D PlaceholderTexture
        {
            get
            {
                if (_placeholderTexture != null)
                {
                    return _placeholderTexture;
                }

                _placeholderTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _placeholderTexture.SetPixels(new[]
                {
                    new Color(0.18f, 0.18f, 0.2f, 1f),
                    new Color(0.75f, 0.18f, 0.75f, 1f),
                    new Color(0.75f, 0.18f, 0.75f, 1f),
                    new Color(0.18f, 0.18f, 0.2f, 1f),
                });
                _placeholderTexture.Apply();
                _placeholderTexture.name = "CardPiecePlaceholder";
                return _placeholderTexture;
            }
        }

        public void Build(BattleSandboxBootstrap host, RectTransform viewportPanel)
        {
            _viewportPanel = viewportPanel;

            var rootGo = new GameObject("TactiRogue3DBoard");
            rootGo.transform.SetParent(host.transform, false);
            _root = rootGo.transform;
            _cellRoot = CreateChild(_root, "Cells");
            _unitRoot = CreateChild(_root, "Units");

            var cameraGo = new GameObject("TactiRogueBoardCamera", typeof(Camera), typeof(PhysicsRaycaster));
            cameraGo.transform.SetParent(_root, false);
            _camera = cameraGo.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.075f, 0.082f, 0.105f, 1f);
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.depth = 5f;
        }

        public void Refresh(
            BattleSandboxBootstrap host,
            IReadOnlyList<BattleEvent> motionEvents = null,
            IReadOnlyDictionary<int, EntityPresentationSnapshot> beforeSnapshots = null)
        {
            TrackPendingDeaths(motionEvents);
            UpdateCameraViewport();
            RebuildBoard(host);
            RefreshCellVisuals(host);
            RefreshUnitViews(host, beforeSnapshots);
            PositionCamera(host.State.Grid.Width, host.State.Grid.Height);
            PlayBattleMotions(motionEvents, beforeSnapshots);
        }

        public bool TryGetBoardCellBackground(GridPosition position, out Color color)
        {
            color = default;
            if (!_cells.TryGetValue(position, out var view))
            {
                return false;
            }

            color = view.CurrentColor;
            return true;
        }

        public bool TryGetUnitCardWorldPosition(int entityId, out Vector3 position)
        {
            position = default;
            if (!_unitViews.TryGetValue(entityId, out var view))
            {
                return false;
            }

            position = view.WorldPosition;
            return true;
        }

        public bool TryGetUnitCardIdleTiltAngle(int entityId, out float idleTiltAngle)
        {
            idleTiltAngle = 0f;
            if (!_unitViews.TryGetValue(entityId, out var view))
            {
                return false;
            }

            idleTiltAngle = view.IdleTiltAngle;
            return true;
        }

        public bool TryGetUnitPresentationHasStandardHierarchy(int entityId)
        {
            return _unitViews.TryGetValue(entityId, out var view) && view.HasStandardPresentationHierarchy();
        }

        public Vector3 GridToWorld(GridPosition position)
        {
            var originX = -(_boardWidth - 1) * CellSize * 0.5f;
            var originZ = -(_boardHeight - 1) * CellSize * 0.5f;
            return new Vector3(originX + position.X * CellSize, 0f, originZ + position.Y * CellSize);
        }

        public static Material CreateColorMaterial(Color color)
        {
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        public static Material CreateTextureMaterial(Texture2D texture)
        {
            var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.mainTexture = texture;
            material.color = Color.white;
            return material;
        }

        private void RebuildBoard(BattleSandboxBootstrap host)
        {
            var width = host.State.Grid.Width;
            var height = host.State.Grid.Height;
            if (_boardWidth == width && _boardHeight == height && _cells.Count == width * height)
            {
                return;
            }

            foreach (Transform child in _cellRoot)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            foreach (var unitView in _unitViews.Values)
            {
                unitView.Destroy();
            }

            _cells.Clear();
            _unitViews.Clear();
            _boardWidth = width;
            _boardHeight = height;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var position = new GridPosition(x, y);
                    var cellGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cellGo.name = $"Cell_{x}_{y}";
                    cellGo.transform.SetParent(_cellRoot, false);
                    cellGo.transform.position = GridToWorld(position);
                    cellGo.transform.localScale = new Vector3(CellSize - CellGap, 0.04f, CellSize - CellGap);
                    var view = cellGo.AddComponent<BoardCell3DView>();
                    view.Initialize(position);
                    view.Clicked += host.HandleBoardCellClicked;
                    view.Hovered += host.HandleBoardCellHovered;
                    view.HoverExited += host.HandleBoardCellHoverExited;
                    _cells[position] = view;
                }
            }
        }

        private void RefreshCellVisuals(BattleSandboxBootstrap host)
        {
            var state = host.State;
            var validTargets = new HashSet<GridPosition>(host.PreviewController.ActivePreview?.ValidTargetCells ?? Enumerable.Empty<GridPosition>());
            var previewKinds = host.PreviewController.ActivePreview?.CellPreviews
                .GroupBy(item => item.Position)
                .ToDictionary(group => group.Key, group => new HashSet<CellPreviewKind>(group.Select(item => item.Kind)))
                ?? new Dictionary<GridPosition, HashSet<CellPreviewKind>>();
            var usePreviewIntentState = host.PreviewController.ActivePreview != null
                                        && host.InputController.CurrentState != BattleInputState.MoveTargeting;
            var displayedDangerCells = new HashSet<GridPosition>(
                ((usePreviewIntentState)
                    ? host.PreviewController.ActivePreview.EnemyIntents
                    : host.Engine.BuildIntentViewData(host.State))
                .SelectMany(intent => intent.DangerCells));
            var selectedEntity = host.SelectionController.SelectedEntityId >= 0 && state.Entities.TryGetValue(host.SelectionController.SelectedEntityId, out var selectedEntityValue)
                ? selectedEntityValue
                : null;
            var selectedCell = host.InputController.CurrentState == BattleInputState.BehaviorTargeting && host.SelectionController.HasCommittedMoveCell
                ? host.SelectionController.CommittedMoveCell
                : selectedEntity?.Position ?? default;
            var hasSelectedCell = selectedEntity != null || (host.InputController.CurrentState == BattleInputState.BehaviorTargeting && host.SelectionController.HasCommittedMoveCell);

            foreach (var pair in _cells)
            {
                var cell = pair.Key;
                var view = pair.Value;
                var isValid = state.Grid.IsValid(cell);
                var background = isValid ? new Color(0.19f, 0.21f, 0.27f, 1f) : new Color(0.05f, 0.05f, 0.06f, 1f);

                if (displayedDangerCells.Contains(cell))
                {
                    background = Color.Lerp(background, new Color(0.65f, 0.14f, 0.14f, 1f), 0.55f);
                }

                if (previewKinds.TryGetValue(cell, out var kinds))
                {
                    if (kinds.Contains(CellPreviewKind.Impact))
                    {
                        background = Color.Lerp(background, new Color(0.95f, 0.66f, 0.18f, 1f), 0.65f);
                    }

                    if (kinds.Contains(CellPreviewKind.Collision))
                    {
                        background = Color.Lerp(background, new Color(0.84f, 0.38f, 0.84f, 1f), 0.75f);
                    }

                    if (kinds.Contains(CellPreviewKind.MovePath))
                    {
                        background = Color.Lerp(background, new Color(0.36f, 0.78f, 0.86f, 1f), 0.45f);
                    }
                }

                if (validTargets.Contains(cell))
                {
                    background = Color.Lerp(background, new Color(0.26f, 0.67f, 0.34f, 1f), 0.45f);
                }

                if (hasSelectedCell && selectedCell.Equals(cell))
                {
                    background = Color.Lerp(background, new Color(0.29f, 0.64f, 0.91f, 1f), 0.7f);
                }

                if (isValid && state.Grid.Occupancy.TryGetValue(cell, out var entityId) && state.Entities.TryGetValue(entityId, out var entity) && entity.IsAlive)
                {
                    var template = host.Engine.Catalog.GetEntity(entity.TemplateId);
                    background = Color.Lerp(background, template?.Tint ?? Color.white, 0.35f);
                }

                view.SetVisual(background, isValid);
            }
        }

        private void RefreshUnitViews(BattleSandboxBootstrap host, IReadOnlyDictionary<int, EntityPresentationSnapshot> beforeSnapshots)
        {
            var activeEntityIds = new HashSet<int>(host.State.LivingEntities()
                .Where(entity => entity.OccupiesCell)
                .Select(entity => entity.EntityId));
            foreach (var dyingEntityId in _dyingEntityIds)
            {
                if (beforeSnapshots != null
                    && beforeSnapshots.TryGetValue(dyingEntityId, out var snapshot)
                    && snapshot.OccupiesCell)
                {
                    activeEntityIds.Add(dyingEntityId);
                }
                else if (host.State.Entities.TryGetValue(dyingEntityId, out var dyingEntity) && dyingEntity.OccupiesCell)
                {
                    activeEntityIds.Add(dyingEntityId);
                }
            }

            foreach (var staleEntityId in _unitViews.Keys.Where(entityId => !activeEntityIds.Contains(entityId)).ToArray())
            {
                _unitViews[staleEntityId].Destroy();
                _unitViews.Remove(staleEntityId);
            }

            foreach (var entityId in activeEntityIds)
            {
                if (!host.State.Entities.TryGetValue(entityId, out var entity))
                {
                    continue;
                }

                if (!_unitViews.TryGetValue(entity.EntityId, out var view))
                {
                    view = new UnitCardPieceView(entity.EntityId, _unitRoot, _warningKeys);
                    _unitViews[entity.EntityId] = view;
                }

                var visual = ResolveVisual(host.State, host.Engine.Catalog, entity);
                var displayPosition = entity.IsAlive || beforeSnapshots == null || !beforeSnapshots.TryGetValue(entity.EntityId, out var snapshot)
                    ? entity.Position
                    : snapshot.Position;
                var selected = host.SelectionController.SelectedEntityId == entity.EntityId;
                view.Refresh(GridToWorld(displayPosition), visual, selected);
            }
        }

        private void TrackPendingDeaths(IReadOnlyList<BattleEvent> motionEvents)
        {
            if (motionEvents == null)
            {
                return;
            }

            foreach (var battleEvent in motionEvents.Where(item => item.EventType == BattleEventType.UnitDied && item.SubjectEntityId >= 0))
            {
                _dyingEntityIds.Add(battleEvent.SubjectEntityId);
            }
        }

        private void PlayBattleMotions(IReadOnlyList<BattleEvent> motionEvents, IReadOnlyDictionary<int, EntityPresentationSnapshot> beforeSnapshots)
        {
            if (motionEvents == null || motionEvents.Count == 0)
            {
                return;
            }

            foreach (var battleEvent in motionEvents)
            {
                switch (battleEvent.EventType)
                {
                    case BattleEventType.EntitySummoned:
                        if (_unitViews.TryGetValue(battleEvent.SubjectEntityId, out var summonedView))
                        {
                            summonedView.PlaySpawn();
                        }

                        break;
                    case BattleEventType.UnitMoved:
                        if (_unitViews.TryGetValue(battleEvent.SubjectEntityId, out var movedView))
                        {
                            var targetWorld = movedView.WorldPosition;
                            var startWorld = ResolveSnapshotWorldPosition(battleEvent.SubjectEntityId, beforeSnapshots, targetWorld.y, targetWorld);
                            movedView.PlayMove(startWorld, targetWorld);
                        }

                        break;
                    case BattleEventType.ActionUsed:
                        if (_unitViews.TryGetValue(battleEvent.SubjectEntityId, out var actorView))
                        {
                            actorView.PlayAttack(ResolveEventTargetWorldPosition(battleEvent, actorView.WorldPosition));
                        }

                        break;
                    case BattleEventType.DamageApplied:
                        if (_unitViews.TryGetValue(battleEvent.SubjectEntityId, out var hitView))
                        {
                            hitView.PlayHit(ResolveEntityWorldPosition(battleEvent.SecondaryEntityId, hitView.WorldPosition));
                        }

                        break;
                    case BattleEventType.CollisionOccurred:
                        var collisionSourcePosition = GridToWorld(battleEvent.Position);
                        if (_unitViews.TryGetValue(battleEvent.SubjectEntityId, out var collisionSubjectView))
                        {
                            collisionSourcePosition = collisionSubjectView.WorldPosition;
                            collisionSubjectView.PlayHit(ResolveEntityWorldPosition(battleEvent.SecondaryEntityId, collisionSubjectView.WorldPosition));
                        }

                        if (_unitViews.TryGetValue(battleEvent.SecondaryEntityId, out var collisionSecondaryView))
                        {
                            collisionSecondaryView.PlayHit(collisionSourcePosition);
                        }

                        break;
                    case BattleEventType.UnitDied:
                        if (_unitViews.TryGetValue(battleEvent.SubjectEntityId, out var deadView))
                        {
                            var entityId = battleEvent.SubjectEntityId;
                            deadView.PlayDeath(() =>
                            {
                                if (_unitViews.TryGetValue(entityId, out var viewToDestroy))
                                {
                                    viewToDestroy.Destroy();
                                    _unitViews.Remove(entityId);
                                }

                                _dyingEntityIds.Remove(entityId);
                            });
                        }

                        break;
                }
            }
        }

        private Vector3 ResolveSnapshotWorldPosition(
            int entityId,
            IReadOnlyDictionary<int, EntityPresentationSnapshot> beforeSnapshots,
            float y,
            Vector3 fallback)
        {
            if (beforeSnapshots != null && beforeSnapshots.TryGetValue(entityId, out var snapshot))
            {
                var world = GridToWorld(snapshot.Position);
                world.y = y;
                return world;
            }

            return fallback;
        }

        private Vector3 ResolveEntityWorldPosition(int entityId, Vector3 fallback)
        {
            return entityId >= 0 && _unitViews.TryGetValue(entityId, out var view) ? view.WorldPosition : fallback;
        }

        private Vector3 ResolveEventTargetWorldPosition(BattleEvent battleEvent, Vector3 fallback)
        {
            if (battleEvent.SecondaryEntityId >= 0 && _unitViews.TryGetValue(battleEvent.SecondaryEntityId, out var targetView))
            {
                return targetView.WorldPosition;
            }

            var world = GridToWorld(battleEvent.Position);
            world.y = fallback.y;
            return world;
        }

        private CardPieceVisualRuntime ResolveVisual(BattleState state, TactiRogueContentDatabase catalog, EntityInstance entity)
        {
            CardPieceVisualDefinition definition = null;
            if (state.InBattleUnitCards.TryGetValue(entity.EntityId, out var boundCardInstanceId)
                && state.TryGetCardInstance(boundCardInstanceId, out var boundCardInstance))
            {
                definition = catalog.GetCardPieceVisual(boundCardInstance.TemplateId);
            }

            definition ??= catalog.GetCardPieceVisual(entity.TemplateId);
            if (definition == null)
            {
                if (_warningKeys.Add($"missing_visual:{entity.TemplateId}"))
                {
                    Debug.LogWarning($"Card piece visual '{entity.TemplateId}' was not found. Using default visual config.");
                }

                return CardPieceVisualRuntime.Default;
            }

            return CardPieceVisualRuntime.FromDefinition(definition);
        }

        private void PositionCamera(int width, int height)
        {
            var boardSpan = Mathf.Max(width, height) * CellSize;
            var center = new Vector3(0f, 0f, 0f);
            _camera.transform.position = center + new Vector3(0f, boardSpan * 0.95f + 3.5f, -boardSpan * 0.95f - 3f);
            _camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            _camera.orthographicSize = Mathf.Max(height * CellSize * 0.72f, width * CellSize * 0.42f) + 1.2f;
        }

        private void UpdateCameraViewport()
        {
            if (_viewportPanel == null || _camera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var corners = new Vector3[4];
            _viewportPanel.GetWorldCorners(corners);
            var xMin = Mathf.Clamp01(corners[0].x / Screen.width);
            var yMin = Mathf.Clamp01(corners[0].y / Screen.height);
            var xMax = Mathf.Clamp01(corners[2].x / Screen.width);
            var yMax = Mathf.Clamp01(corners[2].y / Screen.height);
            var width = Mathf.Max(0.05f, xMax - xMin);
            var height = Mathf.Max(0.05f, yMax - yMin);
            _camera.rect = new Rect(xMin, yMin, width, height);
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }
    }

    public sealed class CardPieceVisualRuntime
    {
        public const string DefaultModelKey = "Assert/Model/sample";
        public const string DefaultFrameModelKey = "Assert/Model/F_Unit";
        public const string DefaultStructureFrameModelKey = "Assert/Model/F_Structure";
        public const string DefaultBaseFrameModelKey = "Assert/Model/F_Base";
        public const string DefaultBackArtKey = "Assert/Picture/卡背";

        public static readonly CardPieceVisualRuntime Default = new CardPieceVisualRuntime
        {
            ModelKey = DefaultModelKey,
            FrameModelKey = DefaultFrameModelKey,
            CardArtKey = string.Empty,
            BackArtKey = DefaultBackArtKey,
            IdleTiltAngle = 45f,
            DefaultScale = 1f,
            YOffset = 0.05f,
        };

        public string ModelKey;
        public string FrameModelKey;
        public string FrameMaterialKey;
        public string CardArtKey;
        public string BackArtKey;
        public float IdleTiltAngle;
        public float DefaultScale;
        public float YOffset;
        public string IdleMotionKey;
        public string MoveMotionKey;
        public string AttackMotionKey;
        public string HitMotionKey;
        public string DeathMotionKey;
        public string SpawnMotionKey;

        public static CardPieceVisualRuntime FromDefinition(CardPieceVisualDefinition definition)
        {
            return new CardPieceVisualRuntime
            {
                ModelKey = string.IsNullOrWhiteSpace(definition.ModelKey) ? DefaultModelKey : definition.ModelKey,
                FrameModelKey = string.IsNullOrWhiteSpace(definition.FrameModelKey) ? DefaultFrameModelKey : definition.FrameModelKey,
                FrameMaterialKey = definition.FrameMaterialKey,
                CardArtKey = definition.CardArtKey,
                BackArtKey = string.IsNullOrWhiteSpace(definition.BackArtKey) ? DefaultBackArtKey : definition.BackArtKey,
                IdleTiltAngle = definition.IdleTiltAngle,
                DefaultScale = definition.DefaultScale <= 0f ? 1f : definition.DefaultScale,
                YOffset = definition.YOffset,
                IdleMotionKey = definition.IdleMotionKey,
                MoveMotionKey = definition.MoveMotionKey,
                AttackMotionKey = definition.AttackMotionKey,
                HitMotionKey = definition.HitMotionKey,
                DeathMotionKey = definition.DeathMotionKey,
                SpawnMotionKey = definition.SpawnMotionKey,
            };
        }

        public string GetMotionKey(MotionDefinitionCategory category)
        {
            switch (category)
            {
                case MotionDefinitionCategory.Idle:
                    return IdleMotionKey;
                case MotionDefinitionCategory.Move:
                    return MoveMotionKey;
                case MotionDefinitionCategory.Attack:
                    return AttackMotionKey;
                case MotionDefinitionCategory.Hit:
                    return HitMotionKey;
                case MotionDefinitionCategory.Death:
                    return DeathMotionKey;
                case MotionDefinitionCategory.Spawn:
                    return SpawnMotionKey;
                default:
                    return string.Empty;
            }
        }
    }
}
