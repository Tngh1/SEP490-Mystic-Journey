using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API
{
    public class TestAPI : MonoBehaviour
    {
        [Header("Login Credentials")]
        [SerializeField] private string testEmailOrUsername = "player@example.com";
        [SerializeField] private string testPassword = "password123";

        [Header("Options")]
        [SerializeField] private bool runOnStart = true;

        void Start()
        {
            if (runOnStart)
                RunFullTest();
        }

        [ContextMenu("Run Full API Test")]
        public void RunFullTest()
        {
            Debug.Log("========== [TestAPI] START ==========");
            Test_LoginGame();
        }

        private void Test_LoginGame()
        {
            Debug.Log("[TestAPI] 1. LoginGame...");

            AuthApi.Instance.LoginGame(
                testEmailOrUsername,
                testPassword,
                response =>
                {
                    Debug.Log($"[TestAPI] LoginGame OK | User={response.UserName} | AccountId={response.AccountId} | PlayerProfileId={response.PlayerProfileId} | DisplayName={response.PlayerDisplayName}");
                    Test_GetMe();
                },
                error => Debug.LogError($"[TestAPI] LoginGame FAIL: {error}")
            );
        }

        private void Test_GetMe()
        {
            Debug.Log("[TestAPI] 2. GetMe...");

            AuthApi.Instance.GetMe(
                response =>
                {
                    Debug.Log($"[TestAPI] GetMe OK | UserName={response.UserName} | Email={response.Email} | Role={response.Role} | LastMap={response.LastMapName}");
                    Test_GetMyProfile();
                },
                error => Debug.LogError($"[TestAPI] GetMe FAIL: {error}")
            );
        }

        private void Test_GetMyProfile()
        {
            Debug.Log("[TestAPI] 3. GetMyProfile...");

            PlayerApi.Instance.GetMyProfile(
                response =>
                {
                    Debug.Log($"[TestAPI] GetMyProfile OK | DisplayName={response.DisplayName} | Class={response.PlayerClass} | Level={response.Level} | Gold={response.Gold} | Gems={response.Gems} | Energy={response.Energy}");
                    Test_GetMyInventory();
                },
                error => Debug.LogError($"[TestAPI] GetMyProfile FAIL: {error}")
            );
        }

        private void Test_GetMyInventory()
        {
            Debug.Log("[TestAPI] 4. GetMyInventory...");

            PlayerApi.Instance.GetMyInventory(
                response =>
                {
                    if (response.Success && response.Data != null)
                    {
                        var inv = response.Data;
                        Debug.Log($"[TestAPI] GetMyInventory OK | TotalItems={inv.TotalItems} | TotalSkins={inv.TotalSkins} | BagCapacity={inv.BagCapacity}");

                        if (inv.EquippedItems != null)
                            foreach (var item in inv.EquippedItems)
                                Debug.Log($"  [EQUIPPED] {item.ItemName} ({item.ItemRarity}) slot={item.EquippedSlot} +{item.EnhancementLevel}");

                        if (inv.BagItems != null)
                            foreach (var item in inv.BagItems)
                                Debug.Log($"  [BAG] {item.ItemName} ({item.ItemRarity}) x{item.Quantity}");
                    }
                    else
                    {
                        Debug.LogWarning($"[TestAPI] GetMyInventory | success={response.Success} | message={response.Message}");
                    }
                    Test_GetAllDungeons();
                },
                error => Debug.LogError($"[TestAPI] GetMyInventory FAIL: {error}")
            );
        }

        private void Test_GetAllDungeons()
        {
            Debug.Log("[TestAPI] 5. GetAllDungeons...");

            DungeonApi.Instance.GetAll(
                1, 5,
                response =>
                {
                    Debug.Log($"[TestAPI] GetAllDungeons OK | TotalCount={response.TotalCount} | Page={response.Page}/{response.TotalPages}");
                    if (response.Items != null)
                        foreach (var d in response.Items)
                            Debug.Log($"  [{d.Type}] {d.Name} | LvReq={d.LevelRequirement} | Difficulty={d.Difficulty} | Members={d.MaxMembers}");
                    Test_GetAllQuests();
                },
                error => Debug.LogError($"[TestAPI] GetAllDungeons FAIL: {error}")
            );
        }

        private void Test_GetAllQuests()
        {
            Debug.Log("[TestAPI] 6. GetAllQuests...");

            QuestApi.Instance.GetAll(
                1, 5,
                response =>
                {
                    Debug.Log($"[TestAPI] GetAllQuests OK | TotalCount={response.TotalCount}");
                    if (response.Items != null)
                        foreach (var q in response.Items)
                            Debug.Log($"  [{q.Type}] {q.Title} | LvReq={q.RequiredLevel} | Exp={q.RewardExperience} | Gold={q.RewardGold}");
                    Test_GetMyMails();
                },
                error => Debug.LogError($"[TestAPI] GetAllQuests FAIL: {error}")
            );
        }

        private void Test_GetMyMails()
        {
            Debug.Log("[TestAPI] 7. GetMyMails...");

            PlayerApi.Instance.GetMyMails(
                result =>
                {
                    Debug.Log($"[TestAPI] GetMyMails OK | Count={result?.TotalCount ?? 0} | Unread={result?.UnreadCount ?? 0}");
                    if (result?.Mails != null)
                        foreach (var m in result.Mails)
                            Debug.Log($"  [{m.Type}] {m.Title} | Read={m.IsRead} | Claimed={m.IsClaimed} | Gold={m.AttachedGold} | Gems={m.AttachedGems}");
                    Test_GetDailyLoginRewards();
                },
                error => Debug.LogError($"[TestAPI] GetMyMails FAIL: {error}")
            );
        }

        private void Test_GetDailyLoginRewards()
        {
            Debug.Log("[TestAPI] 8. GetDailyLoginRewards...");

            DailyLoginApi.Instance.GetAll(
                1, 30,
                response =>
                {
                    Debug.Log($"[TestAPI] GetDailyLoginRewards OK | TotalCount={response.TotalCount}");
                    if (response.Items != null)
                        foreach (var r in response.Items)
                            Debug.Log($"  Day {r.DayNumber} | Type={r.RewardType} | Value={r.RewardValue} | ItemId={r.RewardItemId}");
                    Test_GetAllGachaBanners();
                },
                error => Debug.LogError($"[TestAPI] GetDailyLoginRewards FAIL: {error}")
            );
        }

        private void Test_GetAllGachaBanners()
        {
            Debug.Log("[TestAPI] 9. GetAllGachaBanners...");

            GachaApi.Instance.GetAll(
                1, 10,
                response =>
                {
                    Debug.Log($"[TestAPI] GetAllGachaBanners OK | TotalCount={response.TotalCount}");
                    if (response.Items != null)
                        foreach (var b in response.Items)
                            Debug.Log($"  [{b.Type}] {b.Name} | Cost={b.PullCost} | Pity={b.PityLimit} | Active={b.IsActive}");
                    Test_Logout();
                },
                error => Debug.LogError($"[TestAPI] GetAllGachaBanners FAIL: {error}")
            );
        }

        private void Test_Logout()
        {
            Debug.Log("[TestAPI] 10. Logout...");

            AuthApi.Instance.Logout(
                response =>
                {
                    Debug.Log($"[TestAPI] Logout OK | message={response.message}");
                    Debug.Log("========== [TestAPI] DONE ==========");
                },
                error =>
                {
                    Debug.LogWarning($"[TestAPI] Logout server error (token cleared locally): {error}");
                    Debug.Log("========== [TestAPI] DONE ==========");
                }
            );
        }

        [ContextMenu("Test: Check Token Status")]
        public void Test_CheckToken()
        {
            string token = ApiClient.Instance.GetToken();
            Debug.Log($"[TestAPI] HasToken={ApiClient.Instance.HasToken()}");
            if (!string.IsNullOrEmpty(token))
                Debug.Log($"[TestAPI] Token={token.Substring(0, Mathf.Min(40, token.Length))}...");
            Debug.Log($"[TestAPI] PlayerProfileId={PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0)}");
            Debug.Log($"[TestAPI] UserName={PlayerPrefs.GetString(ApiConfig.UserNameKey, "(empty)")}");
        }

        [ContextMenu("Test: Clear Token")]
        public void Test_ClearToken()
        {
            ApiClient.Instance.ClearToken();
            Debug.Log("[TestAPI] Token cleared.");
        }

        [ContextMenu("Test: Get Dungeon By ID (id=1)")]
        public void Test_GetDungeonById()
        {
            DungeonApi.Instance.GetById(
                1,
                d => Debug.Log($"[TestAPI] Dungeon | {d.Name} | {d.Type} | Difficulty={d.Difficulty} | LvReq={d.LevelRequirement}"),
                e => Debug.LogError($"[TestAPI] {e}")
            );
        }

        [ContextMenu("Test: Get Quest By ID (id=1)")]
        public void Test_GetQuestById()
        {
            QuestApi.Instance.GetById(
                1,
                q => Debug.Log($"[TestAPI] Quest | {q.Title} | {q.Type} | Gold={q.RewardGold} | Exp={q.RewardExperience}"),
                e => Debug.LogError($"[TestAPI] {e}")
            );
        }

        [ContextMenu("Test: Get Gacha Banner By ID (id=1)")]
        public void Test_GetGachaBannerById()
        {
            GachaApi.Instance.GetById(
                1,
                b =>
                {
                    Debug.Log($"[TestAPI] GachaBanner | {b.Name} | {b.Type} | Cost={b.PullCost} | Pity={b.PityLimit}");
                    if (b.BannerItems != null)
                        foreach (var item in b.BannerItems)
                            Debug.Log($"  Item | {item.ItemName} ({item.ItemRarity}) | DropRate={item.DropRate}% | Featured={item.IsFeatured}");
                },
                e => Debug.LogError($"[TestAPI] {e}")
            );
        }

        [ContextMenu("Test: Get My Mails")]
        public void Test_GetMyMailsManual()
        {
            PlayerApi.Instance.GetMyMails(
                result =>
                {
                    Debug.Log($"[TestAPI] Mails count={result?.TotalCount ?? 0} | Unread={result?.UnreadCount ?? 0}");
                    if (result?.Mails != null)
                        foreach (var m in result.Mails)
                            Debug.Log($"  Mail#{m.MailId} [{m.Type}] {m.Title} | SentAt={m.SentAt} | ExpiredAt={m.ExpiredAt}");
                },
                e => Debug.LogError($"[TestAPI] {e}")
            );
        }
    }
}
