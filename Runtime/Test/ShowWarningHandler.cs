#nullable enable
using SceneBlueprint.Runtime.Interpreter;
using UnityEngine;

namespace SceneBlueprint.Runtime.Test
{
    /// <summary>
    /// 屏幕警告处理器——实现 IShowWarningHandler，在测试场景中用 OnGUI 绘制警告文字。
    /// <para>
    /// 实现方式：屏幕中央绘制带描边的大号文字，支持淡入淡出动画。
    /// 不同样式（Warning/Info/Boss）使用不同颜色。
    /// </para>
    /// </summary>
    public class ShowWarningHandler : MonoBehaviour, IShowWarningHandler
    {
        // 显示状态
        private bool _showing;
        private string _text = "";
        private float _duration;
        private float _fontSize;
        private float _elapsed;
        private Color _textColor;

        // 淡入淡出时间（秒）
        private const float FadeInTime = 0.3f;
        private const float FadeOutTime = 0.5f;

        // 蓝图调试暂停状态
        private bool _pausedByBlueprint;

        // 缓存的 GUIStyle（避免每帧创建）
        private GUIStyle? _style;
        private GUIStyle? _shadowStyle;

        public void OnShow(ShowWarningData data)
        {
            _text = data.Text;
            _duration = data.Duration;
            _fontSize = data.FontSize;
            _elapsed = 0f;
            _showing = true;

            // 根据样式选择颜色
            _textColor = data.Style switch
            {
                "Boss" => new Color(1f, 0.2f, 0.1f),    // 红色
                "Warning" => new Color(1f, 0.8f, 0.1f),  // 黄色
                "Info" => new Color(0.3f, 0.9f, 1f),     // 蓝色
                _ => Color.white
            };

            // 重置样式缓存（字号可能变了）
            _style = null;
            _shadowStyle = null;

            Debug.Log($"[ShowWarningHandler] 显示警告: \"{_text}\" (样式={data.Style}, 时长={_duration}s)");
        }

        public void OnHide()
        {
            _showing = false;
        }

        public void OnBlueprintPaused()  => _pausedByBlueprint = true;
        public void OnBlueprintResumed() => _pausedByBlueprint = false;

        private void Update()
        {
            if (!_showing) return;
            if (_pausedByBlueprint) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                _showing = false;
            }
        }

        private void OnGUI()
        {
            if (!_showing) return;

            // 懒初始化样式
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(_fontSize),
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = false
                };

                _shadowStyle = new GUIStyle(_style);
            }

            // 计算透明度（淡入淡出）
            float alpha;
            if (_elapsed < FadeInTime)
            {
                // 淡入
                alpha = _elapsed / FadeInTime;
            }
            else if (_elapsed > _duration - FadeOutTime)
            {
                // 淡出
                alpha = (_duration - _elapsed) / FadeOutTime;
            }
            else
            {
                alpha = 1f;
            }
            alpha = Mathf.Clamp01(alpha);

            // 绘制区域：屏幕中央偏上
            var rect = new Rect(0, Screen.height * 0.25f, Screen.width, _fontSize * 1.5f);

            // 描边（黑色阴影，偏移 2px）
            _shadowStyle!.normal.textColor = new Color(0, 0, 0, alpha * 0.8f);
            var shadowRect = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
            GUI.Label(shadowRect, _text, _shadowStyle);

            // 正文
            _style!.normal.textColor = new Color(_textColor.r, _textColor.g, _textColor.b, alpha);
            GUI.Label(rect, _text, _style);
        }
    }
}
