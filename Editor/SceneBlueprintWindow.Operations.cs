#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NodeGraph.Commands;
using NodeGraph.Core;
using NodeGraph.Math;
using NodeGraph.Serialization;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Snapshot;
using SceneBlueprint.Editor.Persistence;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Runtime;
using SceneBlueprint.Runtime.Snapshot;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        // ── 操作方法 ──

        private void NewGraph()
        {
            if (_session == null) return;

            if (!TeardownSession()) return;

            RecreateSession(null);
            _session!.AddDefaultNodes();
            Repaint();
        }

        private bool SaveBlueprint()
        {
            return SaveBlueprint(interactiveIfNoAsset: true, saveReason: "ManualSave");
        }

        private bool TryAutoSaveBlueprintOnWindowClose()
        {
            return SaveBlueprint(interactiveIfNoAsset: false, saveReason: "WindowCloseAutoSave");
        }

        private bool SaveBlueprint(bool interactiveIfNoAsset, string saveReason)
        {
            if (_session == null) return false;

            string graphJson = _session.SerializeGraph();

            if (_currentAsset != null)
            {
                CaptureSnapshotFromBindingContext();
                string assetPath = AssetDatabase.GetAssetPath(_currentAsset);
                BlueprintAssetStore.SaveAsset(assetPath, graphJson, _currentAsset);
                BlueprintAutosaveDraftStore.DeleteDraft(_currentAsset);
                SBLog.Info(SBLogTags.Blueprint, $"已保存: {assetPath} ({saveReason})");
                _session.UpdateTitle();
                PersistCurrentWorkspaceState();
                ResetAutosaveTracking();
                return true;
            }

            if (!interactiveIfNoAsset)
            {
                if (TryBuildAutosaveDraftPayload(out var autosavePayload, out var autosaveError))
                {
                    BlueprintAutosaveDraftStore.WriteNow(autosavePayload);
                    ResetAutosaveTracking();
                    SBLog.Info(
                        SBLogTags.Blueprint,
                        $"窗口关闭时已写入本地草稿: {BuildDraftTargetLabel(autosavePayload)} ({saveReason})");
                    return true;
                }

                SBLog.Warn(
                    SBLogTags.Blueprint,
                    $"窗口关闭自动保存已跳过：当前蓝图尚未保存为资产，且本地草稿写入失败 ({saveReason}, {autosaveError})");
                return false;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "保存蓝图资产", "NewBlueprint", "asset", "选择蓝图保存位置");

            if (string.IsNullOrEmpty(path))
                return false;

            var newAsset = BlueprintAssetStore.CreateAsset(path, graphJson);
            _session.SetAsset(newAsset);
            _currentAsset = newAsset;
            AnchorToCurrentScene();
            CaptureSnapshotFromBindingContext();
            AssetDatabase.SaveAssets();
            BlueprintAutosaveDraftStore.DeleteDraft(newAsset);
            ClearAnonymousDraftWorkspaceState(deleteDraftFile: true);
            PersistCurrentWorkspaceState();
            _session.UpdateTitle();
            ResetAutosaveTracking();
            SBLog.Info(SBLogTags.Blueprint, $"已创建: {path} (ID: {newAsset.BlueprintId}, {saveReason})");
            return true;
        }

        private void LoadBlueprint()
        {
            if (_session == null) return;

            string path = EditorUtility.OpenFilePanel("加载蓝图资产", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            var asset = AssetDatabase.LoadAssetAtPath<BlueprintAsset>(path);
            if (asset == null)
            { EditorUtility.DisplayDialog("加载失败", "选择的文件不是有效的蓝图资产。", "确定"); return; }
            if (asset.IsEmpty)
            { EditorUtility.DisplayDialog("加载失败", "蓝图资产中没有图数据。", "确定"); return; }

            // 先退出当前会话（含保存确认）
            if (!TeardownSession()) return;

            LoadFromAsset(asset);
        }

        /// <summary>从外部直接加载指定的 BlueprintAsset（供自定义 Inspector 调用）。</summary>
        public void LoadFromAsset(BlueprintAsset asset)
        {
            TryLoadBlueprintAsset(
                asset,
                restoreSceneContext: true,
                sceneAnchorPath: EditorSceneManager.GetActiveScene().path,
                showFailureDialog: true,
                loadReason: "LoadFromAsset");
        }

        // ── 辅助 ──

        private bool TryLoadBlueprintAsset(
            BlueprintAsset asset,
            bool restoreSceneContext,
            string sceneAnchorPath,
            bool showFailureDialog,
            string loadReason)
        {
            if (asset == null || asset.IsEmpty)
                return false;

            try
            {
                var graphJson = BlueprintAssetStore.ReadJson(asset);
                if (graphJson == null)
                    throw new InvalidOperationException("asset.GraphData 为空");

                var typeProvider = _session != null
                    ? _session.CreateProfileTypeProvider()
                    : BuildTypeProviderForReload();
                var serializer = new JsonGraphSerializer(new ActionNodeDataSerializer(), typeProvider);
                var graph = serializer.Deserialize(graphJson);

                _currentAsset = asset;
                RecreateSession(graph);
                _session!.UpdateTitle();
                _session.CenterView();

                bool restoredDraft = TryRecoverAutosaveDraftIfPresent(asset, restoreSceneContext, sceneAnchorPath, loadReason);
                if (!restoredDraft && restoreSceneContext)
                {
                    RestoreMarkersFromSnapshot(asset);
                    _session.RestoreBindingsFromScene();
                    AnchorToCurrentScene();
                    _session.RunBindingValidation();
                }
                else if (!restoredDraft)
                {
                    EnterSuspendedState(sceneAnchorPath);
                    SBLog.Info(
                        SBLogTags.Blueprint,
                        $"已恢复最近蓝图图内容，但当前场景与上次锚定场景不一致，暂以挂起模式打开: {AssetDatabase.GetAssetPath(asset)}");
                }

                PersistCurrentWorkspaceState();
                ResetAutosaveTracking();
                Repaint();

                SBLog.Info(
                    SBLogTags.Blueprint,
                    $"已加载: {AssetDatabase.GetAssetPath(asset)} (节点: {graph.Nodes.Count}, 连线: {graph.Edges.Count}, 模式: {(restoreSceneContext ? "Active" : "Suspended")}, 来源: {loadReason})");

                _session.MarkPreviewDirtyAll(loadReason);
                return true;
            }
            catch (Exception ex)
            {
                if (showFailureDialog)
                    EditorUtility.DisplayDialog("加载失败", $"反序列化图数据失败:\n{ex.Message}", "确定");

                SBLog.Error(SBLogTags.Blueprint, $"加载失败 ({loadReason}): {ex}");
                return false;
            }
        }

        private string GetCurrentAdapterType() => EnsureSpatialModeDescriptor().AdapterType;

        // ── 会话生命周期 ──

        /// <summary>
        /// 退出当前编辑会话。弹出保存确认 -> 清理场景 -> Dispose Session。
        /// </summary>
        /// <returns>true = 成功退出；false = 用户取消</returns>
        private bool TeardownSession()
        {
            if (_session == null) return true;

            bool hasContent = _session.ViewModel.Graph.Nodes.Count > 0;
            if (hasContent)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "关闭蓝图",
                    "当前蓝图有未保存的修改，是否保存？",
                    "保存",      // 0
                    "取消",      // 1
                    "不保存");   // 2

                if (choice == 1) return false;  // 取消
                if (choice == 0 && !SaveBlueprint()) return false;  // 保存
            }

            if (_currentAsset == null)
                ClearAnonymousDraftWorkspaceState(deleteDraftFile: true);

            CleanupScene();

            _currentAsset = null;
            ClearSceneAnchor();
            DisposeSession();

            return true;
        }

        /// <summary>销毁场景中的 Marker 层级。</summary>
        private void CleanupScene()
        {
            Markers.MarkerHierarchyManager.DestroyAll();
        }

        /// <summary>
        /// 从编辑器 BindingContext + 场景中全量 Marker 采集快照并写入 BlueprintAsset。
        /// <para>
        /// 采集分两阶段：
        /// 1. BindingContext 中已绑定的 Marker（携带 scopedBindingKey）
        /// 2. SceneBlueprintMarkers/ 下未被绑定覆盖的 Marker（scopedBindingKey 为空）
        /// 合并结果确保策划放置的所有 Marker 都能在重新加载时恢复。
        /// </para>
        /// </summary>
        private void CaptureSnapshotFromBindingContext()
        {
            if (_currentAsset == null || _session == null) return;

            Undo.RecordObject(_currentAsset, "保存场景快照");
            _currentAsset.SceneSnapshots = BuildSceneSnapshotsForPersistence();
            EditorUtility.SetDirty(_currentAsset);
        }

        private List<BindingSnapshot> BuildSceneSnapshotsForPersistence()
        {
            if (_session == null)
                return new List<BindingSnapshot>();

            var bindingContext = _session.BindingContextPublic;

            var snapshots = SceneSnapshotCollector.CollectFromBindingContext(bindingContext);
            var coveredMarkerIds = new System.Collections.Generic.HashSet<string>();
            foreach (var snapshot in snapshots)
            {
                if (!string.IsNullOrEmpty(snapshot.markerId))
                    coveredMarkerIds.Add(snapshot.markerId);
            }

            var unboundSnapshots = SceneSnapshotCollector.CollectUnboundFromScene(coveredMarkerIds);
            snapshots.AddRange(unboundSnapshots);

            int existingCount = _currentAsset?.SceneSnapshots?.Count ?? 0;
            if (snapshots.Count == 0 && existingCount > 0)
            {
                SBLog.Warn(
                    SBLogTags.Snapshot,
                    "采集到 0 个快照但已有 {0} 个，保留已有快照（场景可能已切换）",
                    existingCount);
                return _currentAsset != null
                    ? new List<BindingSnapshot>(_currentAsset.SceneSnapshots)
                    : new List<BindingSnapshot>();
            }

            SBLog.Debug(
                SBLogTags.Snapshot,
                "保存时采集 {0} 个快照（绑定: {1} / 未绑定: {2}）",
                snapshots.Count,
                snapshots.Count - unboundSnapshots.Count,
                unboundSnapshots.Count);
            return snapshots;
        }

        /// <summary>从 BlueprintAsset 快照恢复场景中的 Marker。</summary>
        private void RestoreMarkersFromSnapshot(BlueprintAsset asset)
        {
            if (asset.SceneSnapshots == null || asset.SceneSnapshots.Count == 0)
            {
                SBLog.Debug(SBLogTags.Snapshot, "无快照数据，跳过 Marker 恢复");
                return;
            }

            RestoreMarkersFromSnapshots(asset.SceneSnapshots);
        }

        private void RestoreMarkersFromSnapshots(IReadOnlyList<BindingSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                SBLog.Debug(SBLogTags.Snapshot, "无快照数据，跳过 Marker 恢复");
                return;
            }

            var result = SceneSnapshotRestorer.Restore(new List<BindingSnapshot>(snapshots));

            SBLog.Info(SBLogTags.Snapshot,
                "加载时自动恢复 Marker：恢复 {0} / 跳过 {1} / 失败 {2}",
                result.restoredCount, result.skippedCount, result.failedCount);
        }
    }
}
