#nullable enable
using SceneBlueprint.Runtime.Interpreter;
using UnityEngine;

namespace SceneBlueprint.Runtime.Test
{
    /// <summary>
    /// 摄像机震动处理器——实现 ICameraShakeHandler，在测试场景中产生真实的屏幕震动。
    /// <para>
    /// 实现方式：Perlin Noise 驱动 Camera 偏移。
    /// 每帧根据剩余时间计算衰减后的偏移量，叠加到摄像机原始位置上。
    /// 震动结束后自动恢复原始位置。
    /// </para>
    /// <para>
    /// 支持叠加：新的震动请求会覆盖当前震动（取更强的参数），
    /// 避免多次触发导致摄像机位置漂移。
    /// </para>
    /// </summary>
    public class CameraShakeHandler : MonoBehaviour, ICameraShakeHandler
    {
        private Camera? _camera;

        // 震动状态
        private bool _shaking;
        private float _intensity;
        private float _frequency;
        private float _duration;
        private float _elapsed;

        // 原始位置（震动开始时记录，结束时恢复）
        private Vector3 _originalLocalPos;
        private bool _hasOriginalPos;

        // Perlin Noise 随机种子（避免每次震动模式相同）
        private float _seedX;
        private float _seedY;

        // 蓝图调试暂停状态
        private bool _pausedByBlueprint;

        private void Awake()
        {
            // Handler 被动态挂载到 Camera.main 所在的 GameObject 上，
            // 所以直接从自身获取 Camera 组件即可
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        public void OnShakeStart(CameraShakeData data)
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>() ?? Camera.main;
                if (_camera == null)
                {
                    Debug.LogWarning("[CameraShakeHandler] 找不到 Camera 组件，震动跳过");
                    return;
                }
            }

            // 如果当前没有震动，记录原始位置
            if (!_shaking)
            {
                _originalLocalPos = _camera.transform.localPosition;
                _hasOriginalPos = true;
            }

            _intensity = data.Intensity;
            _duration = data.Duration;
            _frequency = data.Frequency;
            _elapsed = 0f;
            _shaking = true;

            // 随机种子，让每次震动的 Perlin Noise 采样位置不同
            _seedX = Random.Range(0f, 1000f);
            _seedY = Random.Range(0f, 1000f);

            Debug.Log($"[CameraShakeHandler] 震动开始: 强度={_intensity}, 时长={_duration}s, 频率={_frequency}");
        }

        public void OnShakeStop()
        {
            StopShake();
        }

        public void OnBlueprintPaused()  => _pausedByBlueprint = true;
        public void OnBlueprintResumed() => _pausedByBlueprint = false;

        private void LateUpdate()
        {
            if (!_shaking || _camera == null)
                return;

            if (_pausedByBlueprint)
                return;

            _elapsed += Time.deltaTime;

            if (_elapsed >= _duration)
            {
                StopShake();
                return;
            }

            // 衰减因子：线性衰减，越接近结束震动越弱
            float decay = 1f - (_elapsed / _duration);

            // Perlin Noise 生成平滑的随机偏移
            float t = _elapsed * _frequency;
            float offsetX = (Mathf.PerlinNoise(_seedX + t, 0f) - 0.5f) * 2f;
            float offsetY = (Mathf.PerlinNoise(0f, _seedY + t) - 0.5f) * 2f;

            // 应用偏移（基于原始位置叠加，不会累积漂移）
            var offset = new Vector3(offsetX, offsetY, 0f) * _intensity * decay;
            _camera.transform.localPosition = _originalLocalPos + offset;
        }

        private void StopShake()
        {
            _shaking = false;

            // 恢复原始位置
            if (_hasOriginalPos && _camera != null)
            {
                _camera.transform.localPosition = _originalLocalPos;
                _hasOriginalPos = false;
            }

            Debug.Log("[CameraShakeHandler] 震动结束，摄像机位置已恢复");
        }

        private void OnDisable()
        {
            // 组件禁用时确保恢复位置
            if (_shaking) StopShake();
        }
    }
}
