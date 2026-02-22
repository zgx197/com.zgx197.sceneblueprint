#nullable enable
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// 在编辑器启动时检查 GizmoStyleSO 资产是否存在，不存在则自动创建默认资产。
    /// <para>
    /// 采用方案 B：预创建默认 SO + 保留 C# 回退安全网。
    /// 策划可直接在配置窗口或 Inspector 中调整样式，所有改动通过 git 跟踪。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class GizmoStyleInitializer
    {
        private const string DefaultAssetPath = "Assets/SceneBlueprintData/GizmoStyle.asset";

        static GizmoStyleInitializer()
        {
            // 延迟执行，确保 AssetDatabase 完全就绪
            EditorApplication.delayCall += EnsureDefaultAsset;
        }

        private static void EnsureDefaultAsset()
        {
            // 已存在则跳过
            var guids = AssetDatabase.FindAssets("t:GizmoStyleSO");
            if (guids.Length > 0) return;

            // 确保目录存在
            const string dir = "Assets/SceneBlueprintData";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", "SceneBlueprintData");

            // 创建带默认值的 SO（字段初始值即为 C# 硬编码默认值）
            var asset = ScriptableObject.CreateInstance<GizmoStyleSO>();
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            AssetDatabase.SaveAssets();

            // 刷新缓存
            GizmoStyleConstants.InvalidateCache();

            SBLog.Info(SBLogTags.Template,
                $"已自动创建默认 GizmoStyleSO: {DefaultAssetPath}");
        }

        /// <summary>手动触发创建（供菜单或测试调用）</summary>
        [MenuItem("SceneBlueprint/创建默认 Gizmo 样式", false, 210)]
        public static void CreateDefaultAssetMenu()
        {
            var guids = AssetDatabase.FindAssets("t:GizmoStyleSO");
            if (guids.Length > 0)
            {
                var existingPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var existing = AssetDatabase.LoadAssetAtPath<GizmoStyleSO>(existingPath);
                EditorGUIUtility.PingObject(existing);
                Selection.activeObject = existing;
                SBLog.Info(SBLogTags.Template, $"GizmoStyleSO 已存在: {existingPath}");
                return;
            }

            EnsureDefaultAsset();

            // 选中新创建的资产
            var newAsset = AssetDatabase.LoadAssetAtPath<GizmoStyleSO>(DefaultAssetPath);
            if (newAsset != null)
            {
                EditorGUIUtility.PingObject(newAsset);
                Selection.activeObject = newAsset;
            }
        }
    }
}
