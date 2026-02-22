#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Test
{
    /// <summary>
    /// 迷你怪物 AI 行为组件。
    /// <para>
    /// Phase 1 支持：
    /// - <b>Idle</b>：站桩不动。
    /// - <b>Guard</b>：站桩 + 检测范围内是否有玩家，检测到时转向玩家并变色。
    /// </para>
    /// <para>
    /// Phase 2（后续）：Patrol 巡逻行为。
    /// </para>
    /// </summary>
    public class MonsterBehavior : MonoBehaviour
    {
        public enum BehaviorType
        {
            Idle,
            Guard
        }

        [Header("行为配置")]
        public BehaviorType CurrentBehavior = BehaviorType.Idle;
        public float GuardRadius = 5f;

        [Header("状态（只读）")]
        [SerializeField] private bool _playerInRange;
        [SerializeField] private float _distanceToPlayer;

        /// <summary>全局暂停标志：由 BlueprintDebugController.OnPaused/OnResumed 驱动，对所有违走的怪物 AI 生效。</summary>
        public static bool IsBlueprintPaused = false;

        private Transform? _playerTransform;
        private MeshRenderer? _renderer;
        private Color _originalColor;
        private Color _alertColor = new Color(1f, 0f, 0f, 1f); // 纯红色表示警戒

        private void Start()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null && _renderer.material != null)
            {
                _originalColor = MonsterVisual.GetMaterialColor(_renderer.material);
                _alertColor = new Color(
                    Mathf.Min(_originalColor.r + 0.4f, 1f),
                    _originalColor.g * 0.3f,
                    _originalColor.b * 0.3f
                );
            }

            // 查找玩家
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        private void Update()
        {
            if (IsBlueprintPaused) return;

            switch (CurrentBehavior)
            {
                case BehaviorType.Idle:
                    UpdateIdle();
                    break;
                case BehaviorType.Guard:
                    UpdateGuard();
                    break;
            }
        }

        /// <summary>Idle 行为：什么都不做</summary>
        private void UpdateIdle()
        {
            _playerInRange = false;
            RestoreColor();
        }

        /// <summary>Guard 行为：检测玩家距离，进入范围时警戒</summary>
        private void UpdateGuard()
        {
            if (_playerTransform == null)
            {
                // 重新查找玩家（可能是延迟初始化）
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _playerTransform = player.transform;
                if (_playerTransform == null) return;
            }

            var groundPos = transform.position - Vector3.up * 0.5f; // Cube 底部
            var playerGroundPos = _playerTransform.position;
            playerGroundPos.y = groundPos.y; // 只比较水平距离

            _distanceToPlayer = Vector3.Distance(groundPos, playerGroundPos);
            _playerInRange = _distanceToPlayer <= GuardRadius;

            if (_playerInRange)
            {
                // 转向玩家
                var dir = (_playerTransform.position - transform.position).normalized;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                {
                    var targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                        Time.deltaTime * 5f);
                }

                // 切换到警戒颜色
                SetAlertColor();
            }
            else
            {
                RestoreColor();
            }
        }

        private void SetAlertColor()
        {
            if (_renderer != null && _renderer.material != null)
            {
                var current = MonsterVisual.GetMaterialColor(_renderer.material);
                MonsterVisual.SetMaterialColor(_renderer.material,
                    Color.Lerp(current, _alertColor, Time.deltaTime * 8f));
            }
        }

        private void RestoreColor()
        {
            if (_renderer != null && _renderer.material != null)
            {
                var current = MonsterVisual.GetMaterialColor(_renderer.material);
                MonsterVisual.SetMaterialColor(_renderer.material,
                    Color.Lerp(current, _originalColor, Time.deltaTime * 4f));
            }
        }
    }
}
