using UnityEngine;
using System.Collections.Generic;
using MysticJourney.API.Core;

namespace MysticJourney.Core
{
    public class HeartbeatSender : MonoBehaviour
    {
        [SerializeField] private float heartbeatInterval = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var go = new GameObject("HeartbeatSender");
            DontDestroyOnLoad(go);
            go.AddComponent<HeartbeatSender>();
        }

        private void Start()
        {
            InvokeRepeating(nameof(SendHeartbeat), 1f, heartbeatInterval);
        }

        private void SendHeartbeat()
        {
            if (!ApiClient.Instance.HasToken())
                return;

            // Skip background heartbeat if NetworkReconnectManager is already actively reconnecting
            if (MysticJourney.Networking.NetworkReconnectManager.Instance != null &&
                MysticJourney.Networking.NetworkReconnectManager.Instance.IsReconnecting)
                return;

            string url = ApiConfig.PlayerHeartbeat;
            
            ApiClient.Instance.PostEmpty<object>(url, (res) => 
            {
                MysticJourney.Networking.NetworkReconnectManager.Instance?.ReportNetworkSuccess();
            }, (err) => 
            {
                Debug.LogWarning($"Heartbeat failed: {err.Message}");
                if (err != null && (err.StatusCode == 0 || err.ErrorCode == "NETWORK_ERROR"))
                {
                    MysticJourney.Networking.NetworkReconnectManager.Instance?.ReportNetworkError();
                }
            }, requiresAuth: true);
        }
    }
}
