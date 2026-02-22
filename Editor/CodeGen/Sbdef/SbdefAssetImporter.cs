#nullable enable
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace SceneBlueprint.Editor.CodeGen.Sbdef
{
    /// <summary>
    /// .sbdef 文件的 Unity ScriptedImporter。
    /// <para>
    /// 任意 .sbdef 文件保存时自动触发 <see cref="SbdefCodeGen.Run"/>，
    /// 重新生成 Core/Generated/*.g.cs 常量文件。
    /// </para>
    /// </summary>
    [ScriptedImporter(1, "sbdef")]
    public class SbdefAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            // 创建一个占位资产，使 .sbdef 在 Project 窗口可见
            var asset = ScriptableObject.CreateInstance<SbdefAsset>();
            ctx.AddObjectToAsset("main", asset);
            ctx.SetMainObject(asset);
        }

#if !SB_DISABLE_AUTO_CODEGEN
        private class InternalPostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                bool needsRun = false;
                foreach (var path in importedAssets)
                {
                    if (path.EndsWith(".sbdef", System.StringComparison.OrdinalIgnoreCase))
                    {
                        needsRun = true;
                        break;
                    }
                }
                foreach (var path in deletedAssets)
                {
                    if (path.EndsWith(".sbdef", System.StringComparison.OrdinalIgnoreCase))
                    {
                        needsRun = true;
                        break;
                    }
                }

                if (needsRun)
                    SbdefCodeGen.Run(verbose: false);
            }
        }
#endif
    }

    /// <summary>
    /// .sbdef 文件对应的 Unity 资产对象，仅作占位使 Project 窗口显示图标。
    /// </summary>
    public class SbdefAsset : ScriptableObject { }
}
