#nullable enable
using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace SceneBlueprint.Editor.Knowledge
{
    /// <summary>
    /// 本地 HTTP Server，为 AI 工具（Windsurf/Cursor MCP）提供蓝图上下文和知识查询端点。
    /// 在 Unity Editor 主线程中通过 EditorApplication.update 轮询请求。
    /// </summary>
    public class KnowledgeServer : IDisposable
    {
        public const int DefaultPort = 18900;
        public const string Prefix = "http://localhost:{0}/";

        private HttpListener? _listener;
        private Thread? _listenerThread;
        private volatile bool _running;
        private readonly int _port;
        private readonly KnowledgeServerEndpoints _endpoints;

        public bool IsRunning => _running;
        public int Port => _port;
        public string BaseUrl => string.Format(Prefix, _port);

        public KnowledgeServer(KnowledgeServerEndpoints endpoints, int port = DefaultPort)
        {
            _endpoints = endpoints;
            _port = port;
        }

        // ══════════════════════════════════════
        //  生命周期
        // ══════════════════════════════════════

        public bool Start()
        {
            if (_running) return true;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(string.Format(Prefix, _port));
                _listener.Start();
                _running = true;

                _listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "KnowledgeServer"
                };
                _listenerThread.Start();

                UnityEngine.Debug.Log($"[KnowledgeServer] 已启动: {BaseUrl}");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[KnowledgeServer] 启动失败: {ex.Message}");
                _running = false;
                return false;
            }
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;

            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { /* ignore */ }

            _listenerThread = null;
            _listener = null;
            UnityEngine.Debug.Log("[KnowledgeServer] 已停止");
        }

        public void Dispose()
        {
            Stop();
        }

        // ══════════════════════════════════════
        //  请求处理循环
        // ══════════════════════════════════════

        private void ListenLoop()
        {
            while (_running && _listener != null && _listener.IsListening)
            {
                try
                {
                    var asyncResult = _listener.BeginGetContext(null, null);
                    // 等待请求，超时 500ms 后循环检查 _running
                    if (asyncResult.AsyncWaitHandle.WaitOne(500))
                    {
                        var context = _listener.EndGetContext(asyncResult);
                        HandleRequest(context);
                    }
                }
                catch (HttpListenerException)
                {
                    // Listener 被关闭
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        UnityEngine.Debug.LogWarning($"[KnowledgeServer] 请求处理异常: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // CORS 头
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            try
            {
                string path = request.Url?.AbsolutePath ?? "/";
                string? requestBody = null;

                if (request.HasEntityBody)
                {
                    using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
                    requestBody = reader.ReadToEnd();
                }

                string responseJson = _endpoints.HandleRequest(path, requestBody);
                WriteJsonResponse(response, 200, responseJson);
            }
            catch (Exception ex)
            {
                string errorJson = $"{{\"error\":\"{EscapeJson(ex.Message)}\"}}";
                WriteJsonResponse(response, 500, errorJson);
            }
        }

        private static void WriteJsonResponse(HttpListenerResponse response, int statusCode, string json)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private static string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }
    }
}
