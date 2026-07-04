using System;
using System.Collections.Generic;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;
using MysticJourney.API.Core;

namespace MysticJourney.API.Endpoints
{
    public class FriendApi
    {
        public static void GetFriendList(Action<List<FriendDto>> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get(ApiConfig.GetFriendListEndpoint, onSuccess, onError, requiresAuth: true);
        }

        public static void GetFriendRequests(Action<List<PendingFriendRequestDto>> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get(ApiConfig.GetFriendRequestsEndpoint, onSuccess, onError, requiresAuth: true);
        }

        public static void GetFriendBlocks(Action<List<FriendProfileDto>> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get(ApiConfig.GetFriendBlocksEndpoint, onSuccess, onError, requiresAuth: true);
        }

        public static void GetFriendProfile(int profileId, Action<FriendProfileDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GetFriendProfileEndpoint.Replace("{id}", profileId.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        public static void SearchPlayers(string keyword, Action<List<FriendSearchDto>> onSuccess, Action<ApiException> onError)
        {
            string uri = $"{ApiConfig.SearchPlayersEndpoint}?keyword={UnityEngine.Networking.UnityWebRequest.EscapeURL(keyword)}";
            ApiClient.Instance.Get(uri, onSuccess, onError, requiresAuth: true);
        }

        public static void SendFriendRequest(int targetProfileId, Action<object> onSuccess, Action<ApiException> onError)
        {
            var payload = new FriendRequestPayload { TargetProfileId = targetProfileId };
            ApiClient.Instance.Post(ApiConfig.SendFriendRequestEndpoint, payload, onSuccess, onError, requiresAuth: true);
        }

        public static void AcceptFriendRequest(int requesterId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.AcceptFriendRequestEndpoint.Replace("{requesterId}", requesterId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void DeclineFriendRequest(int requesterId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.DeclineFriendRequestEndpoint.Replace("{requesterId}", requesterId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void RemoveFriend(int targetId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.RemoveFriendEndpoint.Replace("{targetId}", targetId.ToString());
            ApiClient.Instance.Delete(url, onSuccess, onError, requiresAuth: true);
        }

        public static void BlockPlayer(int targetProfileId, Action<object> onSuccess, Action<ApiException> onError)
        {
            var payload = new FriendRequestPayload { TargetProfileId = targetProfileId };
            ApiClient.Instance.Post(ApiConfig.BlockPlayerEndpoint, payload, onSuccess, onError, requiresAuth: true);
        }

        public static void UnblockPlayer(int targetProfileId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.UnblockPlayerEndpoint.Replace("{targetId}", targetProfileId.ToString());
            ApiClient.Instance.Delete(url, onSuccess, onError, requiresAuth: true);
        }
    }
}
