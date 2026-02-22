#nullable enable
using UnityEngine;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// 蓝图数据 JSON 序列化/反序列化工具。
    /// 使用 Unity JsonUtility 实现，输出格式化 JSON 便于阅读和调试。
    /// </summary>
    public static class BlueprintSerializer
    {
        /// <summary>将 SceneBlueprintData 序列化为格式化 JSON 字符串</summary>
        public static string ToJson(SceneBlueprintData data, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(data, prettyPrint);
        }

        /// <summary>从 JSON 字符串反序列化为 SceneBlueprintData</summary>
        public static SceneBlueprintData? FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<SceneBlueprintData>(json);
        }
    }
}
