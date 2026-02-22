#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline.Interaction
{
    /// <summary>
    /// 默认的标记选中控制器。
    ///
    /// 职责：
    /// 1) Pick 模式：使用“强接管”拾取（AddDefaultControl + hotControl + evt.Use）。
    /// 2) Edit 模式：非抢占式单击选中（MouseDown 记录，MouseUp 提交）。
    ///
    /// 说明：
    /// - 本类只处理输入仲裁与 Selection 提交，不负责渲染。
    /// - 命中计算由 <see cref="IMarkerHitTestService"/> 提供，便于后续替换命中策略。
    /// </summary>
    internal sealed class DefaultMarkerSelectionController : IMarkerSelectionController
    {
        // Pick 模式状态（强接管）
        private int _pickControlId;
        private bool _pendingPick;

        // Edit 模式状态（非抢占式单击）
        private SceneMarker? _editClickCandidate;
        private bool _editClickPending;
        private Vector2 _editMouseDownPos;
        private const float EditClickMaxDragPixels = 4f;

        public void ResetState()
        {
            // 仅在我们自己接管的拾取流程中释放 hotControl，避免影响其他工具。
            if (_pendingPick && _pickControlId != 0 && GUIUtility.hotControl == _pickControlId)
                GUIUtility.hotControl = 0;

            _pendingPick = false;
            _pickControlId = 0;
            ClearEditClickState();
        }

        public void Handle(
            Event evt,
            GizmoRenderPipeline.MarkerInteractionMode interactionMode,
            IMarkerHitTestService hitTestService,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers)
        {
            if (evt == null)
            {
                ResetState();
                return;
            }

            if (IsTraceEvent(evt.type))
            {
                SBLog.Debug(SBLogTags.Selection,
                    $"SelectionController.Handle mode={interactionMode}, evt={evt.type}, button={evt.button}, mods={evt.modifiers}, pendingPick={_pendingPick}, pendingEdit={_editClickPending}, drawCount={drawList.Count}, active={Selection.activeGameObject?.name ?? "null"}");
                Trace($"Handle mode={interactionMode}, evt={evt.type}, button={evt.button}, mods={evt.modifiers}, pendingPick={_pendingPick}, pendingEdit={_editClickPending}, drawCount={drawList.Count}, active={Selection.activeGameObject?.name ?? "null"}");
            }

            if (interactionMode == GizmoRenderPipeline.MarkerInteractionMode.Pick)
            {
                if (CanHandlePick(evt))
                    HandleLegacyPickMode(evt, hitTestService, drawList, renderers);
                else if (evt.type == EventType.Ignore || evt.type == EventType.Used)
                    _pendingPick = false;

                return;
            }

            if (interactionMode == GizmoRenderPipeline.MarkerInteractionMode.Edit)
            {
                if (CanHandleEditClickSelect(evt))
                    HandleEditClickSelect(evt, hitTestService, drawList, renderers);
                else if (evt.type == EventType.Ignore || evt.type == EventType.Used)
                    ClearEditClickState();

                return;
            }

            // 未知模式兜底：清理状态，避免残留。
            ResetState();
        }

        private static bool CanHandlePick(Event evt)
        {
            // Alt/视图工具用于导航相机，不应被拾取层拦截。
            if (evt.alt || Tools.viewToolActive)
                return false;

            return true;
        }

        private static void CommitSelectionStable(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            SBLog.Debug(SBLogTags.Selection,
                $"CommitSelectionStable immediate => {gameObject.name}");
            Trace($"Commit immediate => {gameObject.name}");
            Selection.activeGameObject = gameObject;

            // 延后一帧做一次幂等提交，避免同一输入链后续步骤把选中覆盖掉。
            EditorApplication.delayCall += () =>
            {
                if (gameObject == null)
                    return;

                if (Selection.activeGameObject != gameObject)
                {
                    SBLog.Debug(SBLogTags.Selection,
                        $"CommitSelectionStable delay override => {gameObject.name} (prev={Selection.activeGameObject?.name ?? "null"})");
                    Trace($"Commit delay override => {gameObject.name} (prev={Selection.activeGameObject?.name ?? "null"})");
                    Selection.activeGameObject = gameObject;
                }
            };
        }

        private static bool CanHandleEditClickSelect(Event evt)
        {
            // 文本输入时（如 Inspector 文本框）不应触发场景选中。
            if (EditorGUIUtility.editingTextField)
                return false;

            return true;
        }

        /// <summary>
        /// Edit 模式下的“非抢占式单击选中”流程。
        ///
        /// 采用 MouseDown 记录候选 + MouseUp 提交的两阶段协议，
        /// 目的是让 Unity 默认选择链路先处理，再由我们在收尾阶段稳定提交 marker 选中，
        /// 避免 MouseDown 即提交被后续流程覆盖而出现“瞬间丢失选中”。
        /// 不抢占 hotControl，也不消费事件，让位给 Unity 原生变换。
        /// </summary>
        private void HandleEditClickSelect(
            Event evt,
            IMarkerHitTestService hitTestService,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers)
        {
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (!CanStartEditClickSelection(evt))
                    {
                        SBLog.Debug(SBLogTags.Selection,
                            $"EditSelect.MouseDown reject button/mods: button={evt.button}, mods={evt.modifiers}, alt={evt.alt}, viewTool={Tools.viewToolActive}");
                        Trace($"Edit.MouseDown reject button/mods: button={evt.button}, mods={evt.modifiers}, alt={evt.alt}, viewTool={Tools.viewToolActive}");
                        ClearEditClickState();
                        break;
                    }

                    _editClickPending = true;
                    _editMouseDownPos = evt.mousePosition;
                    _editClickCandidate = hitTestService.FindClosestMarker(evt.mousePosition, drawList, renderers);
                    SBLog.Debug(SBLogTags.Selection,
                        $"EditSelect.MouseDown pending=true candidate={_editClickCandidate?.name ?? "null"} mouse={evt.mousePosition}");
                    Trace($"Edit.MouseDown pending=true candidate={_editClickCandidate?.name ?? "null"} mouse={evt.mousePosition}");
                    break;

                case EventType.MouseDrag:
                    if (!_editClickPending)
                        break;

                    float sqrDrag = (evt.mousePosition - _editMouseDownPos).sqrMagnitude;
                    if (sqrDrag > EditClickMaxDragPixels * EditClickMaxDragPixels)
                    {
                        SBLog.Debug(SBLogTags.Selection,
                            $"EditSelect.MouseDrag cancel click (dragPixels={Mathf.Sqrt(sqrDrag):F2})");
                        Trace($"Edit.MouseDrag cancel click (dragPixels={Mathf.Sqrt(sqrDrag):F2})");
                        ClearEditClickState();
                    }
                    break;

                case EventType.MouseUp:
                    if (!_editClickPending || evt.button != 0)
                    {
                        ClearEditClickState();
                        break;
                    }

                    // MouseDown 未命中时，MouseUp 位置再做一次命中兜底。
                    var resolved = _editClickCandidate
                        ?? hitTestService.FindClosestMarker(evt.mousePosition, drawList, renderers);
                    if (resolved != null)
                    {
                        SBLog.Debug(SBLogTags.Selection,
                            $"EditSelect.MouseUp resolved={resolved.name}, mouse={evt.mousePosition}");
                        Trace($"Edit.MouseUp resolved={resolved.name}, mouse={evt.mousePosition}");
                        CommitSelectionStable(resolved.gameObject);
                    }
                    else
                    {
                        SBLog.Debug(SBLogTags.Selection,
                            $"EditSelect.MouseUp resolved=null, mouse={evt.mousePosition}");
                        Trace($"Edit.MouseUp resolved=null, mouse={evt.mousePosition}");
                    }

                    ClearEditClickState();
                    break;

                case EventType.Ignore:
                    SBLog.Debug(SBLogTags.Selection, "EditSelect.Ignore => clear state");
                    Trace("Edit.Ignore => clear state");
                    ClearEditClickState();
                    break;

                case EventType.Used:
                    // 日志显示在某些输入链中 MouseDown 之后直接进入 Used，且不会再收到 MouseUp。
                    // 这里提供收尾兜底：若仍存在 pending 点击，则按当前候选提交一次选中。
                    if (_editClickPending)
                    {
                        var usedResolved = _editClickCandidate
                            ?? hitTestService.FindClosestMarker(evt.mousePosition, drawList, renderers);

                        if (usedResolved != null)
                        {
                            SBLog.Debug(SBLogTags.Selection,
                                $"EditSelect.Used fallback commit => {usedResolved.name}, mouse={evt.mousePosition}");
                            Trace($"Edit.Used fallback commit => {usedResolved.name}, mouse={evt.mousePosition}");
                            CommitSelectionStable(usedResolved.gameObject);
                        }
                        else
                        {
                            SBLog.Debug(SBLogTags.Selection,
                                $"EditSelect.Used fallback resolved=null, mouse={evt.mousePosition}");
                            Trace($"Edit.Used fallback resolved=null, mouse={evt.mousePosition}");
                        }

                        ClearEditClickState();
                    }
                    else
                    {
                        SBLog.Debug(SBLogTags.Selection, "EditSelect.Used => no pending, skip");
                        Trace("Edit.Used => no pending, skip");
                    }
                    break;
            }
        }

        private static bool CanStartEditClickSelection(Event evt)
        {
            if (evt.button != 0)
                return false;

            // Alt/视图工具用于相机导航，不应触发选中。
            if (evt.alt || Tools.viewToolActive)
                return false;

            // Ctrl/Shift 保留给框选/多选与项目内其他快捷交互。
            if (evt.control || evt.shift)
                return false;

            return true;
        }

        /// <summary>
        /// Pick 模式（强接管）。
        /// 通过 defaultControl/hotControl 获得稳定命中与点击反馈。
        /// </summary>
        private void HandleLegacyPickMode(
            Event evt,
            IMarkerHitTestService hitTestService,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers)
        {
            _pickControlId = GUIUtility.GetControlID(FocusType.Passive);

            switch (evt.type)
            {
                case EventType.Layout:
                    if (GUIUtility.hotControl == 0)
                        HandleUtility.AddDefaultControl(_pickControlId);
                    break;

                case EventType.MouseDown:
                    if (evt.button != 0 || evt.shift || evt.control || evt.alt) break;
                    if (HandleUtility.nearestControl != _pickControlId) break;

                    var picked = hitTestService.FindClosestMarker(evt.mousePosition, drawList, renderers);
                    if (picked != null)
                    {
                        GUIUtility.hotControl = _pickControlId;
                        Selection.activeGameObject = picked.gameObject;
                        _pendingPick = true;
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_pendingPick && evt.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        _pendingPick = false;
                        evt.Use();
                    }
                    break;
            }
        }

        private void ClearEditClickState()
        {
            if (_editClickPending || _editClickCandidate != null)
            {
                SBLog.Debug(SBLogTags.Selection,
                    $"ClearEditClickState pending={_editClickPending}, candidate={_editClickCandidate?.name ?? "null"}");
                Trace($"ClearEditClickState pending={_editClickPending}, candidate={_editClickCandidate?.name ?? "null"}");
            }
            _editClickCandidate = null;
            _editClickPending = false;
            _editMouseDownPos = Vector2.zero;
        }

        private static bool IsTraceEvent(EventType type)
        {
            return type == EventType.MouseDown
                || type == EventType.MouseUp
                || type == EventType.MouseDrag
                || type == EventType.Used
                || type == EventType.Ignore;
        }

        private static void Trace(string message)
        {
            // Debug.Log($"[SB.Selection.Trace][SelectionController] {message}");
        }
    }
}
