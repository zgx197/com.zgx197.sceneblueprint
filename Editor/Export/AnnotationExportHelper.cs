#nullable enable
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// Annotation 导出辅助工具——从 Marker 上收集 MarkerAnnotation 数据并转换为导出格式。
    /// <para>
    /// 被 BlueprintExporter 的后处理阶段调用，用于：
    /// 1. 从 AreaMarker 收集子 PointMarker
    /// 2. 从 PointMarker 收集 MarkerAnnotation 数据
    /// 3. 将 Annotation 数据写入 SceneBindingEntry.Annotations
    /// </para>
    /// </summary>
    public static class AnnotationExportHelper
    {
        /// <summary>
        /// 从 AreaMarker 下收集所有子 PointMarker。
        /// </summary>
        public static List<PointMarker> CollectChildPointMarkers(AreaMarker area)
        {
            var result = new List<PointMarker>();
            var parent = area.transform;
            for (int i = 0; i < parent.childCount; i++)
            {
                var pm = parent.GetChild(i).GetComponent<PointMarker>();
                if (pm != null)
                    result.Add(pm);
            }
            return result;
        }

        /// <summary>
        /// 从 PointMarker 上收集所有 MarkerAnnotation 的导出数据。
        /// <para>
        /// 无 Annotation 时返回空数组并打印日志。
        /// </para>
        /// </summary>
        /// <param name="pointMarker">目标 PointMarker</param>
        /// <param name="actionTypeId">调用方的 Action TypeId（用于日志上下文）</param>
        public static AnnotationDataEntry[] CollectAnnotations(
            PointMarker pointMarker, string actionTypeId)
        {
            var annotations = MarkerCache.GetAnnotations(pointMarker);
            if (annotations.Length == 0)
            {
                SBLog.Info(SBLogTags.Export,
                    $"PointMarker '{pointMarker.GetDisplayLabel()}' (ID: {pointMarker.MarkerId}) " +
                    $"无 MarkerAnnotation，将使用节点 '{actionTypeId}' 的全局默认配置");
                return System.Array.Empty<AnnotationDataEntry>();
            }

            var entries = new List<AnnotationDataEntry>();
            foreach (var annotation in annotations)
            {
                var data = new Dictionary<string, object>();
                annotation.CollectExportData(data);

                var properties = new List<PropertyValue>();
                foreach (var kvp in data)
                {
                    properties.Add(new PropertyValue
                    {
                        Key = kvp.Key,
                        ValueType = InferValueType(kvp.Value),
                        Value = SerializeValue(kvp.Value)
                    });
                }

                entries.Add(new AnnotationDataEntry
                {
                    TypeId = annotation.AnnotationTypeId,
                    Properties = properties.ToArray()
                });
            }

            return entries.ToArray();
        }

        /// <summary>
        /// 通过 MarkerId 或 StableObjectId 在场景中查找 SceneMarker。
        /// <para>支持 "marker:xxx" 格式的 StableObjectId，自动去除前缀。</para>
        /// </summary>
        public static SceneMarker? FindMarkerById(string markerIdOrStableId)
        {
            if (string.IsNullOrEmpty(markerIdOrStableId)) return null;

            // 解析 StableObjectId 前缀（如 "marker:154eb90985ea" → "154eb90985ea"）
            const string markerPrefix = "marker:";
            var pureId = markerIdOrStableId.StartsWith(markerPrefix)
                ? markerIdOrStableId.Substring(markerPrefix.Length)
                : markerIdOrStableId;

            var allMarkers = MarkerCache.GetAll();
            foreach (var marker in allMarkers)
            {
                if (marker != null && marker.MarkerId == pureId)
                    return marker;
            }
            return null;
        }

        /// <summary>
        /// 从任意 SceneMarker 上收集所有 MarkerAnnotation 的导出数据。
        /// <para>用于从 AreaMarker 自身收集 WaveSpawnConfig 等 Annotation。</para>
        /// </summary>
        public static AnnotationDataEntry[] CollectAnnotationsFromMarker(
            SceneMarker marker, string actionTypeId)
        {
            var annotations = MarkerCache.GetAnnotations(marker);
            if (annotations.Length == 0)
            {
                SBLog.Info(SBLogTags.Export,
                    $"Marker '{marker.GetDisplayLabel()}' (ID: {marker.MarkerId}) " +
                    $"无 MarkerAnnotation (Action: '{actionTypeId}')");
                return System.Array.Empty<AnnotationDataEntry>();
            }

            var entries = new List<AnnotationDataEntry>();
            foreach (var annotation in annotations)
            {
                var data = new Dictionary<string, object>();
                annotation.CollectExportData(data);

                var properties = new List<PropertyValue>();
                foreach (var kvp in data)
                {
                    properties.Add(new PropertyValue
                    {
                        Key = kvp.Key,
                        ValueType = InferValueType(kvp.Value),
                        Value = SerializeValue(kvp.Value)
                    });
                }

                entries.Add(new AnnotationDataEntry
                {
                    TypeId = annotation.AnnotationTypeId,
                    Properties = properties.ToArray()
                });
            }

            return entries.ToArray();
        }

        /// <summary>
        /// 为 PointMarker 构建空间载荷 JSON（position + rotation）。
        /// </summary>
        public static string BuildPointSpatialPayload(PointMarker pointMarker)
        {
            var t = pointMarker.transform;
            return JsonUtility.ToJson(new PointSpatialData
            {
                position = t.position,
                rotation = t.rotation.eulerAngles
            });
        }

        /// <summary>
        /// 为 AreaMarker 构建区域几何载荷 JSON（center + rotation + size + shape）。
        /// </summary>
        public static string BuildAreaSpatialPayload(AreaMarker area)
        {
            var t = area.transform;
            return JsonUtility.ToJson(new AreaSpatialData
            {
                center = t.position,
                rotation = t.rotation.eulerAngles,
                size = area.BoxSize,
                shape = area.Shape.ToString()
            });
        }

        [System.Serializable]
        private struct PointSpatialData
        {
            public Vector3 position;
            public Vector3 rotation;
        }

        [System.Serializable]
        private struct AreaSpatialData
        {
            public Vector3 center;
            public Vector3 rotation;
            public Vector3 size;
            public string shape;
        }

        private static string InferValueType(object value)
        {
            return value switch
            {
                int    => "Int",
                float  => "Float",
                bool   => "Bool",
                string => "String",
                System.Collections.IList => "Json",
                System.Collections.IDictionary => "Json",
                _ => "String"
            };
        }

        private static string SerializeValue(object value)
        {
            if (value is float f)  return f.ToString(CultureInfo.InvariantCulture);
            if (value is int   i)  return i.ToString(CultureInfo.InvariantCulture);
            if (value is bool  b)  return b ? "true" : "false";
            if (value is string s) return s;

            // 列表类型 → JSON 数组
            if (value is System.Collections.IList list)
                return SerializeList(list);

            // 字典类型 → JSON 对象
            if (value is System.Collections.IDictionary dict)
                return SerializeDict(dict);

            return value?.ToString() ?? "";
        }

        private static string SerializeList(System.Collections.IList list)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var item in list)
            {
                if (!first) sb.Append(',');
                first = false;
                if (item is System.Collections.IDictionary d)
                    sb.Append(SerializeDict(d));
                else
                    sb.Append(SerializePrimitive(item));
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string SerializeDict(System.Collections.IDictionary dict)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (System.Collections.DictionaryEntry kv in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"');
                sb.Append(kv.Key?.ToString() ?? "");
                sb.Append('"');
                sb.Append(':');
                var v = kv.Value;
                if (v is System.Collections.IDictionary nestedDict)
                    sb.Append(SerializeDict(nestedDict));
                else if (v is System.Collections.IList nestedList)
                    sb.Append(SerializeList(nestedList));
                else
                    sb.Append(SerializePrimitive(v));
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializePrimitive(object? v)
        {
            if (v == null)  return "null";
            if (v is float  fv) return fv.ToString(CultureInfo.InvariantCulture);
            if (v is double dv) return dv.ToString(CultureInfo.InvariantCulture);
            if (v is int    iv) return iv.ToString(CultureInfo.InvariantCulture);
            if (v is bool   bv) return bv ? "true" : "false";
            if (v is string sv)
            {
                // JSON 字符串转义
                return '"' + sv.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + '"';
            }
            return '"' + (v.ToString() ?? "") + '"';
        }
    }
}
