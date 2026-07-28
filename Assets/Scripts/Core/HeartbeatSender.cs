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

            string url = ApiConfig.PlayerHeartbeat;
            
            ApiClient.Instance.PostEmpty<object>(url, (res) => 
            {
                // Heartbeat successful
            }, (err) => 
            {
                Debug.LogWarning($"Heartbeat failed: {err.Message}");
            }, requiresAuth: true);
        }
    }
}
