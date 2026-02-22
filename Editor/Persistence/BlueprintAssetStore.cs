#nullable enable
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.Persistence
{
    /// <summary>
    /// 蓝图资产持久化存储——处理 .blueprint.json 与 BlueprintAsset 的 Unity 文件操作。
    /// 不含图序列化逻辑，仅负责文件 I/O 和 AssetDatabase 交互。
    /// </summary>
    internal static class BlueprintAssetStore
    {
        /// <summary>读取 asset 关联的图 JSON（asset.IsEmpty 时返回 null）。</summary>
        public static string? ReadJson(BlueprintAsset asset) => asset.GraphData?.text;

        /// <summary>对已有 asset 保存新图 JSON，自增版本号并刷新 AssetDatabase。</summary>
        public static void SaveAsset(string assetPath, string graphJson, BlueprintAsset asset)
        {
            WriteJsonFile(assetPath, graphJson, asset);
            asset.Version++;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>新建 BlueprintAsset，写入图 JSON 并保存。</summary>
        public static BlueprintAsset CreateAsset(string path, string graphJson)
        {
            var asset = ScriptableObject.CreateInstance<BlueprintAsset>();
            asset.InitializeNew(Path.GetFileNameWithoutExtension(path));
            AssetDatabase.CreateAsset(asset, path);
            WriteJsonFile(path, graphJson, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ── 内部 ──

        private static void WriteJsonFile(string assetPath, string graphJson, BlueprintAsset asset)
        {
            string jsonPath = GetJsonPath(assetPath);
            File.WriteAllText(jsonPath, graphJson, Encoding.UTF8);
            AssetDatabase.ImportAsset(jsonPath);
            asset.GraphData = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
        }

        private static string GetJsonPath(string assetPath)
        {
            string dir  = Path.GetDirectoryName(assetPath) ?? "";
            string name = Path.GetFileNameWithoutExtension(assetPath);
            return (dir + "/" + name + ".blueprint.json").Replace('\\', '/');
        }
    }
}
