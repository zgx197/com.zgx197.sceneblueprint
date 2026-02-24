#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Editor.Markers.Annotations;

namespace SceneBlueprint.Editor.Markers.ToolKit
{
    /// <summary>
    /// AreaMarker 自定义 Inspector 编辑器。
    /// <para>
    /// 在默认属性之外，新增"位置生成工具"折叠面板：
    /// - 选择生成策略（随机 / 圆形阵型）
    /// - 设置数量、最小间距
    /// - [生成] 直接创建子 PointMarker（无临时预览，零 Handle 冲突）
    /// - [重新随机] 删除已生成的子 PointMarker 并重新生成
    /// - [清除全部] 删除所有子 PointMarker
    /// - 微调：策划直接在 Hierarchy 中选中 PointMarker，用 Unity 原生 W/E/R 操作
    /// </para>
    /// </summary>
    // TODO: 阶段2迁移 - 位置生成工具将迁移到业务层作为 IMarkerEditorExtension
    // [CustomEditor(typeof(AreaMarker))]
    public class AreaMarkerEditor : UnityEditor.Editor
    {
        // ── 位置生成工具状态 ──
        private bool _toolFoldout;
        private PositionGenerationStrategy _strategy = PositionGenerationStrategy.Random;
        private int _count = 4;
        private float _minSpacing = 2f;
        private int _seed;

        // ── 自动添加标注 ──
        private bool _autoAddAnnotation = true;
        private int _selectedAnnotationIndex;
        private string[] _annotationDisplayNames = Array.Empty<string>();
        private IReadOnlyList<AnnotationDefinition>? _applicableAnnotations;

        private AreaMarker Target => (AreaMarker)target;

        private void OnEnable()
        {
            _seed = Environment.TickCount & 0x7FFFFFFF;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            _toolFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_toolFoldout, "位置生成工具");
            if (_toolFoldout)
            {
                DrawPositionGeneratorPanel();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>
        /// 绘制位置生成工具面板
        /// </summary>
        private void DrawPositionGeneratorPanel()
        {
            EditorGUI.indentLevel++;

            // 策略选择
            _strategy = (PositionGenerationStrategy)EditorGUILayout.EnumPopup("生成策略", _strategy);

            // 数量
            _count = EditorGUILayout.IntSlider("数量", _count, 1, 50);

            // 最小间距（仅随机策略）
            if (_strategy == PositionGenerationStrategy.Random)
            {
                _minSpacing = EditorGUILayout.Slider("最小间距", _minSpacing, 0.5f, 10f);
            }

            EditorGUILayout.Space(4);

            // ── 自动添加标注选项 ──
            RefreshApplicableAnnotations();
            if (_applicableAnnotations != null && _applicableAnnotations.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                _autoAddAnnotation = EditorGUILayout.Toggle("自动添加标注", _autoAddAnnotation);
                using (new EditorGUI.DisabledScope(!_autoAddAnnotation))
                {
                    _selectedAnnotationIndex = EditorGUILayout.Popup(
                        _selectedAnnotationIndex, _annotationDisplayNames);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);

            // ── 当前子 PointMarker 统计 ──
            int childCount = CountChildPointMarkers();
            if (childCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"已有 {childCount} 个子 PointMarker\n" +
                    "在 Hierarchy 中选中它们即可用 W/E/R 微调位置和朝向",
                    MessageType.Info);
            }

            EditorGUILayout.Space(4);

            // ── 生成按钮（每次点击都重新随机）──
            if (GUILayout.Button("随机生成", GUILayout.Height(28)))
            {
                _seed = Environment.TickCount & 0x7FFFFFFF;
                ClearChildPointMarkers();
                GeneratePointMarkers();
            }

            // ── 按钮行 2：清除全部 ──
            using (new EditorGUI.DisabledScope(childCount == 0))
            {
                var defaultBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("清除全部子点位", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog(
                        "清除确认",
                        $"确定要删除 {Target.gameObject.name} 下的 {childCount} 个子 PointMarker 吗？",
                        "删除", "取消"))
                    {
                        ClearChildPointMarkers();
                    }
                }
                GUI.backgroundColor = defaultBg;
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 在 AreaMarker 下直接创建子 PointMarker
        /// </summary>
        private void GeneratePointMarkers()
        {
            var results = PositionGenerator.Generate(
                Target, _count, _strategy, _minSpacing, _seed);

            if (results.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[位置生成工具] 未能生成任何点位，请检查区域大小和间距设置");
                return;
            }

            var areaMarker = Target;
            var parentTransform = areaMarker.transform;

            Undo.SetCurrentGroupName("生成位置点");
            int undoGroup = Undo.GetCurrentGroup();

            var createdObjects = new List<GameObject>();

            for (int i = 0; i < results.Length; i++)
            {
                var gen = results[i];

                string objName = $"Point_生成点_{(i + 1):D2}";
                var go = new GameObject(objName);
                Undo.RegisterCreatedObjectUndo(go, "创建位置点");

                go.transform.SetParent(parentTransform);
                go.transform.position = gen.Position;
                go.transform.rotation = gen.Rotation;

                var pointMarker = go.AddComponent<PointMarker>();
                pointMarker.MarkerName = $"生成点 {i + 1}";
                pointMarker.Tag = areaMarker.Tag;
                pointMarker.SubGraphId = areaMarker.SubGraphId;

                // 自动添加标注组件
                if (_autoAddAnnotation && _applicableAnnotations != null
                    && _selectedAnnotationIndex >= 0
                    && _selectedAnnotationIndex < _applicableAnnotations.Count)
                {
                    var annDef = _applicableAnnotations[_selectedAnnotationIndex];
                    go.AddComponent(annDef.ComponentType);
                }

                createdObjects.Add(go);
            }

            Undo.CollapseUndoOperations(undoGroup);

            MarkerCache.SetDirty();
            SceneView.RepaintAll();
            Repaint();

            UnityEngine.Debug.Log($"[位置生成工具] 已在 {areaMarker.gameObject.name} 下生成 {createdObjects.Count} 个 PointMarker");
        }

        /// <summary>
        /// 删除 AreaMarker 下所有子 PointMarker
        /// </summary>
        private void ClearChildPointMarkers()
        {
            var areaMarker = Target;
            var children = GetChildPointMarkers();

            if (children.Count == 0) return;

            Undo.SetCurrentGroupName("清除子点位");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var child in children)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }

            Undo.CollapseUndoOperations(undoGroup);

            MarkerCache.SetDirty();
            SceneView.RepaintAll();
            Repaint();
        }

        /// <summary>
        /// 获取 AreaMarker 下所有子 PointMarker
        /// </summary>
        private List<PointMarker> GetChildPointMarkers()
        {
            var result = new List<PointMarker>();
            var parent = Target.transform;
            for (int i = 0; i < parent.childCount; i++)
            {
                var pm = parent.GetChild(i).GetComponent<PointMarker>();
                if (pm != null)
                    result.Add(pm);
            }
            return result;
        }

        /// <summary>
        /// 刷新适用于 PointMarker 的标注定义列表（惰性刷新）
        /// </summary>
        private void RefreshApplicableAnnotations()
        {
            if (_applicableAnnotations != null) return;

            _applicableAnnotations = AnnotationDefinitionRegistry.GetApplicable(
                SceneBlueprint.Core.MarkerTypeIds.Point);

            _annotationDisplayNames = new string[_applicableAnnotations.Count];
            for (int i = 0; i < _applicableAnnotations.Count; i++)
                _annotationDisplayNames[i] = _applicableAnnotations[i].DisplayName;

            // 确保索引合法
            if (_selectedAnnotationIndex >= _applicableAnnotations.Count)
                _selectedAnnotationIndex = 0;
        }

        /// <summary>
        /// 统计子 PointMarker 数量
        /// </summary>
        private int CountChildPointMarkers()
        {
            int count = 0;
            var parent = Target.transform;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).GetComponent<PointMarker>() != null)
                    count++;
            }
            return count;
        }
    }
}
