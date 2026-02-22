#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Contract;

namespace SceneBlueprint.SpatialAbstraction
{
    /// <summary>
    /// 与引擎无关的三维向量。
    /// 使用基础值类型，避免 Domain/Application 依赖 UnityEngine。
    /// </summary>
    public readonly struct SpatialVector3
    {
        public SpatialVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static SpatialVector3 Zero => new(0f, 0f, 0f);
    }

    /// <summary>
    /// 与引擎无关的四元数。
    /// </summary>
    public readonly struct SpatialQuaternion
    {
        public SpatialQuaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }

        public static SpatialQuaternion Identity => new(0f, 0f, 0f, 1f);
    }

    /// <summary>
    /// 场景放置请求。
    /// </summary>
    public readonly struct ScenePlacementRequest
    {
        public ScenePlacementRequest(string surfaceHint, float snapSize, string coordinateMode)
        {
            SurfaceHint = surfaceHint ?? "Any";
            SnapSize = snapSize;
            CoordinateMode = coordinateMode ?? "XZ";
        }

        public string SurfaceHint { get; }
        public float SnapSize { get; }
        public string CoordinateMode { get; }

        public static ScenePlacementRequest Default => new("Any", 0f, "XZ");
    }

    /// <summary>
    /// 场景放置结果。
    /// </summary>
    public readonly struct ScenePlacementResult
    {
        public ScenePlacementResult(bool success, SpatialVector3 position, SpatialQuaternion rotation, string spaceTag)
        {
            Success = success;
            Position = position;
            Rotation = rotation;
            SpaceTag = spaceTag ?? "";
        }

        public bool Success { get; }
        public SpatialVector3 Position { get; }
        public SpatialQuaternion Rotation { get; }
        public string SpaceTag { get; }

        public static ScenePlacementResult Failure => new(false, SpatialVector3.Zero, SpatialQuaternion.Identity, "");
    }

    /// <summary>
    /// 场景绑定的抽象载荷。
    /// </summary>
    public readonly struct SceneBindingPayload
    {
        public SceneBindingPayload(
            string stableObjectId,
            BindingType bindingType,
            string serializedSpatialData,
            string adapterType)
        {
            StableObjectId = stableObjectId ?? "";
            BindingType = bindingType;
            SerializedSpatialData = serializedSpatialData ?? "";
            AdapterType = adapterType ?? "";
        }

        public string StableObjectId { get; }
        public BindingType BindingType { get; }
        public string SerializedSpatialData { get; }
        public string AdapterType { get; }
    }
}
