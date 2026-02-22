#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Test
{
    /// <summary>
    /// 简易第三人称玩家控制器——用于蓝图运行时测试场景。
    /// <para>
    /// 操作：WASD 移动，鼠标右键拖拽旋转视角，滚轮调整距离。
    /// 需要挂在带 CharacterController 的 GameObject 上。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimplePlayerController : MonoBehaviour
    {
        [Header("移动")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _gravity = -15f;
        [SerializeField] private float _rotateSmooth = 10f;

        [Header("相机")]
        [SerializeField] private float _cameraSensitivity = 3f;
        [SerializeField] private float _cameraDistance = 8f;
        [SerializeField] private float _cameraMinDistance = 2f;
        [SerializeField] private float _cameraMaxDistance = 20f;
        [SerializeField] private float _cameraHeight = 3f;
        [SerializeField] private float _cameraPitchMin = -30f;
        [SerializeField] private float _cameraPitchMax = 60f;

        private CharacterController _cc = null!;
        private Camera _cam = null!;
        private float _yaw;
        private float _pitch = 25f;
        private float _verticalVelocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            // 查找或创建相机
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("RuntimeTestCamera");
                _cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                camGo.tag = "MainCamera";
            }

            _yaw = transform.eulerAngles.y;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            HandleCameraInput();
            HandleMovement();
            UpdateCameraPosition();
        }

        /// <summary>鼠标右键拖拽旋转 + 滚轮缩放</summary>
        private void HandleCameraInput()
        {
            // 右键拖拽旋转视角
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * _cameraSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * _cameraSensitivity;
                _pitch = Mathf.Clamp(_pitch, _cameraPitchMin, _cameraPitchMax);
            }

            // 滚轮缩放距离
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _cameraDistance -= scroll * 5f;
                _cameraDistance = Mathf.Clamp(_cameraDistance, _cameraMinDistance, _cameraMaxDistance);
            }
        }

        /// <summary>WASD 移动（相对相机朝向）</summary>
        private void HandleMovement()
        {
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));

            if (input.sqrMagnitude > 0.01f)
            {
                input.Normalize();

                // 基于相机 yaw 方向移动
                var forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                var right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
                var moveDir = forward * input.z + right * input.x;

                // 平滑旋转角色朝向
                var targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                    Time.deltaTime * _rotateSmooth);

                _cc.Move(moveDir * (_moveSpeed * Time.deltaTime));
            }

            // 简单重力
            if (_cc.isGrounded)
            {
                _verticalVelocity = -1f;
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }
            _cc.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));
        }

        /// <summary>更新第三人称相机位置</summary>
        private void UpdateCameraPosition()
        {
            if (_cam == null) return;

            var pivotPos = transform.position + Vector3.up * _cameraHeight;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0);
            var offset = rotation * new Vector3(0, 0, -_cameraDistance);

            _cam.transform.position = pivotPos + offset;
            _cam.transform.LookAt(pivotPos);
        }
    }
}
