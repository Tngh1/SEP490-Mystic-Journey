using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.Core.Services;

public class SkillSlot : MonoBehaviour, IDropHandler
{
    public int requiredLevel;
    // slotIndex: 0..2
    public int slotIndex = 0; // 0..2

    // playerLevel read from GameStateService
    private int playerLevel => GameStateService.Instance?.PlayerLevel ?? 1;

    public Image equippedIcon;
    public GameObject lockImage;

    void Start()
    {
        // Default required levels per slot: slot0 -> 1, slot1 -> 5, slot2 -> 10
        int[] slotRequired = new int[] { 1, 5, 10 };
        if (slotIndex >= 0 && slotIndex < slotRequired.Length)
        {
            requiredLevel = slotRequired[slotIndex];
        }

        lockImage.SetActive(playerLevel < requiredLevel);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (playerLevel < requiredLevel)
        {
            Debug.Log("Slot locked: player level too low.");
            return;
        }

        SkillItem droppedSkill = eventData.pointerDrag.GetComponent<SkillItem>();

        // Ensure the dropped skill has server data (owned by player)
        if (droppedSkill != null && droppedSkill.serverData != null)
        {
            // Get real PlayerSkillId from the dropped skill's server data
            int targetPlayerSkillId = droppedSkill.serverData.PlayerSkillId;

            // Call API to request equipping
            SkillApi.Instance.EquipPlayerSkill(
                targetPlayerSkillId,
                true, // true = equip
                slotIndex,
                onSuccess: (response) =>
                {
                    // On success, display the icon in the slot
                    equippedIcon.sprite = droppedSkill.visualData.skillIcon;
                    equippedIcon.color = Color.white;
                    Debug.Log($"Equipped successfully: {response.SkillName} to slot {slotIndex}!");

                        // Refresh skill list so other slots / items reflect new state
                        var mgr = FindAnyObjectByType<SkillPanelManager>();
                        if (mgr != null) mgr.RefreshSkillList();
                },
                onError: (error) =>
                {
                    Debug.LogError("Server rejected equip: " + error.Message);
                }
            );
        }
        else
        {
            // Optional: notify if user attempts to equip a locked/invalid skill
            Debug.LogWarning("Skill is locked or invalid and cannot be equipped.");
        }
    }
}