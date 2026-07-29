using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Models.Response;

public class HUDSkillManager : MonoBehaviour
{
    [Header("Gắn 3 cái Image (Icon) của HUD ngoài màn hình vào đây")]
    public Image[] hudSkillIcons; // Mảng chứa 3 ô (Slot_1, Slot_2, Slot_3)

    [Header("Master Data")]
    public SkillData[] allSkillsInGame; // Kéo file SkillData vào đây y như SkillPanelManager

    private void OnEnable()
    {
        SkillSlot.OnSkillEquipped += UpdateHUDIcon;
        EnsureMasterData();
        RefreshHUDSkills();
    }

    private void Start()
    {
        EnsureMasterData();
        StartCoroutine(AutoRefreshRoutine());
    }

    private System.Collections.IEnumerator AutoRefreshRoutine()
    {
        // 1. Tải ngay lần đầu
        RefreshHUDSkills();

        // 2. Chờ 0.5s tải lại phòng trường hợp Auth/API chưa nạp kịp
        yield return new WaitForSeconds(0.5f);
        RefreshHUDSkills();

        // 3. Chờ 1.5s tải lại lần nữa để đảm bảo 100% khi vào game skill tự hiện lên HUD mà không cần mở SkillPanel
        yield return new WaitForSeconds(1.5f);
        RefreshHUDSkills();
    }

    private void EnsureMasterData()
    {
        if (allSkillsInGame == null || allSkillsInGame.Length == 0)
        {
            allSkillsInGame = Resources.LoadAll<SkillData>("");
        }
    }

    private void EnsureSlotIndices()
    {
        var allSlots = FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var hudSlots = new System.Collections.Generic.List<SkillSlot>();

        foreach (var s in allSlots)
        {
            if (s != null && !s.transform.IsChildOf(this.transform))
            {
                hudSlots.Add(s);
            }
        }

        hudSlots.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        for (int i = 0; i < hudSlots.Count && i < 3; i++)
        {
            if (hudSlots[i] != null)
            {
                hudSlots[i].slotIndex = i;
            }
        }
    }

    public void RefreshHUDSkills()
    {
        EnsureMasterData();

        // 1. Tự động tìm tất cả ô SkillSlot thuộc HUD (nằm ngoài SkillPanel)
        var allSlots = FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var hudSlots = new System.Collections.Generic.List<SkillSlot>();
        var skillPanelManager = FindFirstObjectByType<SkillPanelManager>(FindObjectsInactive.Include);

        foreach (var s in allSlots)
        {
            if (s != null)
            {
                if (skillPanelManager != null && s.transform.IsChildOf(skillPanelManager.transform))
                    continue; // Bỏ qua slot bên trong Panel
                hudSlots.Add(s);
            }
        }

        // Sắp xếp các ô HUD từ trái sang phải
        hudSlots.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        // Gán slotIndex chuẩn (0, 1, 2) và cập nhật lock state
        for (int i = 0; i < hudSlots.Count && i < 3; i++)
        {
            if (hudSlots[i] != null)
            {
                hudSlots[i].slotIndex = i;
                hudSlots[i].RefreshLockState();
            }
        }

        Debug.Log("[HUDSkillManager] Start fetching skills...");

        // 2. Tự động tải danh sách skill đang trang bị để hiển thị lên HUD
        MysticJourney.API.Endpoints.SkillApi.Instance.GetMySkills(
            onSuccess: (response) =>
            {
                Debug.Log($"[HUDSkillManager] Fetch success. Total skills: {(response.Skills != null ? response.Skills.Count : 0)}");
                if (response.Skills == null || allSkillsInGame == null) return;
                
                foreach (var ps in response.Skills)
                {
                    if (ps.EquippedSlot.HasValue && ps.EquippedSlot.Value >= 0 && ps.EquippedSlot.Value < hudSlots.Count)
                    {
                        var visual = System.Array.Find(allSkillsInGame, d => d != null && d.skillId == ps.SkillId);
                        if (visual != null && visual.skillIcon != null)
                        {
                            var slot = hudSlots[ps.EquippedSlot.Value];
                            if (slot != null)
                            {
                                Debug.Log($"[HUDSkillManager] Loaded equipped skill {visual.name} (id={visual.skillId}) at slot {ps.EquippedSlot.Value}");
                                if (slot.equippedIcon != null)
                                {
                                    slot.equippedIcon.gameObject.SetActive(true);
                                    slot.equippedIcon.enabled = true;
                                    slot.equippedIcon.sprite = visual.skillIcon;
                                    slot.equippedIcon.color = Color.white;
                                }

                                // Broadcast to PlayerCombat and HUD SkillSlots immediately on game load
                                SkillSlot.BroadcastSkillEquipped(ps.EquippedSlot.Value, visual, ps);
                            }
                        }
                    }
                }
            },
            onError: (error) => 
            {
                Debug.LogError($"[HUDSkillManager] Failed to fetch skills: {error.Message}");
            }
        );
    }

    private void OnDisable()
    {
        SkillSlot.OnSkillEquipped -= UpdateHUDIcon;
    }

    private void UpdateHUDIcon(int slotIndex, SkillData vData, PlayerSkillResponse sData)
    {
        Debug.Log($"[HUDSkillManager] UpdateHUDIcon called with slotIndex: {slotIndex}");
        // Kiểm tra xem ô đó có hợp lệ trong mảng HUD không
        if (slotIndex >= 0 && slotIndex < hudSkillIcons.Length)
        {
            if (hudSkillIcons[slotIndex] != null && vData != null && vData.skillIcon != null)
            {
                Debug.Log($"[HUDSkillManager] Setting sprite for slot {slotIndex}: {vData.skillIcon.name}");
                hudSkillIcons[slotIndex].gameObject.SetActive(true);
                hudSkillIcons[slotIndex].enabled = true;
                hudSkillIcons[slotIndex].sprite = vData.skillIcon;
                hudSkillIcons[slotIndex].color = Color.white; // Hiện rõ ảnh lên
            }
            else
            {
                Debug.LogWarning($"[HUDSkillManager] Failed to set sprite. hudSkillIcon is null? {hudSkillIcons[slotIndex] == null}, vData null? {vData == null}, skillIcon null? {vData?.skillIcon == null}");
            }
        }
    }
}