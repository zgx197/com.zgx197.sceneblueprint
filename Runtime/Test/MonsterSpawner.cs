#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Runtime.Interpreter;
using UnityEngine;

namespace SceneBlueprint.Runtime.Test
{
    /// <summary>
    /// 怪物生成器——实现 ISpawnHandler，在测试场景中创建可视化怪物（彩色 Cube）。
    /// </summary>
    public class MonsterSpawner : MonoBehaviour, ISpawnHandler
    {
        [Header("生成配置")]
        [SerializeField] private Vector3 _monsterScale = new Vector3(1f, 1f, 1f);

        private readonly List<GameObject> _spawnedMonsters = new();
        private Transform? _monsterRoot;

        private void Awake()
        {
            // 创建怪物根节点方便管理
            var rootGo = new GameObject("[Monsters]");
            _monsterRoot = rootGo.transform;
        }

        /// <summary>清除所有已生成的怪物</summary>
        public void ClearAll()
        {
            for (int i = _spawnedMonsters.Count - 1; i >= 0; i--)
            {
                if (_spawnedMonsters[i] != null)
                    Destroy(_spawnedMonsters[i]);
            }
            _spawnedMonsters.Clear();
        }

        // ── ISpawnHandler ──

        public void OnSpawn(SpawnData data)
        {
            var monster = CreateMonsterCube(data);
            _spawnedMonsters.Add(monster);

            Debug.Log($"[MonsterSpawner] 生成怪物: {data.MonsterId} Lv{data.Level} " +
                      $"行为={data.Behavior} 位置={data.Position} 警戒半径={data.GuardRadius}");
        }

        public void OnSpawnBatchComplete(int totalCount)
        {
            Debug.Log($"[MonsterSpawner] 批量生成完毕，共 {totalCount} 个怪物");
        }

        // ── 内部方法 ──

        private GameObject CreateMonsterCube(SpawnData data)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_monsterRoot);
            go.transform.localScale = _monsterScale;

            // 添加可视化组件
            var visual = go.AddComponent<MonsterVisual>();
            visual.Initialize(
                data.MonsterId,
                data.Level,
                data.Behavior,
                data.GuardRadius,
                data.Position,
                data.EulerRotation
            );

            // 添加行为组件
            var behavior = go.AddComponent<MonsterBehavior>();
            behavior.GuardRadius = data.GuardRadius;

            switch (data.Behavior?.ToLower())
            {
                case "guard":
                    behavior.CurrentBehavior = MonsterBehavior.BehaviorType.Guard;
                    break;
                case "idle":
                default:
                    behavior.CurrentBehavior = MonsterBehavior.BehaviorType.Idle;
                    break;
            }

            return go;
        }
    }
}
