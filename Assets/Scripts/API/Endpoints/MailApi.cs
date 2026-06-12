using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Tương ứng MailsController → /api/mails
    // GetById, GetByPlayer: không cần auth
    // MarkAsRead, ClaimReward, DeleteMail: cần auth
    public class MailApi : MonoBehaviour
    {
        private static MailApi _instance;

        public static MailApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[MailApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MailApi>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // GET /api/mails/{id}
        public void GetById(int mailId, Action<MailResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailById, mailId);
            Debug.Log($"[MailApi] GetById → mailId={mailId}");

            ApiClient.Instance.Get<MailResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[MailApi] ✅ GetById OK | Title={response.Title} | IsRead={response.IsRead}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[MailApi] ❌ GetById FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // GET /api/mails/player/{playerProfileId}
        // Lấy tất cả mail của player theo profileId
        public void GetByPlayerId(int playerProfileId, Action<MailResponse[]> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailByPlayer, playerProfileId);
            Debug.Log($"[MailApi] GetByPlayerId → playerProfileId={playerProfileId}");

            ApiClient.Instance.Get<MailResponse[]>(
                endpoint,
                response =>
                {
                    Debug.Log($"[MailApi] ✅ GetByPlayerId OK | Count={response?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[MailApi] ❌ GetByPlayerId FAIL | playerProfileId={playerProfileId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // Shortcut: lấy mail của player đang đăng nhập
        public void GetMyMails(Action<MailResponse[]> onSuccess, Action<ApiException> onError)
        {
            int profileId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
            if (profileId <= 0)
            {
                Debug.LogError("[MailApi] ❌ GetMyMails FAIL: Chưa có PlayerProfileId – hãy LoginGame() trước.");
                onError?.Invoke(new ApiException
                {
                    StatusCode = 0,
                    ErrorCode = "NO_PROFILE_ID",
                    Message = "PlayerProfileId not found. Please login first.",
                    RawBody = ""
                });
                return;
            }
            GetByPlayerId(profileId, onSuccess, onError);
        }

        // POST /api/mails/{id}/read  (cần auth)
        // Đánh dấu mail đã đọc
        public void MarkAsRead(int mailId, Action<MailResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailRead, mailId);
            Debug.Log($"[MailApi] MarkAsRead → mailId={mailId}");

            ApiClient.Instance.PostEmpty<MailResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[MailApi] ✅ MarkAsRead OK | mailId={mailId} | IsRead={response.IsRead}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[MailApi] ❌ MarkAsRead FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // POST /api/mails/{id}/claim  (cần auth)
        // Nhận phần thưởng từ mail (gold, gems, item đính kèm)
        public void ClaimReward(int mailId, Action<MailResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailClaim, mailId);
            Debug.Log($"[MailApi] ClaimReward → mailId={mailId}");

            ApiClient.Instance.PostEmpty<MailResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[MailApi] ✅ ClaimReward OK | mailId={mailId} | IsClaimed={response.IsClaimed}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[MailApi] ❌ ClaimReward FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // DELETE /api/mails/{id}?playerProfileId={profileId}  (cần auth)
        // Xóa mail khỏi hộp thư; playerProfileId dùng để xác minh quyền sở hữu
        public void Delete(int mailId, int playerProfileId, Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailDelete, mailId) + $"?playerProfileId={playerProfileId}";
            Debug.Log($"[MailApi] Delete → mailId={mailId} playerProfileId={playerProfileId}");

            ApiClient.Instance.Delete<SimpleResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[MailApi] ✅ Delete OK | mailId={mailId}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[MailApi] ❌ Delete FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }
    }
}
