using System;
using System.Collections;
using UnityEngine;
using MysticJourney.API.Core;
using MysticJourney.UI;
using MysticJourney.Core.Services;

namespace MysticJourney.Networking
{
    /// <summary>
    /// Handles automatic network reconnection during short connectivity drops.
    /// Shows a "Reconnecting..." UIPopup with a "Return to Menu" button both inside and outside dungeons.
    /// After successful reconnection:
    /// - If in dungeon: Shows UIPopup to "Resume Dungeon" or "Return to Menu".
    /// - If outside dungeon: Dismisses popup and resumes normal gameplay.
    /// </summary>
    public class NetworkReconnectManager : MonoBehaviour
    {
        public static NetworkReconnectManager Instance { get; private set; }

        public bool IsReconnecting { get; private set; } = false;

        private bool _wasInDungeon = false;
        private Coroutine _reconnectCoroutine;
        private bool _userClickedReturnToMenu = false;
        private float _lastPingTime = 0f;
        private const float PING_INTERVAL = 2.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (Instance != null) return;
            var go = new GameObject("[NetworkReconnectManager]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NetworkReconnectManager>();
        }

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

        /// <summary>
        /// Call when an API or network operation fails due to connection error.
        /// </summary>
        public void ReportNetworkError()
        {
            // Do not report if user isn't logged in, or already reconnecting, or already clicked Return to Menu
            if (!ApiClient.Instance.HasToken() || IsReconnecting || _userClickedReturnToMenu)
                return;

            IsReconnecting = true;
            _wasInDungeon = DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon;

            Debug.LogWarning($"[NetworkReconnectManager] Network loss detected! Entering Reconnecting state (WasInDungeon={_wasInDungeon}).");

            ShowReconnectingPopup();

            if (_reconnectCoroutine != null)
                StopCoroutine(_reconnectCoroutine);
            _reconnectCoroutine = StartCoroutine(AutoReconnectCoroutine());
        }

        /// <summary>
        /// Call when an API request or heartbeat succeeds while in reconnecting state.
        /// </summary>
        public void ReportNetworkSuccess()
        {
            if (!IsReconnecting)
                return;

            Debug.Log("[NetworkReconnectManager] Network connection restored successfully!");

            IsReconnecting = false;
            _userClickedReturnToMenu = false;

            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }

            // Dismiss the "Reconnecting..." popup
            if (UIPopupManager.Instance != null)
            {
                UIPopupManager.Instance.HidePopup();
            }

            // If disconnected while inside a dungeon, prompt the player to Resume Dungeon or Return to Menu
            if (_wasInDungeon && DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            {
                ShowResumeDungeonPopup();
            }
            else
            {
                _wasInDungeon = false;
                Debug.Log("[NetworkReconnectManager] Outside dungeon — resumed normal gameplay.");
            }
        }

        private void ShowReconnectingPopup()
        {
            UIPopupBox.Show(
                caller: null,
                titleText: "Reconnecting...",
                message: "Connection lost. Reconnecting to server...",
                onConfirm: OnReturnToMenuClicked,
                onCancel: null,
                confirmText: "Return to Menu"
            );
        }

        private void ShowResumeDungeonPopup()
        {
            UIPopupBox.Show(
                caller: null,
                titleText: "Reconnected",
                message: "Network connection restored. Do you want to resume the dungeon?",
                onConfirm: OnResumeDungeonClicked,
                onCancel: OnReturnToMenuClicked,
                confirmText: "Resume Dungeon",
                cancelText: "Return to Menu"
            );
        }

        private IEnumerator AutoReconnectCoroutine()
        {
            while (IsReconnecting && !_userClickedReturnToMenu)
            {
                yield return new WaitForSecondsRealtime(PING_INTERVAL);

                if (!IsReconnecting || _userClickedReturnToMenu)
                    yield break;

                if (!ApiClient.Instance.HasToken())
                {
                    IsReconnecting = false;
                    yield break;
                }

                // Attempt ping / heartbeat to verify network connection
                string url = ApiConfig.PlayerHeartbeat;
                ApiClient.Instance.PostEmpty<object>(url,
                    onSuccess: _ =>
                    {
                        ReportNetworkSuccess();
                    },
                    onError: err =>
                    {
                        // Still disconnected, keep retrying in loop unless error is 401/session expired
                        if (err != null && (err.StatusCode == 401 || err.ErrorCode == "SESSION_EXPIRED" || err.ErrorCode == "SESSION_OVERRIDDEN"))
                        {
                            IsReconnecting = false;
                            if (UIPopupManager.Instance != null) UIPopupManager.Instance.HidePopup();
                            SessionService.Logout("Your session has ended. Please log in again.");
                        }
                        else
                        {
                            Debug.LogWarning($"[NetworkReconnectManager] Reconnect ping failed: {err?.Message}. Retrying...");
                        }
                    },
                    requiresAuth: true
                );
            }
        }

        private void OnResumeDungeonClicked()
        {
            Debug.Log("[NetworkReconnectManager] Player chose to Resume Dungeon.");
            _wasInDungeon = false;
            _userClickedReturnToMenu = false;
            if (UIPopupManager.Instance != null)
            {
                UIPopupManager.Instance.HidePopup();
            }
        }

        private void OnReturnToMenuClicked()
        {
            Debug.Log("[NetworkReconnectManager] Player clicked Return to Menu.");
            _userClickedReturnToMenu = true;
            IsReconnecting = false;
            _wasInDungeon = false;

            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }

            if (UIPopupManager.Instance != null)
            {
                UIPopupManager.Instance.HidePopup();
            }

            SessionService.Logout();
        }

        public void ResetState()
        {
            IsReconnecting = false;
            _wasInDungeon = false;
            _userClickedReturnToMenu = false;
            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }
        }
    }
}
