#nullable enable
using NodeGraph.View;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Editor.WindowServices;
using SceneBlueprint.Runtime;
using UnityEngine;

namespace SceneBlueprint.Editor.Session
{
    /// <summary>
    /// Session 的只读视图——直接持有 Session 引用和回调接口，
    /// 替换原来由 7 个 <c>Func&lt;&gt;</c> 委托组成的 <c>BlueprintEditorContext</c>。
    /// <para>
    /// 设计要点：
    /// <list type="bullet">
    /// <item>struct 装箱一次（赋给 IBlueprintEditorContext 时），零额外闭包分配</item>
    /// <item>所有属性/方法直接转发给 Session 字段，无间接委托层</item>
    /// <item>Session 在 <c>InitCore</c> 完成后构造此视图，ViewModel 此时已非 null</item>
    /// </list>
    /// </para>
    /// </summary>
    internal readonly struct SessionReadView
        : IBlueprintReadContext, IBlueprintUIContext, IBlueprintEditorContext
    {
        private readonly BlueprintEditorSession _session;
        private readonly IWindowCallbacks       _callbacks;
        private readonly IEditorSpatialModeDescriptor _spatial;

        internal SessionReadView(
            BlueprintEditorSession        session,
            IWindowCallbacks              callbacks,
            IEditorSpatialModeDescriptor  spatial)
        {
            _session   = session;
            _callbacks = callbacks;
            _spatial   = spatial;
        }

        // ── IBlueprintReadContext ──
        GraphViewModel? IBlueprintReadContext.ViewModel           => _session.ViewModel;
        BlueprintAsset? IBlueprintReadContext.CurrentAsset        => _session.CurrentAsset;
        ActionRegistry  IBlueprintReadContext.ActionRegistry      => _session.ActionRegistry;
        Vector2         IBlueprintReadContext.GetWindowSize()     => _callbacks.GetWindowSize();
        string          IBlueprintReadContext.GetAdapterType()    => _spatial.AdapterType;

        // ── IBlueprintUIContext ──
        void IBlueprintUIContext.RequestRepaint()         => _callbacks.Repaint();
        void IBlueprintUIContext.EnsureWorkbenchVisible() => _callbacks.EnsureWorkbenchVisible();
    }
}
