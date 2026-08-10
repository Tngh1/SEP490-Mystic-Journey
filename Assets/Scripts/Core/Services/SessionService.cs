using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.Core.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MysticJourney.Core.Services
{
    /// <summary>
    /// Kết thúc phiên đăng nhập và quay về màn hình đăng nhập (MainMenuScene).
    /// Gom về một chỗ vì có nhiều nút Logout (Settings, PlayerProfilePanel) — mỗi nơi tự viết
    /// một kiểu là chắc chắn lệch nhau.
    /// </summary>
    public static class SessionService
    {
        private static bool _loggingOut;

        // Set bởi các nơi bị BUỘC logout (session hết hạn/bị đè, mất kết nối) để LoginUIManager
        // hiện lý do sau khi LoadScene xong — logout do người chơi tự bấm không truyền reason,
        // nên MainMenuScene tải bình thường không hiện popup nào.
        public static string PendingLogoutReason { get; private set; }

        public static void ClearPendingLogoutReason()
        {
            PendingLogoutReason = null;
        }

        public static void Logout(string reason = null)
        {
            // Bấm 2 lần liên tiếp: lần 2 phải bị bỏ qua, nếu không sẽ gọi LoadScene giữa lúc
            // request logout đầu đang bay và cảnh mới bị load hai lần.
            if (_loggingOut) return;
            _loggingOut = true;

            if (!string.IsNullOrEmpty(reason))
                PendingLogoutReason = reason;

            Debug.Log("[SessionService] Logging out...");

            // Rời phòng Photon TRƯỚC khi mất token: presence/party của tài khoản cũ còn treo
            // trong lobby thì người khác vẫn thấy mình online sau khi đã đăng xuất.
            if (PhotonManager.Instance != null)
                PhotonManager.Instance.Shutdown(notify: false);

            // Gọi API logout khi token CÒN hiệu lực (ApiClient đọc token ở thời điểm gửi request,
            // nên ClearToken trước là gửi đi một request không có Authorization → server không bao
            // giờ thu hồi refresh token). AuthApi tự ClearToken + reset GameState ở CẢ hai nhánh
            // success/error, nên hỏng mạng vẫn đăng xuất được ở phía client.
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

        private static void FinishLogout()
        {
            // Vị trí map đã cache của tài khoản cũ: không xoá thì tài khoản kế tiếp đăng nhập vào
            // sẽ spawn ở đúng chỗ người trước đứng. (MapPositionCache.Clear vốn có sẵn cho mục
            // đích này nhưng trước giờ không có ai gọi.)
            MapPositionCache.Clear();

            // Các manager DontDestroyOnLoad giữ dữ liệu THEO TÀI KHOẢN. LoadScene không xoá được
            // chúng, và Awake của bản mới trong Main sẽ tự Destroy chính nó khi thấy Instance cũ
            // còn sống → tài khoản mới dùng lại cache quest/bestiary của tài khoản cũ.
            //
            // Dùng FindObjects... chứ KHÔNG dùng property Instance: getter của MonsterManager tự
            // TẠO một GameObject mới khi chưa có instance, nên chỉ đọc Instance lúc dọn dẹp là
            // sinh ra đúng cái mình đang muốn xoá.
            DestroyAll<QuestManager>();
            DestroyAll<MonsterManager>();
            DestroyAll<DungeonManager>();

            _loggingOut = false;

            Debug.Log($"[SessionService] Logged out. Loading {GameConstants.Scenes.MainMenu}.");
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
        }

        // ponytail: chỉ dọn 3 manager giữ dữ liệu theo tài khoản. ApiClient/AudioManager/các
        // BaseApiService phải sống để còn gọi được API đăng nhập lại. Nếu sau này thêm manager
        // DontDestroyOnLoad có cache theo tài khoản thì thêm vào đây.
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
