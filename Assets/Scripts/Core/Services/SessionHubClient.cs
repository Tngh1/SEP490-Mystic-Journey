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
    // Executes mono behaviour operation.
    // Validates input parameters against null or empty values.
    public class SessionHubClient : MonoBehaviour
    {
        private const char RecordSeparator = '\x1E';

        private const string HandshakeRequest = "{\"protocol\":\"json\",\"version\":1}\x1E";
        private const string PingMessage = "{\"type\":6}\x1E";

        private const float PingIntervalSeconds = 15f;

        private const float ReconcileIntervalSeconds = 5f;

        // Executes instance operation.
        public static SessionHubClient Instance { get; private set; }

        private const float MaxReconnectDelaySeconds = 60f;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private float _lastPingTime;

        private int _consecutiveFailures;
        private float _nextAttemptTime;
        private string _lastFailureMessage;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        // Executes auto start operation.
        private static void AutoStart()
        {
            var go = new GameObject("SessionHubClient");
            DontDestroyOnLoad(go);
            go.AddComponent<SessionHubClient>();
        }

        // Performs startup initialization for SessionHubClient on the first active frame.
        // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
        private void Start()
        {
            InvokeRepeating(nameof(Reconcile), 1f, ReconcileIntervalSeconds);
        }
        // Initializes internal component caches and dependencies for SessionHubClient upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }


        // Per-frame update loop for SessionHubClient.
        // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
                action?.Invoke();
        }

        // Unsubscribe this component's event handlers and release its temporary runtime resources.
        private void OnDestroy()
        {
            Disconnect();
            if (Instance == this)
                Instance = null;
        }
        // Executes on application quit operation.
        private void OnApplicationQuit() => Disconnect();

        // Executes disconnect for logout operation.
        public void DisconnectForLogout()
        {
            Disconnect();
            ResetBackoff();
        }

        // Executes reconcile operation.
        private void Reconcile()
        {
            bool hasToken = ApiClient.Instance != null && ApiClient.Instance.HasToken();
            bool isOpen = _socket != null && _socket.State == WebSocketState.Open;

            if (!hasToken)

            {
                if (_socket != null) Disconnect();
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

            if (_socket.State != WebSocketState.Connecting)
                OnDisconnected(_socket, $"Socket ở trạng thái {_socket.State}.");
        }

        // Executes connect operation.
        // Validates input parameters against null or empty values.
        private void Connect()
        {
            string token = ApiClient.Instance.GetToken();
            if (string.IsNullOrEmpty(token)) return;

            string wsBase = ApiConfig.BaseUrl
                .Replace("https://", "wss://")
                .Replace("http://", "ws://");
            var uri = new Uri($"{wsBase}{ApiConfig.GameHub}?access_token={Uri.EscapeDataString(token)}");

            _socket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            _lastPingTime = Time.unscaledTime;

            _ = RunAsync(_socket, _cts.Token, uri);
        }

        // Executes run async operation.
        private async Task RunAsync(ClientWebSocket socket, CancellationToken ct, Uri uri)
        {
            try
            {
                await socket.ConnectAsync(uri, ct);
                await SendRawAsync(socket, HandshakeRequest, ct);

                _mainThreadQueue.Enqueue(() => OnConnected(socket));

                await ReceiveLoopAsync(socket, ct);

                if (!ct.IsCancellationRequested)
                    _mainThreadQueue.Enqueue(() => OnDisconnected(socket, "Server đã đóng kết nối."));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                _mainThreadQueue.Enqueue(() => OnDisconnected(socket, message));
            }
        }

        // Executes on connected operation.
        private void OnConnected(ClientWebSocket socket)
        {
            if (socket != _socket) return;

            if (_consecutiveFailures > 0)
                Debug.Log($"[SessionHubClient] Đã nối lại được hub sau {_consecutiveFailures} lần thử.");

            ResetBackoff();
        }

        // Executes on disconnected operation.
        private void OnDisconnected(ClientWebSocket socket, string message)
        {
            if (socket != _socket) return;

            _consecutiveFailures++;

            float delay = Mathf.Min(
                ReconcileIntervalSeconds * Mathf.Pow(2f, _consecutiveFailures - 1),
                MaxReconnectDelaySeconds);
            _nextAttemptTime = Time.unscaledTime + delay;

            if (!string.Equals(_lastFailureMessage, message, StringComparison.Ordinal))
            {
                _lastFailureMessage = message;
                Debug.LogWarning($"[SessionHubClient] Hub connection ended: {message} Thử lại sau {delay:0}s.");
            }

            Disconnect();
        }

        // Executes reset backoff operation.
        private void ResetBackoff()
        {
            _consecutiveFailures = 0;
            _nextAttemptTime = 0f;
            _lastFailureMessage = null;
        }

        // Executes receive loop async operation.
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

                var chunks = pending.ToString().Split(RecordSeparator);
                pending.Clear();
                pending.Append(chunks[chunks.Length - 1]);

                for (int i = 0; i < chunks.Length - 1; i++)
                    HandleMessage(socket, chunks[i]);
            }
        }

        // Executes handle message operation.
        // Validates input parameters against null or empty values.
        private void HandleMessage(ClientWebSocket sourceSocket, string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            JObject message;
            try
            {
                message = JObject.Parse(json);
            }
            catch (Exception)
            {
                Debug.LogWarning("[SessionHubClient] Bỏ qua message không phải JSON hợp lệ.");
                return;
            }

            var handshakeError = message["error"];
            if (handshakeError != null)
            {
                Debug.LogWarning($"[SessionHubClient] Handshake bị từ chối: {handshakeError}");
                return;
            }

            int type = message["type"]?.Value<int>() ?? 0;

            if (type == 7)
            {
                Debug.LogWarning($"[SessionHubClient] Server đóng kết nối: {message["error"]}");
                return;
            }
            if (type != 1) return;

            if (!string.Equals(message["target"]?.Value<string>(), "SessionOverridden", StringComparison.Ordinal))
                return;

            var payload = message["arguments"]?.First;
            int accountId = payload?["accountId"]?.Value<int>() ?? 0;
            string newSessionId = payload?["newSessionId"]?.Value<string>() ?? string.Empty;

            int myAccountId = PlayerPrefs.GetInt(ApiConfig.AccountIdKey, 0);
            if (accountId != 0 && myAccountId != 0 && accountId != myAccountId)
            {
                Debug.LogWarning($"[SessionHubClient] Bỏ qua SessionOverridden của account {accountId} (đang đăng nhập {myAccountId}).");
                return;
            }

            string reason = string.IsNullOrEmpty(newSessionId)
                ? "Your session was ended for security reasons. Please log in again."
                : "Your account has been logged in on another device.";

            Debug.LogWarning($"[SessionHubClient] SessionOverridden → đăng xuất. {reason}");

            _mainThreadQueue.Enqueue(() =>
            {
                if (sourceSocket != _socket) return;

                ApiClient.Instance?.ClearToken();
                SessionService.Logout(reason);
            });
        }

        // Executes send async operation.
        private Task SendAsync(string payload)
        {
            var socket = _socket;
            var cts = _cts;
            if (socket == null || socket.State != WebSocketState.Open || cts == null)
                return Task.CompletedTask;

            return SendSafeAsync(socket, payload, cts.Token);
        }

        // Executes send safe async operation.
        private static async Task SendSafeAsync(ClientWebSocket socket, string payload, CancellationToken ct)
        {
            try
            {
                await SendRawAsync(socket, payload, ct);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionHubClient] Ping thất bại: {ex.Message}");
            }
        }

        // Executes send raw async operation.
        private static Task SendRawAsync(ClientWebSocket socket, string payload, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        // Executes disconnect operation.
        private void Disconnect()
        {
            try { _cts?.Cancel(); } catch (Exception) { }
            _cts?.Dispose();
            _cts = null;

            try { _socket?.Abort(); } catch (Exception) { }
            _socket?.Dispose();
            _socket = null;
        }
    }
}
