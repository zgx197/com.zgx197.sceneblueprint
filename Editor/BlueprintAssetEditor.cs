#nullable enable
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime;
using NodeGraph.Serialization;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// BlueprintAsset 的自定义 Inspector 编辑器。
    /// 在默认属性之外显示图数据摘要信息和快捷操作按钮。
    /// </summary>
    [CustomEditor(typeof(BlueprintAsset))]
    public class BlueprintAssetEditor : UnityEditor.Editor
    {
        // ── 缓存的摘要信息（避免每帧解析 JSON）──
        private bool _summaryDirty = true;
        private int _nodeCount;
        private int _edgeCount;
        private string[] _actionTypes = System.Array.Empty<string>();
        private string _cachedGraphJson = "";

        public override void OnInspectorGUI()
        {
            // 绘制默认属性（BlueprintId, BlueprintName, Description, Version）
            DrawDefaultInspector();

            var asset = (BlueprintAsset)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("图数据概要", EditorStyles.boldLabel);

            if (asset.IsEmpty)
            {
                EditorGUILayout.HelpBox("蓝图为空，尚未保存图数据。", MessageType.Info);
            }
            else
            {
                // 检测 GraphJson 是否变化，需要重新解析
                string graphJson = asset.GraphData?.text ?? "";
                if (_summaryDirty || _cachedGraphJson != graphJson)
                {
                    ParseSummary(graphJson);
                    _cachedGraphJson = graphJson;
                    _summaryDirty = false;
                }

                EditorGUILayout.LabelField("节点数", _nodeCount.ToString());
                EditorGUILayout.LabelField("连线数", _edgeCount.ToString());

                if (_actionTypes.Length > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("包含的行动类型", EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    foreach (var typeId in _actionTypes)
                    {
                        EditorGUILayout.LabelField($"• {typeId}", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(4);

                // 显示 JSON 数据大小
                int jsonSize = System.Text.Encoding.UTF8.GetByteCount(asset.GraphData?.text ?? "");
                string sizeText = jsonSize < 1024
                    ? $"{jsonSize} B"
                    : $"{jsonSize / 1024f:F1} KB";
                EditorGUILayout.LabelField("数据大小", sizeText);
            }

            EditorGUILayout.Space(8);

            // 快捷操作按钮
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("在蓝图编辑器中打开", GUILayout.Height(24)))
            {
                OpenInEditor(asset);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>解析 GraphJson 提取摘要信息（轻量解析，不完整反序列化）</summary>
        private void ParseSummary(string graphJson)
        {
            _nodeCount = 0;
            _edgeCount = 0;
            _actionTypes = System.Array.Empty<string>();

            if (string.IsNullOrEmpty(graphJson)) return;

            try
            {
                var serializer = new JsonGraphSerializer(new ActionNodeDataSerializer());
                var graph = serializer.Deserialize(graphJson);

                _nodeCount = graph.Nodes.Count;
                _edgeCount = graph.Edges.Count;

                // 收集唯一的 TypeId
                var typeSet = new System.Collections.Generic.HashSet<string>();
                foreach (var node in graph.Nodes)
                {
                    if (!string.IsNullOrEmpty(node.TypeId))
                        typeSet.Add(node.TypeId);
                }

                _actionTypes = new string[typeSet.Count];
                typeSet.CopyTo(_actionTypes);
                System.Array.Sort(_actionTypes);
            }
            catch (System.Exception ex)
            {
                SBLog.Warn(SBLogTags.Blueprint, $"解析图数据摘要失败: {ex.Message}");
            }
        }

        private void OpenInEditor(BlueprintAsset asset)
        {
            var window = EditorWindow.GetWindow<SceneBlueprintWindow>();
            window.titleContent = new GUIContent("场景蓝图编辑器");
            window.minSize = new Vector2(800, 600);
            window.Show();
            window.LoadFromAsset(asset);
        }
    }
}
