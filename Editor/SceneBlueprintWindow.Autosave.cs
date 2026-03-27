#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Serialization;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Persistence;
using SceneBlueprint.Editor.Settings;
using SceneBlueprint.Runtime;
using SceneBlueprint.Runtime.Snapshot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        private const int MinDraftAutosaveIntervalSeconds = 15;
        private const int MinDraftAutosaveIdleDelaySeconds = 3;

        [SerializeField] private string _anonymousDraftId = string.Empty;
        private bool _autosaveGraphDirty;
        private double _lastAutosaveWriteTime;
        private double _lastAutosaveInteractionTime;

        private void AttachAutosaveWindowLifecycle()
        {
            _lastAutosaveInteractionTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnAutosaveEditorUpdate;
            EditorApplication.update += OnAutosaveEditorUpdate;
        }

        private void DetachAutosaveWindowLifecycle()
        {
            EditorApplication.update -= OnAutosaveEditorUpdate;
        }

        private void AttachAutosaveSessionHooks()
        {
            if (_session == null)
                return;

            _session.ViewModel.Commands.OnHistoryChanged -= OnAutosaveGraphHistoryChanged;
            _session.ViewModel.Commands.OnHistoryChanged += OnAutosaveGraphHistoryChanged;
        }

        private void DetachAutosaveSessionHooks()
        {
            if (_session == null)
                return;

            _session.ViewModel.Commands.OnHistoryChanged -= OnAutosaveGraphHistoryChanged;
        }

        private void OnAutosaveGraphHistoryChanged()
        {
            MarkAutosaveGraphDirty();
        }

        private void MarkAutosaveGraphDirty()
        {
            _autosaveGraphDirty = true;
            _lastAutosaveInteractionTime = EditorApplication.timeSinceStartup;
        }

        private void RecordAutosaveInteraction(Event evt)
        {
            if (evt == null)
                return;

            switch (evt.type)
            {
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseDrag:
                case EventType.MouseMove:
                case EventType.KeyDown:
                case EventType.KeyUp:
                case EventType.ScrollWheel:
                case EventType.DragPerform:
                case EventType.DragUpdated:
                    _lastAutosaveInteractionTime = EditorApplication.timeSinceStartup;
                    break;
            }
        }

        private void ResetAutosaveTracking()
        {
            _autosaveGraphDirty = false;
            double now = EditorApplication.timeSinceStartup;
            _lastAutosaveWriteTime = now;
            _lastAutosaveInteractionTime = now;
        }

        private void OnAutosaveEditorUpdate()
        {
            if (_session == null)
                return;

            if (!ShouldRunDraftAutosave())
                return;

            var workspace = SceneBlueprintSettingsService.User.Workspace;
            if (!workspace.EnableLocalDraftAutosave)
                return;

            int intervalSeconds = Math.Max(MinDraftAutosaveIntervalSeconds, workspace.DraftAutosaveIntervalSeconds);
            int idleDelaySeconds = Math.Max(MinDraftAutosaveIdleDelaySeconds, workspace.DraftAutosaveIdleDelaySeconds);
            double now = EditorApplication.timeSinceStartup;

            if (now - _lastAutosaveWriteTime < intervalSeconds)
                return;

            if (now - _lastAutosaveInteractionTime < idleDelaySeconds)
                return;

            bool assetDirty = _currentAsset != null && EditorUtility.IsDirty(_currentAsset);
            if (!_autosaveGraphDirty && !assetDirty)
                return;

            if (!_autosaveGraphDirty && _lastAutosaveWriteTime >= _lastAutosaveInteractionTime)
                return;

            if (!TryBuildAutosaveDraftPayload(out var payload, out var error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                    SBLog.Warn(SBLogTags.Blueprint, $"本地草稿自动保存已跳过: {error}");
                return;
            }

            BlueprintAutosaveDraftStore.QueueWrite(payload);
            _lastAutosaveWriteTime = now;
            _autosaveGraphDirty = false;
            SBLog.Debug(
                SBLogTags.Blueprint,
                "已写入本地草稿自动保存: {0}",
                BuildDraftTargetLabel(payload));
        }

        private static bool ShouldRunDraftAutosave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return false;

            return true;
        }

        private bool TryBuildAutosaveDraftPayload(out BlueprintAutosaveDraftPayload payload, out string error)
        {
            payload = null!;
            error = string.Empty;

            if (_session == null)
            {
                error = "当前没有活动蓝图会话。";
                return false;
            }

            string graphJson = _session.SerializeGraph();
            var snapshots = BuildSceneSnapshotsForPersistence().ToArray();
            if (_currentAsset != null)
            {
                var variables = CloneVariables(_currentAsset.Variables);

                return BlueprintAutosaveDraftStore.TryBuildPayload(
                    _currentAsset,
                    graphJson,
                    _anchoredScenePath,
                    _currentAsset.LevelId,
                    variables,
                    snapshots,
                    out payload,
                    out error);
            }

            if (_session.ViewModel.Graph.Nodes.Count == 0)
            {
                error = "当前未保存蓝图为空图，跳过匿名草稿保存。";
                return false;
            }

            string anonymousDraftId = EnsureAnonymousDraftId();
            return BlueprintAutosaveDraftStore.TryBuildAnonymousPayload(
                anonymousDraftId,
                graphJson,
                _anchoredScenePath,
                snapshots,
                out payload,
                out error);
        }

        private bool TryRecoverAutosaveDraftIfPresent(
            BlueprintAsset asset,
            bool restoreSceneContext,
            string sceneAnchorPath,
            string loadReason)
        {
            if (!BlueprintAutosaveDraftStore.HasDraft(asset))
                return false;

            if (!BlueprintAutosaveDraftStore.TryLoadDraft(asset, out var payload, out var error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                    SBLog.Warn(SBLogTags.Blueprint, $"本地草稿读取失败: {error}");
                return false;
            }

            var capturedAt = new DateTime(payload!.CapturedAtUtcTicks, DateTimeKind.Utc).ToLocalTime();
            int choice = EditorUtility.DisplayDialogComplex(
                "发现本地自动保存草稿",
                "该蓝图存在一份尚未写回正式资产的本地草稿。\n" +
                $"目标: {BuildDraftTargetLabel(payload)}\n" +
                $"草稿时间: {capturedAt:yyyy-MM-dd HH:mm:ss}\n\n" +
                "是否恢复这份本地草稿？",
                "恢复草稿",
                "保留正式资产",
                "删除草稿");

            if (choice == 2)
            {
                BlueprintAutosaveDraftStore.DeleteDraft(asset);
                return false;
            }

            if (choice != 0)
                return false;

            return TryApplyAutosaveDraft(payload, restoreSceneContext, sceneAnchorPath, loadReason);
        }

        private bool TryRestoreAnonymousDraftOnWindowOpen()
        {
            var workspace = SceneBlueprintSettingsService.User.Workspace;
            if (string.IsNullOrWhiteSpace(workspace.LastAnonymousDraftId))
                return false;

            string anonymousDraftId = workspace.LastAnonymousDraftId;
            if (!BlueprintAutosaveDraftStore.HasAnonymousDraft(anonymousDraftId))
            {
                ClearAnonymousDraftWorkspaceState(deleteDraftFile: false);
                return false;
            }

            if (!BlueprintAutosaveDraftStore.TryLoadAnonymousDraft(anonymousDraftId, out var payload, out var error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                    SBLog.Warn(SBLogTags.Blueprint, $"匿名草稿读取失败: {error}");
                return false;
            }

            var capturedAt = new DateTime(payload!.CapturedAtUtcTicks, DateTimeKind.Utc).ToLocalTime();
            int choice = EditorUtility.DisplayDialogComplex(
                "发现未保存蓝图草稿",
                "检测到一份未保存为正式资产的本地蓝图草稿。\n" +
                $"草稿时间: {capturedAt:yyyy-MM-dd HH:mm:ss}\n\n" +
                "是否恢复这份未保存蓝图？",
                "恢复草稿",
                "忽略",
                "删除草稿");

            if (choice == 2)
            {
                ClearAnonymousDraftWorkspaceState(deleteDraftFile: true);
                return false;
            }

            if (choice != 0)
                return false;

            return TryApplyAnonymousAutosaveDraft(payload, "RestoreAnonymousDraft");
        }

        private bool TryApplyAutosaveDraft(
            BlueprintAutosaveDraftPayload payload,
            bool restoreSceneContext,
            string sceneAnchorPath,
            string loadReason)
        {
            if (_currentAsset == null)
                return false;

            try
            {
                var typeProvider = _session != null
                    ? _session.CreateProfileTypeProvider()
                    : BuildTypeProviderForReload();
                var serializer = new JsonGraphSerializer(new ActionNodeDataSerializer(), typeProvider);
                var graph = serializer.Deserialize(payload.GraphJson);

                Undo.RecordObject(_currentAsset, "恢复本地蓝图草稿");
                _currentAsset.LevelId = Mathf.Max(0, payload.LevelId);
                _currentAsset.Variables = CloneVariables(payload.Variables);
                _currentAsset.SceneSnapshots = new List<BindingSnapshot>(payload.SceneSnapshots ?? Array.Empty<BindingSnapshot>());
                EditorUtility.SetDirty(_currentAsset);

                RecreateSession(graph);
                _session!.UpdateTitle();
                _session.CenterView();

                if (restoreSceneContext)
                {
                    RestoreMarkersFromSnapshot(_currentAsset);
                    _session.RestoreBindingsFromScene();
                    AnchorToCurrentScene();
                    _session.RunBindingValidation();
                }
                else
                {
                    EnterSuspendedState(sceneAnchorPath);
                    SBLog.Info(
                        SBLogTags.Blueprint,
                        $"已恢复本地草稿图内容，但当前场景与草稿锚定场景不一致，暂以挂起模式打开: {payload.BlueprintAssetPath}");
                }

                ResetAutosaveTracking();
                SBLog.Info(
                    SBLogTags.Blueprint,
                    $"已恢复本地自动保存草稿: {payload.BlueprintAssetPath} (来源: {loadReason})");
                return true;
            }
            catch (Exception ex)
            {
                SBLog.Error(SBLogTags.Blueprint, $"恢复本地自动保存草稿失败: {ex.Message}");
                return false;
            }
        }

        private bool TryApplyAnonymousAutosaveDraft(
            BlueprintAutosaveDraftPayload payload,
            string loadReason)
        {
            try
            {
                var typeProvider = _session != null
                    ? _session.CreateProfileTypeProvider()
                    : BuildTypeProviderForReload();
                var serializer = new JsonGraphSerializer(new ActionNodeDataSerializer(), typeProvider);
                var graph = serializer.Deserialize(payload.GraphJson);

                _currentAsset = null;
                _anonymousDraftId = payload.BlueprintId ?? string.Empty;
                RecreateSession(graph);
                _session!.UpdateTitle();
                _session.CenterView();

                if (payload.SceneSnapshots is { Length: > 0 })
                {
                    RestoreMarkersFromSnapshots(payload.SceneSnapshots);
                    _session.RestoreBindingsFromScene();
                }

                if (string.IsNullOrWhiteSpace(payload.AnchoredScenePath)
                    || string.Equals(EditorSceneManager.GetActiveScene().path, payload.AnchoredScenePath, StringComparison.Ordinal))
                {
                    AnchorToCurrentScene();
                }
                else
                {
                    EnterSuspendedState(payload.AnchoredScenePath);
                }

                PersistAnonymousDraftWorkspaceState(_anonymousDraftId);
                ResetAutosaveTracking();
                MarkAutosaveGraphDirty();
                SBLog.Info(SBLogTags.Blueprint, $"已恢复未保存蓝图草稿 ({loadReason})");
                return true;
            }
            catch (Exception ex)
            {
                SBLog.Error(SBLogTags.Blueprint, $"恢复未保存蓝图草稿失败: {ex.Message}");
                return false;
            }
        }

        private string EnsureAnonymousDraftId()
        {
            if (!string.IsNullOrWhiteSpace(_anonymousDraftId))
                return _anonymousDraftId;

            _anonymousDraftId = Guid.NewGuid().ToString("N");
            PersistAnonymousDraftWorkspaceState(_anonymousDraftId);
            return _anonymousDraftId;
        }

        private void PersistAnonymousDraftWorkspaceState(string anonymousDraftId)
        {
            var user = SceneBlueprintSettingsService.User;
            var workspace = user.Workspace;
            workspace.LastOpenedBlueprintAssetGuid = string.Empty;
            workspace.LastOpenedBlueprintAssetPath = string.Empty;
            workspace.LastAnonymousDraftId = anonymousDraftId ?? string.Empty;
            workspace.LastAnchoredScenePath = _anchoredScenePath ?? string.Empty;
            user.SaveConfig();
        }

        private void ClearAnonymousDraftWorkspaceState(bool deleteDraftFile)
        {
            string currentAnonymousDraftId = _anonymousDraftId;
            var user = SceneBlueprintSettingsService.User;
            var workspace = user.Workspace;
            string workspaceDraftId = workspace.LastAnonymousDraftId;

            workspace.LastAnonymousDraftId = string.Empty;
            user.SaveConfig();

            if (deleteDraftFile)
            {
                if (!string.IsNullOrWhiteSpace(currentAnonymousDraftId))
                    BlueprintAutosaveDraftStore.DeleteAnonymousDraft(currentAnonymousDraftId);
                if (!string.IsNullOrWhiteSpace(workspaceDraftId) && !string.Equals(workspaceDraftId, currentAnonymousDraftId, StringComparison.Ordinal))
                    BlueprintAutosaveDraftStore.DeleteAnonymousDraft(workspaceDraftId);
            }

            _anonymousDraftId = string.Empty;
        }

        private static string BuildDraftTargetLabel(BlueprintAutosaveDraftPayload payload)
        {
            if (payload.IsAnonymousDraft)
                return "未保存蓝图草稿";

            return string.IsNullOrWhiteSpace(payload.BlueprintAssetPath)
                ? payload.DraftDisplayName
                : payload.BlueprintAssetPath;
        }

        private static VariableDeclaration[] CloneVariables(VariableDeclaration[]? variables)
        {
            if (variables == null || variables.Length == 0)
                return Array.Empty<VariableDeclaration>();

            return variables
                .Select(static variable => new VariableDeclaration
                {
                    Index = variable.Index,
                    Name = variable.Name ?? string.Empty,
                    Type = variable.Type ?? string.Empty,
                    Scope = variable.Scope ?? string.Empty,
                    InitialValue = variable.InitialValue ?? string.Empty
                })
                .ToArray();
        }

        private string GetAutosaveStatusText()
        {
            var workspace = SceneBlueprintSettingsService.User.Workspace;
            if (!workspace.EnableLocalDraftAutosave)
                return "Autosave Off";

            if (_session == null)
                return "Autosave Idle";

            if (_autosaveGraphDirty)
                return _currentAsset != null ? "Autosave Pending" : "Autosave Draft Pending";

            if (_lastAutosaveWriteTime <= 0)
                return _currentAsset != null ? "Autosave Ready" : "Autosave Draft Ready";

            int seconds = Mathf.Max(0, Mathf.RoundToInt((float)(EditorApplication.timeSinceStartup - _lastAutosaveWriteTime)));
            return _currentAsset != null
                ? $"Autosave {seconds}s ago"
                : $"Draft Saved {seconds}s ago";
        }

        private Color GetAutosaveStatusColor()
        {
            var workspace = SceneBlueprintSettingsService.User.Workspace;
            if (!workspace.EnableLocalDraftAutosave)
                return new Color(0.6f, 0.6f, 0.6f);

            if (_autosaveGraphDirty)
                return new Color(1f, 0.82f, 0.32f);

            return new Color(0.62f, 0.88f, 0.66f);
        }

        private string GetAutosaveStatusTooltip()
        {
            var workspace = SceneBlueprintSettingsService.User.Workspace;
            string enabledText = workspace.EnableLocalDraftAutosave ? "已启用" : "已关闭";
            int intervalSeconds = Math.Max(MinDraftAutosaveIntervalSeconds, workspace.DraftAutosaveIntervalSeconds);
            int idleDelaySeconds = Math.Max(MinDraftAutosaveIdleDelaySeconds, workspace.DraftAutosaveIdleDelaySeconds);

            return
                "本地草稿自动保存状态\n" +
                $"状态: {enabledText}\n" +
                $"草稿目录: {BlueprintAutosaveDraftStore.DraftDirectoryDisplayPath}\n" +
                $"自动保存间隔: {intervalSeconds}s\n" +
                $"空闲延迟: {idleDelaySeconds}s";
        }
    }
}
