using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class QuestApi : MonoBehaviour
    {
        private static QuestApi _instance;

        public static QuestApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[QuestApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<QuestApi>();
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
            Action<PagedResultResponse<QuestResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string type = null,
            bool? isActive = null,
            string mapName = null)
        {
            var endpoint = $"{ApiConfig.QuestAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={UnityEngine.Networking.UnityWebRequest.EscapeURL(search)}";
            if (!string.IsNullOrEmpty(type)) endpoint += $"&type={UnityEngine.Networking.UnityWebRequest.EscapeURL(type)}";
            if (isActive.HasValue) endpoint += $"&isActive={isActive.Value}";
            if (!string.IsNullOrEmpty(mapName)) endpoint += $"&mapName={UnityEngine.Networking.UnityWebRequest.EscapeURL(mapName)}";

            ApiClient.Instance.Get<PagedResultResponse<QuestResponse>>(
                endpoint,
                response =>
                {
                    Debug.Log($"[QuestApi] GetAll OK | TotalCount={response.TotalCount}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[QuestApi] GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        public void GetById(int questId, Action<QuestResponse> onSuccess, Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.QuestById, questId);

            ApiClient.Instance.Get<QuestResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[QuestApi] GetById OK | questId={questId} | Title={response.Title}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[QuestApi] GetById FAIL | questId={questId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }
    }
}
