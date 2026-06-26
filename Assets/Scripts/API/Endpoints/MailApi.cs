using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class MailApi : BaseApiService<MailApi>
    {

        public void GetMyMails(Action<PlayerMeMailsResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"GetMyMails → Đang tải danh sách thư của người chơi hiện tại");
            ApiClient.Instance.Get<PlayerMeMailsResponse>(
                ApiConfig.PlayerMeMails,
                response =>
                {
                    SafeDebugLog($"GetMyMails OK | Tổng thư: {response.TotalCount} | Chưa đọc: {response.UnreadCount}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMyMails FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true); 
        }

        public void GetById(int mailId, Action<MailResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailById, mailId);
            SafeDebugLog($"GetById → mailId={mailId}");
            ApiClient.Instance.Get<MailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | Title={response.Title} | IsRead={response.IsRead}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }

        public void GetByPlayerId(int playerProfileId, Action<MailResponse[]> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailByPlayer, playerProfileId);
            SafeDebugLog($"GetByPlayerId → playerProfileId={playerProfileId}");
            ApiClient.Instance.Get<MailResponse[]>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetByPlayerId OK | Count={response?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetByPlayerId FAIL | playerProfileId={playerProfileId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }


        public void MarkAsRead(int mailId, Action<MailResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailRead, mailId);
            SafeDebugLog($"MarkAsRead → mailId={mailId}");
            ApiClient.Instance.PostEmpty<MailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"MarkAsRead OK | mailId={mailId} | IsRead={response.IsRead}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"MarkAsRead FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void ClaimReward(int mailId, Action<MailResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailClaim, mailId);
            SafeDebugLog($"ClaimReward → mailId={mailId}");
            ApiClient.Instance.PostEmpty<MailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"ClaimReward OK | mailId={mailId} | IsClaimed={response.IsClaimed}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ClaimReward FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void Delete(int mailId, int playerProfileId, Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailDelete, mailId) + $"?playerProfileId={playerProfileId}";
            SafeDebugLog($"Delete → mailId={mailId} playerProfileId={playerProfileId}");
            ApiClient.Instance.Delete<SimpleResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"Delete OK | mailId={mailId}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Delete FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
