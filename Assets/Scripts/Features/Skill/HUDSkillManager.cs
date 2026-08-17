using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Models.Response;

// Executes core business logic for mono behaviour.
public class HUDSkillManager : MonoBehaviour
{
    [Header("Gắn 3 cái Image (Icon) của HUD ngoài màn hình vào đây")]
    public Image[] hudSkillIcons;

    [Header("Master Data")]
    public SkillData[] allSkillsInGame;

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        SkillSlot.OnSkillEquipped += UpdateHUDIcon;
        EnsureMasterData();
        RefreshHUDSkills();
    }

    // Performs startup initialization for HUDSkillManager on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        EnsureMasterData();
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(AutoRefreshRoutine());
    }

    // Executes core business logic for auto refresh routine.
    private System.Collections.IEnumerator AutoRefreshRoutine()
    {
        RefreshHUDSkills();

        yield return new WaitForSeconds(0.5f);
        RefreshHUDSkills();

        yield return new WaitForSeconds(1.5f);
        RefreshHUDSkills();
    }

    // Executes core business logic for ensure master data.
    private void EnsureMasterData()
    {
        if (allSkillsInGame == null || allSkillsInGame.Length == 0)
        {
            allSkillsInGame = Resources.LoadAll<SkillData>("");
        }
    }

    // Executes core business logic for ensure slot indices.
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

    // Executes core business logic for refresh hud skills.
    public void RefreshHUDSkills()
    {
        EnsureMasterData();

        var allSlots = FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var hudSlots = new System.Collections.Generic.List<SkillSlot>();
        var skillPanelManager = FindFirstObjectByType<SkillUIManager>(FindObjectsInactive.Include);

        foreach (var s in allSlots)
        {
            if (s != null)
            {
                if (skillPanelManager != null && s.transform.IsChildOf(skillPanelManager.transform))
                    continue;
                hudSlots.Add(s);
            }
        }

        hudSlots.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        for (int i = 0; i < hudSlots.Count && i < 3; i++)
        {
            if (hudSlots[i] != null)
            {
                hudSlots[i].slotIndex = i;
                hudSlots[i].RefreshLockState();
            }
        }

        Debug.Log("[HUDSkillManager] Start fetching skills...");

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
                            // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
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

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        SkillSlot.OnSkillEquipped -= UpdateHUDIcon;
    }

    // Executes core business logic for update hud icon.
    private void UpdateHUDIcon(int slotIndex, SkillData vData, PlayerSkillResponse sData)
    {
        Debug.Log($"[HUDSkillManager] UpdateHUDIcon called with slotIndex: {slotIndex}");
        if (slotIndex >= 0 && slotIndex < hudSkillIcons.Length)
        {
            if (hudSkillIcons[slotIndex] != null && vData != null && vData.skillIcon != null)
            {
                Debug.Log($"[HUDSkillManager] Setting sprite for slot {slotIndex}: {vData.skillIcon.name}");
                hudSkillIcons[slotIndex].gameObject.SetActive(true);
                hudSkillIcons[slotIndex].enabled = true;
                hudSkillIcons[slotIndex].sprite = vData.skillIcon;
                hudSkillIcons[slotIndex].color = Color.white;
            }
            else
            {
                Debug.LogWarning($"[HUDSkillManager] Failed to set sprite. hudSkillIcon is null? {hudSkillIcons[slotIndex] == null}, vData null? {vData == null}, skillIcon null? {vData?.skillIcon == null}");
            }
        }
    }
}
