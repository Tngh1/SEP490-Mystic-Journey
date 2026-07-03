using System;
using MysticJourney.API.Models;

namespace MysticJourney.API.Endpoints
{
    public class FriendApi
    {
        public static void GetFriendList(Action<FriendDto[]> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get(ApiConfig.FriendList, onSuccess, onError, requiresAuth: true);
        }

        public static void GetFriendRequests(Action<PendingFriendRequestDto[]> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get(ApiConfig.FriendRequests, onSuccess, onError, requiresAuth: true);
        }

        public static void SendFriendRequest(string targetName, Action<object> onSuccess, Action<ApiException> onError)
        {
            var payload = new FriendRequestPayload { TargetName = targetName };
            ApiClient.Instance.Post(ApiConfig.FriendRequestSend, payload, onSuccess, onError, requiresAuth: true);
        }

        public static void AcceptFriendRequest(int requesterId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = string.Format(ApiConfig.FriendRequestAccept, requesterId);
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void SearchPlayers(string token, string keyword, System.Action<List<FriendSearchDto>> onSuccess, System.Action<string> onError)
        {
            string uri = $"{ApiConfig.SearchPlayersEndpoint}?keyword={UnityWebRequest.EscapeURL(keyword)}";
            RestClient.Get<List<FriendSearchDto>>(new RequestHelper
            {
                Uri = uri,
                Headers = new Dictionary<string, string> { { "Authorization", "Bearer " + token } }
            }).Then(onSuccess).Catch(err => onError(err.Message));
        }

        public static void DeclineFriendRequest(int requesterId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = string.Format(ApiConfig.FriendRequestDecline, requesterId);
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void RemoveFriend(int targetId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = string.Format(ApiConfig.FriendRemove, targetId);
            ApiClient.Instance.Delete(url, onSuccess, onError, requiresAuth: true);
        }

        public static void BlockPlayer(string targetName, Action<object> onSuccess, Action<ApiException> onError)
        {
            var payload = new FriendRequestPayload { TargetName = targetName };
            ApiClient.Instance.Post(ApiConfig.FriendBlock, payload, onSuccess, onError, requiresAuth: true);
        }
    }
}
