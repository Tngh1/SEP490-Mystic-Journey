using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;
using UnityEngine.Networking;

namespace MysticJourney.API.Endpoints
{
    // Initializes a new default instance of the GuildApi class.
    public class GuildApi
    {

        // ─── Player APIs ───────────────────────────────────────────────────────
        // Load my guild using on success and on error; it sends the GET API request.
        public static void GetMyGuild(
            Action<GuildDetailResponseDto> onSuccess,
            Action<ApiException> onError = null)
        {
            string url = ApiConfig.GuildMyGuild + "?t=" + System.DateTime.Now.Ticks;
            ApiClient.Instance.Get<GuildDetailResponseDto>(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes get guild list operation.
        public static void GetGuildList(string search, int? joinPolicy, int? minLevel, Action<List<GuildResponseDto>> onSuccess, Action<ApiException> onError)
        {
            string url = $"{ApiConfig.GuildList}?search={UnityWebRequest.EscapeURL(search ?? "")}";
            if (joinPolicy.HasValue) url += $"&joinPolicy={joinPolicy.Value}";
            if (minLevel.HasValue) url += $"&minLevel={minLevel.Value}";
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes get guild rankings operation.
        public static void GetGuildRankings(Action<List<GuildRankResponseDto>> onSuccess, Action<ApiException> onError)
        {
            string url = $"{ApiConfig.GuildList}/rankings";
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes get guild detail operation.
        public static void GetGuildDetail(int id, Action<GuildDetailResponseDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildDetail.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes get members operation.
        public static void GetMembers(int id, Action<List<GuildMemberResponseDto>> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildMembers.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }


        // Executes create guild operation.
        public static void CreateGuild(CreateGuildRequestDto payload, Action<GuildResponseDto> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Post(ApiConfig.GuildList, payload, onSuccess, onError, requiresAuth: true);
        }

        // Executes dissolve guild operation.
        public static void DissolveGuild(int id, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildDetail.Replace("{id}", id.ToString());
            ApiClient.Instance.Delete(url, onSuccess, onError, requiresAuth: true);
        }


        // Executes apply to guild operation.
        public static void ApplyToGuild(int id, Action<GuildJoinResultDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildApply.Replace("{id}", id.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes leave guild operation.
        public static void LeaveGuild(int id, Action<GuildJoinResultDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildLeave.Replace("{id}", id.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes level up operation.
        public static void LevelUp(int id, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildLevelUp.Replace("{id}", id.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }


        // Executes get applications operation.
        public static void GetApplications(int id, Action<List<GuildApplicationDTO>> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildApplications.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes approve application operation.
        public static void ApproveApplication(int id, int appId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildApproveApp.Replace("{id}", id.ToString()).Replace("{appId}", appId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes reject application operation.
        public static void RejectApplication(int id, int appId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildRejectApp.Replace("{id}", id.ToString()).Replace("{appId}", appId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }


        // Executes invite member operation.
        public static void InviteMember(int id, int inviteeProfileId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildInvite.Replace("{id}", id.ToString());
            var payload = new InvitePlayerRequest { inviteeProfileId = inviteeProfileId };
            ApiClient.Instance.Post(url, payload, onSuccess, onError, requiresAuth: true);
        }

        // Executes kick member operation.
        public static void KickMember(int id, int memberId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildKick.Replace("{id}", id.ToString()).Replace("{memberId}", memberId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes promote member operation.
        public static void PromoteMember(int id, int memberId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildPromote.Replace("{id}", id.ToString()).Replace("{memberId}", memberId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes demote member operation.
        public static void DemoteMember(int id, int memberId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildDemote.Replace("{id}", id.ToString()).Replace("{memberId}", memberId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes transfer leader operation.
        public static void TransferLeader(int id, int newLeaderProfileId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildTransferLeader.Replace("{id}", id.ToString());
            var payload = new TransferLeaderRequest { newLeaderProfileId = newLeaderProfileId };
            ApiClient.Instance.Post(url, payload, onSuccess, onError, requiresAuth: true);
        }


        // Executes update settings operation.
        public static void UpdateSettings(int id, int? requiredLevel, int? joinPolicy, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = $"/api/guilds/{id}/settings";
            var payload = new { requiredLevel = requiredLevel, joinPolicy = joinPolicy };

            ApiClient.Instance.Put<object, object>(
                url, payload,
                response => onSuccess?.Invoke(response),
                error => onError?.Invoke(error)
            );
        }

        // Executes update notice operation.
        public static void UpdateNotice(int id, string notice, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildNotice.Replace("{id}", id.ToString());
            var payload = new ChangeNoticeRequest { notice = notice };
            ApiClient.Instance.Put(url, payload, onSuccess, onError, requiresAuth: true);
        }

        // Executes update icon operation.
        public static void UpdateIcon(int id, int iconId, int? bannerId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildIcon.Replace("{id}", id.ToString());
            var payload = new ChangeIconRequest { iconId = iconId, bannerId = bannerId };
            ApiClient.Instance.Put(url, payload, onSuccess, onError, requiresAuth: true);
        }


        // Executes donate operation.
        public static void Donate(int id, string currencyType, int amount, Action<GuildDonateResultDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildDonate.Replace("{id}", id.ToString());
            var payload = new DonateRequest { currencyType = currencyType, amount = amount };
            ApiClient.Instance.Post(url, payload, onSuccess, onError, requiresAuth: true);
        }


        // Executes get logs operation.
        public static void GetLogs(int id, Action<List<GuildLogDto>> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildLogs.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes get chat operation.
        public static void GetChat(int id, Action<List<GuildMessageDTO>> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildChat.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        // Executes send chat operation.
        public static void SendChat(int id, string content, Action<GuildMessageDTO> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildChat.Replace("{id}", id.ToString());
            var payload = new SendGuildMessageRequest { content = content };
            ApiClient.Instance.Post(url, payload, onSuccess, onError, requiresAuth: true);
        }
    }
}
