using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.Networking;
using MysticJourney.Core.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MysticJourney.Core.Services
{
    // Initializes a new default instance of the SessionService class.
    public static class SessionService
    {
        private static bool _loggingOut;

        // Executes core business logic for pending logout reason.
        public static string PendingLogoutReason { get; private set; }

        // Executes core business logic for clear pending logout reason.
        public static void ClearPendingLogoutReason()
        {
            PendingLogoutReason = null;
        }

        // Executes core business logic for prepare for credential login.
        // Logic details: validates required non-empty string arguments.
        public static void PrepareForCredentialLogin()
        {
            SessionHubClient.Instance?.DisconnectForLogout();
            ApiClient.Instance?.ClearToken();

            if (NetworkReconnectManager.Instance != null)
                NetworkReconnectManager.Instance.ResetState();

            PendingLogoutReason = null;
            _loggingOut = false;
        }

        // Revokes active refresh token for the calling client type and clears authentication session cookies.
        public static void Logout(string reason = null)
        {
            if (_loggingOut) return;
            _loggingOut = true;

            if (!string.IsNullOrEmpty(reason))
                PendingLogoutReason = reason;

            Debug.Log("[SessionService] Logging out...");

            SessionHubClient.Instance?.DisconnectForLogout();


            if (PhotonManager.Instance != null)
                PhotonManager.Instance.Shutdown(notify: false);

            if (ApiClient.Instance != null && ApiClient.Instance.HasToken())
            {
                AuthApi.Instance.Logout(
                    onSuccess: _ => FinishLogout(),
                    onError: _ => FinishLogout());
            }
            else
            {
                ApiClient.Instance?.ClearToken();
                GameStateService.Instance?.Reset();
                FinishLogout();
            }
        }

        // Executes core business logic for finish logout.
        private static void FinishLogout()
        {
            MapPositionCache.Clear();

            DestroyAll<QuestUIManager>();
            DestroyAll<MonsterManager>();
            DestroyAll<DungeonManager>();

            _loggingOut = false;

            Debug.Log($"[SessionService] Logged out. Loading {GameConstants.Scenes.MainMenu}.");
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
        }

        // Executes core business logic for component.
        private static void DestroyAll<T>() where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var found = Object.FindObjectsOfType<T>(true);
#endif
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    Object.Destroy(found[i].gameObject);
            }
        }
    }
}
