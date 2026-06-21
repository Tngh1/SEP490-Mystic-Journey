# Kiến trúc API - Hệ thống Gọi API & Lưu/Load Dữ Liệu

> Tài liệu này mô tả kiến trúc hệ thống API của game Mystic Journey, cách dữ liệu được gọi từ server, lưu trữ cục bộ, và phục hồi khi khởi động lại.

---

## 1. Tổng quan kiến trúc

Hệ thống API gồm **4 tầng chính**, mỗi tầng có vai trò rõ ràng:

```
┌─────────────────────────────────────────────────────────┐
│  Tầng 4: Giao diện người dùng (UI/Scene)             │
│  GameBootstrap, MenuUIManager, UIInventory, QuestPanel │
└────────────────────────┬────────────────────────────────┘
                         │ gọi API endpoint
┌────────────────────────▼────────────────────────────────┐
│  Tầng 3: Quản lý nghiệp vụ (Manager / Service)       │
│  QuestManager, GameStateService, SettingsService       │
└────────────────────────┬────────────────────────────────┘
                         │ gọi endpoint methods
┌────────────────────────▼────────────────────────────────┐
│  Tầng 2: Endpoint classes (13 class)                  │
│  AuthApi, WorldApi, PlayerApi, InventoryApi, ...       │
└────────────────────────┬────────────────────────────────┘
                         │ gọi HTTP methods
┌────────────────────────▼────────────────────────────────┐
│  Tầng 1: Core Engine - ApiClient                      │
│  Gửi HTTP request (GET/POST/PUT/DELETE) bằng Unity    │
│  WebRequest, quản lý JWT token, parse JSON response   │
└────────────────────────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│  Backend Server (http://localhost:5176)                │
│  ASP.NET Core API - lưu dữ liệu vào Database          │
└────────────────────────────────────────────────────────┘
```

---

## 2. Tầng 1 - ApiClient (`Assets/Scripts/API/Core/ApiClient.cs`)

### 2.1 Vai trò

**ApiClient** là "trái tim" của toàn bộ hệ thống mạng. Đây là singleton `MonoBehaviour` chịu trách nhiệm:

- Gửi các HTTP request (GET, POST, PUT, DELETE) đến backend
- Tự động gắn **JWT Bearer token** vào header nếu cần xác thực
- Parse JSON response thành object C#
- Xử lý lỗi mạng (ConnectionError, ProtocolError) và lỗi HTTP (4xx, 5xx)
- Lưu/truy xuất JWT token từ `PlayerPrefs`

### 2.2 Token Management

```csharp
// Lưu token khi login thành công
ApiClient.Instance.SaveToken(response.AccessToken);

// Lấy token hiện tại
string token = ApiClient.Instance.GetToken();

// Kiểm tra đã đăng nhập chưa
if (ApiClient.Instance.HasToken()) { /* đã login */ }

// Xóa token khi logout
ApiClient.Instance.ClearToken();
```

Token được lưu với key `"mj_access_token"` trong `PlayerPrefs`.

### 2.3 Các phương thức HTTP

| Phương thức | Mục đích | Ví dụ |
|---|---|---|
| `Get<T>()` | Lấy dữ liệu từ server | Load inventory, profile |
| `Post<TReq, TRes>()` | Gửi dữ liệu tạo mới | Login, accept quest |
| `PostEmpty<T>()` | Gửi request không body | Logout, mark as read |
| `Put<TReq, TRes>()` | Cập nhật dữ liệu | Update position, batch quest progress |
| `Delete<T>()` | Xóa dữ liệu | Delete mail |

### 2.4 Cú pháp gọi API chuẩn

```csharp
// GET - không cần body
ApiClient.Instance.Get<MyResponse>(
    "/api/my-endpoint",
    onSuccess: response => {
        Debug.Log($"OK: {response.Data}");
    },
    onError: error => {
        Debug.LogError($"FAIL: {error.StatusCode} - {error.Message}");
    },
    requiresAuth: true   // true = gắn JWT token
);

// POST - có request body
var body = new MyRequest { Field = value };
ApiClient.Instance.Post<MyRequest, MyResponse>(
    "/api/my-endpoint",
    body,
    onSuccess: response => { /* xử lý */ },
    onError: error => { /* xử lý lỗi */ },
    requiresAuth: false
);
```

### 2.5 Error Handling

```csharp
// ApiException chứa:
public int StatusCode;     // mã HTTP (200, 401, 500...)
public string ErrorCode;   // mã lỗi nghiệp vụ ("INVALID_TOKEN", "NOT_FOUND")
public string Message;     // thông báo lỗi
public string RawBody;     // raw JSON từ server
```

Ba loại lỗi được xử lý:
1. **NETWORK_ERROR** (result = ConnectionError/DataProcessingError) - không kết nối được server
2. **HTTP_ERROR** (status >= 400) - server trả lỗi nghiệp vụ
3. **PARSE_ERROR** - JSON không khớp DTO

---

## 3. Tầng 2 - BaseApiService (`Assets/Scripts/API/Core/BaseApiService.cs`)

### 3.1 Vai trò

`BaseApiService<T>` là **generic singleton base class** mà tất cả 13 endpoint class kế thừa. Nó cung cấp:

- Singleton pattern thread-safe (dùng `lock`)
- Tự tạo GameObject `[TênClass]` trong scene nếu chưa có
- `DontDestroyOnLoad` - giữ instance khi chuyển scene
- An toàn khi thoát ứng dụng

### 3.2 13 Endpoint Classes

| Class | Namespace | Mục đích |
|---|---|---|
| `AuthApi` | `API.Endpoints` | Đăng nhập, đăng xuất, lấy thông tin account |
| `WorldApi` | `API.Endpoints` | Vị trí player, tương tác NPC, mở rương, daily login |
| `PlayerApi` | `API.Endpoints` | Profile, inventory, equip/unequip item |
| `InventoryApi` | `API.Endpoints` | Inventory riêng (trùng lặp một số method với PlayerApi) |
| `QuestApi` | `API.Endpoints` | Danh sách quest catalog (chỉ đọc, không cần auth) |
| `PlayerQuestApi` | `API.Endpoints` | Quest của player: accept, progress, complete, claim |
| `ShopApi` | `API.Endpoints` | Danh sách vật phẩm shop |
| `MailApi` | `API.Endpoints` | Hộp thư: đọc, claim reward, xóa |
| `DungeonApi` | `API.Endpoints` | Danh sách dungeon (chỉ đọc) |
| `AchievementApi` | `API.Endpoints` | Thành tựu player |
| `GachaApi` | `API.Endpoints` | Banner gacha |
| `SkinApi` | `API.Endpoints` | Equip/unequip skin |
| `DailyLoginApi` | `API.Endpoints` | Phần thưởng đăng nhập hàng ngày |

### 3.3 Mẫu gọi endpoint

```csharp
// Tất cả endpoint đều follow pattern này:
ApiClient.Instance.Get<TResponse>(
    ApiConfig.EndpointPath,
    onSuccess: response => { /* callback khi thành công */ },
    onError: error => { /* callback khi lỗi */ },
    requiresAuth: true/false
);
```

---

## 4. Tầng 3 - Services

### 4.1 GameStateService (`Assets/Scripts/Core/Services/GameStateService.cs`)

**Static singleton** (không phải MonoBehaviour) lưu trạng thái phiên chơi hiện tại:

```csharp
GameStateService.Instance.PlayerProfileId   // ID profile player
GameStateService.Instance.PlayerLevel      // Level hiện tại
GameStateService.Instance.PlayerName       // Tên player
GameStateService.Instance.PlayerClass      // Class (Knight, Archer...)
GameStateService.Instance.CurrentMapName   // Map đang đứng
GameStateService.Instance.LastPosition     // Vector3 vị trí cuối
```

**Hai phương thức persistence:**

```csharp
// Load từ PlayerPrefs → RAM
GameStateService.Instance.LoadFromPlayerPrefs();

// Lưu từ RAM → PlayerPrefs (dùng cho offline fallback)
GameStateService.Instance.SaveToPlayerPrefs();
```

**PlayerPrefs keys được dùng:**

| Key | Kiểu | Mô tả |
|---|---|---|
| `mj_player_profile_id` | int | Profile ID |
| `mj_player_level` | int | Level |
| `mj_user_name` | string | Username |
| `mj_player_class` | string | Class |
| `mj_last_map_name` | string | Map đang đứng |
| `mj_position_x` | float | Tọa độ X |
| `mj_position_y` | float | Tọa độ Y |

### 4.2 WorldState (`Assets/Scripts/Data/Runtime/WorldState.cs`)

Wrapper tĩnh trỏ đến `GameStateService.Instance`. Cung cấp cú pháp ngắn gọn:

```csharp
// Thay vì:
GameStateService.Instance.PlayerLevel = 5;

// Dùng:
WorldState.PlayerLevel = 5;

// Và:
WorldState.LoadFromPlayerPrefs();
WorldState.SaveToPlayerPrefs();
```

### 4.3 SettingsService (`Assets/Scripts/Core/Services/SettingsService.cs`)

**Static singleton** quản lý cài đặt game (audio, graphics):

```csharp
// Audio
SettingsService.Instance.MasterVolume   // 0.0 - 1.0
SettingsService.Instance.MusicVolume    // 0.0 - 1.0
SettingsService.Instance.SfxVolume       // 0.0 - 1.0
SettingsService.Instance.IsMuted         // bool

// Graphics
SettingsService.Instance.DisplayModeIndex  // int
SettingsService.Instance.ResolutionIndex  // int
SettingsService.Instance.ShowDamageNumbers // bool

// Load/Save (PlayerPrefs)
SettingsService.Instance.Load();
SettingsService.Instance.Save();

// Setter methods tự động apply
SettingsService.Instance.SetMasterVolume(0.5f);
SettingsService.Instance.SetMusicVolume(0.8f);
SettingsService.Instance.SetMuted(true);
```

### 4.4 QuestManager (`Assets/Scripts/Features/Quest/QuestManager.cs`)

**MonoBehaviour singleton** quản lý quest system. Đây là class phức tạp nhất với **client-side caching** và **offline queue**:

**Cấu trúc cache:**

```csharp
Dictionary<int, PlayerQuestState> _cache      // trạng thái quest
Dictionary<int, PlayerQuestResponse> _responses // raw response từ server
Dictionary<int, int> _pendingBatch            // progress chờ sync
Dictionary<int, PlayerQuestState> _snapshot   // snapshot để rollback nếu sync fail
```

**Trạng thái quest:** `NotStarted` → `InProgress` → `Completed` → `Claimed`

---

## 5. Luồng dữ liệu chi tiết

### 5.1 Đăng nhập (Login Flow)

```
Người dùng nhập email/password
        │
        ▼
AuthApi.Instance.LoginGame(email, password)
        │
        ▼
POST /api/accounts/login-game
        │
        ├── Lỗi → gọi onError, hiển thị thông báo
        │
        └── Thành công → LoginGameResponse
                │
                ├── Lưu JWT token → PlayerPrefs
                │   ApiClient.Instance.SaveToken(response.AccessToken)
                │
                ├── Lưu profile session → PlayerPrefs + GameStateService
                │   SaveProfileSession() → PlayerLevel, PlayerClass
                │
                ├── Lưu world session → PlayerPrefs + GameStateService
                │   SaveWorldSession() → MapName, PositionX, PositionY
                │
                └── Gọi onSuccess → chuyển sang Main Scene
```

**Code chi tiết trong AuthApi.LoginGame():**

```csharp
ApiClient.Instance.Post<LoginGameRequest, LoginGameResponse>(
    ApiConfig.LoginGame,
    body,
    response => {
        // 1. Lưu token
        ApiClient.Instance.SaveToken(response.AccessToken);

        // 2. Lưu profile session
        PlayerPrefs.SetInt(ApiConfig.AccountIdKey, response.AccountId);
        PlayerPrefs.SetString(ApiConfig.UserNameKey, response.UserName);
        SaveProfileSession(response.PlayerProfileId, response.Level, response.PlayerClass);

        // 3. Lưu world session (vị trí player)
        SaveWorldSession(response.LastMapName, response.PositionX, response.PositionY);

        PlayerPrefs.Save();
    },
    error => { /* xử lý lỗi */ },
    requiresAuth: false
);
```

### 5.2 Khởi động game - Load dữ liệu (Bootstrap Flow)

Khi game khởi động, `GameBootstrap` chạy trước mọi scene khác:

```
GameBootstrap.Start()
        │
        ├── Có token?
        │       ├── CÓ → Gọi AuthApi.GetMe() → WorldApi.GetState()
        │       │           │                    │
        │       │           │                    └── Lưu position → GameStateService
        │       │           └── Lưu profile → GameStateService
        │       │           │
        │       │           └── Lỗi? → LoadLocalWorldSession() (từ PlayerPrefs)
        │       │
        │       └── KHÔNG → LoadLocalWorldSession() (từ PlayerPrefs)
        │
        ├── Load scene "Main" (additive)
        │
        ├── Load scene theo WorldState.CurrentMapName (additive)
        │
        └── Destroy GameBootstrap
```

```csharp
private IEnumerator LoadWorldSession()
{
    if (ApiClient.Instance.HasToken())
    {
        // Bước 1: Gọi GetMe lấy thông tin profile
        AuthApi.Instance.GetMe(
            _ => {
                // Bước 2: Gọi GetState lấy vị trí
                WorldApi.Instance.GetState(state => {
                    WorldState.PlayerProfileId = state.PlayerProfileId;
                }, error => {
                    // Fallback: đọc từ PlayerPrefs
                    LoadLocalWorldSession();
                });
            },
            error => {
                // Fallback: đọc từ PlayerPrefs
                LoadLocalWorldSession();
            }
        );
        yield return new WaitUntil(() => done);
    }
    else
    {
        // Không có token → chỉ dùng PlayerPrefs
        LoadLocalWorldSession();
    }
}

private static void LoadLocalWorldSession()
{
    WorldState.CurrentMapName = PlayerPrefs.GetString("mj_last_map_name", "ElfForest");
    WorldState.LastPosition = new Vector3(
        PlayerPrefs.GetFloat("mj_position_x", 0f),
        PlayerPrefs.GetFloat("mj_position_y", 0f), 0f);
    WorldState.PlayerLevel = PlayerPrefs.GetInt("mj_player_level", 1);
    WorldState.PlayerClass = PlayerPrefs.GetString("mj_player_class", "Knight");
}
```

### 5.3 Lưu vị trí player (Save Position Flow)

Mỗi khi player di chuyển hoặc chuyển map:

```
Player di chuyển
        │
        ▼
WorldApi.Instance.UpdatePosition(mapName, position)
        │
        ▼
PUT /api/world/position
body: { MapName, PositionX, PositionY }
        │
        ├── Thành công
        │       ├── Cập nhật GameStateService.Instance
        │       ├── Cập nhật PlayerPrefs (fallback)
        │       └── Trả về PlayerWorldPositionResponse
        │
        └── Lỗi → gọi onError, dữ liệu cũ giữ nguyên
```

```csharp
var body = new UpdateWorldPositionRequest {
    MapName = currentMap,
    PositionX = player.position.x,
    PositionY = player.position.y
};
ApiClient.Instance.Put<UpdateWorldPositionRequest, ApiResponse<PlayerWorldPositionResponse>>(
    ApiConfig.WorldPosition, body,
    response => {
        // Cập nhật cả RAM và PlayerPrefs
        GameStateService.Instance.CurrentMapName = mapName;
        GameStateService.Instance.LastPosition = position;
        PlayerPrefs.SetString(ApiConfig.LastMapNameKey, mapName);
        PlayerPrefs.SetFloat(ApiConfig.PositionXKey, position.x);
        PlayerPrefs.SetFloat(ApiConfig.PositionYKey, position.y);
        PlayerPrefs.Save();
    },
    error => { Debug.LogError($"Update position fail: {error.Message}"); },
    requiresAuth: true
);
```

### 5.4 Load Inventory

```
Mở Inventory Panel
        │
        ▼
InventoryApi.Instance.GetInventory()
        │
        ▼
GET /api/inventory/me
        │
        └── Trả về ApiResponse<InventorySummaryResponse>
                ├── Data.BagItems[]     → danh sách items
                ├── Data.TotalItems     → tổng số items
                └── Data.EquipSlots[]   → items đang trang bị
```

### 5.5 Quest System - Load

```
Mở Quest Panel
        │
        ▼
QuestManager.LoadMyQuests()
        │
        ├── Có token?
        │       └── CÓ → PlayerQuestApi.Instance.GetMyQuests()
        │       │           │
        │       │           └── GET /api/playerquests/me
        │       │               │
        │       │               ├── Clear cache cũ
        │       │               ├── Upsert từng quest vào _cache
        │       │               ├── ApplyOfflineQueue() (nếu có)
        │       │               ├── Start BatchSyncLoop()
        │       │               └── Gọi OnQuestsLoaded event
        │       │
        │       └── KHÔNG → Clear cache, dùng empty list
        │
        └── Không có token → trả về empty list
```

### 5.6 Quest System - Progress (Offline Queue)

```
Player thực hiện quest (giết quái, thu thập...)
        │
        ▼
QuestManager.AddProgress(questId, amount)
        │
        ├── Tăng progress trong _cache (RAM)
        ├── Đánh dấu isDirty = true
        ├── Thêm vào _pendingBatch
        └── Gọi OnQuestProgressChanged event
```

**Batch Sync mỗi 1 giây:**

```
BatchSyncLoop() (chạy mỗi 1 giây)
        │
        ├── Có pending items?
        │       ├── CÓ → PUT /api/playerquests/batch-progress
        │       │           body: [{ QuestId, Progress }, ...]
        │       │           │
        │       │           ├── Thành công → cập nhật _cache từ server
        │       │           └── Thất bại → rollback từ _snapshot
        │       │
        │       └── KHÔNG → tiếp tục đợi
```

**Offline Queue (khi thoát game):**

```
OnApplicationQuit()
        │
        ▼
FlushOfflineQueue()
        │
        ├── Tìm tất cả quest isDirty trong _cache
        ├── Serialize thành JSON
        └── Lưu vào PlayerPrefs key: "mj_quest_offline_queue"
```

```
Game khởi động lại → LoadMyQuests()
        │
        ▼
HandleLoadedQuestResponses()
        │
        ├── Load quests từ server
        │
        ▼
ApplyOfflineQueue()
        │
        ├── Đọc JSON từ PlayerPrefs
        ├── Merge progress vào _cache (lấy max)
        └── Xóa offline queue
```

---

## 6. Cấu trúc Response từ Server

### 6.1 Wrapper Response

Hầu hết API trả về wrapped response:

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }
}
```

### 6.2 Ví dụ thực tế

```csharp
WorldApi.Instance.GetState(
    response => {
        // response là WorldStateResponse
        // Lưu vào GameStateService
        GameStateService.Instance.PlayerProfileId = response.PlayerProfileId;
    },
    error => { /* xử lý */ }
);
```

---

## 7. Lưu ý quan trọng

### 7.1 Offline Support

- **Vị trí player**: được lưu vào PlayerPrefs mỗi khi di chuyển
- **Cài đặt**: SettingsService tự động load/save PlayerPrefs
- **Quest progress**: được lưu offline và merge khi có mạng

### 7.2 Fallback Strategy

```
Có token?
├── CÓ → Gọi API server
│         ├── Thành công → dùng dữ liệu server
│         └── Thất bại → dùng PlayerPrefs (fallback)
└── KHÔNG → dùng PlayerPrefs (guest mode)
```

### 7.3 Khi nào cần Auth?

| API | requiresAuth | Lý do |
|---|---|---|
| LoginGame | `false` | Chưa có token |
| GetMe | `true` | Cần token để xác thực |
| GetAll Quests | `false` | Danh sách công khai |
| GetMy Quests | `true` | Chỉ quest của player đó |
| Shop Items | `false` | Danh sách công khai |
| Inventory | `true` | Chỉ inventory của player đó |

### 7.4 Đổi API Server

Chỉ cần sửa một dòng trong `ApiConfig`:

```csharp
// Assets/Scripts/API/Core/ApiConfig.cs
public const string BaseUrl = "http://localhost:5176";
// Đổi thành domain thật khi deploy:
// public const string BaseUrl = "https://api.mysticjourney.com";
```
