#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Test
{
    /// <summary>
    /// 怪物可视化组件——用彩色 Cube 表示怪物，显示信息标签和警戒范围。
    /// 使用 Unlit/Color 内置 Shader 确保在任何渲染管线下都能正常显示。
    /// </summary>
    public class MonsterVisual : MonoBehaviour
    {
        [Header("数据")]
        public string MonsterId = "";
        public int Level = 1;
        public string Behavior = "Idle";
        public float GuardRadius = 5f;

        [Header("可视化")]
        [SerializeField] private Color _cubeColor = Color.red;
        [SerializeField] private bool _showGuardRadius = true;

        private MeshRenderer? _meshRenderer;
        private Material? _material;

        // 颜色映射表：不同怪物 ID 使用不同颜色
        private static readonly Color[] PaletteColors = new[]
        {
            new Color(0.9f, 0.2f, 0.2f), // 红
            new Color(0.2f, 0.5f, 0.9f), // 蓝
            new Color(0.9f, 0.6f, 0.1f), // 橙
            new Color(0.6f, 0.2f, 0.8f), // 紫
            new Color(0.2f, 0.8f, 0.8f), // 青
            new Color(0.8f, 0.8f, 0.2f), // 黄
            new Color(0.9f, 0.4f, 0.6f), // 粉
            new Color(0.4f, 0.7f, 0.3f), // 绿
        };

        /// <summary>
        /// 初始化怪物可视化。
        /// </summary>
        public void Initialize(string monsterId, int level, string behavior, float guardRadius,
            Vector3 position, Vector3 eulerRotation)
        {
            MonsterId = monsterId;
            Level = level;
            Behavior = behavior;
            GuardRadius = guardRadius;

            transform.position = position + Vector3.up * 0.5f; // Cube 底部齐地
            transform.rotation = Quaternion.Euler(eulerRotation);

            // 根据 MonsterId 哈希分配颜色
            int hash = string.IsNullOrEmpty(monsterId) ? 0 : monsterId.GetHashCode();
            _cubeColor = PaletteColors[Mathf.Abs(hash) % PaletteColors.Length];

            gameObject.name = $"Monster_{monsterId}_Lv{level}";

            SetupVisual();
        }

        private void SetupVisual()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null) return;

            // 使用 Unlit/Color —— 最简单、最可靠的内置 Shader
            _material = new Material(Shader.Find("Unlit/Color")!);
            _material.SetColor("_Color", _cubeColor);
            _meshRenderer.material = _material;
        }

        private void OnDrawGizmos()
        {
            if (!_showGuardRadius) return;

            var basePos = transform.position - Vector3.up * 0.5f;

            if (Behavior == "Guard" && GuardRadius > 0.1f)
            {
                Gizmos.color = new Color(_cubeColor.r, _cubeColor.g, _cubeColor.b, 0.3f);
                DrawWireCircle(basePos + Vector3.up * 0.05f, GuardRadius, 32);

                Gizmos.color = new Color(_cubeColor.r, _cubeColor.g, _cubeColor.b, 0.08f);
                Gizmos.DrawSphere(basePos + Vector3.up * 0.05f, GuardRadius);
            }

            // 朝向箭头
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
        }

        private void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float step = 360f / segments;
            var prev = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i * Mathf.Deg2Rad;
                var next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void OnGUI()
        {
            if (Camera.main == null) return;

            var screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            if (screenPos.z < 0) return;

            screenPos.y = Screen.height - screenPos.y;

            var label = $"{MonsterId} Lv{Level}\n[{Behavior}]";
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = _cubeColor }
            };

            var size = style.CalcSize(new GUIContent(label));
            var rect = new Rect(screenPos.x - size.x / 2, screenPos.y - size.y / 2, size.x, size.y);

            var bgRect = new Rect(rect.x - 4, rect.y - 2, rect.width + 8, rect.height + 4);
            GUI.Box(bgRect, GUIContent.none);
            GUI.Label(rect, label, style);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        // ── 颜色工具（Unlit/Color 统一使用 _Color） ──

        public static void SetMaterialColor(Material mat, Color color)
        {
            mat.SetColor("_Color", color);
        }

        public static Color GetMaterialColor(Material mat)
        {
            return mat.GetColor("_Color");
        }
    }
}
