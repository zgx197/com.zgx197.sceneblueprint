#nullable enable
using System.Collections.Generic;
using System.Globalization;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter.Adapters
{
    /// <summary>
    /// IActionQuery 的 Package 侧实现——直接委托给 BlueprintFrame 的查询方法。
    /// <para>
    /// 零拷贝适配：所有查询直接转发到 BlueprintFrame，无额外内存分配。
    /// IncomingTransitions 在首次查询时按需构建并缓存。
    /// </para>
    /// </summary>
    public class ClassActionQuery : IActionQuery
    {
        private BlueprintFrame _frame;

        // 入边信息缓存：actionIndex → IncomingTransitionInfo[]
        private Dictionary<int, IncomingTransitionInfo[]>? _incomingCache;

        public ClassActionQuery(BlueprintFrame frame)
        {
            _frame = frame;
        }

        /// <summary>切换到新 Frame（Load 时调用）</summary>
        public void SetFrame(BlueprintFrame frame)
        {
            _frame = frame;
            _incomingCache = null;
        }

        public int ActionCount => _frame.ActionCount;

        public bool IsCompleted
        {
            get => _frame.IsCompleted;
            set => _frame.IsCompleted = value;
        }

        public IReadOnlyList<int>? GetActionIndices(string typeId)
        {
            var list = _frame.GetActionIndices(typeId);
            return list.Count > 0 ? list : null;
        }

        public string? GetProperty(int actionIndex, string key)
        {
            var val = _frame.GetProperty(actionIndex, key);
            return string.IsNullOrEmpty(val) ? null : val;
        }

        public float GetPropertyFloat(int actionIndex, string key, float defaultValue = 0f)
        {
            var raw = _frame.GetProperty(actionIndex, key);
            if (string.IsNullOrEmpty(raw)) return defaultValue;
            return float.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
        }

        public int GetPropertyInt(int actionIndex, string key, int defaultValue = 0)
        {
            var raw = _frame.GetProperty(actionIndex, key);
            if (string.IsNullOrEmpty(raw)) return defaultValue;
            return int.TryParse(raw, out var v) ? v : defaultValue;
        }

        public bool GetPropertyBool(int actionIndex, string key, bool defaultValue = false)
        {
            var raw = _frame.GetProperty(actionIndex, key);
            if (string.IsNullOrEmpty(raw)) return defaultValue;
            return bool.TryParse(raw, out var v) ? v : defaultValue;
        }

        public string GetTypeId(int actionIndex) => _frame.GetTypeId(actionIndex);

        public IReadOnlyList<IncomingTransitionInfo>? GetIncomingTransitions(int actionIndex)
        {
            EnsureIncomingCache();
            return _incomingCache!.TryGetValue(actionIndex, out var list) ? list : null;
        }

        private void EnsureIncomingCache()
        {
            if (_incomingCache != null) return;
            _incomingCache = new Dictionary<int, IncomingTransitionInfo[]>();

            foreach (var (toIdx, transIndices) in _frame.IncomingTransitions)
            {
                var infos = new IncomingTransitionInfo[transIndices.Count];
                for (int i = 0; i < transIndices.Count; i++)
                {
                    var t = _frame.Transitions[transIndices[i]];
                    int fromIdx = _frame.GetActionIndex(t.FromActionId);
                    infos[i] = new IncomingTransitionInfo(
                        fromIdx,
                        t.FromPortId.GetHashCode(),
                        t.ToPortId.GetHashCode());
                }
                _incomingCache[toIdx] = infos;
            }
        }
    }
}
