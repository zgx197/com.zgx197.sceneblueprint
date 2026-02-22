#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Core;
using SceneBlueprint.Contract;

namespace SceneBlueprint.SpatialAbstraction.Defaults
{
    /// <summary>
    /// 默认的作用域键生成策略。
    /// 键格式：nodeId/bindingKey —— 每个节点实例拥有独立作用域，彻底避免同名 key 冲突。
    /// </summary>
    public sealed class DefaultBindingScopePolicy : IBindingScopePolicy
    {
        public string BuildScopedKey(string nodeId, string bindingKey)
        {
            var nid = string.IsNullOrEmpty(nodeId) ? "__unknown__" : nodeId;
            var bk = string.IsNullOrEmpty(bindingKey) ? "__empty__" : bindingKey;
            return nid + "/" + bk;
        }
    }

    /// <summary>
    /// 默认的空实现放置策略。
    /// 用于 C1 阶段占位，后续由具体 Adapter 替换。
    /// </summary>
    public sealed class NullScenePlacementPolicy : IScenePlacementPolicy
    {
        public bool TryGetPlacement(in ScenePlacementRequest request, out ScenePlacementResult result)
        {
            _ = request;
            result = ScenePlacementResult.Failure;
            return false;
        }
    }

    /// <summary>
    /// 默认的内存身份服务占位实现。
    /// 用于 C1 阶段快速验证链路，稳定性能力在 C2 阶段增强。
    /// </summary>
    public sealed class InMemorySceneObjectIdentityService : ISceneObjectIdentityService
    {
        private readonly Dictionary<string, object> _idToObject = new();
        private readonly Dictionary<object, string> _objectToId = new(ReferenceEqualityComparer.Instance);

        public string GetOrCreateStableId(object sceneObject)
        {
            if (sceneObject == null)
                throw new ArgumentNullException(nameof(sceneObject));

            if (_objectToId.TryGetValue(sceneObject, out var existing))
                return existing;

            var id = Guid.NewGuid().ToString("N");
            _objectToId[sceneObject] = id;
            _idToObject[id] = sceneObject;
            return id;
        }

        public bool TryResolve(string stableId, out object? sceneObject)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                sceneObject = null;
                return false;
            }

            if (_idToObject.TryGetValue(stableId, out var obj))
            {
                sceneObject = obj;
                return true;
            }

            sceneObject = null;
            return false;
        }

        /// <summary>
        /// 参考相等比较器（避免对象重写 Equals 导致映射混淆）。
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }

    /// <summary>
    /// 默认的绑定编解码占位实现。
    /// </summary>
    public sealed class PassthroughSpatialBindingCodec : ISpatialBindingCodec
    {
        private readonly ISceneObjectIdentityService _identityService;

        public PassthroughSpatialBindingCodec(ISceneObjectIdentityService identityService)
        {
            _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        }

        public SceneBindingPayload Encode(object sceneObject, BindingType bindingType)
        {
            var id = _identityService.GetOrCreateStableId(sceneObject);
            return new SceneBindingPayload(id, bindingType, "{}", "Placeholder");
        }

        public bool TryDecode(in SceneBindingPayload payload, out object? sceneObject)
        {
            return _identityService.TryResolve(payload.StableObjectId, out sceneObject);
        }
    }
}
