using UnityEngine;
using Proyecto26;
using System.Collections.Generic;

namespace Core
{
    public class HeartbeatSender : MonoBehaviour
    {
        [SerializeField] private float heartbeatInterval = 30f;
        private string token;

        private void Start()
        {
            token = PlayerPrefs.GetString("AuthToken", "");
            InvokeRepeating(nameof(SendHeartbeat), 1f, heartbeatInterval);
        }

        private void SendHeartbeat()
        {
            if (string.IsNullOrEmpty(token)) return;

            // This assumes API.Core.ApiConfig is accessible, otherwise we can hardcode for this file or ensure proper namespaces.
            string url = $"{API.Core.ApiConfig.BaseUrl}/api/presence/heartbeat";
            
            RestClient.Post(new RequestHelper
            {
                Uri = url,
                Headers = new Dictionary<string, string> { { "Authorization", "Bearer " + token } }
            }).Then(res => 
            {
                // Heartbeat successful
            }).Catch(err => 
            {
                Debug.LogWarning($"Heartbeat failed: {err.Message}");
            });
        }
    }
}
