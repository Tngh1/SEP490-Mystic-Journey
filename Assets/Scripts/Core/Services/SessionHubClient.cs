using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MysticJourney.API.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MysticJourney.Core.Services
{
    /// <summary>
    /// Nối tới hub SignalR của backend (/hubs/game) để nhận realtime sự kiện "SessionOverridden"
    /// — tài khoản này vừa được đăng nhập ở máy khác. Nhận được là đăng xuất ngay về MainMenu
    /// kèm popup lý do, thay vì phải chờ tới request kế tiếp mới bị 401.
    ///
    /// Đây CHỈ là kênh thông báo nhanh, KHÔNG phải nguồn sự thật của việc kick: mất mạng, hub
    /// chết, hay app chưa nối kịp thì người chơi vẫn bị đá đúng ở request kế tiếp qua nhánh
    /// 401/SESSION_OVERRIDDEN trong ApiClient. Vì vậy mọi lỗi ở đây đều chỉ log rồi thử lại,
    /// không bao giờ tự đăng xuất người chơi.
    ///
    /// Nói giao thức SignalR bằng WebSocket thuần thay vì dùng package
    /// Microsoft.AspNetCore.SignalR.Client: phía nhận chỉ cần handshake + tách record + trả ping,
    /// còn package kia kéo theo cả cây Microsoft.Extensions.* vào Unity.
    /// </summary>
    public class SessionHubClient : MonoBehaviour
    {
        // SignalR phân tách các message trong cùng một khung WebSocket bằng ký tự này (0x1E),
        // nên một lần Receive có thể chứa nhiều message hoặc nửa message.
        private const char RecordSeparator = '\x1E';

        private const string HandshakeRequest = "{\"protocol\":\"json\",\"version\":1}\x1E";
        private const string PingMessage = "{\"type\":6}\x1E";

        // Server đóng kết nối nếu client im lặng quá ClientTimeoutInterval (mặc định 30s), nên
        // client phải tự gửi ping. 15s là nửa khoảng đó — mất một ping vẫn chưa bị đóng.
        private const float PingIntervalSeconds = 15f;

        // Nhịp đối chiếu "đang có token / đang nối" để tự nối lại sau khi rớt mạng.
        private const float ReconcileIntervalSeconds = 5f;

        // Trần của backoff: hub chết cả buổi thì vẫn thử lại mỗi phút — đủ để tự hồi khi server
        // bật lại, mà không biến console thành log rác.
        private const float MaxReconnectDelaySeconds = 60f;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private float _lastPingTime;

        // Backoff cho việc nối lại. Chỉ đọc/ghi trên main thread (Reconcile + hàng đợi trong
        // Update), nên không cần lock dù nguồn báo lỗi là task chạy trên threadpool.
        private int _consecutiveFailures;
        private float _nextAttemptTime;
        private string _lastFailureMessage;

        // Task nhận chạy trên threadpool, còn SessionService.Logout gọi SceneManager.LoadScene —
        // Unity API chỉ được đụng từ main thread, nên phải xếp hàng rồi chạy trong Update.
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var go = new GameObject("SessionHubClient");
            DontDestroyOnLoad(go);
            go.AddComponent<SessionHubClient>();
        }

        private void Start()
        {
            InvokeRepeating(nameof(Reconcile), 1f, ReconcileIntervalSeconds);
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
                action?.Invoke();
        }

        private void OnDestroy() => Disconnect();
        private void OnApplicationQuit() => Disconnect();

        // Một vòng đối chiếu trạng thái thay vì bắt sự kiện login/logout ở từng chỗ: có token mà
        // chưa nối thì nối, mất token mà còn nối thì ngắt. Nhờ vậy đăng nhập, đăng xuất, và rớt
        // mạng giữa game đều tự xử lý bằng cùng một đoạn code.
        private void Reconcile()
        {
            bool hasToken = ApiClient.Instance != null && ApiClient.Instance.HasToken();
            bool isOpen = _socket != null && _socket.State == WebSocketState.Open;

            if (!hasToken)
            {
                if (_socket != null) Disconnect();
                // Đăng xuất rồi đăng nhập lại phải nối ngay, không bắt chờ hết backoff của
                // phiên trước.
                ResetBackoff();
                return;
            }

            if (_socket == null)
            {
                if (Time.unscaledTime >= _nextAttemptTime) Connect();
                return;
            }

            if (isOpen)
            {
                if (Time.unscaledTime - _lastPingTime >= PingIntervalSeconds)
                {
                    _lastPingTime = Time.unscaledTime;
                    _ = SendAsync(PingMessage);
                }
                return;
            }

            // Đang ở Connecting thì để yên; các state còn lại (Closed/Aborted/CloseReceived) là
            // kết nối đã chết. Đi qua OnDisconnected thay vì Disconnect() trực tiếp để mọi
            // đường mất kết nối đều cộng vào cùng một bộ đếm backoff.
            if (_socket.State != WebSocketState.Connecting)
                OnDisconnected(_socket, $"Socket ở trạng thái {_socket.State}.");
        }

        private void Connect()
        {
            string token = ApiClient.Instance.GetToken();
            if (string.IsNullOrEmpty(token)) return;

            // WebSocket không gửi được header Authorization, nên token đi qua query string —
            // Program.cs chỉ nhận query token ở đúng path /hubs/game.
            string wsBase = ApiConfig.BaseUrl
                .Replace("https://", "wss://")
                .Replace("http://", "ws://");
            var uri = new Uri($"{wsBase}{ApiConfig.GameHub}?access_token={Uri.EscapeDataString(token)}");

            _socket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            _lastPingTime = Time.unscaledTime;

            _ = RunAsync(_socket, _cts.Token, uri);
        }

        private async Task RunAsync(ClientWebSocket socket, CancellationToken ct, Uri uri)
        {
            try
            {
                await socket.ConnectAsync(uri, ct);
                await SendRawAsync(socket, HandshakeRequest, ct);

                // Xoá backoff chỉ khi đã nối được thật: nếu xoá ngay lúc bắt đầu thử thì một hub
                // liên tục từ chối sẽ không bao giờ giãn nhịp ra.
                _mainThreadQueue.Enqueue(() => OnConnected(socket));

                await ReceiveLoopAsync(socket, ct);

                // Vòng nhận thoát mà không có exception (server đóng đẹp) cũng là mất kết nối.
                if (!ct.IsCancellationRequested)
                    _mainThreadQueue.Enqueue(() => OnDisconnected(socket, "Server đã đóng kết nối."));
            }
            catch (OperationCanceledException)
            {
                // Chủ động ngắt khi logout / thoát game.
            }
            catch (Exception ex)
            {
                // Báo lỗi phải về main thread: backoff được Reconcile đọc, và Debug.LogWarning từ
                // threadpool thì mất stack trace của Unity.
                string message = ex.Message;
                _mainThreadQueue.Enqueue(() => OnDisconnected(socket, message));
            }
        }

        // Cả hai hàm dưới đều nhận socket đã gây ra sự kiện để bỏ qua báo cáo của socket cũ:
        // logout-rồi-login tạo socket mới, mà task của socket cũ có thể báo lỗi muộn sau đó.
        private void OnConnected(ClientWebSocket socket)
        {
            if (socket != _socket) return;

            if (_consecutiveFailures > 0)
                Debug.Log($"[SessionHubClient] Đã nối lại được hub sau {_consecutiveFailures} lần thử.");

            ResetBackoff();
        }

        private void OnDisconnected(ClientWebSocket socket, string message)
        {
            if (socket != _socket) return;

            _consecutiveFailures++;

            // 5s, 10s, 20s, 40s, rồi chốt ở 60s.
            float delay = Mathf.Min(
                ReconcileIntervalSeconds * Mathf.Pow(2f, _consecutiveFailures - 1),
                MaxReconnectDelaySeconds);
            _nextAttemptTime = Time.unscaledTime + delay;

            // Server chết cả buổi thì lý do không đổi — log một lần cho mỗi lần "đứt" thay vì mỗi
            // nhịp Reconcile. Hub chỉ là kênh thông báo nhanh nên im lặng ở đây là chấp nhận được:
            // nhánh 401/SESSION_OVERRIDDEN trong ApiClient vẫn kick đúng.
            if (!string.Equals(_lastFailureMessage, message, StringComparison.Ordinal))
            {
                _lastFailureMessage = message;
                Debug.LogWarning($"[SessionHubClient] Hub connection ended: {message} Thử lại sau {delay:0}s.");
            }

            Disconnect();
        }

        private void ResetBackoff()
        {
            _consecutiveFailures = 0;
            _nextAttemptTime = 0f;
            _lastFailureMessage = null;
        }

        private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[4096];
            var pending = new StringBuilder();

            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    return;
                }

                pending.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                // Một khung có thể chứa nhiều message, và message cuối có thể còn dở — chỉ xử lý
                // phần đã có dấu phân tách, phần dư giữ lại cho khung sau.
                var chunks = pending.ToString().Split(RecordSeparator);
                pending.Clear();
                pending.Append(chunks[chunks.Length - 1]);

                for (int i = 0; i < chunks.Length - 1; i++)
                    HandleMessage(chunks[i]);
            }
        }

        private void HandleMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            JObject message;
            try
            {
                message = JObject.Parse(json);
            }
            catch (Exception)
            {
                // Dữ liệu từ ngoài vào: hỏng định dạng thì bỏ qua, không để nó làm chết vòng nhận.
                Debug.LogWarning("[SessionHubClient] Bỏ qua message không phải JSON hợp lệ.");
                return;
            }

            // Handshake lỗi: server trả {"error":"..."} thay vì {} rỗng.
            var handshakeError = message["error"];
            if (handshakeError != null)
            {
                Debug.LogWarning($"[SessionHubClient] Handshake bị từ chối: {handshakeError}");
                return;
            }

            int type = message["type"]?.Value<int>() ?? 0;

            // 6 = Ping của server, 7 = Close. Còn {} (handshake OK) không có "type" → type 0.
            if (type == 7)
            {
                Debug.LogWarning($"[SessionHubClient] Server đóng kết nối: {message["error"]}");
                return;
            }
            if (type != 1) return; // Chỉ quan tâm Invocation.

            if (!string.Equals(message["target"]?.Value<string>(), "SessionOverridden", StringComparison.Ordinal))
                return;

            var payload = message["arguments"]?.First;
            int accountId = payload?["accountId"]?.Value<int>() ?? 0;
            string newSessionId = payload?["newSessionId"]?.Value<string>() ?? string.Empty;

            // Chặn trường hợp nhận được thông báo của tài khoản khác: group của hub vốn theo từng
            // tài khoản nên không nên xảy ra, nhưng đá oan người chơi thì rất khó lần ra. Chỉ chặn
            // khi biết chắc accountId lệch — không biết thì vẫn đá (giống lối fail-open ở BE).
            int myAccountId = PlayerPrefs.GetInt(ApiConfig.AccountIdKey, 0);
            if (accountId != 0 && myAccountId != 0 && accountId != myAccountId)
            {
                Debug.LogWarning($"[SessionHubClient] Bỏ qua SessionOverridden của account {accountId} (đang đăng nhập {myAccountId}).");
                return;
            }

            // newSessionId rỗng = phiên bị thu hồi mà không có phiên kế nhiệm (BE gửi vậy khi đổi
            // mật khẩu ở web portal), nên không thể nói là "đăng nhập ở thiết bị khác".
            string reason = string.IsNullOrEmpty(newSessionId)
                ? "Your session was ended for security reasons. Please log in again."
                : "Your account has been logged in on another device.";

            Debug.LogWarning($"[SessionHubClient] SessionOverridden → đăng xuất. {reason}");

            _mainThreadQueue.Enqueue(() =>
            {
                // ClearToken trước để Reconcile thấy mất token và tự ngắt hub, đồng thời
                // SessionService bỏ qua việc gọi API logout bằng token đã bị vô hiệu.
                ApiClient.Instance?.ClearToken();
                SessionService.Logout(reason);
            });
        }

        private Task SendAsync(string payload)
        {
            var socket = _socket;
            var cts = _cts;
            if (socket == null || socket.State != WebSocketState.Open || cts == null)
                return Task.CompletedTask;

            return SendSafeAsync(socket, payload, cts.Token);
        }

        private static async Task SendSafeAsync(ClientWebSocket socket, string payload, CancellationToken ct)
        {
            try
            {
                await SendRawAsync(socket, payload, ct);
            }
            catch (Exception ex)
            {
                // Ping thất bại chỉ có nghĩa kết nối đã chết; Reconcile sẽ dọn và nối lại.
                Debug.LogWarning($"[SessionHubClient] Ping thất bại: {ex.Message}");
            }
        }

        private static Task SendRawAsync(ClientWebSocket socket, string payload, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        private void Disconnect()
        {
            try { _cts?.Cancel(); } catch (Exception) { }
            _cts?.Dispose();
            _cts = null;

            // Không CloseAsync (cần await, và ở đây có thể đang trong OnDestroy): Abort đóng ngay
            // và vòng nhận thoát bằng exception, server tự dọn connection.
            try { _socket?.Abort(); } catch (Exception) { }
            _socket?.Dispose();
            _socket = null;
        }
    }
}

// ponytail: ClientWebSocket không chạy trên WebGL (browser không cho mở socket kiểu này) — build
// WebGL sẽ ném PlatformNotSupportedException ở ConnectAsync, bị catch thành log warning nên game
// vẫn chạy, chỉ mất realtime và quay về kick-ở-request-kế-tiếp. Nếu sau này cần WebGL thì thay
// phần transport bằng websocket-sharp (đã có sẵn trong Plugins/Photon) hoặc một jslib wrapper,
// giữ nguyên phần đọc giao thức phía trên.
