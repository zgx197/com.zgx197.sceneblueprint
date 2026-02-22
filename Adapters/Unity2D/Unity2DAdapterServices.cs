#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.SpatialAbstraction;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Adapters.Unity2D
{
    /// <summary>
    /// Unity2D 适配器服务入口。
    /// C4 阶段承接 XY 放置与 2D 绑定编码能力。
    /// </summary>
    public static class Unity2DAdapterServices
    {
        public const string AdapterType = "Unity2D";

        private static readonly ISceneObjectIdentityService IdentityServiceInstance = new Unity2DSceneObjectIdentityService();
        private static readonly ISpatialBindingCodec BindingCodecInstance = new Unity2DSpatialBindingCodec(IdentityServiceInstance);

        public static ISpatialBindingCodec BindingCodec => BindingCodecInstance;

        public static bool TryGetSceneViewPlacement(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            _ = sceneView;
            return Unity2DScenePlacementUtility.TryGetXYPlacement(mousePos, out worldPos);
        }

        public static void EncodeBinding(
            GameObject? sceneObject,
            BindingType bindingType,
            out string stableObjectId,
            out string adapterType,
            out string spatialPayloadJson)
        {
            adapterType = AdapterType;
            spatialPayloadJson = "{}";
            stableObjectId = "";

            if (sceneObject == null)
                return;

            var payload = BindingCodecInstance.Encode(sceneObject, bindingType);
            stableObjectId = payload.StableObjectId;

            if (!string.IsNullOrEmpty(payload.AdapterType))
                adapterType = payload.AdapterType;
            if (!string.IsNullOrEmpty(payload.SerializedSpatialData))
                spatialPayloadJson = payload.SerializedSpatialData;
        }
    }

    internal static class Unity2DScenePlacementUtility
    {
        /// <summary>
        /// 从鼠标位置获取 XY 平面上的世界坐标。
        /// 策略：Physics2D.GetRayIntersection -> PickGameObject(Renderer中心Z) -> Z=0 平面回退。
        /// </summary>
        public static bool TryGetXYPlacement(Vector2 mousePos, out Vector3 worldPos)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mousePos);

            var hit2D = Physics2D.GetRayIntersection(ray, 1000f);
            if (hit2D.collider != null)
            {
                worldPos = hit2D.point;
                return true;
            }

            var pickedGO = HandleUtility.PickGameObject(mousePos, false);
            if (pickedGO != null)
            {
                var renderer = pickedGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    float surfaceZ = renderer.bounds.center.z;
                    var surfacePlane = new Plane(Vector3.forward, new Vector3(0f, 0f, surfaceZ));
                    if (surfacePlane.Raycast(ray, out float surfaceEnter))
                    {
                        worldPos = ray.GetPoint(surfaceEnter);
                        return true;
                    }
                }
            }

            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                worldPos = ray.GetPoint(enter);
                return true;
            }

            worldPos = Vector3.zero;
            return false;
        }
    }

    /// <summary>
    /// Unity2D 场景对象稳定标识实现。
    /// </summary>
    public sealed class Unity2DSceneObjectIdentityService : ISceneObjectIdentityService
    {
        private const string MarkerPrefix = "marker:";
        private const string GlobalObjectPrefix = "global:";
        private const string InstancePrefix = "instance:";

        public string GetOrCreateStableId(object sceneObject)
        {
            if (sceneObject == null)
                throw new ArgumentNullException(nameof(sceneObject));

            var go = AsGameObject(sceneObject);
            if (go == null)
                throw new ArgumentException("sceneObject 必须是 GameObject 或 Component", nameof(sceneObject));

            var marker = go.GetComponent<SceneMarker>();
            if (marker != null && !string.IsNullOrEmpty(marker.MarkerId))
            {
                return MarkerPrefix + marker.MarkerId;
            }

            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(go);
            var globalIdStr = globalId.ToString();
            if (!string.IsNullOrEmpty(globalIdStr) && !globalIdStr.EndsWith("-0-0-0-0"))
            {
                return GlobalObjectPrefix + globalIdStr;
            }

            return InstancePrefix + go.GetInstanceID().ToString();
        }

        public bool TryResolve(string stableId, out object? sceneObject)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                sceneObject = null;
                return false;
            }

            if (stableId.StartsWith(MarkerPrefix, StringComparison.Ordinal))
            {
                string markerId = stableId.Substring(MarkerPrefix.Length);
                foreach (var marker in UnityEngine.Object.FindObjectsOfType<SceneMarker>())
                {
                    if (marker.MarkerId == markerId)
                    {
                        sceneObject = marker.gameObject;
                        return true;
                    }
                }

                sceneObject = null;
                return false;
            }

            if (stableId.StartsWith(GlobalObjectPrefix, StringComparison.Ordinal))
            {
                sceneObject = null;
                return false;
            }

            if (stableId.StartsWith(InstancePrefix, StringComparison.Ordinal))
            {
                string raw = stableId.Substring(InstancePrefix.Length);
                if (int.TryParse(raw, out int instanceId))
                {
                    var obj = EditorUtility.InstanceIDToObject(instanceId);
                    if (obj != null)
                    {
                        sceneObject = obj;
                        return true;
                    }
                }
            }

            sceneObject = null;
            return false;
        }

        private static GameObject? AsGameObject(object sceneObject)
        {
            if (sceneObject is GameObject go) return go;
            if (sceneObject is Component component) return component.gameObject;
            return null;
        }
    }

    /// <summary>
    /// Unity2D 场景绑定编解码实现（Transform / Area 最小可用）。
    /// </summary>
    public sealed class Unity2DSpatialBindingCodec : ISpatialBindingCodec
    {
        private readonly ISceneObjectIdentityService _identityService;

        public Unity2DSpatialBindingCodec(ISceneObjectIdentityService identityService)
        {
            _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        }

        public SceneBindingPayload Encode(object sceneObject, BindingType bindingType)
        {
            if (sceneObject == null)
                throw new ArgumentNullException(nameof(sceneObject));

            var stableId = _identityService.GetOrCreateStableId(sceneObject);
            string payloadJson = BuildSpatialPayload(sceneObject, bindingType);
            return new SceneBindingPayload(stableId, bindingType, payloadJson, Unity2DAdapterServices.AdapterType);
        }

        public bool TryDecode(in SceneBindingPayload payload, out object? sceneObject)
        {
            return _identityService.TryResolve(payload.StableObjectId, out sceneObject);
        }

        private static string BuildSpatialPayload(object sceneObject, BindingType bindingType)
        {
            var go = AsGameObject(sceneObject);
            if (go == null)
                return "{}";

            switch (bindingType)
            {
                case BindingType.Transform:
                    return JsonUtility.ToJson(new Transform2DPayload
                    {
                        Type = "Transform2D",
                        Position = new Vector2(go.transform.position.x, go.transform.position.y),
                        RotationZ = go.transform.eulerAngles.z
                    });

                case BindingType.Area:
                    return EncodeAreaPayload(go);

                default:
                    return "{}";
            }
        }

        private static string EncodeAreaPayload(GameObject go)
        {
            var area = go.GetComponent<AreaMarker>();
            if (area != null)
            {
                if (area.Shape == AreaShape.Polygon && area.Vertices.Count > 0)
                {
                    var worldVerts = area.GetWorldVertices();
                    var points = new List<Vector2>(worldVerts.Count);
                    foreach (var p in worldVerts)
                        points.Add(new Vector2(p.x, p.y));

                    return JsonUtility.ToJson(new Area2DPayload
                    {
                        Type = "Area2D",
                        Shape = "Polygon",
                        Points = points.ToArray()
                    });
                }

                return JsonUtility.ToJson(new Area2DPayload
                {
                    Type = "Area2D",
                    Shape = "Box",
                    Center = new Vector2(go.transform.position.x, go.transform.position.y),
                    Size = new Vector2(area.BoxSize.x, area.BoxSize.y)
                });
            }

            return JsonUtility.ToJson(new Area2DPayload
            {
                Type = "Area2D",
                Shape = "PointFallback",
                Center = new Vector2(go.transform.position.x, go.transform.position.y),
                Size = Vector2.zero
            });
        }

        private static GameObject? AsGameObject(object sceneObject)
        {
            if (sceneObject is GameObject go) return go;
            if (sceneObject is Component component) return component.gameObject;
            return null;
        }

        [Serializable]
        private sealed class Transform2DPayload
        {
            public string Type = "Transform2D";
            public Vector2 Position;
            public float RotationZ;
        }

        [Serializable]
        private sealed class Area2DPayload
        {
            public string Type = "Area2D";
            public string Shape = "Box";
            public Vector2 Center;
            public Vector2 Size;
            public Vector2[] Points = Array.Empty<Vector2>();
        }
    }
}
