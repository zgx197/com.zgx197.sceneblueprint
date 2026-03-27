#nullable enable
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using SceneBlueprint.Contract;
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
        /// Annotation 数据后处理回调委托。
        /// <para>
        /// 在 <see cref="MarkerAnnotation.CollectExportData"/> 之后、序列化为 PropertyValue 之前调用。
        /// 业务侧可注册回调来修改导出数据（如解析 Inherit 回退值）。
        /// </para>
        /// </summary>
        /// <param name="annotation">当前正在导出的 Annotation 组件</param>
        /// <param name="data">CollectExportData 产出的原始数据字典（可修改）</param>
        /// <param name="ownerMarker">Annotation 所在的 SceneMarker</param>
        public delegate void AnnotationPostProcessor(
            MarkerAnnotation annotation,
            IDictionary<string, object> data,
            SceneMarker ownerMarker);

        /// <summary>
        /// Annotation 数据后处理事件。
        /// <para>
        /// 在每个 Annotation 的 CollectExportData 完成后触发，
        /// 允许业务侧修改导出数据（如将编辑器 UI 概念解析为运行时最终值）。
        /// </para>
        /// </summary>
        public static event AnnotationPostProcessor? OnAnnotationDataCollected;

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
        /// 无 Annotation 时静默返回空数组。
        /// 是否必须存在 Annotation 由绑定声明和校验链路负责，而不是导出辅助层。
        /// </para>
        /// </summary>
        /// <param name="pointMarker">目标 PointMarker</param>
        /// <param name="actionTypeId">调用方的 Action TypeId（用于日志上下文）</param>
        public static AnnotationDataEntry[] CollectAnnotations(
            PointMarker pointMarker, string actionTypeId)
        {
            var annotations = MarkerCache.GetAnnotations(pointMarker);
            if (annotations.Length == 0)
                return System.Array.Empty<AnnotationDataEntry>();

            var entries = new List<AnnotationDataEntry>();
            foreach (var annotation in annotations)
            {
                var data = new Dictionary<string, object>();
                annotation.CollectExportData(data);
                OnAnnotationDataCollected?.Invoke(annotation, data, pointMarker);

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
                return System.Array.Empty<AnnotationDataEntry>();

            var entries = new List<AnnotationDataEntry>();
            foreach (var annotation in annotations)
            {
                var data = new Dictionary<string, object>();
                annotation.CollectExportData(data);
                OnAnnotationDataCollected?.Invoke(annotation, data, marker);

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
        /// 为 AreaMarker 构建区域几何载荷 JSON。
        /// Box/Polygon → type="Polygon"（世界坐标底面顶点 + 高度）
        /// Circle     → type="Circle"（圆心 + 半径 + 高度）
        /// Capsule    → type="Capsule"（两端点 + 半径 + 高度）
        /// </summary>
        public static string BuildAreaSpatialPayload(AreaMarker area)
        {
            switch (area.Shape)
            {
                case AreaShape.Circle:
                    return JsonUtility.ToJson(new AreaCircleSpatialData
                    {
                        type = "Circle",
                        center = area.transform.position,
                        radius = area.Radius,
                        height = area.Height
                    });

                case AreaShape.Capsule:
                {
                    var (pA, pB) = area.GetCapsuleWorldPoints();
                    return JsonUtility.ToJson(new AreaCapsuleSpatialData
                    {
                        type = "Capsule",
                        pointA = pA,
                        pointB = pB,
                        radius = area.CapsuleRadius,
                        height = area.Height
                    });
                }

                default: // Box 和 Polygon 统一走 Polygon 路径
                {
                    // GenerateFloorVerticesCW 为 Box 生成 4 顶点、Polygon 返回原始顶点（均为局部坐标）
                    var localVerts = area.GenerateFloorVerticesCW();
                    var verts = area.FloorVerticesToWorld(localVerts);
                    EnsureClockwiseXZ(verts);
                    return JsonUtility.ToJson(new AreaPolygonSpatialData
                    {
                        type = "Polygon",
                        floorVertices = verts,
                        height = area.Height
                    });
                }
            }
        }

        /// <summary>确保顶点数组为俯视 XZ 平面顺时针绕序</summary>
        private static void EnsureClockwiseXZ(Vector3[] verts)
        {
            if (verts.Length < 3) return;
            float sum = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % verts.Length];
                sum += (b.x - a.x) * (b.z + a.z);
            }
            if (sum < 0)
                System.Array.Reverse(verts);
        }

        [System.Serializable]
        private struct PointSpatialData
        {
            public Vector3 position;
            public Vector3 rotation;
        }

        [System.Serializable]
        private struct AreaPolygonSpatialData
        {
            public string type;
            public Vector3[] floorVertices;
            public float height;
        }

        [System.Serializable]
        private struct AreaCircleSpatialData
        {
            public string type;
            public Vector3 center;
            public float radius;
            public float height;
        }

        [System.Serializable]
        private struct AreaCapsuleSpatialData
        {
            public string type;
            public Vector3 pointA;
            public Vector3 pointB;
            public float radius;
            public float height;
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
