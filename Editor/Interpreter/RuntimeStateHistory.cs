#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Editor.Interpreter
{
    public sealed class RuntimeStateHistoryRecord
    {
        public RuntimeStateHistoryRecord(int tick, RuntimeStatePresentationResult presentationResult)
        {
            Tick = tick;
            PresentationResult = presentationResult ?? throw new ArgumentNullException(nameof(presentationResult));
        }

        public int Tick { get; }

        public RuntimeStatePresentationResult PresentationResult { get; }
    }

    public sealed class RuntimeStateHistoryStore
    {
        private readonly int _capacity;
        private readonly List<RuntimeStateHistoryRecord> _records;

        public RuntimeStateHistoryStore(int capacity = 300)
        {
            _capacity = Math.Max(1, capacity);
            _records = new List<RuntimeStateHistoryRecord>(_capacity);
        }

        public int Count => _records.Count;

        public IReadOnlyList<RuntimeStateHistoryRecord> Records => _records;

        public void Clear()
        {
            _records.Clear();
        }

        public void Record(int tick, RuntimeStatePresentationResult presentationResult)
        {
            if (_records.Count > 0 && _records[_records.Count - 1].Tick == tick)
            {
                _records[_records.Count - 1] = new RuntimeStateHistoryRecord(tick, presentationResult);
                return;
            }

            _records.Add(new RuntimeStateHistoryRecord(tick, presentationResult));
            if (_records.Count > _capacity)
            {
                _records.RemoveAt(0);
            }
        }

        public bool TryGetRecord(int tick, out RuntimeStateHistoryRecord? record)
        {
            for (var index = _records.Count - 1; index >= 0; index--)
            {
                if (_records[index].Tick != tick)
                {
                    continue;
                }

                record = _records[index];
                return true;
            }

            record = null;
            return false;
        }

        public RuntimeStateHistoryRecord? GetLatest()
        {
            return _records.Count == 0 ? null : _records[_records.Count - 1];
        }
    }
}
