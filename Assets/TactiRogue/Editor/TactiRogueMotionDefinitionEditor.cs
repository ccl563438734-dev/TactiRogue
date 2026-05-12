using System;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

namespace TactiRogue
{
    public sealed class TactiRogueMotionDefinitionEditor : EditorWindow
    {
        private MotionDefinition _definition;
        private int _selectedSegmentIndex;
        private Vector2 _categoryScroll;
        private Vector2 _segmentScroll;
        private Vector2 _parameterScroll;
        private GameObject _previewRoot;
        private UnitPresentationView _previewView;
        private Sequence _previewSequence;
        private double _lastPreviewTime;

        [MenuItem("Tools/TactiRogue/Motion Definition Editor")]
        public static void Open()
        {
            GetWindow<TactiRogueMotionDefinitionEditor>("Motion Definition Editor");
        }

        private void OnDisable()
        {
            StopPreview();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCategoryColumn();
                DrawSegmentColumn();
                DrawParameterColumn();
            }

            DrawTimeline();
            DrawPreviewControls();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var nextDefinition = (MotionDefinition)EditorGUILayout.ObjectField(_definition, typeof(MotionDefinition), false, GUILayout.MinWidth(240f));
                if (nextDefinition != _definition)
                {
                    StopPreview();
                    _definition = nextDefinition;
                    _selectedSegmentIndex = 0;
                }

                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                {
                    CreateDefinitionAsset();
                }

                GUILayout.FlexibleSpace();
                GUI.enabled = _definition != null;
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                {
                    EditorUtility.SetDirty(_definition);
                    AssetDatabase.SaveAssets();
                }

                GUI.enabled = true;
            }
        }

        private void DrawCategoryColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(140f)))
            {
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
                _categoryScroll = EditorGUILayout.BeginScrollView(_categoryScroll);
                foreach (MotionDefinitionCategory category in Enum.GetValues(typeof(MotionDefinitionCategory)))
                {
                    var selected = _definition != null && _definition.Category == category;
                    if (GUILayout.Toggle(selected, category.ToString(), "Button") && _definition != null && _definition.Category != category)
                    {
                        RecordDefinition("Change motion category");
                        _definition.Category = category;
                        EditorUtility.SetDirty(_definition);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSegmentColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(260f)))
            {
                EditorGUILayout.LabelField("Segments", EditorStyles.boldLabel);
                GUI.enabled = _definition != null;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add"))
                    {
                        RecordDefinition("Add motion segment");
                        _definition.Segments.Add(new MotionSegment());
                        _selectedSegmentIndex = _definition.Segments.Count - 1;
                        EditorUtility.SetDirty(_definition);
                    }

                    if (GUILayout.Button("Duplicate") && HasSelectedSegment)
                    {
                        RecordDefinition("Duplicate motion segment");
                        _definition.Segments.Insert(_selectedSegmentIndex + 1, SelectedSegment.Clone());
                        _selectedSegmentIndex++;
                        EditorUtility.SetDirty(_definition);
                    }
                }

                _segmentScroll = EditorGUILayout.BeginScrollView(_segmentScroll, GUILayout.MinHeight(260f));
                if (_definition != null)
                {
                    for (var index = 0; index < _definition.Segments.Count; index++)
                    {
                        var segment = _definition.Segments[index];
                        var label = segment == null ? "<missing>" : $"{index + 1}. {segment.SegmentType} -> {segment.TargetLayer}";
                        if (GUILayout.Toggle(_selectedSegmentIndex == index, label, "Button"))
                        {
                            _selectedSegmentIndex = index;
                        }
                    }
                }

                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = HasSelectedSegment && _selectedSegmentIndex > 0;
                    if (GUILayout.Button("Up"))
                    {
                        RecordDefinition("Move segment up");
                        SwapSegments(_selectedSegmentIndex, _selectedSegmentIndex - 1);
                        _selectedSegmentIndex--;
                    }

                    GUI.enabled = HasSelectedSegment && _definition != null && _selectedSegmentIndex < _definition.Segments.Count - 1;
                    if (GUILayout.Button("Down"))
                    {
                        RecordDefinition("Move segment down");
                        SwapSegments(_selectedSegmentIndex, _selectedSegmentIndex + 1);
                        _selectedSegmentIndex++;
                    }

                    GUI.enabled = HasSelectedSegment;
                    if (GUILayout.Button("Delete"))
                    {
                        RecordDefinition("Delete motion segment");
                        _definition.Segments.RemoveAt(_selectedSegmentIndex);
                        _selectedSegmentIndex = Mathf.Clamp(_selectedSegmentIndex, 0, _definition.Segments.Count - 1);
                        EditorUtility.SetDirty(_definition);
                    }

                    GUI.enabled = true;
                }
            }
        }

        private void DrawParameterColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(360f)))
            {
                EditorGUILayout.LabelField("Segment Parameters", EditorStyles.boldLabel);
                _parameterScroll = EditorGUILayout.BeginScrollView(_parameterScroll, GUILayout.MinHeight(260f));
                if (HasSelectedSegment)
                {
                    var segment = SelectedSegment;
                    EditorGUI.BeginChangeCheck();
                    segment.Enabled = EditorGUILayout.Toggle("Enabled", segment.Enabled);
                    segment.SegmentType = (MotionSegmentType)EditorGUILayout.EnumPopup("Segment Type", segment.SegmentType);
                    segment.TargetLayer = (MotionTargetLayer)EditorGUILayout.EnumPopup("Target Layer", segment.TargetLayer);
                    segment.Duration = Mathf.Max(0f, EditorGUILayout.FloatField("Duration", segment.Duration));
                    segment.Delay = Mathf.Max(0f, EditorGUILayout.FloatField("Delay", segment.Delay));
                    segment.Ease = (Ease)EditorGUILayout.EnumPopup("Ease", segment.Ease);
                    segment.Curve = EditorGUILayout.CurveField("Curve", segment.Curve);
                    segment.Vector = EditorGUILayout.Vector3Field("Vector", segment.Vector);
                    segment.Strength = EditorGUILayout.FloatField("Strength", segment.Strength);
                    segment.Vibrato = EditorGUILayout.IntField("Vibrato", segment.Vibrato);
                    segment.Elasticity = EditorGUILayout.FloatField("Elasticity", segment.Elasticity);
                    segment.UseDirection = EditorGUILayout.Toggle("Use Direction", segment.UseDirection);
                    segment.UseDistanceScale = EditorGUILayout.Toggle("Use Distance Scale", segment.UseDistanceScale);
                    segment.ResetOnComplete = EditorGUILayout.Toggle("Reset On Complete", segment.ResetOnComplete);
                    segment.MarkerName = EditorGUILayout.TextField("Marker", segment.MarkerName);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordDefinition("Edit motion segment");
                        EditorUtility.SetDirty(_definition);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Select or add a segment.", MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTimeline()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(_definition == null ? "Timeline" : $"Timeline | Total {_definition.TotalDuration:0.00}s", EditorStyles.boldLabel);
                if (_definition == null || _definition.Segments.Count == 0)
                {
                    EditorGUILayout.LabelField("No segments.");
                    return;
                }

                var cursor = 0f;
                foreach (var segment in _definition.Segments)
                {
                    if (segment == null || !segment.Enabled)
                    {
                        continue;
                    }

                    var start = cursor + Mathf.Max(0f, segment.Delay);
                    var end = start + Mathf.Max(0f, segment.Duration);
                    EditorGUILayout.LabelField($"{segment.SegmentType} [{segment.TargetLayer}]  {start:0.00}s - {end:0.00}s");
                    cursor = end;
                }
            }
        }

        private void DrawPreviewControls()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUI.enabled = _definition != null;
                if (GUILayout.Button("Preview Motion", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                {
                    StartPreview();
                }

                GUI.enabled = _previewRoot != null;
                if (GUILayout.Button("Stop Preview", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                {
                    StopPreview();
                }

                if (GUILayout.Button("Reset Preview", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    ResetPreview();
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
        }

        private bool HasSelectedSegment => _definition != null
                                           && _selectedSegmentIndex >= 0
                                           && _selectedSegmentIndex < _definition.Segments.Count
                                           && _definition.Segments[_selectedSegmentIndex] != null;

        private MotionSegment SelectedSegment => _definition.Segments[_selectedSegmentIndex];

        private void CreateDefinitionAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Motion Definition",
                "Motion_New.asset",
                "asset",
                "Choose where to save the motion definition.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var asset = CreateInstance<MotionDefinition>();
            asset.Id = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _definition = asset;
            _selectedSegmentIndex = 0;
        }

        private void SwapSegments(int left, int right)
        {
            var temp = _definition.Segments[left];
            _definition.Segments[left] = _definition.Segments[right];
            _definition.Segments[right] = temp;
            EditorUtility.SetDirty(_definition);
        }

        private void RecordDefinition(string label)
        {
            if (_definition != null)
            {
                Undo.RecordObject(_definition, label);
            }
        }

        private void StartPreview()
        {
            StopPreview();
            if (_definition == null)
            {
                return;
            }

            _previewRoot = new GameObject("MotionDefinitionPreview");
            _previewRoot.hideFlags = HideFlags.HideAndDontSave;
            _previewView = _previewRoot.AddComponent<UnitPresentationView>();
            _previewView.EnsureStructure();
            _previewView.ConfigureDefaultPose(45f, 1f);
            _previewView.SetFramePrefab(Resources.Load<GameObject>(CardPieceVisualRuntime.DefaultFrameModelKey), null);
            _previewView.SetPortrait(BattleBoard3DController.PlaceholderTexture);
            _previewRoot.transform.position = Vector3.zero;

            if (_definition.Category == MotionDefinitionCategory.Spawn)
            {
                _previewView.ScaleRoot.localScale = Vector3.zero;
            }

            var context = new MotionRuntimeContext
            {
                startWorldPosition = Vector3.zero,
                targetWorldPosition = new Vector3(1.5f, 0f, 0f),
                direction = Vector3.right,
                gridDistance = 2,
                hasHit = true,
            };
            _previewSequence = MotionPlayer.Play(_previewView, _definition, context, false, true);
            if (_previewSequence != null)
            {
                _previewSequence.SetUpdate(UpdateType.Manual);
            }

            _lastPreviewTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += TickPreview;
        }

        private void StopPreview()
        {
            EditorApplication.update -= TickPreview;
            _previewSequence?.Kill();
            _previewSequence = null;
            if (_previewRoot != null)
            {
                DestroyImmediate(_previewRoot);
                _previewRoot = null;
                _previewView = null;
            }
        }

        private void ResetPreview()
        {
            if (_previewView != null)
            {
                _previewView.ResetVisualState();
            }
        }

        private void TickPreview()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = Mathf.Clamp((float)(now - _lastPreviewTime), 0f, 0.05f);
            _lastPreviewTime = now;
            DOTween.ManualUpdate(delta, delta);
            Repaint();
        }
    }
}
