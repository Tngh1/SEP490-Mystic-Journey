using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;
using UnityEngine.Networking;

namespace MysticJourney.API.Endpoints
{
    public class GuildApi
    {
        // ─── View ─────────────────────────────────────────────────────────

        public static void GetMyGuild(
            Action<GuildDetailResponseDto> onSuccess,
            Action<ApiException> onError = null)
        {
            string url = ApiConfig.GuildMyGuild + "?t=" + System.DateTime.Now.Ticks;
            ApiClient.Instance.Get<GuildDetailResponseDto>(url, onSuccess, onError, requiresAuth: true);
        }

        public static void GetGuildList(string search, int? joinPolicy, int? minLevel, Action<List<GuildResponseDto>> onSuccess, Action<ApiException> onError)
        {
            string url = $"{ApiConfig.GuildList}?search={UnityWebRequest.EscapeURL(search ?? "")}";
            if (joinPolicy.HasValue) url += $"&joinPolicy={joinPolicy.Value}";
            if (minLevel.HasValue) url += $"&minLevel={minLevel.Value}";
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        public static void GetGuildRankings(Action<List<GuildRankResponseDto>> onSuccess, Action<ApiException> onError)
        {
            string url = $"{ApiConfig.GuildList}/rankings";
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        public static void GetGuildDetail(int id, Action<GuildDetailResponseDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildDetail.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        public static void GetMembers(int id, Action<List<GuildMemberResponseDto>> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildMembers.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        // ─── Create / Dissolve ────────────────────────────────────────────

        public static void CreateGuild(CreateGuildRequestDto payload, Action<GuildResponseDto> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Post(ApiConfig.GuildList, payload, onSuccess, onError, requiresAuth: true);
        }

        public static void DissolveGuild(int id, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildDetail.Replace("{id}", id.ToString());
            ApiClient.Instance.Delete(url, onSuccess, onError, requiresAuth: true);
        }

        // ─── Join / Leave / Level Up ──────────────────────────────────────

        public static void ApplyToGuild(int id, Action<GuildJoinResultDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildApply.Replace("{id}", id.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void LeaveGuild(int id, Action<GuildJoinResultDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildLeave.Replace("{id}", id.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void LevelUp(int id, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildLevelUp.Replace("{id}", id.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        // ─── Applications ─────────────────────────────────────────────────

        public static void GetApplications(int id, Action<List<GuildApplicationDTO>> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildApplications.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        public static void ApproveApplication(int id, int appId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildApproveApp.Replace("{id}", id.ToString()).Replace("{appId}", appId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void RejectApplication(int id, int appId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildRejectApp.Replace("{id}", id.ToString()).Replace("{appId}", appId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        // ─── Member Management ────────────────────────────────────────────

        public static void InviteMember(int id, int inviteeProfileId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildInvite.Replace("{id}", id.ToString());
            var payload = new InvitePlayerRequest { inviteeProfileId = inviteeProfileId };
            ApiClient.Instance.Post(url, payload, onSuccess, onError, requiresAuth: true);
        }

        public static void KickMember(int id, int memberId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildKick.Replace("{id}", id.ToString()).Replace("{memberId}", memberId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void PromoteMember(int id, int memberId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildPromote.Replace("{id}", id.ToString()).Replace("{memberId}", memberId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void DemoteMember(int id, int memberId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildDemote.Replace("{id}", id.ToString()).Replace("{memberId}", memberId.ToString());
            ApiClient.Instance.PostEmpty(url, onSuccess, onError, requiresAuth: true);
        }

        public static void TransferLeader(int id, int newLeaderProfileId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildTransferLeader.Replace("{id}", id.ToString());
            var payload = new TransferLeaderRequest { newLeaderProfileId = newLeaderProfileId };
            ApiClient.Instance.Post(url, payload, onSuccess, onError, requiresAuth: true);
        }

        // ─── Settings ─────────────────────────────────────────────────────

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

        public static void UpdateNotice(int id, string notice, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildNotice.Replace("{id}", id.ToString());
            var payload = new ChangeNoticeRequest { notice = notice };
            ApiClient.Instance.Put(url, payload, onSuccess, onError, requiresAuth: true);
        }

        public static void UpdateIcon(int id, int iconId, int? bannerId, Action<object> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildIcon.Replace("{id}", id.ToString());
            var payload = new ChangeIconRequest { iconId = iconId, bannerId = bannerId };
            ApiClient.Instance.Put(url, payload, onSuccess, onError, requiresAuth: true);
        }

        // ─── Donate ───────────────────────────────────────────────────────

        public static void Donate(int id, int amount, Action<GuildDonateResultDto> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildDonate.Replace("{id}", id.ToString());
            var payload = new DonateRequest { amount = amount };
            ApiClient.Instance.Post(url, payload, onSuccess, onError, requiresAuth: true);
        }

        // ─── Logs & Chat ──────────────────────────────────────────────────

        public static void GetLogs(int id, Action<List<GuildLogDto>> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildLogs.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        public static void GetChat(int id, Action<List<GuildMessageDTO>> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildChat.Replace("{id}", id.ToString());
            ApiClient.Instance.Get(url, onSuccess, onError, requiresAuth: true);
        }

        public static void SendChat(int id, string content, Action<GuildMessageDTO> onSuccess, Action<ApiException> onError)
        {
            string url = ApiConfig.GuildChat.Replace("{id}", id.ToString());
            var payload = new SendGuildMessageRequest { content = content };
            ApiClient.Instance.Post(url, payload, onSuccess, onError, requiresAuth: true);
        }
    }
}
