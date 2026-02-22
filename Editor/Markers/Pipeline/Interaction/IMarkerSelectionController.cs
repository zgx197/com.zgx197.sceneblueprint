#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Editor.Markers.Pipeline.Interaction
{
    /// <summary>
    /// 标记选中控制器。
    /// 负责输入事件仲裁（Edit/Pick）以及 Selection 提交策略。
    /// </summary>
    internal interface IMarkerSelectionController
    {
        /// <summary>
        /// 重置控制器的瞬时状态（例如 pending 点击候选）。
        /// 在模式切换或上下文失效时调用。
        /// </summary>
        void ResetState();

        /// <summary>
        /// 处理当前帧输入事件。
        /// </summary>
        void Handle(
            Event evt,
            GizmoRenderPipeline.MarkerInteractionMode interactionMode,
            IMarkerHitTestService hitTestService,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers);
    }
}
