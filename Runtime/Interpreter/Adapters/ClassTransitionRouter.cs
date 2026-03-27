#nullable enable
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter.Adapters
{
    /// <summary>
    /// ITransitionRouter 的 Package 侧实现——从 BlueprintFrame 的出边表查找转场目标，
    /// 生成 Contract.PortEvent（int hash）写入 FrameView.PendingEvents。
    /// </summary>
    public class ClassTransitionRouter : ITransitionRouter
    {
        private BlueprintFrame _frame;

        public ClassTransitionRouter(BlueprintFrame frame)
        {
            _frame = frame;
        }

        /// <summary>切换到新 Frame（Load 时调用）</summary>
        public void SetFrame(BlueprintFrame frame)
        {
            _frame = frame;
        }

        public void EmitFlowEvent(ref FrameView view, int actionIndex, string portId)
        {
            int fromHash = portId.GetHashCode();
            var transitionIndices = _frame.GetOutgoingTransitionIndices(actionIndex);

            for (int t = 0; t < transitionIndices.Count; t++)
            {
                var transition = _frame.Transitions[transitionIndices[t]];
                if (transition.FromPortId == portId)
                {
                    int toIndex = _frame.GetActionIndex(transition.ToActionId);
                    if (toIndex >= 0)
                    {
                        int toHash = transition.ToPortId.GetHashCode();
                        var ev = new PortEvent(actionIndex, fromHash, toIndex, toHash);
#if UNITY_EDITOR || DEBUG
                        ev.DebugFromPortId = portId;
                        ev.DebugToPortId = transition.ToPortId;
#endif
                        view.PendingEvents.Add(ev);
                    }
                }
            }

            view.States[actionIndex].EventEmitted = true;
        }

        public void EmitOutEvent(ref FrameView view, int actionIndex)
            => EmitFlowEvent(ref view, actionIndex, "out");
    }
}
