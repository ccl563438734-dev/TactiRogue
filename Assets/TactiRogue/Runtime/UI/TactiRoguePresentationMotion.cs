using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace TactiRogue
{
    public enum UnitSocketType
    {
        Shadow,
        VFX,
        Selection,
    }

    public enum MotionDefinitionCategory
    {
        Idle,
        Move,
        Attack,
        Hit,
        Cast,
        Death,
        Spawn,
        Special,
    }

    public enum MotionTargetLayer
    {
        UnitRoot,
        MotionRoot,
        RotationRoot,
        ScaleRoot,
        VisualRoot,
        Frame,
        Portrait,
        ShadowSocket,
        VFXSocket,
        SelectionSocket,
    }

    public enum MotionSegmentType
    {
        MoveBy,
        MoveTo,
        RotateBy,
        RotateTo,
        ScaleBy,
        ScaleTo,
        ShakePosition,
        ShakeRotation,
        PunchScale,
        Pause,
        CallbackMarker,
    }

    [Serializable]
    public sealed class MotionRuntimeContext
    {
        public Vector3 startWorldPosition;
        public Vector3 targetWorldPosition;
        public Vector3 direction = Vector3.forward;
        public int gridDistance;
        public bool hasHit;
        public bool hasCollision;
        public Transform targetTransform;

        public static MotionRuntimeContext At(Vector3 position)
        {
            return new MotionRuntimeContext
            {
                startWorldPosition = position,
                targetWorldPosition = position,
                direction = Vector3.forward,
            };
        }
    }

    [Serializable]
    public sealed class MotionSegment
    {
        public bool Enabled = true;
        public MotionSegmentType SegmentType = MotionSegmentType.MoveBy;
        public MotionTargetLayer TargetLayer = MotionTargetLayer.MotionRoot;
        public float Duration = 0.2f;
        public float Delay;
        public Ease Ease = Ease.OutQuad;
        public AnimationCurve Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public Vector3 Vector = Vector3.zero;
        public float Strength = 0.1f;
        public int Vibrato = 10;
        public float Elasticity = 1f;
        public bool UseDirection;
        public bool UseDistanceScale;
        public bool ResetOnComplete = true;
        public string MarkerName;

        public MotionSegment Clone()
        {
            return new MotionSegment
            {
                Enabled = Enabled,
                SegmentType = SegmentType,
                TargetLayer = TargetLayer,
                Duration = Duration,
                Delay = Delay,
                Ease = Ease,
                Curve = Curve == null ? null : new AnimationCurve(Curve.keys),
                Vector = Vector,
                Strength = Strength,
                Vibrato = Vibrato,
                Elasticity = Elasticity,
                UseDirection = UseDirection,
                UseDistanceScale = UseDistanceScale,
                ResetOnComplete = ResetOnComplete,
                MarkerName = MarkerName,
            };
        }
    }

    [CreateAssetMenu(fileName = "MotionDefinition", menuName = "TactiRogue/Motion Definition")]
    public sealed class MotionDefinition : ScriptableObject
    {
        public string Id;
        public MotionDefinitionCategory Category = MotionDefinitionCategory.Idle;
        public List<MotionSegment> Segments = new List<MotionSegment>();

        public float TotalDuration
        {
            get
            {
                var total = 0f;
                foreach (var segment in Segments)
                {
                    if (segment == null || !segment.Enabled)
                    {
                        continue;
                    }

                    total += Mathf.Max(0f, segment.Delay) + Mathf.Max(0f, segment.Duration);
                }

                return total;
            }
        }
    }

    public sealed class UnitPresentationView : MonoBehaviour
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private MaterialPropertyBlock _portraitBlock;
        private readonly List<Renderer> _frameRenderers = new List<Renderer>();

        [SerializeField] private Transform _motionRoot;
        [SerializeField] private Transform _rotationRoot;
        [SerializeField] private Transform _scaleRoot;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Transform _frameRoot;
        [SerializeField] private Transform _portraitRoot;
        [SerializeField] private Transform _shadowSocket;
        [SerializeField] private Transform _vfxSocket;
        [SerializeField] private Transform _selectionSocket;
        [SerializeField] private Renderer _portraitRenderer;

        private Quaternion _defaultRotationRootRotation = Quaternion.identity;
        private Vector3 _defaultVisualRootScale = Vector3.one;
        private GameObject _frameInstance;
        private GameObject _portraitPlane;

        public Transform UnitRoot => transform;
        public Transform MotionRoot => _motionRoot;
        public Transform RotationRoot => _rotationRoot;
        public Transform ScaleRoot => _scaleRoot;
        public Transform VisualRoot => _visualRoot;
        public Transform FrameRoot => _frameRoot;
        public Transform PortraitRoot => _portraitRoot;
        public Renderer PortraitRenderer => _portraitRenderer;
        public IReadOnlyList<Renderer> FrameRenderers => _frameRenderers;

        public static UnitPresentationView CreateGenerated(int entityId, Transform parent)
        {
            var root = new GameObject($"UnitRoot_{entityId}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<UnitPresentationView>();
            view.EnsureStructure();
            return view;
        }

        public void EnsureStructure()
        {
            _motionRoot = FindOrCreate(UnitRoot, "MotionRoot", _motionRoot);
            _rotationRoot = FindOrCreate(_motionRoot, "RotationRoot", _rotationRoot);
            _scaleRoot = FindOrCreate(_rotationRoot, "ScaleRoot", _scaleRoot);
            _visualRoot = FindOrCreate(_scaleRoot, "VisualRoot", _visualRoot);
            _frameRoot = FindOrCreate(_visualRoot, "Frame", _frameRoot);
            _portraitRoot = FindOrCreate(_visualRoot, "Portrait", _portraitRoot);
            _shadowSocket = FindOrCreate(_visualRoot, "ShadowSocket", _shadowSocket);
            _vfxSocket = FindOrCreate(_visualRoot, "VFXSocket", _vfxSocket);
            _selectionSocket = FindOrCreate(_visualRoot, "SelectionSocket", _selectionSocket);

            if (_portraitRenderer == null)
            {
                EnsurePortraitPlane();
            }

            RefreshRendererCaches();
        }

        public void ConfigureDefaultPose(float idleTiltAngle, float defaultScale)
        {
            EnsureStructure();
            _defaultRotationRootRotation = Quaternion.Euler(-(90f - idleTiltAngle), 0f, 0f);
            _defaultVisualRootScale = Vector3.one * Mathf.Max(0.01f, defaultScale);
            ResetVisualState();
        }

        public void SetFramePrefab(GameObject framePrefab, Material frameMaterial)
        {
            EnsureStructure();
            if (_frameInstance != null)
            {
                DestroyRuntimeObject(_frameInstance);
                _frameInstance = null;
            }

            foreach (Transform child in _frameRoot)
            {
                DestroyRuntimeObject(child.gameObject);
            }

            if (framePrefab != null)
            {
                _frameInstance = Instantiate(framePrefab, _frameRoot, false);
                _frameInstance.name = "FrameModel";
            }
            else
            {
                _frameInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _frameInstance.name = "RuntimeFrameFallback";
                _frameInstance.transform.SetParent(_frameRoot, false);
                _frameInstance.transform.localScale = new Vector3(1.08f, 1.42f, 0.08f);
                var collider = _frameInstance.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyRuntimeObject(collider);
                }
            }

            RefreshRendererCaches();
            SetFrameMaterial(frameMaterial);
        }

        public void SetPortrait(Texture texture)
        {
            EnsureStructure();
            if (_portraitRenderer == null)
            {
                return;
            }

            if (_portraitBlock == null)
            {
                _portraitBlock = new MaterialPropertyBlock();
            }

            _portraitRenderer.GetPropertyBlock(_portraitBlock);
            _portraitBlock.SetTexture(MainTexId, texture);
            _portraitRenderer.SetPropertyBlock(_portraitBlock);
        }

        public void SetPortrait(Sprite sprite)
        {
            SetPortrait(sprite == null ? null : sprite.texture);
        }

        public void SetFrameMaterial(Material material)
        {
            EnsureStructure();
            if (material == null)
            {
                return;
            }

            foreach (var renderer in _frameRenderers)
            {
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        public void SetSelected(bool selected)
        {
            EnsureStructure();
            if (_selectionSocket != null)
            {
                _selectionSocket.gameObject.SetActive(selected);
            }
        }

        public Transform GetSocket(UnitSocketType socketType)
        {
            EnsureStructure();
            switch (socketType)
            {
                case UnitSocketType.Shadow:
                    return _shadowSocket;
                case UnitSocketType.VFX:
                    return _vfxSocket;
                case UnitSocketType.Selection:
                    return _selectionSocket;
                default:
                    return _vfxSocket;
            }
        }

        public Transform GetTarget(MotionTargetLayer layer)
        {
            EnsureStructure();
            switch (layer)
            {
                case MotionTargetLayer.UnitRoot:
                    return UnitRoot;
                case MotionTargetLayer.MotionRoot:
                    return _motionRoot;
                case MotionTargetLayer.RotationRoot:
                    return _rotationRoot;
                case MotionTargetLayer.ScaleRoot:
                    return _scaleRoot;
                case MotionTargetLayer.VisualRoot:
                    return _visualRoot;
                case MotionTargetLayer.Frame:
                    return _frameRoot;
                case MotionTargetLayer.Portrait:
                    return _portraitRoot;
                case MotionTargetLayer.ShadowSocket:
                    return _shadowSocket;
                case MotionTargetLayer.VFXSocket:
                    return _vfxSocket;
                case MotionTargetLayer.SelectionSocket:
                    return _selectionSocket;
                default:
                    return _motionRoot;
            }
        }

        public void ResetVisualState()
        {
            EnsureStructure();
            KillTweens();
            _motionRoot.localPosition = Vector3.zero;
            _motionRoot.localRotation = Quaternion.identity;
            _motionRoot.localScale = Vector3.one;
            _rotationRoot.localPosition = Vector3.zero;
            _rotationRoot.localRotation = _defaultRotationRootRotation;
            _rotationRoot.localScale = Vector3.one;
            _scaleRoot.localPosition = Vector3.zero;
            _scaleRoot.localRotation = Quaternion.identity;
            _scaleRoot.localScale = Vector3.one;
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = _defaultVisualRootScale;
            _frameRoot.localPosition = Vector3.zero;
            _frameRoot.localRotation = Quaternion.identity;
            _frameRoot.localScale = Vector3.one;
            _portraitRoot.localPosition = Vector3.zero;
            _portraitRoot.localRotation = Quaternion.identity;
            _portraitRoot.localScale = Vector3.one;
        }

        public void ResetLayer(MotionTargetLayer layer)
        {
            var target = GetTarget(layer);
            if (target == null || layer == MotionTargetLayer.UnitRoot)
            {
                return;
            }

            target.localPosition = Vector3.zero;
            target.localScale = layer == MotionTargetLayer.VisualRoot ? _defaultVisualRootScale : Vector3.one;
            target.localRotation = layer == MotionTargetLayer.RotationRoot ? _defaultRotationRootRotation : Quaternion.identity;
        }

        public void KillTweens()
        {
            KillTween(UnitRoot);
            KillTween(_motionRoot);
            KillTween(_rotationRoot);
            KillTween(_scaleRoot);
            KillTween(_visualRoot);
            KillTween(_frameRoot);
            KillTween(_portraitRoot);
            KillTween(_shadowSocket);
            KillTween(_vfxSocket);
            KillTween(_selectionSocket);
        }

        private void EnsurePortraitPlane()
        {
            if (_portraitPlane != null)
            {
                _portraitRenderer = _portraitPlane.GetComponent<Renderer>();
                return;
            }

            _portraitPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _portraitPlane.name = "PortraitSurface";
            _portraitPlane.transform.SetParent(_portraitRoot, false);
            _portraitPlane.transform.localPosition = new Vector3(0f, 0f, -0.014f);
            _portraitPlane.transform.localRotation = Quaternion.identity;
            _portraitPlane.transform.localScale = new Vector3(0.86f, 1.12f, 1f);
            var collider = _portraitPlane.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyRuntimeObject(collider);
            }

            _portraitRenderer = _portraitPlane.GetComponent<Renderer>();
        }

        private void RefreshRendererCaches()
        {
            _frameRenderers.Clear();
            if (_frameRoot != null)
            {
                _frameRenderers.AddRange(_frameRoot.GetComponentsInChildren<Renderer>(true));
            }

            if (_portraitRenderer == null && _portraitRoot != null)
            {
                _portraitRenderer = _portraitRoot.GetComponentInChildren<Renderer>(true);
            }
        }

        private static Transform FindOrCreate(Transform parent, string name, Transform existing)
        {
            if (existing != null)
            {
                return existing;
            }

            var found = parent.Find(name);
            if (found != null)
            {
                return found;
            }

            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void KillTween(Transform target)
        {
            if (target != null)
            {
                DOTween.Kill(target);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }

    public static class MotionPlayer
    {
        private const string ImpactMarker = "Impact";

        public static Sequence Play(
            UnitPresentationView view,
            MotionDefinition definition,
            MotionRuntimeContext context,
            bool resetBefore = false,
            bool resetAfter = true,
            Action<string> markerCallback = null)
        {
            if (view == null)
            {
                return null;
            }

            context ??= MotionRuntimeContext.At(view.UnitRoot.position);
            definition ??= CreateFallbackDefinition(MotionDefinitionCategory.Idle);
            if (resetBefore)
            {
                view.ResetVisualState();
            }

            var sequence = DOTween.Sequence();
            sequence.SetTarget(view.UnitRoot);

            foreach (var segment in definition.Segments)
            {
                if (segment == null || !segment.Enabled)
                {
                    continue;
                }

                if (segment.Delay > 0f)
                {
                    sequence.AppendInterval(segment.Delay);
                }

                AppendSegment(sequence, view, segment, context, markerCallback);
            }

            if (resetAfter)
            {
                sequence.OnComplete(view.ResetVisualState);
            }

            return sequence;
        }

        public static MotionDefinition LoadOrFallback(string resourceKey, MotionDefinitionCategory fallbackCategory)
        {
            if (!string.IsNullOrWhiteSpace(resourceKey))
            {
                var loaded = Resources.Load<MotionDefinition>(resourceKey);
                if (loaded != null)
                {
                    return loaded;
                }
            }

            return CreateFallbackDefinition(fallbackCategory);
        }

        public static MotionDefinition CreateFallbackDefinition(MotionDefinitionCategory category)
        {
            var definition = ScriptableObject.CreateInstance<MotionDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Id = $"fallback_{category.ToString().ToLowerInvariant()}";
            definition.Category = category;

            switch (category)
            {
                case MotionDefinitionCategory.Move:
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.MoveTo,
                        TargetLayer = MotionTargetLayer.UnitRoot,
                        Duration = 0.22f,
                        Ease = Ease.OutQuad,
                        ResetOnComplete = false,
                    });
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.PunchScale,
                        TargetLayer = MotionTargetLayer.ScaleRoot,
                        Duration = 0.16f,
                        Vector = new Vector3(0.06f, -0.04f, 0.06f),
                        Vibrato = 5,
                        Elasticity = 0.6f,
                    });
                    break;
                case MotionDefinitionCategory.Attack:
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.MoveBy,
                        TargetLayer = MotionTargetLayer.MotionRoot,
                        Duration = 0.08f,
                        Vector = new Vector3(0f, 0f, -0.12f),
                        UseDirection = true,
                        Ease = Ease.OutQuad,
                    });
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.MoveBy,
                        TargetLayer = MotionTargetLayer.MotionRoot,
                        Duration = 0.1f,
                        Vector = new Vector3(0f, 0f, 0.34f),
                        UseDirection = true,
                        Ease = Ease.InQuad,
                    });
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.CallbackMarker,
                        MarkerName = ImpactMarker,
                    });
                    break;
                case MotionDefinitionCategory.Hit:
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.ShakePosition,
                        TargetLayer = MotionTargetLayer.Portrait,
                        Duration = 0.16f,
                        Strength = 0.07f,
                        Vibrato = 12,
                        Elasticity = 0.7f,
                    });
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.PunchScale,
                        TargetLayer = MotionTargetLayer.ScaleRoot,
                        Duration = 0.16f,
                        Vector = new Vector3(0.08f, -0.06f, 0.08f),
                        Vibrato = 6,
                        Elasticity = 0.7f,
                    });
                    break;
                case MotionDefinitionCategory.Death:
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.ScaleTo,
                        TargetLayer = MotionTargetLayer.ScaleRoot,
                        Duration = 0.18f,
                        Vector = Vector3.zero,
                        Ease = Ease.InBack,
                        ResetOnComplete = false,
                    });
                    break;
                case MotionDefinitionCategory.Spawn:
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.ScaleTo,
                        TargetLayer = MotionTargetLayer.ScaleRoot,
                        Duration = 0.2f,
                        Vector = Vector3.one,
                        Ease = Ease.OutBack,
                        ResetOnComplete = false,
                    });
                    break;
                case MotionDefinitionCategory.Idle:
                    definition.Segments.Add(new MotionSegment
                    {
                        SegmentType = MotionSegmentType.RotateBy,
                        TargetLayer = MotionTargetLayer.RotationRoot,
                        Duration = 0.22f,
                        Vector = new Vector3(0f, 0f, 1.2f),
                        Ease = Ease.InOutSine,
                    });
                    break;
            }

            return definition;
        }

        private static void AppendSegment(
            Sequence sequence,
            UnitPresentationView view,
            MotionSegment segment,
            MotionRuntimeContext context,
            Action<string> markerCallback)
        {
            if (segment.SegmentType == MotionSegmentType.Pause)
            {
                sequence.AppendInterval(Mathf.Max(0f, segment.Duration));
                return;
            }

            if (segment.SegmentType == MotionSegmentType.CallbackMarker)
            {
                sequence.AppendCallback(() => markerCallback?.Invoke(segment.MarkerName));
                return;
            }

            var target = view.GetTarget(segment.TargetLayer);
            if (target == null)
            {
                return;
            }

            var duration = Mathf.Max(0.001f, segment.Duration);
            var vector = ResolveVector(segment, context);
            Tween tween = null;

            switch (segment.SegmentType)
            {
                case MotionSegmentType.MoveBy:
                    tween = target.DOLocalMove(target.localPosition + vector, duration);
                    break;
                case MotionSegmentType.MoveTo:
                    tween = segment.TargetLayer == MotionTargetLayer.UnitRoot
                        ? target.DOMove(context.targetWorldPosition, duration)
                        : target.DOLocalMove(vector, duration);
                    break;
                case MotionSegmentType.RotateBy:
                    tween = target.DOLocalRotate(target.localEulerAngles + vector, duration, RotateMode.Fast);
                    break;
                case MotionSegmentType.RotateTo:
                    tween = target.DOLocalRotate(vector, duration, RotateMode.Fast);
                    break;
                case MotionSegmentType.ScaleBy:
                    tween = target.DOScale(target.localScale + vector, duration);
                    break;
                case MotionSegmentType.ScaleTo:
                    tween = target.DOScale(vector, duration);
                    break;
                case MotionSegmentType.ShakePosition:
                    tween = target.DOShakePosition(duration, segment.Strength, Mathf.Max(1, segment.Vibrato), segment.Elasticity * 90f);
                    break;
                case MotionSegmentType.ShakeRotation:
                    tween = target.DOShakeRotation(duration, vector == Vector3.zero ? Vector3.one * segment.Strength : vector, Mathf.Max(1, segment.Vibrato), segment.Elasticity * 90f);
                    break;
                case MotionSegmentType.PunchScale:
                    tween = target.DOPunchScale(vector, duration, Mathf.Max(1, segment.Vibrato), segment.Elasticity);
                    break;
            }

            if (tween == null)
            {
                return;
            }

            if (segment.Ease == Ease.INTERNAL_Custom && segment.Curve != null)
            {
                tween.SetEase(segment.Curve);
            }
            else
            {
                tween.SetEase(segment.Ease);
            }

            if (segment.ResetOnComplete && segment.TargetLayer != MotionTargetLayer.UnitRoot)
            {
                tween.OnComplete(() => view.ResetLayer(segment.TargetLayer));
            }

            sequence.Append(tween);
        }

        private static Vector3 ResolveVector(MotionSegment segment, MotionRuntimeContext context)
        {
            var value = segment.Vector;
            if (segment.UseDistanceScale)
            {
                value *= Mathf.Max(1, context.gridDistance);
            }

            if (!segment.UseDirection)
            {
                return value;
            }

            var direction = context.direction;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.forward;
            }

            direction.Normalize();
            var horizontalMagnitude = new Vector2(value.x, value.z).magnitude;
            return new Vector3(direction.x * horizontalMagnitude, value.y, direction.z * horizontalMagnitude);
        }
    }
}
