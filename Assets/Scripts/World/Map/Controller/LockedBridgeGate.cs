using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.Core.Utilities;
using UnityEngine;

/// <summary>
/// Gắn script này vào Cổng / Chướng ngại vật / Collider chặn trên Cầu (BridgeOldLong) ở map AbandonedCastle.
/// 
/// Cơ chế:
///   - Yêu cầu người chơi hoàn thành nhiệm vụ mở đường ở AbandonedCastle (quest ID lấy từ scene) để có chìa khóa Mystic Key (ItemId 33).
///   - Nếu chưa mở khóa: Collider bật (chặn người chơi), bấm E hoặc va chạm sẽ hiện thông báo cần chìa khóa.
///   - Nếu đã mở khóa: Collider tự động tắt, cho phép người chơi đi qua cầu sang đảo hoang!
/// </summary>
[RequireComponent(typeof(WorldInteractable))]
public class LockedBridgeGate : MonoBehaviour
{
    private const string GateUnlockedPrefsKey = "mj_abandonedcastle_bridge_unlocked";

    [Header("Gate Settings")]
    // Giá trị thật lấy từ override trong scene AbandonedCastle (đang là quest "Break the Skeleton
    // Army"), KHÔNG phải default này. Không ghi số quest vào tooltip nữa: mỗi lần chèn quest mới là
    // số lại lệch, và trước đây tooltip/comment/field đã nói 3 số khác nhau.
    [Tooltip("Quest ID yêu cầu hoàn thành để mở cầu. Scene sẽ override giá trị này.")]
    [SerializeField] private int requiredQuestId = 26;

    [Tooltip("Object key dùng cho tương tác cầu.")]
    [SerializeField] private string objectKey = "AbandonedCastle.LockedBridgeGate";

    [Tooltip("Tên hiển thị khi người chơi đứng gần cầu.")]
    [SerializeField] private string displayName = "Locked Bridge";

    [Tooltip("Collider2D dùng để chặn người chơi khi cầu bị khóa.")]
    [SerializeField] private Collider2D blockingCollider2D;

    [Tooltip("Collider2D phụ cũng phải được tắt khi cầu mở (ví dụ collider trên BridgeOldLong).")]
    [SerializeField] private Collider2D additionalBlockingCollider2D;

    [Tooltip("Collider 3D dùng để chặn (nếu dùng 3D).")]
    [SerializeField] private Collider blockingCollider;

    [Tooltip("Visual root của cổng/xích/rào chắn (sẽ ẩn đi khi mở khóa).")]
    [SerializeField] private GameObject visualBarrier;

    [Header("Messages")]
    [Tooltip("Thông báo hiển thị khi người chơi cố đi qua lúc cầu đang khóa.")]
    [SerializeField] private string lockedMessage = "The bridge is locked! You need the Mystic Key from Natalie (Rest in Peace quest) to unlock the way to the island!";

    [SerializeField] private string unlockedMessage = "You used the Mystic Key to unlock the bridge!";


    private bool _isUnlocked;
    private WorldInteractable _interactable;

    private void Awake()
    {
        if (blockingCollider2D == null) blockingCollider2D = GetComponent<Collider2D>();
        if (blockingCollider == null) blockingCollider = GetComponent<Collider>();
        _interactable = GetComponent<WorldInteractable>();

        if (_interactable != null)
        {
            _interactable.ConfigureObject(
                objectKey,
                displayName,
                "Use Mystic Key",
                requiredQuestId,
                1,
                2.75f
            );
        }
    }

    private void OnEnable()
    {
        WorldRuntimeEvents.QuestsChanged += CheckUnlockState;
        CheckUnlockState();
    }

    private void OnDisable()
    {
        WorldRuntimeEvents.QuestsChanged -= CheckUnlockState;
    }

    private void Start()
    {
        CheckUnlockState();
    }

    /// <summary>
    /// Kiểm tra trạng thái mở khóa cầu dựa vào QuestManager.
    /// </summary>
    public void CheckUnlockState()
    {
        if (_isUnlocked) return;

        if (IsPersistedUnlocked())
        {
            UnlockGate(showNotice: false);
            return;
        }

        LockGate();
    }

    private bool IsPersistedUnlocked()
    {
        return PlayerPrefs.GetInt(GateUnlockedPrefsKey, 0) == 1;
    }

    private void UnlockGate(bool showNotice = true)
    {
        _isUnlocked = true;
        PlayerPrefs.SetInt(GateUnlockedPrefsKey, 1);
        PlayerPrefs.Save();

        if (blockingCollider2D != null) blockingCollider2D.enabled = false;
        if (additionalBlockingCollider2D != null) additionalBlockingCollider2D.enabled = false;
        if (blockingCollider != null) blockingCollider.enabled = false;

        if (visualBarrier != null) visualBarrier.SetActive(false);

        if (showNotice)
        {
            WorldRuntimeEvents.RaiseMessage(unlockedMessage);
        }

        Debug.Log($"[LockedBridgeGate] Cầu '{gameObject.name}' đã được MỞ KHÓA thành công!");
    }

    private void LockGate()
    {
        _isUnlocked = false;

        if (blockingCollider2D != null) blockingCollider2D.enabled = true;
        if (additionalBlockingCollider2D != null) additionalBlockingCollider2D.enabled = true;
        if (blockingCollider != null) blockingCollider.enabled = true;

        if (visualBarrier != null) visualBarrier.SetActive(true);
    }

    /// <summary>
    /// Hiển thị thông báo khóa khi người chơi chạm vào barrier hoặc bấm E.
    /// </summary>
    public void NotifyLocked()
    {
        if (_isUnlocked) return;
        WorldRuntimeEvents.RaiseMessage(lockedMessage);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isUnlocked && other.CompareTag("Player"))
        {
            NotifyLocked();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isUnlocked && other.CompareTag("Player"))
        {
            NotifyLocked();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isUnlocked && collision.gameObject.CompareTag("Player"))
        {
            NotifyLocked();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_isUnlocked && collision.gameObject.CompareTag("Player"))
        {
            NotifyLocked();
        }
    }

    /// <summary>
    /// Được gọi nếu có đính kèm WorldInteractable và người chơi bấm phím E.
    /// </summary>
    public void InteractWithGate()
    {
        CheckUnlockState();
        if (!_isUnlocked)
        {
            TryUnlockWithMysticKey();
        }
    }

    private void TryUnlockWithMysticKey()
    {
        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[LockedBridgeGate] No Token!");
            NotifyLocked();
            return;
        }

        var inventory = InventoryApi.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[LockedBridgeGate] InventoryApi.Instance is null!");
            NotifyLocked();
            return;
        }

        Debug.Log("[LockedBridgeGate] TryUnlockWithMysticKey - calling GetInventory...");
        inventory.GetInventory(
            onSuccess: inv =>
            {
                if (inv?.BagItems == null)
                {
                    Debug.LogWarning("[LockedBridgeGate] BagItems is null!");
                    NotifyLocked();
                    return;
                }

                Debug.Log($"[LockedBridgeGate] GetInventory success. BagItems found.");
                var foundKey = false;
                foreach (var item in inv.BagItems)
                {
                    Debug.Log($"[LockedBridgeGate] Checking item: '{item.ItemName}', Quantity: {item.Quantity}, Id: {item.ItemId}, InventoryItemId: {item.InventoryItemId}");
                    if (item.ItemName != null &&
                        item.ItemName.IndexOf("Mystic Key", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                        item.Quantity > 0)
                    {
                        Debug.Log("[LockedBridgeGate] Found Mystic Key!");
                        foundKey = true;
                        ProgressQuestThenConsumeKey(item.InventoryItemId);
                        return;
                    }
                }

                if (!foundKey)
                {
                    Debug.Log("[LockedBridgeGate] Mystic Key NOT found in inventory.");
                    NotifyLocked();
                }
            },
            onError: err =>
            {
                Debug.LogWarning($"[LockedBridgeGate] GetInventory failed: {err.Message}");
                NotifyLocked();
            }
        );
    }

    private void ProgressQuestThenConsumeKey(int inventoryItemId)
    {
        WorldApi.Instance.InteractObject(
            objectKey,
            "Interact",
            requiredQuestId,
            1,
            response =>
            {
                if (response?.Quest != null)
                    QuestManager.Instance?.ApplyServerQuestState(response.Quest);
                QuestManager.Instance?.AddProgress(requiredQuestId, 1);
                WorldRuntimeEvents.RaiseQuestsChanged();
                ConsumeMysticKey(inventoryItemId);
            },
            error =>
            {
                Debug.LogWarning($"[LockedBridgeGate] Quest progress failed: {error.Message}");
                NotifyLocked();
            });
    }

    private void ConsumeMysticKey(int inventoryItemId)
    {
        InventoryApi.Instance.ConsumeItem(
            inventoryItemId, 1,
            _ =>
            {
                Debug.Log("[LockedBridgeGate] Mystic Key removed from inventory.");
                InventoryManager.RefreshAny(refreshStats: false);
                UnlockGate(showNotice: true);
            },
            err =>
            {
                Debug.LogWarning($"[LockedBridgeGate] Failed to remove Mystic Key: {err.Message}");
                NotifyLocked();
            }
        );
    }
}
