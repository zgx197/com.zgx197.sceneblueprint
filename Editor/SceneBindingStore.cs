#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 场景绑定存储抽象。
    /// 负责蓝图与场景绑定分组的读取与写入。
    /// </summary>
    public interface ISceneBindingStore
    {
        bool IsBoundToBlueprint(BlueprintAsset asset);

        bool TryLoadBindingGroups(BlueprintAsset asset, out IReadOnlyList<SubGraphBindingGroup> bindingGroups);

        void SaveBindingGroups(BlueprintAsset asset, IReadOnlyList<SubGraphBindingGroup> bindingGroups);
    }

    /// <summary>
    /// 基于 SceneBlueprintManager 的默认场景绑定存储。
    /// </summary>
    public sealed class SceneManagerBindingStore : ISceneBindingStore
    {
        public bool IsBoundToBlueprint(BlueprintAsset asset)
        {
            if (asset == null)
                return false;

            var manager = Object.FindObjectOfType<SceneBlueprintManager>();
            if (manager == null)
                return false;

            return manager.BlueprintAsset == asset;
        }

        public bool TryLoadBindingGroups(BlueprintAsset asset, out IReadOnlyList<SubGraphBindingGroup> bindingGroups)
        {
            if (asset == null)
            {
                bindingGroups = System.Array.Empty<SubGraphBindingGroup>();
                return false;
            }

            var manager = Object.FindObjectOfType<SceneBlueprintManager>();
            if (manager == null || manager.BlueprintAsset != asset || manager.BindingGroups.Count == 0)
            {
                bindingGroups = System.Array.Empty<SubGraphBindingGroup>();
                return false;
            }

            bindingGroups = manager.BindingGroups;
            return true;
        }

        public void SaveBindingGroups(BlueprintAsset asset, IReadOnlyList<SubGraphBindingGroup> bindingGroups)
        {
            if (asset == null)
                throw new System.ArgumentNullException(nameof(asset));

            var manager = Object.FindObjectOfType<SceneBlueprintManager>();
            if (manager == null)
            {
                var go = new GameObject("SceneBlueprintManager");
                manager = go.AddComponent<SceneBlueprintManager>();
                Undo.RegisterCreatedObjectUndo(go, "创建场景蓝图管理器");
                SBLog.Info(SBLogTags.Binding, "已在场景中创建 SceneBlueprintManager");
            }

            Undo.RecordObject(manager, "同步蓝图到场景");
            manager.BlueprintAsset = asset;
            manager.BindingGroups.Clear();

            foreach (var group in bindingGroups)
            {
                manager.BindingGroups.Add(group);
            }

            EditorUtility.SetDirty(manager);
        }
    }
}
