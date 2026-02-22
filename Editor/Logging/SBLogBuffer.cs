#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace SceneBlueprint.Editor.Logging
{
    /// <summary>
    /// 环形缓冲区，存储最近 N 条日志条目。
    /// <para>满时自动覆盖最旧条目，读取时按时间顺序返回。</para>
    /// </summary>
    public class SBLogBuffer
    {
        private SBLogEntry[] _entries;
        private int _head;   // 下一个写入位置
        private int _count;  // 当前条目数

        /// <summary>缓冲区容量</summary>
        public int Capacity => _entries.Length;

        /// <summary>当前条目数</summary>
        public int Count => _count;

        /// <summary>每次有新条目写入时触发</summary>
        public event Action<SBLogEntry>? OnEntryAdded;

        /// <summary>清空时触发</summary>
        public event Action? OnCleared;

        public SBLogBuffer(int capacity = 500)
        {
            _entries = new SBLogEntry[Math.Max(capacity, 16)];
            _head = 0;
            _count = 0;
        }

        /// <summary>写入一条日志</summary>
        public void Push(in SBLogEntry entry)
        {
            _entries[_head] = entry;
            _head = (_head + 1) % _entries.Length;
            if (_count < _entries.Length)
                _count++;
            OnEntryAdded?.Invoke(entry);
        }

        /// <summary>按时间顺序获取所有条目</summary>
        public List<SBLogEntry> GetAll()
        {
            var result = new List<SBLogEntry>(_count);
            if (_count == 0) return result;

            int start = _count < _entries.Length ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _entries.Length;
                result.Add(_entries[idx]);
            }
            return result;
        }

        /// <summary>按条件过滤条目</summary>
        public List<SBLogEntry> Filter(
            SBLogLevel? minLevel = null,
            string? tag = null,
            string? keyword = null)
        {
            var all = GetAll();
            var result = new List<SBLogEntry>();

            foreach (var e in all)
            {
                if (minLevel.HasValue && e.Level < minLevel.Value) continue;
                if (!string.IsNullOrEmpty(tag) && !string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(keyword) && e.Message.IndexOf(keyword!, StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(e);
            }
            return result;
        }

        /// <summary>清空缓冲区</summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
            Array.Clear(_entries, 0, _entries.Length);
            OnCleared?.Invoke();
        }

        /// <summary>调整容量（清空现有数据）</summary>
        public void Resize(int newCapacity)
        {
            newCapacity = Math.Max(newCapacity, 16);
            _entries = new SBLogEntry[newCapacity];
            _head = 0;
            _count = 0;
        }

        /// <summary>导出为文本（可选过滤）</summary>
        public string ExportAsText(
            SBLogLevel? minLevel = null,
            string? tag = null,
            string? keyword = null)
        {
            var entries = (minLevel == null && tag == null && keyword == null)
                ? GetAll()
                : Filter(minLevel, tag, keyword);

            var sb = new StringBuilder();
            sb.AppendLine("=== SceneBlueprint Log Export ===");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var filterDesc = new List<string>();
            if (minLevel.HasValue) filterDesc.Add($"Level>={minLevel.Value}");
            if (!string.IsNullOrEmpty(tag)) filterDesc.Add($"Tag={tag}");
            if (!string.IsNullOrEmpty(keyword)) filterDesc.Add($"Keyword=\"{keyword}\"");
            sb.AppendLine($"Filter: {(filterDesc.Count > 0 ? string.Join(", ", filterDesc) : "All")}");
            sb.AppendLine($"Entries: {entries.Count}");
            sb.AppendLine();

            foreach (var e in entries)
                sb.AppendLine(e.ToExportString());

            return sb.ToString();
        }
    }
}
