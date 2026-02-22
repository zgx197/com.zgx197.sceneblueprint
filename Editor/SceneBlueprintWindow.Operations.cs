#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NodeGraph.Commands;
using NodeGraph.Core;
using NodeGraph.Math;
using NodeGraph.Serialization;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Persistence;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        // ── 操作方法 ──

        private void NewGraph()
        {
            if (_session == null) return;

            bool confirm = _session.ViewModel.Graph.Nodes.Count == 0 ||
                EditorUtility.DisplayDialog("新建蓝图", "当前蓝图未保存，确定要新建吗？", "确定", "取消");

            if (confirm)
            {
                _currentAsset = null;
                RecreateSession(null);
                _session!.SetAsset(null);
                _session.AddDefaultNodes();
                Repaint();
            }
        }

        private void SaveBlueprint()
        {
            if (_session == null) return;

            string graphJson = _session.SerializeGraph();

            if (_currentAsset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(_currentAsset);
                BlueprintAssetStore.SaveAsset(assetPath, graphJson, _currentAsset);
                SBLog.Info(SBLogTags.Blueprint, $"已保存: {assetPath}");
                _session.UpdateTitle();
            }
            else
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "保存蓝图资产", "NewBlueprint", "asset", "选择蓝图保存位置");

                if (!string.IsNullOrEmpty(path))
                {
                    var newAsset = BlueprintAssetStore.CreateAsset(path, graphJson);
                    _session.SetAsset(newAsset);
                    _session.UpdateTitle();
                    SBLog.Info(SBLogTags.Blueprint, $"已创建: {path} (ID: {newAsset.BlueprintId})");
                }
            }
        }

        private void LoadBlueprint()
        {
            if (_session == null) return;

            bool confirm = _session.ViewModel.Graph.Nodes.Count == 0 ||
                EditorUtility.DisplayDialog("加载蓝图", "当前蓝图未保存，确定要加载吗？", "确定", "取消");
            if (!confirm) return;

            string path = EditorUtility.OpenFilePanel("加载蓝图资产", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            var asset = AssetDatabase.LoadAssetAtPath<BlueprintAsset>(path);
            if (asset == null)
            { EditorUtility.DisplayDialog("加载失败", "选择的文件不是有效的蓝图资产。", "确定"); return; }
            if (asset.IsEmpty)
            { EditorUtility.DisplayDialog("加载失败", "蓝图资产中没有图数据。", "确定"); return; }

            LoadFromAsset(asset);
        }

        /// <summary>从外部直接加载指定的 BlueprintAsset（供自定义 Inspector 调用）。</summary>
        public void LoadFromAsset(BlueprintAsset asset)
        {
            if (asset == null || asset.IsEmpty) return;

            try
            {
                var graphJson = BlueprintAssetStore.ReadJson(asset);
                if (graphJson == null) throw new System.InvalidOperationException("asset.GraphData 为空");
                var typeProvider = _session != null
                    ? _session.CreateProfileTypeProvider()
                    : BuildTypeProviderForReload();
                var serializer = new JsonGraphSerializer(new ActionNodeDataSerializer(), typeProvider);
                var graph = serializer.Deserialize(graphJson);

                _currentAsset = asset;
                RecreateSession(graph);
                // A1: RecreateSession 内部已调用 SetAsset(_currentAsset)，无需重复设置
                _session!.RestoreBindingsFromScene();
                _session.CenterView();
                Repaint();

                SBLog.Info(SBLogTags.Blueprint, $"已加载: {AssetDatabase.GetAssetPath(asset)}" +
                    $" (节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count})");

                _session.RunBindingValidation();
                _session.MarkPreviewDirtyAll("LoadFromAsset");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("加载失败", $"反序列化图数据失败:\n{ex.Message}", "确定");
                SBLog.Error(SBLogTags.Blueprint, $"加载失败: {ex}");
            }
        }

        private void SyncToScene()
        {
            if (_currentAsset == null)
            {
                EditorUtility.DisplayDialog("同步失败", "请先保存蓝图资产后再同步到场景。", "确定");
                return;
            }
            SaveBlueprint();
            if (_currentAsset == null)
            {
                EditorUtility.DisplayDialog("同步失败", "蓝图资产保存失败，请重试。", "确定");
                return;
            }
            _session?.SyncToScene();
        }

        // ── 辅助 ──
        private string GetCurrentAdapterType() => EnsureSpatialModeDescriptor().AdapterType;
    }
}
