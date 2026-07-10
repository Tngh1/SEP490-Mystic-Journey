using UnityEngine;

/// <summary>
/// Minimal OnGUI-based connection UI for Phase 1 runtime testing.
/// Provides Host / Client / Disconnect buttons and shows live session info.
///
/// Scope: Phase 1 only. Phase 2 will replace this with a polished Canvas UI
/// using the project's DESIGN.md system. Do not promote this script past
/// Phase 2 — it is intentionally hacky (OnGUI, global mutable state).
///
/// Both Host and Client call the same <see cref="PhotonManager.StartAsync"/>
/// with the same session name. Fusion's Shared Mode auto-elects the first
/// peer that created the room as the StateAuthority (host), and every
/// subsequent peer that joins with the same name becomes a client.
///
/// Usage:
///   1. Add this component to a GameObject in the scene.
///   2. Wire <see cref="sessionName"/> in the Inspector (or leave default).
///   3. Enter Play mode in the Editor. Click "Host or Join".
///   4. Build and run a standalone client. Click "Client" on the same name.
/// </summary>
public class MultiplayerBootstrap : MonoBehaviour
{
    [Header("Connection")]
    [Tooltip("Room name. Both Host and Client must use the same value to find each other.")]
    [SerializeField] private string sessionName = "Test_Mystic_001";

    [Header("UI")]
    [Tooltip("Show the OnGUI panel. Disable to hide it after testing.")]
    [SerializeField] private bool showGui = true;

    [Tooltip("Screen-space anchor for the OnGUI panel.")]
    [SerializeField] private Vector2 panelOrigin = new Vector2(12f, 12f);
    [SerializeField] private float panelWidth = 260f;

    private enum ConnectState { Idle, Connecting, Connected }
    private ConnectState _state = ConnectState.Idle;
    private string _lastError = "";

    private void Awake()
    {
        // Survive scene unloads triggered by GameBootstrap.
        DontDestroyOnLoad(gameObject);
    }

    private void OnGUI()
    {
        if (!showGui) return;

        var rect = new Rect(panelOrigin.x, panelOrigin.y, panelWidth, 220f);
        GUI.Box(rect, "Multiplayer (Phase 1 Test)");

        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 24f, rect.width - 16f, rect.height - 32f));

        if (PhotonManager.Instance == null)
        {
            GUILayout.Label("PhotonManager not found in scene.");
            GUILayout.Label("Add a GameObject with PhotonManager component.");
            GUILayout.EndArea();
            return;
        }

        sessionName = GUILayout.TextField(sessionName);

        bool isConnected = PhotonManager.Instance.IsConnected;

        if (!isConnected)
        {
            GUI.enabled = _state != ConnectState.Connecting;
            if (GUILayout.Button(_state == ConnectState.Connecting ? "Connecting..." : "Host or Join"))
            {
                StartConnection();
            }
            GUI.enabled = true;
        }
        else
        {
            GUILayout.Label($"State: {(PhotonManager.Instance.IsHost ? "HOST" : "CLIENT")}");
            GUILayout.Label($"Local PlayerRef: {PhotonManager.Instance.LocalPlayerRef}");

            int playerCount = CountSpawnedPlayers();
            GUILayout.Label($"Players in session: {playerCount}");

            if (GUILayout.Button("Disconnect"))
            {
                PhotonManager.Instance.Shutdown();
                _state = ConnectState.Idle;
            }
        }

        if (!string.IsNullOrEmpty(_lastError))
        {
            var prevColor = GUI.color;
            GUI.color = Color.red;
            GUILayout.Label(_lastError);
            GUI.color = prevColor;
        }

        GUILayout.EndArea();
    }

    private async void StartConnection()
    {
        _state = ConnectState.Connecting;
        _lastError = "";
        try
        {
            await PhotonManager.Instance.StartAsync(sessionName);
            _state = ConnectState.Connected;
        }
        catch (System.Exception ex)
        {
            _lastError = ex.Message;
            _state = ConnectState.Idle;
            Debug.LogError($"[MultiplayerBootstrap] StartAsync threw: {ex}");
        }
    }

    private int CountSpawnedPlayers()
    {
        var runner = PhotonManager.Instance.Runner;
        if (runner == null) return 0;

        var players = runner.GetAllNetworkObjects();
        int count = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].GetComponent<NetworkPlayer>() != null) count++;
        }
        return count;
    }
}
