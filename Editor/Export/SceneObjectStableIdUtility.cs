#nullable enable
using SceneBlueprint.Runtime.Markers;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// 场景对象稳定 ID 生成工具（C2）。
    /// 优先使用 SceneMarker.MarkerId，其次使用 Unity GlobalObjectId。
    /// </summary>
    internal static class SceneObjectStableIdUtility
    {
        private const string MarkerPrefix = "marker:";
        private const string GlobalObjectPrefix = "global:";
        private const string InstancePrefix = "instance:";

        public static string GetStableId(GameObject? gameObject)
        {
            if (gameObject == null) return "";

            var marker = gameObject.GetComponent<SceneMarker>();
            if (marker != null && !string.IsNullOrEmpty(marker.MarkerId))
            {
                return MarkerPrefix + marker.MarkerId;
            }

            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            var globalIdStr = globalId.ToString();
            if (!string.IsNullOrEmpty(globalIdStr) && !globalIdStr.EndsWith("-0-0-0-0"))
            {
                return GlobalObjectPrefix + globalIdStr;
            }

            return InstancePrefix + gameObject.GetInstanceID().ToString();
        }
    }
}
