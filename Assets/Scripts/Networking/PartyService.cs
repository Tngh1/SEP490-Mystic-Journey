using Fusion; // Sử dụng thư viện mạng Photon Fusion
using UnityEngine; // Sử dụng thư viện lõi của Unity
using MysticJourney.Core.Utilities;

/// <summary>
/// Stateless facade cho mọi hoạt động của Party (tổ đội). Đây là điểm duy nhất để UI giao tiếp
/// giúp không rò rỉ logic nghiệp vụ ra các bảng/popup UI. Nó điều phối 3 thành phần mạng:
///   • <see cref="PhotonManager"/>  — xử lý kết nối và sinh đối tượng PartyLobby.
///   • <see cref="PartyLobby"/>     — quản lý danh sách thành viên / trạng thái / các hàm RPC.
///   • <see cref="PlayerPresence"/> — định danh của mỗi người chơi + hộp thư nhận lời mời.
///
/// Mọi thứ ở đây chỉ là một lớp dịch thuật mỏng từ ý định ("mời bạn này") sang lời gọi mạng đúng;
/// không có trạng thái cục bộ nào bị thay đổi ở đây — trạng thái chuẩn (authoritative state)
/// nằm trên các đối tượng của Fusion. UI đọc trạng thái thông qua PartyLobby.Local + các event của nó.
/// </summary>
public static class PartyService
{
    /// <summary>Party hiện tại mà người chơi đang tham gia, trả về null nếu không có.</summary>
    public static PartyLobby CurrentParty => PartyLobby.Local; // Lấy tham chiếu đến PartyLobby của bản thân

    /// <summary>Đúng (true) khi người chơi đang trong một party và là chủ phòng (host) của nó.</summary>
    public static bool IsHost => PartyLobby.Local != null && PartyLobby.Local.IsLocalHost; // Kiểm tra quyền chủ phòng

    /// <summary>Đúng (true) khi đang kết nối vào sảnh xã hội chung (có thể mời/tạo party).</summary>
    public static bool IsOnline => PhotonManager.Instance != null && PhotonManager.Instance.IsConnected; // Kiểm tra trạng thái mạng

    // ─────────────────────────────────────────────────────────────────────────
    // 24.3 Create Party (Tạo tổ đội)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo một party do người chơi hiện tại làm chủ. Yêu cầu phải đang kết nối vào sảnh xã hội.
    /// Trả về party vừa tạo (hoặc party hiện tại nếu đã ở trong 1 party), hoặc null nếu offline/sinh lỗi.
    /// </summary>
    public static PartyLobby CreateParty()
    {
        if (!IsOnline) // Kiểm tra xem có đang online không
        {
            Debug.LogWarning("[PartyService] CreateParty ignored — not connected to social lobby."); // Cảnh báo nếu chưa kết nối sảnh
            return null; // Không tạo được party
        }
        return PhotonManager.Instance.CreateParty(); // Gọi sang PhotonManager để sinh (spawn) PartyLobby
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 24.4 Invite Player (Mời người chơi)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Lý do một lời mời không thể gửi được. UI cần enum này vì "bạn đang offline" 
    /// và "bạn của bạn đang offline" nhìn giống hệt nhau nếu chỉ dùng kiểu bool.</summary>
    public enum InviteResult
    {
        Sent,             // Đã gửi thành công
        NotConnected,     // Client hiện tại chưa kết nối sảnh / chưa có mặt trong sảnh
        FriendOffline,    // Người bạn kia không có mặt trong sảnh này
        PartyUnavailable, // Không thể tạo được party, hoặc mình không phải là chủ phòng
        PartyFull,        // Party đã đầy người
        MapLocked,        // Người được mời chưa mở map chứa dungeon này
    }

    /// <summary>
    /// Mời một người bạn đang online thông qua profile id. Người bạn đó phải đang có mặt
    /// trong sảnh xã hội (tức là có <see cref="PlayerPresence"/> đang hoạt động). Tự động tạo
    /// party nếu người mời chưa có party.
    /// </summary>
    public static InviteResult InviteByProfileId(
        int friendProfileId,
        int requiredMapId = MapProgressionRules.FirstMapId)
    {
        if (!IsOnline) return InviteResult.NotConnected; // Nếu mình chưa online thì báo lỗi NotConnected

        // Kiểm tra xem bạn bè có thể liên lạc được không TRƯỚC KHI tạo party, 
        // tránh việc lời mời lỗi mà chủ phòng bị kẹt lại trong một party trống.
        var target = PlayerPresence.Find(friendProfileId); // Tìm thông tin người bạn trên mạng
        if (target == null) // Nếu không tìm thấy
        {
            Debug.Log($"[PartyService] Invite failed — friend {friendProfileId} is not online in the lobby."); // Log lỗi bạn offline
            return InviteResult.FriendOffline; // Trả về kết quả bạn offline
        }

        var me = PlayerPresence.Local; // Lấy thông tin bản thân mình
        if (me == null) return InviteResult.NotConnected; // Nếu chưa có thông tin bản thân -> lỗi chưa kết nối

        if (!MapProgressionRules.CanInviteToMap(requiredMapId, target.HighestUnlockedMapId))
        {
            Debug.Log($"[PartyService] Invite failed: friend {friendProfileId} has unlocked map " +
                      $"{target.HighestUnlockedMapId}, but map {requiredMapId} is required.");
            return InviteResult.MapLocked;
        }

        var party = CurrentParty ?? CreateParty(); // Lấy party hiện tại, nếu chưa có thì tạo mới
        if (party == null || !party.IsLocalHost) return InviteResult.PartyUnavailable; // Nếu party null hoặc mình không làm chủ -> báo lỗi
        if (party.MemberCount >= PartyLobby.MaxMembers) return InviteResult.PartyFull; // Nếu số thành viên quá giới hạn -> báo party đầy

        target.RPC_ReceiveInvite(me.ProfileId, me.DisplayName); // Gửi RPC (Remote Procedure Call) mời người kia qua mạng
        party.RegisterPendingInvite(); // Đăng ký (đếm) 1 lời mời đang chờ xử lý trong party
        return InviteResult.Sent; // Báo gửi lời mời thành công
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 24.6 Join Party (accept) / decline (Chấp nhận / Từ chối vào Party)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Chấp nhận một lời mời từ chủ phòng thông qua PROFILE id. Bởi vì party nằm
    /// trong phòng chung nên người được mời ĐÃ ĐƯỢC KẾT NỐI SẴN, chỉ cần đăng ký
    /// bản thân vào danh sách của chủ phòng — không cần kết nối lại. Trả về false nếu không tìm thấy party.
    ///
    /// Khóa (Key) dựa trên profile id, không phải PlayerRef vì: Fusion tái sử dụng PlayerRefs 
    /// khi người chơi ra vào. Nếu dùng PlayerRef, slot rỗng có thể bị người lạ chiếm trước khi 
    /// bạn kịp bấm Chấp Nhận, khiến bạn vào nhầm party của người lạ đó.
    /// </summary>
    public static bool AcceptInvite(int hostProfileId)
    {
        if (!IsOnline) return false; // Nếu chưa online thì không thể chấp nhận mời

        var party = FindPartyByHostProfileId(hostProfileId); // Tìm Party của người đã mời mình bằng profile id của họ
        if (party == null) // Nếu không tìm thấy
        {
            Debug.LogWarning("[PartyService] AcceptInvite — host's party no longer exists."); // Báo cảnh báo party không còn tồn tại
            return false; // Thất bại
        }

        var runner = PhotonManager.Instance.Runner; // Lấy bộ kết nối mạng
        var me = PlayerPresence.Local; // Lấy thông tin bản thân
        if (runner == null || me == null) return false; // Nếu thiếu 1 trong 2 -> Thất bại

        // Gọi lệnh RPC xin vào party, gửi kèm các thông tin hiển thị của bản thân (Tên, Class, Level, Skin)
        party.RPC_Join(runner.LocalPlayer, me.ProfileId, me.DisplayName, me.PlayerClass, me.Level, WorldState.EquippedSkinId);
        return true; // Chấp nhận lời mời thành công
    }

    /// <summary>Từ chối lời mời từ một chủ phòng (giảm biến đếm số lời mời đang chờ của họ). 
    /// Dùng Profile id vì lý do tái sử dụng PlayerRef tương tự như <see cref="AcceptInvite"/>.</summary>
    public static void DeclineInvite(int hostProfileId)
    {
        FindPartyByHostProfileId(hostProfileId)?.RPC_InviteResolved(); // Tìm party của host đó, nếu có thì gọi hàm hủy chờ lời mời
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 24.5 Kick / 24.7 Leave / 24.8 Ready (Đuổi / Rời / Sẵn sàng)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Chỉ dành cho Host: Kích (đuổi) một thành viên thông qua PlayerRef của họ.</summary>
    public static void KickMember(PlayerRef target)
    {
        var party = CurrentParty; // Lấy party hiện tại
        if (party == null || !party.IsLocalHost) return; // Nếu không có hoặc mình không phải là host thì bỏ qua
        var runner = PhotonManager.Instance?.Runner; // Lấy NetworkRunner
        if (runner == null) return; // Nếu runner null thì bỏ qua
        party.RPC_Kick(runner.LocalPlayer, target); // Gọi hàm RPC để kích thành viên đó khỏi party
    }

    /// <summary>
    /// Rời khỏi party hiện hành. Một thành viên bình thường sẽ tự xóa slot của họ; 
    /// nếu là chủ phòng (host) thì sẽ giải tán toàn bộ party cho tất cả mọi người (xem <see cref="PartyLobby.LeaveAsHost"/>).
    /// </summary>
    public static void LeaveParty()
    {
        var party = CurrentParty; // Lấy party hiện tại
        if (party == null) return; // Nếu không trong party nào thì bỏ qua
        var runner = PhotonManager.Instance?.Runner; // Lấy bộ kết nối
        if (runner == null) return; // Runner lỗi thì bỏ qua

        if (party.IsLocalHost) // Nếu mình là chủ phòng
            party.LeaveAsHost(); // Giải tán toàn bộ party
        else // Nếu mình là thành viên thường
            party.RPC_Leave(runner.LocalPlayer); // Gọi lệnh RPC xin phép rời đi
    }

    /// <summary>Đặt cờ sẵn sàng (ready) cho người chơi. Chủ phòng mặc định luôn luôn sẵn sàng.</summary>
    public static void SetReady(bool ready)
    {
        var party = CurrentParty; // Lấy party hiện tại
        if (party == null) return; // Không có thì bỏ qua
        var runner = PhotonManager.Instance?.Runner; // Lấy runner
        if (runner == null) return; // Runner lỗi thì bỏ qua
        party.RPC_SetReady(runner.LocalPlayer, ready); // Gọi lệnh RPC thông báo trạng thái sẵn sàng cho cả party
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Start Dungeon (host) (Bắt đầu Hầm ngục - chủ phòng)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Chỉ dành cho Host: Công khai hầm ngục đang chọn cho party để mọi 
    /// bảng điều khiển của thành viên đều thấy (24.2). An toàn không lỗi nếu chưa vào party hoặc không phải host.
    /// </summary>
    public static void SetDungeon(int configId, string sceneName, string dungeonName)
    {
        var party = CurrentParty; // Lấy party
        if (party == null || !party.IsLocalHost) return; // Chỉ cho phép chủ phòng
        party.HostSetDungeon(configId, sceneName, dungeonName); // Đồng bộ cấu hình hầm ngục (ID, tên màn, tên hầm ngục)
    }

    /// <summary>
    /// Chỉ dành cho Host: Yêu cầu bắt đầu vào hầm ngục. PartyLobby sẽ kiểm tra điều kiện (≥2 người,
    /// tất cả đã sẵn sàng, không còn lời mời chờ) và chuyển trạng thái State→Loading. Bước 5 gắn việc
    /// load scene + gọi API Enter vào <see cref="PartyLobby.OnDungeonStartRequested"/>.
    /// </summary>
    public static void StartDungeon(int configId, string sceneName)
    {
        var party = CurrentParty; // Lấy party
        if (party == null || !party.IsLocalHost) return; // Chỉ cho phép chủ phòng
        party.HostStartDungeon(configId, sceneName); // Gọi hàm phía host để xác nhận bắt đầu hầm ngục
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers (Các hàm hỗ trợ)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Tìm một party đang tồn tại mà chủ phòng có mã profile id truyền vào, hoặc null nếu không thấy.</summary>
    public static PartyLobby FindPartyByHostProfileId(int hostProfileId)
    {
        if (hostProfileId <= 0) return null; // ID không hợp lệ thì trả về null
        foreach (var p in PartyLobby.All) // Lặp qua tất cả các party trên mạng
            if (p != null && p.HostProfileId == hostProfileId) return p; // Nếu tìm thấy party do người đó tạo, trả về party đó
        return null; // Trả về null nếu tìm hết không thấy
    }
}
