using System;
using System.Collections.Generic;
using Fusion;
using MysticJourney.API.Models.Response;
using UnityEngine;

/// <summary>
/// Danh sách thành viên (roster) dùng chung cho party trước khi vào hầm ngục. 
/// Chỉ được sinh ra (Spawn) DUY NHẤT một lần bởi chủ phòng (master client) 
/// thông qua <see cref="PhotonManager.EnsurePartyLobbySpawned"/> ngay sau khi vào phòng mạng của party. 
/// Mọi client khác sẽ đọc danh sách thành viên được đồng bộ này để vẽ giao diện các ô trống 
/// và trạng thái sẵn sàng (ready) trong <see cref="UIPartyPanel"/>.
///
/// Mô hình thẩm quyền (Authority model - Shared Mode): chủ phòng giữ quyền quản lý trạng thái (StateAuthority) 
/// trên đối tượng này và là người duy nhất có quyền thay đổi các mảng có nhãn [Networked]. 
/// Các client khác muốn thay đổi (tham gia / sẵn sàng / đuổi / rời đi) phải gửi yêu cầu thông qua RPC 
/// tới người giữ StateAuthority.
///
/// Vòng đời của Party:
///   Lobby (Sảnh chờ) → Đang gom người, mời bạn bè, kiểm tra sẵn sàng.
///   Loading (Đang tải) → Chủ phòng đã bấm Bắt đầu (Start); cả nhóm đang trong quá trình chuyển map vào hầm ngục.
///   InDungeon (Trong hầm ngục) → Phiên chơi hầm ngục đang diễn ra (đã sinh nhân vật, đang chiến đấu).
///
/// Party này tồn tại bên trong phòng chung của SẢNH XÃ HỘI (xem PhotonManager) nên một chủ phòng 
/// vẫn có thể mời những người bạn đang đứng rảnh rỗi thông qua <see cref="PlayerPresence"/>.
/// </summary>
public class PartyLobby : NetworkBehaviour, IStateAuthorityChanged
{
    public const int MaxMembers = 4; // Số lượng thành viên tối đa

    public enum PartyState { Lobby, Loading, InDungeon } // Các trạng thái của party

    /// <summary>
    /// Tất cả các party đang hoạt động trong phòng hiện tại. Do phòng sảnh xã hội chung
    /// có thể chứa nhiều party cùng lúc, nên biến này là một danh sách (Dictionary), không phải dạng Singleton.
    /// Khóa (Key) là NetworkId của đối tượng PartyLobby.
    /// </summary>
    private static readonly Dictionary<NetworkId, PartyLobby> _all = new();

    /// <summary>Chế độ xem chỉ đọc (Read-only) danh sách tất cả các party đang hoạt động trong phòng.</summary>
    public static IReadOnlyCollection<PartyLobby> All => _all.Values;

    /// <summary>
    /// Party hiện tại mà người chơi đang tham gia (tự tạo hoặc tham gia của người khác), hoặc null nếu chưa có.
    /// Được gán giá trị khi người chơi chiếm một vị trí trong danh sách; và bị xóa (null) khi họ rời đi.
    /// </summary>
    public static PartyLobby Local { get; private set; }

    /// <summary>Sự kiện được gọi mỗi khi <see cref="Local"/> thay đổi (khi người chơi vào/ra một party).</summary>
    public static event Action OnLocalPartyChanged;

    /// <summary>Dữ liệu của một thành viên trong party. Là dạng NetworkStruct để có thể lưu trữ trong NetworkArray.</summary>
    public struct Member : INetworkStruct
    {
        public PlayerRef Player;          // Mã định danh người chơi, nếu = default(PlayerRef) nghĩa là ô trống
        public int ProfileId;             // Mã profile trên hệ thống Backend
        public NetworkString<_32> Name;   // Tên người chơi
        public int PlayerClass;           // Lớp nhân vật (vd: Hiệp sĩ, Pháp sư...)
        public int Level;                 // Cấp độ người chơi
        public NetworkBool Ready;         // Trạng thái đã sẵn sàng chưa
        public int SkinId;                // ID trang phục đang mặc — dùng để vẽ hình đại diện

        // Hàm kiểm tra xem slot này đã có người chiếm chưa
        public bool IsOccupied => Player != default; 
    }

    [Networked, Capacity(MaxMembers)] // Đánh dấu mảng này được đồng bộ qua mạng, chứa tối đa MaxMembers
    public NetworkArray<Member> Members => default;

    [Networked] public PlayerRef HostPlayer { get; set; } // Người chơi nào đang làm chủ phòng

    [Networked] public PartyState State { get; set; } // Trạng thái hiện tại của party

    /// <summary>
    /// Số lượng lời mời đã được gửi đi nhưng chưa được chấp nhận hay từ chối. 
    /// Chỉ do máy chủ phòng (host) quản lý.
    /// Dùng để làm điều kiện chặn không cho "Bắt đầu Hầm ngục" nếu vẫn còn "lời mời đang chờ". 
    /// Nó chỉ là một biến đếm đơn giản — khi có một lượt chấp nhận (<see cref="RPC_Join"/>) 
    /// hoặc từ chối (<see cref="RPC_InviteResolved"/>) thì sẽ trừ bớt biến này đi.
    /// </summary>
    [Networked] public int PendingInviteCount { get; set; }

    // Thông tin về hầm ngục sẽ đánh, được chủ phòng cập nhật để mọi client cùng load
    // chung một scene/session giống nhau. Các thành viên sẽ tái sử dụng DungeonSessionId của host
    // thay vì phải gọi lại API Enter lên Backend (tránh bị trùng lặp phía máy chủ).
    [Networked] public int DungeonConfigId { get; set; } // ID cấu hình hầm ngục
    [Networked] public int DungeonSessionId { get; set; } // ID phiên đánh hầm ngục do backend cấp
    [Networked] public NetworkString<_32> DungeonSceneName { get; set; } // Tên màn hình (Scene) sẽ load
    [Networked] public NetworkString<_32> DungeonName { get; set; } // Tên hiển thị của hầm ngục

    /// <summary>Sự kiện được gọi trên tất cả các client mỗi khi danh sách thành viên/chủ phòng có thay đổi.</summary>
    public event Action OnRosterChanged;

    /// <summary>Sự kiện được gọi trên tất cả các client mỗi khi trạng thái <see cref="State"/> thay đổi.</summary>
    public event Action<PartyState> OnPartyStateChanged;

    /// <summary>
    /// Sự kiện chỉ kích hoạt trên máy chủ phòng khi party sắp bước vào hầm ngục 
    /// (Trạng thái vừa chuyển sang Loading). Bước 5 sẽ nối sự kiện này với DungeonManager. 
    /// Tham số truyền vào: configId, sceneName.
    /// </summary>
    public event Action<int, string> OnDungeonStartRequested;

    public event Action<PartyChatMessageResponse> PartyMessageReceived; // Sự kiện khi nhận được tin nhắn chat trong party

    private ChangeDetector _changes; // Bộ dò thay đổi thuộc tính mạng của Fusion

    public override void Spawned()
    {
        _all[Object.Id] = this; // Đăng ký party này vào danh sách tổng
        _changes = GetChangeDetector(ChangeDetector.Source.SnapshotFrom); // Khởi tạo bộ dò thay đổi

        if (HasStateAuthority) // Nếu mình là người tạo party (chủ phòng)
        {
            HostPlayer = Object.InputAuthority; // Lưu lại người tạo là chủ
            State = PartyState.Lobby; // Gán trạng thái ban đầu là Sảnh chờ
            // Gán thông tin cá nhân của chủ phòng vào vị trí số 0 trong danh sách
            SeatSelf(0, ready: true); 
        }

        RefreshLocalMembership(); // Cập nhật lại xem bản thân mình có thuộc party này không
        OnRosterChanged?.Invoke(); // Gọi sự kiện báo giao diện cập nhật danh sách
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _all.Remove(Object.Id); // Gỡ party khỏi danh sách
        if (Local == this) // Nếu đây là party mình đang tham gia
        {
            Local = null; // Xóa trạng thái party của mình
            OnLocalPartyChanged?.Invoke(); // Báo cập nhật
        }
    }

    public override void Render()
    {
        // Điều phối các thay đổi dữ liệu mạng thành các sự kiện UI tương ứng
        foreach (var change in _changes.DetectChanges(this))
        {
            if (change == nameof(State))
                OnPartyStateChanged?.Invoke(State);
            else
                OnRosterChanged?.Invoke();
        }

        RefreshLocalMembership(); // Liên tục kiểm tra xem mình còn ở trong party không
    }

    /// <summary>
    /// Giữ cho con trỏ tĩnh <see cref="Local"/> luôn đồng bộ với trạng thái thành viên:
    /// Party này sẽ trở thành Local (party của mình) khi mình được xếp vào danh sách, 
    /// và sẽ bị xóa khi mình không còn nằm trong danh sách nữa.
    /// </summary>
    private void RefreshLocalMembership()
    {
        if (Runner == null) return;

        bool localSeated = FindSlot(Runner.LocalPlayer) >= 0; // Tìm xem mình có vị trí trong mảng Members không

        if (localSeated && Local != this)
        {
            Local = this; // Cập nhật Local party
            OnLocalPartyChanged?.Invoke(); // Thông báo
        }
        else if (!localSeated && Local == this)
        {
            Local = null; // Xóa Local party
            OnLocalPartyChanged?.Invoke(); // Thông báo
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Local helpers (Các hàm hỗ trợ - Chỉ dành cho State Authority)
    // ─────────────────────────────────────────────────────────────────────────

    // Lấy thông tin hiện hành từ WorldState để đẩy vào một vị trí trong mảng
    private void SeatSelf(int slot, bool ready)
    {
        string className = WorldState.PlayerClass ?? "Knight";
        if (!Enum.TryParse<CharacterClass>(className, true, out var parsed))
            parsed = CharacterClass.Knight;

        var arr = Members;
        arr.Set(slot, new Member
        {
            Player = Object.InputAuthority,
            ProfileId = WorldState.PlayerProfileId,
            Name = WorldState.PlayerName ?? "Host",
            PlayerClass = (int)parsed,
            Level = Mathf.Max(1, WorldState.PlayerLevel),
            Ready = ready,
            SkinId = WorldState.EquippedSkinId,
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Queries (Các hàm truy vấn lấy thông tin)
    // ─────────────────────────────────────────────────────────────────────────

    public bool IsLocalHost =>
        Object != null && Runner != null && HostPlayer == Runner.LocalPlayer; // Mình có phải là chủ phòng của party này không?

    /// <summary>
    /// Lấy Profile ID của thành viên làm chủ phòng (id này không đổi kể cả khi qua phòng mạng khác), 
    /// hoặc trả về 0 nếu không tìm thấy. Dùng id này để đặt tên chung cho phòng hầm ngục, 
    /// để mọi người trong party cùng kết nối vào một phòng duy nhất.
    /// </summary>
    public int HostProfileId
    {
        get
        {
            for (int i = 0; i < MaxMembers; i++)
            {
                var m = Members[i];
                if (m.IsOccupied && m.Player == HostPlayer) return m.ProfileId;
            }
            return 0;
        }
    }

    /// <summary>Tên hiển thị của chủ phòng, hoặc chuỗi rỗng nếu không tìm thấy ô của host.</summary>
    public string HostDisplayName
    {
        get
        {
            for (int i = 0; i < MaxMembers; i++)
            {
                var m = Members[i];
                if (m.IsOccupied && m.Player == HostPlayer) return m.Name.Value;
            }
            return string.Empty;
        }
    }

    // Số lượng thành viên hiện tại có trong mảng
    public int MemberCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MaxMembers; i++)
                if (Members[i].IsOccupied) n++;
            return n;
        }
    }

    // Số lượng người đã bấm Sẵn sàng
    public int ReadyCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MaxMembers; i++)
            {
                var m = Members[i];
                if (m.IsOccupied && m.Ready) n++;
            }
            return n;
        }
    }

    /// <summary>Trả về đúng (true) khi MỌI ô có người đều đã ở trạng thái sẵn sàng (chủ phòng tính là luôn sẵn sàng).</summary>
    public bool AllReady
    {
        get
        {
            bool any = false;
            for (int i = 0; i < MaxMembers; i++)
            {
                var m = Members[i];
                if (!m.IsOccupied) continue; // Nếu ô trống thì bỏ qua
                any = true;
                if (!m.Ready) return false; // Thấy 1 người chưa sẵn sàng là trả về false ngay
            }
            return any; // Sẽ trả về true nếu có ít nhất 1 người và không ai vi phạm điều kiện trên
        }
    }

    /// <summary>
    /// Điều kiện để Chủ phòng có thể bấm Bắt đầu hầm ngục: 
    /// ít nhất 2 thành viên, tất cả đều sẵn sàng, không còn lời mời nào đang chờ phản hồi, 
    /// và party đang ở trong sảnh chờ.
    /// </summary>
    public bool CanStartDungeon =>
        PartyLifecycleRules.CanStartDungeon(true, (int)State, MemberCount, ReadyCount, PendingInviteCount);
        
    // Kiểm tra xem mình có đang thuộc party này không
    public bool IsLocalMember =>
        Runner != null && FindSlot(Runner.LocalPlayer) >= 0;

    // Gửi tin nhắn chat trong nội bộ party
    public bool BroadcastPartyMessage(PartyChatMessageResponse message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content))
        {
            return false;
        }

        if (Runner == null || !Runner.IsRunning || !IsLocalMember)
        {
            return false;
        }

        int senderId = WorldState.PlayerProfileId > 0 ? WorldState.PlayerProfileId : message.SenderId;
        if (!PartyLifecycleRules.CanUsePartyChat(IsLocalMember, HasMemberProfileId(senderId), senderId))
        {
            return false;
        }

        string senderName = !string.IsNullOrWhiteSpace(WorldState.PlayerName)
            ? WorldState.PlayerName
            : message.SenderName;

        // Giới hạn độ dài nội dung để tránh vượt quá kích thước mạng
        string networkSenderName = NetworkChatText.ClampUtf8(senderName, NetworkChatText.MaxSenderNameBytes);
        string networkContent = NetworkChatText.ClampUtf8(message.Content, NetworkChatText.MaxContentBytes);
        string networkSentAt = NetworkChatText.ClampUtf8(
            string.IsNullOrWhiteSpace(message.SentAt) ? DateTime.UtcNow.ToString("O") : message.SentAt,
            NetworkChatText.MaxTimestampBytes);

        RPC_PartyMessageReceived(senderId, networkSenderName, networkContent, networkSentAt); // Gọi RPC để phân phát tin nhắn
        return true;
    }

    // Các tham số dạng chuỗi (string) thường, KHÔNG dùng NetworkString<_N> vì NetworkString 
    // luôn chiếm toàn bộ dung lượng tĩnh của nó, trong khi string tự động cấp phát độ dài thay đổi theo UTF-8, 
    // giúp tiết kiệm kích thước gói tin gửi qua mạng (Fusion giới hạn gói gửi 512-byte).
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_PartyMessageReceived(
        int senderId,
        string senderName,
        string content,
        string sentAt)
    {
        RefreshLocalMembership();

        if (!PartyLifecycleRules.CanUsePartyChat(Local == this, HasMemberProfileId(senderId), senderId))
        {
            return;
        }

        // Bắn event để UI cập nhật tin nhắn
        PartyMessageReceived?.Invoke(new PartyChatMessageResponse
        {
            SenderId = senderId,
            SenderName = senderName ?? string.Empty,
            Content = content ?? string.Empty,
            Channel = "Party",
            SentAt = sentAt ?? string.Empty
        });
    }

    // Kiểm tra xem party có ai trùng khớp Profile ID này không
    private bool HasMemberProfileId(int profileId)
    {
        for (int i = 0; i < MaxMembers; i++)
        {
            var member = Members[i];
            if (member.IsOccupied && member.ProfileId == profileId)
            {
                return true;
            }
        }

        return false;
    }

    // Tìm xem người chơi PlayerRef này đang ở ô thứ mấy
    private int FindSlot(PlayerRef player)
    {
        for (int i = 0; i < MaxMembers; i++)
            if (Members[i].Player == player) return i;
        return -1; // Không tìm thấy
    }

    // Lấy vị trí trống đầu tiên trong party
    private int FirstFreeSlot()
    {
        for (int i = 0; i < MaxMembers; i++)
            if (!Members[i].IsOccupied) return i;
        return -1; // Hết chỗ
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Invite bookkeeping (host state authority)
    // Các hàm theo dõi số lượng lời mời - Chỉ chạy trên máy chủ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gọi nội bộ trên máy chủ phòng ngay sau khi gửi đi một lời mời 
    /// (thông qua <see cref="PlayerPresence.RPC_ReceiveInvite"/> của người nhận) 
    /// để tăng biến đếm số lượng đang chờ lên.
    /// </summary>
    public void RegisterPendingInvite()
    {
        if (!HasStateAuthority) return;
        PendingInviteCount++;
    }

    /// <summary>
    /// Yêu cầu từ Client gửi về Host: báo rằng một lời mời đã được giải quyết (nhưng không phải là đồng ý gia nhập). 
    /// Có thể là người đó từ chối, hoặc lời mời hết hạn. Host sẽ giảm số đếm này đi.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_InviteResolved()
    {
        if (!HasStateAuthority) return;
        if (PendingInviteCount > 0) PendingInviteCount--;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RPCs — yêu cầu từ Client gửi lên Host (người giữ StateAuthority) để áp dụng thay đổi
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Được gọi bởi một client khác ngay sau khi họ xin gia nhập phòng của host thành công, 
    /// nhằm đăng ký họ vào danh sách thành viên. Các thông tin định danh như tên, class, level
    /// được Client lấy từ WorldState truyền qua các tham số.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Join(PlayerRef player, int profileId, NetworkString<_32> name, int playerClass, int level, int skinId)
    {
        if (!HasStateAuthority) return;
        if (!PartyLifecycleRules.CanJoin((int)State, MemberCount, FindSlot(player) >= 0)) return;

        int slot = FirstFreeSlot(); // Tìm ô trống
        if (slot < 0) return; // Party đã đầy

        var arr = Members; // Cập nhật mảng
        arr.Set(slot, new Member
        {
            Player = player,
            ProfileId = profileId,
            Name = name,
            PlayerClass = playerClass,
            Level = level,
            Ready = false, // Vừa vào mặc định là chưa sẵn sàng
            SkinId = skinId,
        });

        // Người vừa vào lấp chỗ cho 1 lời mời đang chờ, nên ta giảm số đếm chờ đi
        if (PendingInviteCount > 0) PendingInviteCount--;
    }

    /// <summary>Bật/tắt trạng thái Sẵn sàng của thành viên. Ô của chủ phòng luôn mặc định là sẵn sàng.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetReady(PlayerRef player, NetworkBool ready)
    {
        if (!HasStateAuthority) return;
        int slot = FindSlot(player);
        if (!PartyLifecycleRules.CanChangeReady(slot >= 0, player == HostPlayer)) return;

        var m = Members[slot];
        m.Ready = ready; // Đổi trạng thái
        var arr = Members;
        arr.Set(slot, m); // Lưu lại
    }

    /// <summary>Chỉ dành cho Host: Xóa một thành viên khỏi danh sách (24.5 Đuổi người - Kick).</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Kick(PlayerRef requester, PlayerRef target)
    {
        if (!HasStateAuthority) return;
        int slot = FindSlot(target);
        if (!PartyLifecycleRules.CanKick(requester == HostPlayer, slot >= 0, target == HostPlayer)) return;
        RemoveMember(target);
    }

    /// <summary>
    /// Một Client muốn rời khỏi party (xóa ô trống của họ). Việc Host rời đi được xử lý 
    /// riêng bằng <see cref="LeaveAsHost"/> vì hành động đó sẽ giải tán toàn bộ party.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Leave(PlayerRef player)
    {
        if (!HasStateAuthority) return;
        int slot = FindSlot(player);
        if (!PartyLifecycleRules.CanLeave(slot >= 0, player == HostPlayer)) return;
        RemoveMember(player);
    }

    /// <summary>Called by PhotonManager on every peer; only StateAuthority mutates the roster.</summary>
    public void HandleNetworkPlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        // A host-owned PartyLobby is despawned by Fusion when the host leaves Shared Mode.
        // Non-host disconnects need an explicit roster cleanup on StateAuthority.
        if (player == HostPlayer) return;
        RemoveMember(player);
    }

    private bool RemoveMember(PlayerRef player)
    {
        int slot = FindSlot(player);
        if (slot < 0) return false;
        var arr = Members;
        arr.Set(slot, default);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Leave / disband (24.7 Giải tán / Thoát party)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Host rời đi = giải tán nhóm. Hàm này chạy trên Host: báo cho mọi thành viên biết
    /// party bị giải tán (<see cref="NotifyMembersDisbanded"/>), rồi hủy sinh (despawn) đối tượng party.
    /// Nhờ đó mọi thành viên khác đều nhận được <see cref="Despawned"/>, giúp họ xóa <see cref="Local"/>
    /// và gọi <see cref="OnLocalPartyChanged"/> — qua đó đóng panel UI tương ứng của họ.
    ///
    /// Quyền chủ phòng cố tình KHÔNG ĐƯỢC CHUYỂN GIAO cho thành viên còn lại: party thuộc 
    /// về người tạo ra nó. Do đó khi host rời đi, party bị giải tán hoàn toàn chứ không âm thầm 
    /// tiếp tục hoạt động dưới tên một người chủ mới mà các thành viên chưa bao giờ tự bầu.
    /// </summary>
    public void LeaveAsHost()
    {
        if (!HasStateAuthority) return;

        // Báo TRƯỚC khi despawn: xem RPC_PartyDisbanded để biết vì sao thông báo phải
        // đi trên presence của từng người thay vì trên đối tượng party này.
        NotifyMembersDisbanded();

        if (Runner != null && Object != null)
            Runner.Despawn(Object); // Hủy đối tượng mạng này
    }

    /// <summary>
    /// Gửi tin "party đã bị giải tán" tới mọi thành viên trừ chủ phòng.
    ///
    /// Nếu không có bước này thì thành viên chỉ thấy <see cref="Despawned"/> chạy và
    /// danh sách rỗng đi — GIỐNG HỆT lúc bị đuổi bằng <see cref="RPC_Kick"/> (cũng chỉ
    /// xóa ô của họ mà không nói gì). Đó chính là lý do việc host đóng party bị hiểu
    /// thành "bị kick": cơ chế giải tán vốn đã đúng, chỉ thiếu lời thông báo.
    /// </summary>
    private void NotifyMembersDisbanded()
    {
        string hostName = HostDisplayName;

        for (int i = 0; i < MaxMembers; i++)
        {
            var m = Members[i];
            if (!m.IsOccupied) continue;
            if (m.Player == HostPlayer) continue; // Host tự biết, không cần tự báo mình

            // Tra theo ProfileId trước: PlayerRef bị Fusion tái sử dụng khi người chơi
            // ra/vào phòng, nên nó không phải khóa định danh đáng tin (cùng lý do
            // PartyService.AcceptInvite dùng profile id). Chỉ lùi về PlayerRef khi
            // ProfileId chưa kịp đồng bộ.
            var presence = PlayerPresence.Find(m.ProfileId) ?? PlayerPresence.FindByPlayer(m.Player);
            presence?.RPC_PartyDisbanded(hostName);
        }
    }

    /// <summary>Hàm gọi lại từ Fusion: Quyền StateAuthority của ta trên đối tượng này bị thay đổi.</summary>
    public void StateAuthorityChanged()
    {
        // Không làm gì cả: host không bao giờ chuyển đối tượng này cho thành viên (host rời mạng thì giải tán).
        // Hàm này giữ lại để thỏa mãn interface IStateAuthorityChanged mà Fusion yêu cầu.
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Start dungeon (Bắt đầu hầm ngục - chức năng của host)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Chỉ host mới dùng: Công khai hầm ngục đã chọn để mọi người trong party cùng thấy (24.2). 
    /// Được gọi ngay khi host đổi hoặc chọn map, TRƯỚC KHI ấn nút Start.
    /// </summary>
    public void HostSetDungeon(int configId, string sceneName, string dungeonName)
    {
        if (!HasStateAuthority) return;
        DungeonConfigId = configId;
        DungeonSceneName = sceneName ?? string.Empty;
        DungeonName = dungeonName ?? string.Empty;
    }

    /// <summary>
    /// Chỉ host mới dùng: Khởi động quá trình vào hầm ngục. Kiểm tra lại điều kiện, công khai 
    /// hầm ngục mục tiêu, chuyển trạng thái State→Loading, sau đó gọi 
    /// <see cref="OnDungeonStartRequested"/> trên máy host để DungeonManager bắt đầu gọi API Enter (Bước 5).
    /// </summary>
    public void HostStartDungeon(int configId, string sceneName)
    {
        if (!HasStateAuthority) return;
        if (!CanStartDungeon) return; // Nếu chưa đủ điều kiện thì chặn luôn

        DungeonConfigId = configId;
        DungeonSceneName = sceneName ?? string.Empty;
        State = PartyState.Loading; // Chuyển state sang Loading

        OnDungeonStartRequested?.Invoke(configId, sceneName); // Gọi event kích hoạt tải map
    }

    /// <summary>
    /// Chỉ host mới dùng: Công bố ID phiên hầm ngục nhận từ Backend cho toàn bộ party,
    /// đồng thời đặt trạng thái party là in-dungeon để các thành viên dịch chuyển vào hầm
    /// mà không cần phải gọi API Enter một lần nữa (tránh trùng dữ liệu backend).
    /// </summary>
    public void HostPublishDungeonSession(int sessionId)
    {
        if (!HasStateAuthority) return;
        DungeonSessionId = sessionId;
        State = PartyState.InDungeon;
    }

    /// <summary>
    /// Chỉ host mới dùng: Lùi trạng thái từ <see cref="PartyState.Loading"/> về lại 
    /// <see cref="PartyState.Lobby"/> trong trường hợp quy trình tải hầm ngục bị thất bại 
    /// (ví dụ Backend từ chối do thiếu thể lực). Reset lại ID phiên bản để host có thể
    /// chọn lại mà không khiến party bị kẹt vĩnh viễn ở trạng thái Đang tải.
    /// </summary>
    public void RevertToLobby()
    {
        if (!HasStateAuthority) return;
        if (State != PartyState.Loading) return; // Đang không phải Loading thì bỏ qua
        DungeonSessionId = 0;
        State = PartyState.Lobby; // Trả lại sảnh chờ
    }
}
