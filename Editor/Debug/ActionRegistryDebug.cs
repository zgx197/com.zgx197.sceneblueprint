#nullable enable
using UnityEngine;
using UnityEditor;
using System.Linq;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Debug
{
    /// <summary>
    /// ActionRegistry 调试工具——用于排查节点注册问题。
    /// </summary>
    public static class ActionRegistryDebug
    {
        [MenuItem("SceneBlueprint/Debug/打印已注册节点", priority = 300)]
        public static void PrintRegisteredActions()
        {
            UnityEngine.Debug.Log("=== ActionRegistry 调试信息 ===");
            
            var registry = SceneBlueprintProfile.CreateActionRegistry();
            var all = registry.GetAll();
            
            UnityEngine.Debug.Log($"<b>总计注册节点数: {all.Count}</b>");
            UnityEngine.Debug.Log("");
            
            var grouped = all.GroupBy(d => d.Category).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                UnityEngine.Debug.Log($"<color=cyan>【{group.Key}】</color> ({group.Count()} 个)");
                foreach (var def in group.OrderBy(d => d.DisplayName))
                {
                    UnityEngine.Debug.Log($"  • {def.TypeId} → \"{def.DisplayName}\"");
                }
                UnityEngine.Debug.Log("");
            }
            
            UnityEngine.Debug.Log("=== 调试结束 ===");
        }
        
        [MenuItem("SceneBlueprint/Debug/检查 VFX 节点", priority = 301)]
        public static void CheckVFXNodes()
        {
            var registry = SceneBlueprintProfile.CreateActionRegistry();
            
            bool hasCameraShake = registry.TryGet("VFX.CameraShake", out var shakeDef);
            bool hasScreenFlash = registry.TryGet("VFX.ScreenFlash", out var flashDef);
            
            UnityEngine.Debug.Log($"VFX.CameraShake: {(hasCameraShake ? "✓ 已注册" : "✗ 未注册")}");
            UnityEngine.Debug.Log($"VFX.ScreenFlash: {(hasScreenFlash ? "✓ 已注册" : "✗ 未注册")}");
            
            if (hasCameraShake)
            {
                UnityEngine.Debug.Log($"  Category: {shakeDef!.Category}");
                UnityEngine.Debug.Log($"  DisplayName: {shakeDef.DisplayName}");
            }
            
            if (hasScreenFlash)
            {
                UnityEngine.Debug.Log($"  Category: {flashDef!.Category}");
                UnityEngine.Debug.Log($"  DisplayName: {flashDef.DisplayName}");
            }
        }
    }
}
