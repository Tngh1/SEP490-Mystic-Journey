using System;
using System.Collections;
using UnityEngine;
using MysticJourney.API.Core;
using MysticJourney.UI;
using MysticJourney.Core.Services;

namespace MysticJourney.Networking
{
    // Executes core business logic for mono behaviour.
    public class NetworkReconnectManager : MonoBehaviour
    {
        // Executes core business logic for instance.
        public static NetworkReconnectManager Instance { get; private set; }

        // Executes core business logic for is reconnecting.
        public bool IsReconnecting { get; private set; } = false;

        private bool _wasInDungeon = false;
        private Coroutine _reconnectCoroutine;
        private bool _userClickedReturnToMenu = false;
        private const float PING_INTERVAL = 2.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        // Executes core business logic for auto start.
        private static void AutoStart()
        {
            if (Instance != null) return;
            var go = new GameObject("[NetworkReconnectManager]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NetworkReconnectManager>();
        }

        // Initializes internal component caches and dependencies for NetworkReconnectManager upon GameObject instantiation.
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

        // Executes core business logic for report network error.
        public void ReportNetworkError()
        {
            if (!ApiClient.Instance.HasToken() || IsReconnecting || _userClickedReturnToMenu)
                return;

            IsReconnecting = true;
            _wasInDungeon = DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon;

            Debug.LogWarning($"[NetworkReconnectManager] Network loss detected! Entering Reconnecting state (WasInDungeon={_wasInDungeon}).");

            ShowReconnectingPopup();

            if (_reconnectCoroutine != null)
                StopCoroutine(_reconnectCoroutine);
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            _reconnectCoroutine = StartCoroutine(AutoReconnectCoroutine());
        }

        // Executes core business logic for report network success.
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

            if (UIPopup.Instance != null)
            {
                UIPopup.Instance.HidePopup();
            }

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

        // Update visibility for reconnecting popup; it updates navigation or visibility through show.
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

        // Update visibility for resume dungeon popup; it updates navigation or visibility through show.
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

        // Executes core business logic for auto reconnect coroutine.
        // Logic details: validates required non-empty string arguments.
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

                string url = ApiConfig.PlayerHeartbeat;
                ApiClient.Instance.PostEmpty<object>(url,
                    onSuccess: _ =>
                    {
                        ReportNetworkSuccess();
                    },
                    onError: err =>
                    {
                        if (err != null && (err.StatusCode == 401 || err.ErrorCode == "SESSION_EXPIRED" || err.ErrorCode == "SESSION_OVERRIDDEN"))
                        {
                            IsReconnecting = false;
                            if (UIPopup.Instance != null) UIPopup.Instance.HidePopup();

                            if (string.IsNullOrEmpty(SessionService.PendingLogoutReason))
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

        // Executes core business logic for on resume dungeon clicked.
        private void OnResumeDungeonClicked()
        {
            Debug.Log("[NetworkReconnectManager] Player chose to Resume Dungeon.");
            _wasInDungeon = false;
            _userClickedReturnToMenu = false;
            if (UIPopup.Instance != null)
            {
                UIPopup.Instance.HidePopup();
            }
        }

        // Executes core business logic for on return to menu clicked.
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

            if (UIPopup.Instance != null)
            {
                UIPopup.Instance.HidePopup();
            }

            SessionService.Logout();
        }

        // Executes core business logic for reset state.
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
