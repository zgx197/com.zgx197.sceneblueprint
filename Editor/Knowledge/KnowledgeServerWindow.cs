#nullable enable
using SceneBlueprint.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Knowledge
{
    /// <summary>
    /// MCP Knowledge Server 管理窗口。
    /// 提供 Server 启停、端口配置、端点测试和状态监控。
    /// </summary>
    public class KnowledgeServerWindow : EditorWindow
    {
        private int _port = KnowledgeServer.DefaultPort;
        private bool _autoStart;
        private string _testEndpoint = "/health";
        private string _testBody = "";
        private string _testResult = "";
        private Vector2 _scrollPos;

        [MenuItem("SceneBlueprint/Knowledge/MCP Server 管理", priority = 210)]
        public static void Open()
        {
            var window = GetWindow<KnowledgeServerWindow>();
            window.titleContent = new GUIContent("MCP Server");
            window.minSize = new Vector2(360, 300);
            window.Show();
        }

        private void OnEnable()
        {
            _port = SceneBlueprintSettingsService.GetKnowledgeServerPort();
            _autoStart = SceneBlueprintSettingsService.ShouldAutoStartKnowledgeServer();
        }

        private void OnGUI()
        {
            var service = KnowledgeService.Instance;
            bool running = service.IsServerRunning;
            bool shouldSaveSettings = false;

            // ── 标题 ──
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("SceneBlueprint MCP Knowledge Server", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── 状态 ──
            EditorGUILayout.BeginHorizontal();
            {
                var prevColor = GUI.color;
                GUI.color = running ? new Color(0.3f, 0.9f, 0.3f) : Color.gray;
                GUILayout.Label(running ? "● 运行中" : "○ 已停止", EditorStyles.boldLabel, GUILayout.Width(70));
                GUI.color = prevColor;

                if (running)
                    GUILayout.Label($"http://localhost:{_port}/", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── 控制 ──
            EditorGUILayout.BeginHorizontal();
            {
                using (new EditorGUI.DisabledScope(running))
                {
                    EditorGUI.BeginChangeCheck();
                    _port = EditorGUILayout.IntField("端口", _port);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _port = Mathf.Max(0, _port);
                        shouldSaveSettings = true;
                    }
                }

                if (running)
                {
                    if (GUILayout.Button("停止", GUILayout.Width(60)))
                        service.StopServer();
                }
                else
                {
                    if (GUILayout.Button("启动", GUILayout.Width(60)))
                    {
                        service.LoadManifest();
                        service.StartServer(_port);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            _autoStart = EditorGUILayout.ToggleLeft("打开蓝图窗口时自动启动 MCP", _autoStart);
            if (EditorGUI.EndChangeCheck())
            {
                shouldSaveSettings = true;
            }

            if (shouldSaveSettings)
            {
                SceneBlueprintSettingsService.SaveKnowledgeServerSettings(_autoStart, _port);
            }

            EditorGUILayout.Space(8);

            // ── 端点列表 ──
            EditorGUILayout.LabelField("可用端点", EditorStyles.boldLabel);
            var endpoints = new[]
            {
                ("/health", "GET", "健康检查"),
                ("/context", "GET", "获取当前蓝图上下文"),
                ("/search", "POST", "搜索知识文档 {\"query\":\"...\",\"role\":\"designer\"}"),
                ("/doc", "POST", "获取指定文档 {\"title\":\"...\"}"),
                ("/action-doc", "POST", "获取 Action 文档 {\"actionTypeId\":\"Spawn.Wave\"}"),
                ("/layers", "GET", "列出所有知识层级"),
                ("/docs", "POST", "列出指定角色的文档 {\"role\":\"designer\"}"),
                ("/prompt-config", "GET", "获取 Prompt 配置"),
                ("/assemble", "POST", "组装完整 Prompt {\"question\":\"...\",\"role\":\"designer\"}"),
            };

            EditorGUI.indentLevel++;
            foreach (var (path, method, desc) in endpoints)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"{method}", EditorStyles.miniLabel, GUILayout.Width(35));
                GUILayout.Label(path, EditorStyles.miniLabel, GUILayout.Width(100));
                GUILayout.Label(desc, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8);

            // ── 端点测试 ──
            EditorGUILayout.LabelField("端点测试", EditorStyles.boldLabel);
            _testEndpoint = EditorGUILayout.TextField("路径", _testEndpoint);
            _testBody = EditorGUILayout.TextField("Body (JSON)", _testBody);

            using (new EditorGUI.DisabledScope(!running))
            {
                if (GUILayout.Button("发送请求"))
                {
                    TestEndpoint();
                }
            }

            if (!string.IsNullOrEmpty(_testResult))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("响应：", EditorStyles.miniLabel);
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(120));
                EditorGUILayout.TextArea(_testResult, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void TestEndpoint()
        {
            try
            {
                var client = new System.Net.WebClient();
                client.Headers.Add("Content-Type", "application/json");
                string url = $"http://localhost:{_port}{_testEndpoint}";

                if (string.IsNullOrEmpty(_testBody))
                {
                    _testResult = client.DownloadString(url);
                }
                else
                {
                    _testResult = client.UploadString(url, _testBody);
                }
            }
            catch (System.Exception ex)
            {
                _testResult = $"错误: {ex.Message}";
            }
        }
    }
}
