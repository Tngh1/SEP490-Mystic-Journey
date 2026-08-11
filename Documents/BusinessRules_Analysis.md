# PHÂN TÍCH BUSINESS RULES — MYSTIC JOURNEY

Đối chiếu danh sách chức năng với 148 BR hiện có trong tài liệu.

---

## 0. CÁCH ĐỌC TÀI LIỆU

**Ký hiệu trạng thái**

| Ký hiệu | Ý nghĩa |
|---|---|
| OK | BR đã có và khớp với hệ thống thực tế |
| SAI | BR đã có nhưng mô tả **không đúng** code hiện tại → cần sửa lời văn BR |
| THIẾU | Chức năng có thật trong code nhưng **không BR nào** phủ → cần thêm BR mới |
| N/A | BR nằm ngoài phạm vi chức năng đang xét |

**Nguồn kiểm chứng.** Repo này là **Unity client**. Đã đọc thực tế:
`Assets/Scripts/API/Core/ApiConfig.cs` (toàn bộ route), 20 class trong
`API/Endpoints/`, các DTO trong `API/Models/`, `Networking/PartyLobby.cs`,
`Networking/PartyService.cs`, `Features/Gacha/GachaUIManager.cs`.

**Giới hạn.** Các chức năng Web/Admin (Register, Forget Password, Ban Player,
Manage Content, View Statistics...) **không có code trong repo này** — chúng thuộc
tầng BE + FE web. Phần đó được đánh giá trên logic tài liệu, không phải kiểm chứng
code, và được ghi rõ là *chưa kiểm chứng*.

---

## 1. BR NỀN TẢNG — ÁP DỤNG CHO MỌI CHỨC NĂNG

BR-001 → BR-010 là rule xuyên suốt. Để tránh lặp 10 dòng ở mỗi chức năng, quy ước:
**mọi chức năng dưới đây đều kế thừa BR-001..BR-010**, bảng mapping chỉ liệt kê BR
*đặc thù* của chức năng đó.

| BR | Nội dung | Ghi chú áp dụng |
|---|---|---|
| BR-001 | 1 Account ↔ 1 Player Profile | OK — code lưu `AccountId` + `PlayerProfileId` riêng |
| BR-002 | Write cần account active, không ban | OK — `requiresAuth: true` ở mọi endpoint ghi |
| BR-003 | Phân quyền check ở server | OK về nguyên tắc |
| BR-004 | Chỉ đọc/sửa resource mình sở hữu | OK — pattern `/me` khắp API |
| BR-005 | Chỉ server đổi số dư/progress/kết quả combat | **SAI một phần** — xem §3.4 (Party dùng host-authority) |
| BR-006 | Idempotency key chống trùng | **THIẾU** cho quest progress + monster defeat — xem §5 |
| BR-007 | Transaction atomic, reject stale version | OK |
| BR-008 | Audit log hành động sensitive | OK |
| BR-009 | Chuẩn hoá + lọc nội dung text | OK — có `ChatModerationResultResponse` |
| BR-010 | Không lộ stack trace/token trong lỗi | OK |

---

## 2. MAPPING — NHÓM WEB (Guest / Player / Admin)

*Chưa kiểm chứng code — repo này không chứa BE/FE web.*

### 2.1 Xác thực & tài khoản

| # | Chức năng | BR đã có | Trạng thái | Thiếu / Cần thêm |
|---|---|---|---|---|
| 1 | Register | BR-011, 012, 013, 014 | OK | BR-011 nói "verified email" nhưng chưa rõ tài khoản chưa verify có login được không → cần làm rõ |
| 2 | Login (Web) | BR-015, 014, 016, 020 | OK | — |
| 3 | Forget Password (RP3/RP4) | BR-013, 014, 017 | OK | BR-013 chưa nói rõ sau khi reset phải **thu hồi mọi session cũ** |
| 4 | Change Password | BR-017, 018, 012 | OK | — |
| 5 | Logout (Web) | BR-019, 020 | OK | **BR-149** (fail-open logout) — xem §5 |

### 2.2 Wiki công khai (Guest)

| # | Chức năng | BR đã có | Trạng thái | Thiếu / Cần thêm |
|---|---|---|---|---|
| 6 | View Character Wiki | BR-022, 027, 028 | OK | — |
| 7 | View Item List | BR-022, 027, 028, 036 | OK | — |
| 8 | View Skill Wiki | BR-022, 027, 028 | OK | — |
| 9 | View Monster Wiki | BR-022, 027, 028, 038 | **SAI** | BR-027 nói wiki phản ánh config live. Nhưng code có **discovery riêng từng player** (`IsDiscovered`, `TimesDefeated`, endpoint `/monsters/{id}/discover`, `/monsters/me/catalog`). Không BR nào mô tả → **BR-163** |
| 10 | View Account Profile | BR-021, 004 | OK | — |

### 2.3 Quản trị người chơi (Admin)

| # | Chức năng | BR đã có | Trạng thái | Thiếu / Cần thêm |
|---|---|---|---|---|
| 10.1 | View Player List | BR-029, 030, 028 | OK | — |
| 10.2 | View Player Profile | BR-029, 030 | OK | — |
| 10.3 | Ban Player | BR-031, 016, 008 | OK | BR-031/016 chỉ chặn session **mới**; chưa nói ban phải thu hồi session đang mở |
| 10.4 | Unban Player | BR-032, 008 | OK | — |

### 2.4 Quản lý nội dung (Admin)

| # | Chức năng | BR đã có | Trạng thái | Thiếu / Cần thêm |
|---|---|---|---|---|
| 12.1 | View Category Content List | BR-028, 033 | OK | — |
| 12.2 | Create Category Content | BR-023, 033 | OK | — |
| 12.3 | Update Category Content | BR-023, 024, 033 | OK | — |
| 13.1 | View Content List | BR-028, 022 | OK | — |
| 13.2 | Create Content | BR-025, 026, 009 | OK | — |
| 13.3 | Update Content | BR-025, 026 | OK | — |

### 2.5 Quản lý master data (Admin)

| # | Chức năng | BR đã có | Trạng thái | Thiếu / Cần thêm |
|---|---|---|---|---|
| 14.1 | View Item List | BR-028, 033 | OK | — |
| 14.2 | Update Item Stats | BR-033, 034, 035, 036 | OK | — |
| 15.1 | View Monster List | BR-028, 033 | OK | — |
| 15.2 | Update Monster | BR-033, 034, 037, 038, 039 | OK | — |
| 19.1 | View Dungeon List | BR-028, 033 | OK | — |
| 19.2 | Update Dungeon Item List | BR-040, 041, 034, 037 | OK | — |
| 20.1 | View Quest List | BR-028, 033 | OK | — |
| 20.2 | Create Quest | BR-042, 035, 034 | OK | — |
| 20.3 | Update Quest | BR-042, 035 | OK | **BR-164** — sửa quest đang có người nhận thì progress cũ xử lý thế nào |
| 21.1 | View Achievement List | BR-028, 033 | OK | — |
| 21.2 | Update Achievement | BR-043, 044 | OK | — |

### 2.6 Gacha / Shop / Mail / Daily (Admin)

| # | Chức năng | BR đã có | Trạng thái | Thiếu / Cần thêm |
|---|---|---|---|---|
| 16.1 | View Gacha Banner | BR-028, 046 | OK | — |
| 16.2 | Update Gacha Pool | BR-045, 046, 047, 048 | OK | — |
| 17.1 | View Shop Item List | BR-028 | OK | — |
| 17.2 | Create Shop Item | BR-049, 034 | OK | **BR-160** — shop có 2 loại (fixed + daily deals), BR không phân biệt |
| 17.3 | Update Shop Item | BR-049, 050, 051 | OK | — |
| 22.1 | View Mailbox List | BR-055, 028 | OK | — |
| 22.2 | Create Mailbox | BR-055, 056, 057, 058 | OK | — |
| 23.1 | View Daily Login | BR-028 | OK | — |
| 23.2 | Create Daily Login Campaign | BR-088, 089, 034 | OK | — |
| 23.3 | Update Daily Login Campaign | BR-088, 089 | OK | — |
| — | View Statistics | BR-054, 053, 052 | OK | **BR-167** — statistics là read-only aggregate, cần chốt kỳ + timezone |

---

## 3. MAPPING — NHÓM GAME CLIENT (đã kiểm chứng code)

### 3.1 Session & Profile

| # | Chức năng | BR đã có | Trạng thái | Ghi chú kiểm chứng |
|---|---|---|---|---|
| — | Login Game | BR-059, 060, 015, 016, 020 | OK | `AuthApi.LoginGame` gửi `ClientType="Game"`; có `AuthRefreshToken` + SignalR `/hubs/game` để đẩy sự kiện "phiên bị đè" → BR-060 khớp |
| — | Logout Game | BR-061, 019 | **SAI một phần** | `AuthApi.Logout` **xoá token local ngay cả khi server trả lỗi** (nhánh `onError` vẫn gọi `ClearToken()`). BR-019 nói "xoá local là không đủ" nhưng không mô tả hành vi fail-open này → **BR-149** |
| — | Configure Game Settings | *(không có BR)* | **THIẾU** | `SettingsService` lưu setting client (âm thanh, đồ hoạ). Không BR nào phân định setting client-local vs setting ảnh hưởng kết quả server → **BR-152** |
| 28.1 | View Player Profile | BR-021, 004 | OK | `/api/playerprofiles/{0}` |
| 28.2 | Update Profile | BR-062, 063 | OK | có `PlayerProfileChangeName` riêng |

### 3.2 Nhân vật & Kỹ năng

| # | Chức năng | BR đã có | Trạng thái | Ghi chú kiểm chứng |
|---|---|---|---|---|
| 29.1 | View Character Classes | BR-071, 022 | OK | `/api/wiki/classes` |
| 29.2 | Create Character | BR-064, 065, 066 | OK | `CharacterApi.CreateCharacter` |
| 29.3 | Upgrade Character | BR-067, 068 | **THIẾU** | Code có `GetLevelUpOptions` + `AllocateStat` → người chơi **tự cộng điểm** khi lên cấp. BR-067/068 chỉ nói "upgrade atomic" và "derived attribute do server tính", **không có BR nào cho việc phân phối điểm thủ công** → **BR-155** |
| 29.4 | View Attribute List | BR-068 | OK | `CharacterStats` |
| 30.1 | View Skill List | BR-071 | OK | `PlayerSkillsMe` |
| 30.2 | Learn Skill | BR-072, 073 | **SAI** | **Không có endpoint Learn.** SkillApi chỉ có `Upgrade / Equip / Dismantle / RecordCast`. Grep `learnskill\|skillbook` → 0 kết quả. BR-072 mô tả "tiêu Skill Book để học" — luồng này **không tồn tại trong API** → **BR-156** |
| 30.3 | Upgrade Skill | BR-074, 075 | OK | `PlayerSkillsUpgrade`. **BR-074 và BR-075 trùng nội dung** — xem §6 |
| 30.4 | Assign Skill Slot | BR-076 | OK | `PlayerSkillsEquip` |
| — | Dismantle Skill | *(không có BR)* | **THIẾU** | `PlayerSkillsDismantle` tồn tại nhưng **không BR nào** mô tả → **BR-157** |

### 3.3 Inventory / Daily / Currency

| # | Chức năng | BR đã có | Trạng thái | Ghi chú kiểm chứng |
|---|---|---|---|---|
| 31.1 | View Inventory | BR-079, 080 | OK | `InventoryMe` |
| 31.2 | View Item Detail | BR-079 | OK | — |
| 31.3 | Consume Item | BR-081, 082, 085 | OK | `InventoryConsume` |
| 31.4 | Equip Item | BR-083, 084, 085 | OK | — |
| 31.5 | Unequip Item | BR-084 | OK | — |
| 31.6 | Equip Skin | BR-086, 087 | OK | `SkinEquip` |
| 31.7 | Unequip Skin | BR-086 | OK | — |
| 32.1 | Claim Missed Login Reward | BR-089, 090 | OK | `WorldDailyLoginRetroClaim` — khớp BR-089 |
| 32.2 | Receive Daily Login Reward | BR-088, 090 | OK | `WorldDailyLoginClaim` |
| 42.1 | View Currency Balance | BR-124, 052 | **SAI** | `CurrencyBalanceResponse` chỉ có **`Gold` + `Gems`**, *không có Energy*. Energy nằm ở `PlayerProfileDTO.Energy` + `MaxEnergy` (tài nguyên hồi theo thời gian). BR-124 nói "3 currency, balance độc lập" là **không đúng cấu trúc dữ liệu** → xem §4.1 + **BR-153** |
| 42.2 | View Shop | BR-049, 051, 125 | OK | có 2 catalog: `PlayerShopFixed` + `PlayerShopDailyDeals` |
| 42.3 | Purchase Item | BR-126, 127, 050 | OK | `PlayerShopPurchase` |
| — | Refresh Daily Deals | *(không có BR)* | **THIẾU** | `PlayerShopRefresh` + `RefreshesRemainingToday` / `MaxDailyRefreshes`. Không BR nào mô tả giới hạn refresh/ngày → **BR-160** |

### 3.4 Party (Photon — không qua BE)

**Phát hiện quan trọng:** Party **không có endpoint backend nào**. Toàn bộ chạy trên
Photon Fusion (`PartyLobby.cs`, `PartyService.cs`), state đồng bộ qua `[Networked]`,
quyền do **host client** giữ (`IsLocalHost`). `PartyLobby.MaxMembers = 4` là **hằng số
compile-time trong client**, không phải config server.

| # | Chức năng | BR đã có | Trạng thái | Ghi chú kiểm chứng |
|---|---|---|---|---|
| 39.1 | Create Party | BR-103 | **SAI** | `PartyService.CreateParty()` — host là client, server không biết party tồn tại |
| 39.2 | Invite Player | BR-105, 106 | **SAI** | `InviteByProfileId` chạy client-side; giới hạn `PartyFull` check bằng `MaxMembers` trong client |
| 39.3 | Kick Member | BR-107 | **SAI** | `KickMember(PlayerRef)` — RPC host-only, server không xác thực |
| 39.4 | Accept Party Request | BR-106 | **SAI** | `AcceptInvite(hostProfileId)` |
| 39.5 | Decline Party Request | BR-106 | **SAI** | `DeclineInvite(hostProfileId)` |
| 39.6 | Leave Party | BR-108 | **SAI** | `LeaveParty()` |
| 39.7 | Ready For Dungeon | BR-109, 104 | **SAI** | `SetReady(bool)` + `StartDungeon` — client quyết định |

→ BR-103..109 mô tả đúng *ý định nghiệp vụ* nhưng **mâu thuẫn BR-005** ("chỉ server đổi
progress"). Cần bổ sung **BR-158** (ranh giới host-authority) và **BR-159** (mất party khi
disconnect). Điểm chốt an toàn: `DungeonApi.Enter` **vẫn qua server** nên phần thưởng
không bị host giả mạo — cần ghi rõ điều này thành rule.

### 3.5 Explore / Combat / Dungeon

| # | Chức năng | BR đã có | Trạng thái | Ghi chú kiểm chứng |
|---|---|---|---|---|
| — | Explore Map | BR-069, 070, 091 | OK | `WorldApi.UpdatePosition` |
| — | Talk To NPC | BR-092, 093 | OK | `WorldNpcTalk`, `WorldNpcTurnIn` |
| — | Open Chest | BR-094a, 095a | OK | `WorldInteract`. *(BR-094/095 bị trùng số — xem §6)* |
| — | Interact With Object | BR-092, 093 | OK | `WorldInteract` |
| 37.1 | View Enemy List | BR-027 | OK | `MonsterSpawns`, `MonsterCatalogForPlayer` |
| 37.2 | Fight Enemy | BR-096a, 094b, 095b | **THIẾU** | Client gọi `/monsters/{id}/defeat` để **tự khai báo đã giết**. BR-005 phủ chung nhưng không có rule nào yêu cầu server **kiểm chứng tính hợp lệ của kill** → **BR-161** |
| 37.3 | Collect Loot | BR-096b, 097 | OK | `MonsterDefeat` trả `ExperienceEarned`, `GoldEarned` |
| 37.4 | Use Skill | BR-077, 078 | OK | `PlayerSkillsRecordCast` |
| 38.1 | Enter Dungeon | BR-098, 099 | OK | `DungeonEnter`. Xác nhận BR-098: DTO ghi rõ *"Energy chưa bị trừ ở bước này"* |
| 38.2 | Progress Dungeon | BR-100 | OK | `DungeonSessionProgress` |
| 38.3 | Complete Dungeon | BR-101, 102 | OK | `DungeonSessionComplete` → `DungeonSessionClaimReward`; DTO ghi *"Energy bị trừ TẠI ĐÂY"* → **đúng BR-102** |

### 3.6 Quest / Gacha / Achievement

| # | Chức năng | BR đã có | Trạng thái | Ghi chú kiểm chứng |
|---|---|---|---|---|
| 40.1 | View Quest List | BR-110, 111 | OK | `PlayerQuestMe` |
| 40.2 | Accept Quest | BR-110, 111 | OK | `PlayerQuestAccept` |
| 40.3 | Complete Quest | BR-112, 113 | OK | `PlayerQuestComplete` |
| 40.4 | Claim Quest Reward | BR-114 | OK | `PlayerQuestClaim` |
| — | Batch Quest Progress | BR-112 | **THIẾU** | `PlayerQuestBatchProgress` gửi **lô** progress. BR-006 liệt kê idempotency cho purchase/gacha/reward/mail/donation nhưng **không có quest progress** → **BR-162** |
| 41.1 | View Gacha Banner | BR-118 | OK | `GachaById` |
| 41.2 | View Featured Reward | BR-118, 048 | OK | `BannerItems` |
| 41.3 | View Gacha History | BR-123 | OK | `GachaHistory` (có phân trang) |
| 41.4 | Perform Gacha | BR-119, 120, 121, 122 | **SAI** | Hai điểm lệch: (1) "Gacha Ticket" thực tế là **item trong inventory**, code nhận diện bằng `ItemName.Contains("Lucky Ticket")` — so sánh theo **tên**, không phải ID; (2) `GachaPullRequest` có cờ **`IsFreePull`** — luồng quay miễn phí **không được BR-119 mô tả** ("pull requires sufficient Gacha Tickets") → **BR-154** |
| 47.1 | View Achievement | BR-115, 116 | OK | `AchievementMe` |
| 47.2 | Unlock Achievement | BR-115, 116, 117 | **SAI** | Có endpoint `/achievements/me/{0}/unlock` do **client gọi**. BR-115 nói "progress dùng server event" — nhưng client đang chủ động yêu cầu unlock → **BR-165** (server phải tự tính lại điều kiện) |

### 3.7 Mail / Friend / Chat / Guild

| # | Chức năng | BR đã có | Trạng thái | Ghi chú kiểm chứng |
|---|---|---|---|---|
| 43.1 | View Mailbox List | BR-128 | OK | `MailMe` |
| 43.2 | View Mailbox Detail | BR-129 | OK | `MailRead` |
| 43.3 | Delete Mailbox | BR-130 | OK | `MailById` (DELETE) |
| 43.4 | Claim Mailbox Attachment | BR-131, 090 | OK | `MailClaim` |
| 44.1 | View Friend List | BR-004 | OK | `/api/friend` |
| 44.2 | Add Friend | BR-132, 133 | OK | `SendFriendRequest` |
| 44.3 | Accept Friend Request | BR-134 | OK | **BR-134 bị cắt giữa câu** — xem §6 |
| 44.4 | Decline Friend Request | BR-134 | OK | — |
| 44.5 | Block Player | BR-136 | OK | `BlockPlayer` |
| 44.6 | Delete Friend | BR-135 | OK | `RemoveFriend` |
| — | Unblock Player | *(không có BR)* | **THIẾU** | `UnblockPlayer` tồn tại; BR-136 chỉ nói về *block* → **BR-166** |
| 45.1 | View Chat Message List | BR-139 | OK | `ChatWorldMessages`, `ChatFriendMessages` |
| 45.2 | Send Chat Message | BR-137, 138 | OK | — |
| 45.3 | Report Chat Message | BR-138, 009 | **THIẾU** | Code có hệ thống moderation nhiều tầng: `IsToxic`, `ChatLocked`, **`LockLevel`**, `ViolationCount`, `LockedUntil`, `SeverityThreshold`. BR-138 chỉ nói "pass moderation" — **không mô tả khoá chat leo thang** → **BR-151** |
| 45.4 | Chat Friend | BR-137, 139 | OK | `ChatFriendSend` |
| 45.5 | Chat Party | BR-137 | **SAI** | Party chat **không qua BE** — đi bằng Photon RPC (`PartyLobby` + `NetworkChatText.ClampUtf8`). `PartyChatMessageResponse` có trong DTO nhưng **không endpoint nào dùng**. Không lưu server → không report/audit được → **BR-150** |
| 45.6 | Chat Guild | BR-137, 139 | OK | `GuildChat` (GET/POST) |
| 46.1 | View Guild List | BR-148 | OK | `GuildList` |
| 46.2 | View Guild Detail | BR-148 | OK | `GuildDetail` |
| 46.3 | Create Guild | BR-140, 141 | OK | — |
| 46.4 | Send Guild Invitation | BR-142 | OK | `GuildInvite` |
| 46.5 | Kick Guild Member | BR-142, 146 | OK | `GuildKick` |
| 46.6 | Join Guild | BR-140, 143 | OK | `GuildApply` |
| 46.7 | Leave Guild | BR-146, 147 | OK | `GuildLeave` |
| 46.8 | Donate In Guild | BR-144, 145 | **SAI một phần** | `DonateRequest` chỉ có `currencyType` + `amount`; kết quả trả `goldSpent`, `gemSpent`, `guildExpGained`, `guildMedalsGained`. BR-144 cho phép donate **"eligible items"** — code **không hỗ trợ donate item** |
| 46.9 | Approve Application | BR-142 | OK | `GuildApproveApp` / `GuildRejectApp` |
| — | Promote / Demote / Transfer Leader | BR-142, 147 | **THIẾU** | `GuildPromote`, `GuildDemote`, `GuildTransferLeader` có thật nhưng BR-142 chỉ nói "invite/approve/kick" → **BR-168** |
| — | Guild Level Up / Notice / Icon / Logs | *(không có BR)* | **THIẾU** | `GuildLevelUp`, `GuildNotice`, `GuildIcon`, `GuildLogs` — không BR nào phủ |

---

## 4. BR KHÔNG PHÙ HỢP VỚI HỆ THỐNG — CẦN SỬA HOẶC BỎ

Đây là phần quan trọng nhất: các BR **đang mô tả sai** hệ thống thực tế. Nếu giữ nguyên,
tài liệu và code sẽ lệch nhau khi đi bảo vệ.

### 4.1 BR-124 / BR-125 — "Đúng 3 currency, balance độc lập"

**Code thực tế:**
```csharp
// API/Models/CurrencyDTO.cs
public class CurrencyBalanceResponse {
    public decimal Gold { get; set; }
    public decimal Gems { get; set; }      // ← chỉ 2 field, KHÔNG có Energy
}
// API/Models/PlayerProfileDTO.cs
public int Energy { get; set; }            // ← Energy nằm ở profile, có MaxEnergy
```

**Vấn đề:**
1. **Tên sai:** BR gọi là *Coin*, code gọi là **`Gold`**. Toàn bộ BR-045, 049, 052, 119,
   124, 125, 126, 144 dùng chữ "Coin" — không tồn tại trong code.
2. **Energy không phải currency:** Energy là **tài nguyên hồi theo thời gian** có mức trần
   (`MaxEnergy`), UI ghi rõ *"Energy regenerates over time"*. Nó không nằm trong balance
   endpoint và không có ledger.
3. **BR-052 bất khả thi với Energy:** BR-052 yêu cầu *mọi* thay đổi Coin/Gem/Energy tạo
   ledger entry immutable. Energy hồi liên tục theo thời gian → nếu ghi ledger mỗi lần hồi
   sẽ phình bảng log vô hạn.

**Đề xuất:** đổi BR-124/125 thành: hệ thống có **2 currency có ledger (Gold, Gem)** +
**1 tài nguyên tái tạo (Energy)**; BR-052 chỉ áp cho Gold/Gem. Thống nhất từ *Coin → Gold*.

### 4.2 BR-072 / BR-073 — "Learn Skill tiêu Skill Book"

**Không tồn tại endpoint Learn.** `SkillApi` chỉ có 5 method: `GetMySkills`,
`UpgradePlayerSkill`, `EquipPlayerSkill`, `DismantlePlayerSkill`, `RecordSkillCast`.
Grep `learnskill|skillbook|skill book` toàn bộ `Assets/Scripts` → **0 kết quả**.

**Đề xuất:** hoặc (a) bỏ BR-072/073 và mô tả lại theo luồng thật (skill vào túi qua
gacha/item, rồi `Upgrade` từ level 0), hoặc (b) giữ BR và ghi nhận đây là **chức năng chưa
implement**. Không nên để BR mô tả luồng không có API.

### 4.3 BR-103 → BR-109 — Party server-authoritative

Party chạy **hoàn toàn trên Photon Fusion**, không có route BE nào. Quyền thuộc host client.
`MaxMembers = 4` là `const` trong client → **BR-104 "configured maximum" là sai**, nó không
cấu hình được từ server.

**Đề xuất:** viết lại BR-103..109 theo mô hình *host-authority cho trạng thái nhóm* +
*server-authority cho mọi thứ ảnh hưởng tiến độ/phần thưởng* (BR-158).

### 4.4 BR-137 — "Chat chỉ gửi tới channel có quyền"

Đúng với World / Friend / Guild (đều có endpoint BE). **Sai với Party**: party chat là
Photon RPC, không lưu server → BR-139 (retention) và BR-138 (moderation) **không thể áp
dụng**, và 45.3 Report Chat Message **không hoạt động được** với tin nhắn party.

### 4.5 BR-119 — "Pull cần đủ Gacha Ticket"

Bỏ sót nhánh **`IsFreePull`** trong `GachaPullRequest`. Ngoài ra ticket được nhận diện bằng
`ItemName.Contains("Lucky Ticket")` — **so khớp theo tên hiển thị**, sẽ vỡ nếu đổi tên item
hoặc thêm item khác chứa cụm đó.

### 4.6 BR-115 vs endpoint unlock

BR-115 nói achievement progress **do server event** sinh ra, nhưng tồn tại
`POST /achievements/me/{id}/unlock` cho client gọi. Hai điều này chỉ dung hoà được nếu
server **tự kiểm chứng lại** ngưỡng — cần ghi thành rule (BR-165).

### 4.7 BR không áp dụng cho hệ thống này

| BR | Lý do không áp dụng |
|---|---|
| BR-045 (một phần) | "Coin, Gem, Energy never accepted for a pull" — đúng, nhưng thiếu nhánh free pull |
| BR-050 | "Purchase limits per **character**" — hệ thống 1 account ↔ 1 profile (BR-001), không có nhiều character/account → chỉ còn scope account |
| BR-065 | "Class fixed after creation" — chưa có tính năng đổi class, rule này hiện là no-op |
| BR-115 (một phần) | "account/character scope" — không có multi-character nên scope character vô nghĩa |
| BR-144 | Cho donate "eligible items" — code **chỉ donate Gold/Gem** |
| BR-104 | "configured maximum" cho party size — client hard-code `MaxMembers = 4`, không cấu hình được từ server (xem §4.3) |

---

## 5. BR MỚI ĐỀ XUẤT (BR-149 → BR-168)

Đánh số tiếp từ BR-148. Mỗi rule dưới đây xuất phát từ **hành vi có thật trong code** mà
tài liệu hiện chưa phủ.

### Nhóm Session & Client

**BR-149.** Khi Logout, client xoá token cục bộ **kể cả khi request thu hồi tới server thất
bại**; server phải tự vô hiệu hoá refresh token khi hết hạn hoặc khi phát hiện dùng lại, để
việc thu hồi không phụ thuộc vào một lần gọi mạng thành công.

**BR-150.** Kênh chat Party là kênh **tạm thời, ngang hàng (peer-to-peer)**, không lưu trên
server: không có lịch sử, không truy vết audit và **không thể report**. Mọi nội dung cần
kiểm duyệt hoặc đối chiếu tranh chấp phải đi qua kênh có lưu server (World/Friend/Guild).

**BR-151.** Khi tin nhắn vượt ngưỡng độc hại, hệ thống tăng bộ đếm vi phạm và **khoá chat
theo cấp leo thang** (`LockLevel`) trong thời hạn `LockedUntil`. Trong thời gian khoá, mọi
yêu cầu gửi tin bị từ chối. Ngưỡng severity và thời lượng khoá do server cấu hình, client
chỉ hiển thị cảnh báo.

**BR-152.** Cấu hình game (âm thanh, đồ hoạ, phím tắt) là **tuỳ chọn cục bộ của client**,
không đồng bộ server và **không được ảnh hưởng** tới kết quả tính toán server, xác thực
chống gian lận hay cân bằng game.

### Nhóm Kinh tế

**BR-153.** Hệ thống có **2 loại tiền có ledger** (Gold, Gem) và **1 tài nguyên tái tạo**
(Energy). Energy được server tính lại theo thời gian đã trôi qua, bị chặn ở `MaxEnergy`, và
**không nhận giá trị do client gửi lên**. Nghĩa vụ ghi ledger immutable (BR-052) chỉ áp cho
Gold và Gem.

**BR-154.** Gacha Ticket là **item trong inventory**, phải được xác định bằng **ID item ổn
định**, không bằng tên hiển thị. Lượt quay miễn phí do server cấp không trừ ticket nhưng
**vẫn cộng vào bộ đếm pity** như một lượt quay hợp lệ.

**BR-160.** Shop gồm **2 catalog tách biệt**: catalog cố định và daily deals sinh riêng cho
từng người chơi. Daily deals có số lần refresh giới hạn mỗi ngày
(`MaxDailyRefreshes`); hết lượt thì từ chối, và refresh **không được** dùng để reset giới
hạn mua đã đạt.

### Nhóm Nhân vật & Kỹ năng

**BR-155.** Khi lên cấp, người chơi nhận điểm thuộc tính và **tự phân phối** vào một chỉ số
trong danh sách hợp lệ do server trả về. Server kiểm tra còn điểm chưa dùng và tên chỉ số
hợp lệ trước khi ghi; client không gửi tổng chỉ số cuối cùng.

**BR-156.** Một skill chỉ được nâng cấp/gắn slot khi người chơi **đã sở hữu** nó. Việc sở
hữu skill phát sinh từ nguồn được server xác nhận (gacha, phần thưởng, tiêu item), không từ
yêu cầu trực tiếp của client.

**BR-157.** Tháo rã (dismantle) một skill trả lại **một phần** tài nguyên theo cấu hình,
không hoàn đủ. Skill đang gắn trong slot phải được tháo khỏi slot trước. Hành động này
không thể hoàn tác.

### Nhóm Party & Combat

**BR-158.** Trạng thái nhóm (thành viên, ready, chọn dungeon) do **host client** giữ qua lớp
đồng bộ realtime. Mọi quyết định ảnh hưởng **tiến độ hoặc phần thưởng** — vào dungeon, cập
nhật progress, hoàn thành, nhận thưởng — **phải được server xác thực lại**; server không tin
kết quả do host báo.

**BR-159.** Tư cách thành viên nhóm gắn với **phiên realtime**: mất kết nối là rời nhóm.
Chủ nhóm mất kết nối thì nhóm chuyển chủ hoặc giải tán theo cấu hình. Không có nhóm nào tồn
tại qua việc thoát game.

**BR-161.** Server chỉ chấp nhận báo cáo hạ quái khi **xác minh được** trận đánh hợp lệ:
quái đang sống, thuộc bản đồ/instance mà người chơi có mặt, sát thương và thời gian nằm
trong biên hợp lý. Báo cáo không xác minh được thì **không cộng EXP, Gold hay loot**.

**BR-162.** Cập nhật tiến độ quest và báo hạ quái phải **idempotent theo (người chơi, mã sự
kiện)**. Gửi lại cùng một lô tiến độ không được cộng dồn lần hai. *(Mở rộng BR-006.)*

### Nhóm Nội dung & Quản trị

**BR-163.** Monster Wiki hiển thị theo **tiến độ khám phá riêng từng người chơi**: chỉ quái
đã gặp mới hiện đầy đủ thông số, kèm số lần đã hạ. Quái chưa khám phá bị ẩn hoặc chỉ hiện
thông tin tối thiểu. Trạng thái khám phá do server ghi khi có sự kiện gặp/hạ hợp lệ.

**BR-164.** Sửa cấu hình quest **đang được người chơi thực hiện** không được làm mất tiến độ
đã ghi. Nếu mục tiêu thay đổi tới mức không tương thích, quest đang chạy giữ **phiên bản cấu
hình lúc nhận**, hoặc được reset kèm hoàn lại vật phẩm đã tiêu.

**BR-165.** Yêu cầu mở thành tựu từ client chỉ là **tín hiệu kiểm tra**, không phải lệnh.
Server tự tính lại điều kiện từ dữ liệu sự kiện của chính nó trước khi trao; yêu cầu không
đạt ngưỡng bị từ chối và không đổi trạng thái.

**BR-166.** Bỏ chặn một người chơi khôi phục khả năng nhìn thấy và nhắn tin, nhưng **không
tự phục hồi quan hệ bạn bè** đã bị xoá lúc chặn — phải gửi lại lời mời mới.

**BR-167.** Số liệu thống kê là **kết quả tổng hợp chỉ đọc**, tính từ log immutable theo kỳ
và múi giờ xác định. Không sửa được từ màn hình quản trị và không hiển thị dữ liệu cá nhân
vượt mức cần thiết cho vận hành.

**BR-168.** Thăng/giáng cấp và chuyển quyền Leader tuân theo thứ bậc: không ai tác động lên
vai trò **bằng hoặc cao hơn** mình, và không thể thăng người khác lên cấp cao hơn cấp của
chính người thực hiện. Chuyển quyền Leader là điều kiện bắt buộc để Leader rời bang.

---

## 6. LỖI KỸ THUẬT TRONG TÀI LIỆU BR HIỆN TẠI

Cần sửa trước khi nộp — đây là lỗi đánh số/nội dung, không phụ thuộc code.

### 6.1 Trùng số BR (nghiêm trọng)

Ba số bị dùng **hai lần** cho **hai nội dung khác nhau**:

| Số | Lần 1 | Lần 2 |
|---|---|---|
| **BR-094** | "A one-time chest can be opened once per configured scope..." | "Damage, healing, effects, critical hits and death are calculated..." |
| **BR-095** | "Chest eligibility, opening state and reward grant are committed atomically..." | "A dead character/enemy cannot perform combat actions..." |
| **BR-096** | "A target is attackable only if alive, hostile..." | "Loot is generated from the defeated entity's effective loot table..." |

→ Danh sách đánh tới BR-148 nhưng thực chất có **151 rule**. Phải đánh số lại từ BR-094 trở
đi, hoặc tách thành BR-094a/094b. **Không thể trích dẫn BR-094 trong tài liệu khác khi nó
trỏ tới hai nội dung.**

### 6.2 Trùng nội dung

**BR-074 và BR-075 nói cùng một điều** (điều kiện nâng cấp skill: cần level trước, đủ level
nhân vật, đủ tài nguyên, chưa đạt cap). BR-075 chỉ là diễn đạt lại BR-074 → nên bỏ BR-075.

### 6.3 Câu bị cắt

**BR-134:** *"Only the recipient may accept/decline a pending request;"* — kết thúc bằng dấu
`;` giữa câu, thiếu nửa sau.

### 6.4 Thuật ngữ không thống nhất

*Coin* (trong BR) vs **`Gold`** (trong code) — xuất hiện ở BR-045, 049, 052, 119, 124, 125,
126, 144. Phải chọn một tên và dùng xuyên suốt.

### 6.5 Vấn đề trong danh sách chức năng

- **"View Item List" xuất hiện 2 lần**: mục 7 (wiki công khai cho Guest) và mục 14.1 (quản
  trị). Nên đổi tên để phân biệt, ví dụ *View Item Wiki* vs *Manage Item List*.
- **Thiếu số thứ tự**: 11, 18, 24, 25, 26, 27 không có trong danh sách; các mục
  Explore Map / Talk To NPC / Open Chest / Interact With Object (33–36) không được đánh số.
- Nhóm 10.1–10.4 (quản trị player) bị xếp lẫn vào dãy wiki công khai 6–10.

---

## 7. TỔNG KẾT

| Hạng mục | Số lượng |
|---|---|
| Dòng chức năng đã rà soát (§2 + §3) | 126 |
| BR hiện có | 148 số, thực tế 151 rule (3 số trùng) |
| Dòng chức năng khớp BR (OK) | 101 |
| BR mô tả **sai** hệ thống | 13 sai + 2 sai một phần |
| Chức năng **không BR nào phủ** | 10 |
| BR mới đề xuất | 20 (BR-149 → BR-168) |
| Lỗi tài liệu cần sửa | 3 số trùng, 1 cặp trùng nội dung, 1 câu bị cắt, 1 thuật ngữ lệch |

**13 dòng BR mô tả sai hệ thống:**
mục 9 View Monster Wiki · 30.2 Learn Skill · 42.1 View Currency Balance ·
39.1 Create Party · 39.2 Invite Player · 39.3 Kick Member · 39.4 Accept Party Request ·
39.5 Decline Party Request · 39.6 Leave Party · 39.7 Ready For Dungeon ·
41.4 Perform Gacha · 47.2 Unlock Achievement · 45.5 Chat Party

**2 dòng sai một phần:** Logout Game (fail-open) · 46.8 Donate In Guild (không donate item)

**10 chức năng không BR nào phủ:**
Configure Game Settings · 29.3 Upgrade Character (allocate stat) · Dismantle Skill ·
Refresh Daily Deals · 37.2 Fight Enemy (xác minh kill report) · Batch Quest Progress ·
Unblock Player · 45.3 Report Chat Message (với party) ·
Guild Promote/Demote/Transfer Leader · Guild Level Up/Notice/Icon/Logs

**Ba việc nên làm trước tiên:**

1. **Sửa 3 số BR bị trùng** (§6.1) — lỗi này chặn mọi việc trích dẫn BR về sau.
2. **Chốt lại mô hình currency** (§4.1) — quyết định Energy có phải currency hay không, rồi
   sửa đồng bộ BR-052, 124, 125 và thống nhất *Coin/Gold*.
3. **Quyết định về Party** (§4.3) — nếu giữ kiến trúc Photon host-authority thì viết lại
   BR-103..109 theo BR-158; nếu muốn server-authoritative thì đây là hạng mục phát triển
   thêm, không phải chỉnh tài liệu.

**Hai điểm rủi ro nghiệp vụ đáng chú ý:**

- **Learn Skill (BR-072/073) mô tả luồng không có API.** Cần xác định là chưa implement hay
  BR viết sai.
- **Client tự khai báo hạ quái** (`/monsters/{id}/defeat`) và **tự yêu cầu mở thành tựu**
  (`/achievements/me/{id}/unlock`) mà không có BR nào buộc server kiểm chứng lại → khe hở
  gian lận. BR-161 và BR-165 bù chỗ này.
