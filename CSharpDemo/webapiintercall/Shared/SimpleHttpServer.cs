using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
    /// <summary>
    /// 基于 HttpListener 的极简自托管 HTTP 服务，无 OWIN/Kestrel 依赖。
    /// 提供通用 API：POST /api/message 收消息，GET /api/peers 返回本节点名。
    /// </summary>
    public class SimpleHttpServer
    {
        private readonly HttpListener _listener;
        private readonly string _myName;
        private readonly Action<MessageDto> _onMessage;
        private CancellationTokenSource _cts;

        public SimpleHttpServer(string listenPrefix, string myName, Action<MessageDto> onMessage)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(listenPrefix);
            _myName = myName;
            _onMessage = onMessage ?? (m => { });
        }

        public void Start()
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            Task.Run(() => AcceptLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener.Stop(); } catch { }
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleRequest(context), ct);
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;
            var path = req.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            var method = req.HttpMethod;

            try
            {
                if (path.EndsWith("/api/message", StringComparison.OrdinalIgnoreCase) && method == "POST")
                {
                    string body;
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                        body = reader.ReadToEnd();
                    var msg = JsonHelper.Deserialize(body);
                    if (msg != null)
                        _onMessage(msg);
                    WriteJson(resp, 200, "{\"ok\":true}");
                    return;
                }
                if (path.EndsWith("/api/peers", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    WriteJson(resp, 200, "{\"name\":\"" + EscapeJson(_myName) + "\"}");
                    return;
                }
                resp.StatusCode = 404;
                WriteText(resp, "Not Found");
            }
            catch (Exception ex)
            {
                resp.StatusCode = 500;
                WriteText(resp, ex.Message);
            }
            finally
            {
                resp.Close();
            }
        }

        private static void WriteJson(HttpListenerResponse resp, int statusCode, string json)
        {
            resp.StatusCode = statusCode;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentEncoding = Encoding.UTF8;
            var bytes = Encoding.UTF8.GetBytes(json);
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteText(HttpListenerResponse resp, string text)
        {
            resp.ContentType = "text/plain; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(text ?? "");
            resp.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
