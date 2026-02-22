#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Runtime.Interpreter.Diagnostics
{
    /// <summary>
    /// 最近 N 帧快照的环形缓冲区（Ring Buffer）。
    /// <para>
    /// 对齐 FrameSyncEngine 的 FramePredicted / FrameVerified 多帧持有模式：
    /// 预分配固定数量的快照对象，写入时原地复用，不产生 GC。
    /// </para>
    /// </summary>
    public class BlueprintFrameHistory
    {
        private readonly BlueprintFrameSnapshot[] _ring;
        private int _nextWritePos; // 下一个写入槽位
        private int _count;        // 有效条目数（0..Capacity）

        private static readonly StateDiff[]  _emptyDiffs  = Array.Empty<StateDiff>();
        private static readonly PortEvent[]  _emptyEvents = Array.Empty<PortEvent>();

        // ── 只读属性 ──

        /// <summary>最大可保留帧数</summary>
        public int Capacity => _ring.Length;

        /// <summary>当前有效快照数量</summary>
        public int Count => _count;

        /// <summary>最新一帧的绝对 TickCount（未记录任何帧时为 -1）</summary>
        public int LatestTick { get; private set; } = -1;

        /// <summary>最旧一帧的绝对 TickCount（未记录任何帧时为 -1）</summary>
        public int OldestTick => _count == 0 ? -1 : LatestTick - _count + 1;

        // ── 构造 ──

        /// <param name="capacity">保留帧数（默认 300，约 60fps 下 5 秒）</param>
        public BlueprintFrameHistory(int capacity = 300)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity), "capacity 至少为 2");
            _ring = new BlueprintFrameSnapshot[capacity];
            for (int i = 0; i < capacity; i++)
                _ring[i] = new BlueprintFrameSnapshot();
        }

        // ── 写入 ──

        /// <summary>
        /// 记录当前帧快照，并自动计算与上一帧的 Phase Diff。
        /// 由 <see cref="BlueprintDebugController"/> 在每帧 Tick 末调用。
        /// </summary>
        public void Record(BlueprintFrame frame)
        {
            // 先取上一帧引用（用于 diff，必须在写入前取）
            var prevSnap = _count > 0 ? GetByIndex(0) : null;

            // 取得本次写入槽并捕获状态
            var snap = _ring[_nextWritePos];
            snap.CaptureFrom(frame);
            snap.Diffs = ComputeDiffs(prevSnap, snap);

            // 推进环形指针
            _nextWritePos = (_nextWritePos + 1) % _ring.Length;
            if (_count < _ring.Length) _count++;
            LatestTick = frame.TickCount;
        }

        // ── 读取 ──

        /// <summary>
        /// 按相对索引获取快照。
        /// <c>0</c> = 最新帧，<c>1</c> = 上一帧，依此类推。
        /// 越界返回 <c>null</c>。
        /// </summary>
        public BlueprintFrameSnapshot? GetByIndex(int indexFromLatest)
        {
            if (indexFromLatest < 0 || indexFromLatest >= _count) return null;
            // _nextWritePos 指向下一个写入槽，最新帧在 _nextWritePos - 1
            int pos = (_nextWritePos - 1 - indexFromLatest + _ring.Length * 2) % _ring.Length;
            return _ring[pos];
        }

        /// <summary>
        /// 按绝对 TickCount 查找快照。
        /// 不在历史窗口内时返回 <c>null</c>。
        /// </summary>
        public BlueprintFrameSnapshot? GetByTick(int tick)
        {
            if (_count == 0 || tick > LatestTick || tick < OldestTick) return null;
            return GetByIndex(LatestTick - tick);
        }

        /// <summary>清空历史记录（不释放预分配的对象）</summary>
        public void Clear()
        {
            _nextWritePos = 0;
            _count        = 0;
            LatestTick    = -1;
        }

        // ── 内部 ──

        private static StateDiff[] ComputeDiffs(BlueprintFrameSnapshot? prev, BlueprintFrameSnapshot cur)
        {
            if (prev == null || prev.States.Length == 0 || cur.States.Length == 0)
                return _emptyDiffs;

            var list = new List<StateDiff>();
            int len  = Math.Min(prev.States.Length, cur.States.Length);
            for (int i = 0; i < len; i++)
            {
                var before = prev.States[i].Phase;
                var after  = cur.States[i].Phase;
                if (before != after)
                    list.Add(new StateDiff(i, before, after));
            }
            return list.Count > 0 ? list.ToArray() : _emptyDiffs;
        }
    }
}
