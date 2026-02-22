#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.SpatialAbstraction;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Adapters.Unity3D
{
    /// <summary>
    /// Unity3D 适配器服务入口。
    /// C3 阶段承接 SceneView 放置与场景绑定编码能力。
    /// </summary>
    public static class Unity3DAdapterServices
    {
        public const string AdapterType = "Unity3D";

        private static readonly ISceneObjectIdentityService IdentityServiceInstance = new Unity3DSceneObjectIdentityService();
        private static readonly ISpatialBindingCodec BindingCodecInstance = new Unity3DSpatialBindingCodec(IdentityServiceInstance);

        public static ISpatialBindingCodec BindingCodec => BindingCodecInstance;

        public static bool TryGetSceneViewPlacement(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            _ = sceneView;
            return Unity3DScenePlacementUtility.TryRaycastGround(mousePos, out worldPos);
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

    internal static class Unity3DScenePlacementUtility
    {
        /// <summary>
        /// 从鼠标位置射线投射到场景几何体，获取世界坐标。
        /// 策略：Physics.Raycast -> PickGameObject(Renderer顶面) -> Y=0 平面回退。
        /// </summary>
        public static bool TryRaycastGround(Vector2 mousePos, out Vector3 worldPos)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mousePos);

            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                worldPos = hit.point;
                return true;
            }

            var pickedGO = HandleUtility.PickGameObject(mousePos, false);
            if (pickedGO != null)
            {
                var renderer = pickedGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    float surfaceY = renderer.bounds.max.y;
                    var surfacePlane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
                    if (surfacePlane.Raycast(ray, out float surfaceEnter))
                    {
                        worldPos = ray.GetPoint(surfaceEnter);
                        return true;
                    }
                }
            }

            var plane = new Plane(Vector3.up, Vector3.zero);
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
    /// Unity3D 场景对象稳定标识实现。
    /// </summary>
    public sealed class Unity3DSceneObjectIdentityService : ISceneObjectIdentityService
    {
        private const string MarkerPrefix = "marker:";
        private const string GlobalObjectPrefix = "global:";
        private const string InstancePrefix = "instance:";

        public string GetOrCreateStableId(object sceneObject)
        {
            if (sceneObject == null)
                throw new System.ArgumentNullException(nameof(sceneObject));

            var go = AsGameObject(sceneObject);
            if (go == null)
                throw new System.ArgumentException("sceneObject 必须是 GameObject 或 Component", nameof(sceneObject));

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

            if (stableId.StartsWith(MarkerPrefix, System.StringComparison.Ordinal))
            {
                string markerId = stableId.Substring(MarkerPrefix.Length);
                foreach (var marker in Object.FindObjectsOfType<SceneMarker>())
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

            if (stableId.StartsWith(GlobalObjectPrefix, System.StringComparison.Ordinal))
            {
                // GlobalObjectId 的反查 API 在不同 Unity 版本上差异较大，
                // C3 阶段先保守返回 false；当前导出链路不依赖该反查能力。
                sceneObject = null;
                return false;
            }

            if (stableId.StartsWith(InstancePrefix, System.StringComparison.Ordinal))
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
    /// Unity3D 场景绑定编解码实现（C3 先输出占位空间载荷）。
    /// </summary>
    public sealed class Unity3DSpatialBindingCodec : ISpatialBindingCodec
    {
        private readonly ISceneObjectIdentityService _identityService;

        public Unity3DSpatialBindingCodec(ISceneObjectIdentityService identityService)
        {
            _identityService = identityService ?? throw new System.ArgumentNullException(nameof(identityService));
        }

        public SceneBindingPayload Encode(object sceneObject, BindingType bindingType)
        {
            if (sceneObject == null)
                throw new System.ArgumentNullException(nameof(sceneObject));

            var id = _identityService.GetOrCreateStableId(sceneObject);
            string payloadJson = BuildSpatialPayload(sceneObject, bindingType);
            return new SceneBindingPayload(id, bindingType, payloadJson, Unity3DAdapterServices.AdapterType);
        }

        private static string BuildSpatialPayload(object sceneObject, BindingType bindingType)
        {
            var go = AsGameObject(sceneObject);
            if (go == null) return "{}";

            var t = go.transform;

            switch (bindingType)
            {
                case BindingType.Area:
                    var area = go.GetComponent<AreaMarker>();
                    if (area != null)
                    {
                        if (area.Shape == AreaShape.Box)
                        {
                            return JsonUtility.ToJson(new AreaBoxPayload
                            {
                                shape = "Box",
                                position = t.position,
                                rotation = t.rotation.eulerAngles,
                                boxSize = area.BoxSize,
                                height = area.Height
                            });
                        }
                        else
                        {
                            var worldVerts = area.GetWorldVertices();
                            var verts = new Vector3[worldVerts.Count];
                            for (int i = 0; i < worldVerts.Count; i++)
                                verts[i] = worldVerts[i];
                            return JsonUtility.ToJson(new AreaPolygonPayload
                            {
                                shape = "Polygon",
                                position = t.position,
                                rotation = t.rotation.eulerAngles,
                                vertices = verts,
                                height = area.Height
                            });
                        }
                    }
                    break;

                case BindingType.Transform:
                    return JsonUtility.ToJson(new TransformPayload
                    {
                        position = t.position,
                        rotation = t.rotation.eulerAngles,
                        scale = t.localScale
                    });
            }

            return "{}";
        }

        private static GameObject? AsGameObject(object sceneObject)
        {
            if (sceneObject is GameObject go) return go;
            if (sceneObject is Component component) return component.gameObject;
            return null;
        }

        // ── 空间载荷数据结构（JsonUtility 序列化）──

        [System.Serializable]
        private struct TransformPayload
        {
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 scale;
        }

        [System.Serializable]
        private struct AreaBoxPayload
        {
            public string shape;
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 boxSize;
            public float height;
        }

        [System.Serializable]
        private struct AreaPolygonPayload
        {
            public string shape;
            public Vector3 position;
            public Vector3 rotation;
            public Vector3[] vertices;
            public float height;
        }

        public bool TryDecode(in SceneBindingPayload payload, out object? sceneObject)
        {
            return _identityService.TryResolve(payload.StableObjectId, out sceneObject);
        }
    }
}
