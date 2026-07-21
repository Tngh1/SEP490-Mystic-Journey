using UnityEngine;
using System.Collections.Generic;
using MysticJourney.API.Core;

namespace MysticJourney.Core
{
    public class HeartbeatSender : MonoBehaviour
    {
        [SerializeField] private float heartbeatInterval = 30f;

        private void Start()
        {
            InvokeRepeating(nameof(SendHeartbeat), 1f, heartbeatInterval);
        }

        private void SendHeartbeat()
        {
            string url = $"{ApiConfig.BaseUrl}/api/player/heartbeat";
            
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
