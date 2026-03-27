#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter
{
    public sealed class SpawnWaveStockState
    {
        public string PlanSource = "";
        public string PlanPosMode = "";
        public int PlanWaveCount;
        public string PlanSummary = "";
        public string ExecutionSummary = "";
        public int CurrentWaveIndex;
        public int LastSpawnTick;
        public string CurrentWaveId = "";
        public int CurrentWaveDelayTicks;
        public int CurrentWaveRequestedSpawnCount;
        public int CurrentWaveSubjectSlotCount;
        public int CurrentWavePublicSubjectCount;
        public string CurrentWaveSubjectSlotsSummary = "";
        public string CurrentWaveSubjectIdentitySummary = "";
        public SubjectSemanticDescriptor[] CurrentWaveSubjects = Array.Empty<SubjectSemanticDescriptor>();
        public string NextWaveId = "";
        public int NextWaveRequestedSpawnCount;
        public int NextWaveSubjectSlotCount;
        public int NextWavePublicSubjectCount;
        public string NextWaveSubjectSlotsSummary = "";
        public string NextWaveSubjectIdentitySummary = "";
        public SubjectSemanticDescriptor[] NextWaveSubjects = Array.Empty<SubjectSemanticDescriptor>();
        public int TotalInitialCount;
        public int RemainingTotal;
        public List<SpawnWaveStockEntry> Entries { get; } = new();
    }

    public sealed class SpawnWaveStockEntry
    {
        public string EntryId = "";
        public int MonsterType;
        public string MonsterId = "";
        public string Tag = "";
        public string SpawnMode = "Alive";
        public string InitialBehavior = "Idle";
        public float VisionRange;
        public float HearingRange;
        public int InitialCount;
        public int UnitWeight;
        public int RemainingCount;
    }
}
