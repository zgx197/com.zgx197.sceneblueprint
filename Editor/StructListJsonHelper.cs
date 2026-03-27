#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// Editor 兼容包装器。
    /// 实际 StructList 编解码已经下沉到 Core，避免定义层解析逻辑继续散落在各层。
    /// </summary>
    public static class StructListJsonHelper
    {
        public static List<Dictionary<string, object>> Deserialize(string json, PropertyDefinition[] fields)
            => StructListValueCodec.Deserialize(json, fields);

        public static string Serialize(List<Dictionary<string, object>> items, PropertyDefinition[] fields)
            => StructListValueCodec.Serialize(items, fields);

        public static Dictionary<string, object> CreateDefaultItem(PropertyDefinition[] fields)
            => PropertyDefinitionValueUtility.CreateDefaultStructItem(fields);

        public static int GetItemCount(string? json)
            => StructListValueCodec.GetItemCount(json);
    }
}
