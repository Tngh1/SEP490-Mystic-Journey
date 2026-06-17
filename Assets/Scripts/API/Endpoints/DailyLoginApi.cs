using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class DailyLoginApi : MonoBehaviour
    {
        private static DailyLoginApi _instance;

        public static DailyLoginApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[DailyLoginApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<DailyLoginApi>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void GetAll(
            int page,
            int pageSize,
            Action<PagedResultResponse<DailyLoginRewardResponse>> onSuccess,
            Action<ApiException> onError)
        {
            var endpoint = $"{ApiConfig.DailyLoginRewards}?page={page}&pageSize={pageSize}";

            ApiClient.Instance.Get<PagedResultResponse<DailyLoginRewardResponse>>(
                endpoint,
                response =>
                {
                    Debug.Log($"[DailyLoginApi] GetAll OK | TotalCount={response.TotalCount}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[DailyLoginApi] GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        public void GetStatus(Action<PlayerDailyLoginResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<ApiResponse<PlayerDailyLoginResponse>>(
                ApiConfig.DailyLoginStatus,
                response => onSuccess?.Invoke(response.Data),
                onError,
                requiresAuth: true
            );
        }

        public void Claim(Action<ClaimDailyRewardResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.PostEmpty<ApiResponse<ClaimDailyRewardResponse>>(
                ApiConfig.DailyLoginClaim,
                response => onSuccess?.Invoke(response.Data),
                onError,
                requiresAuth: true
            );
        }
    }
}
