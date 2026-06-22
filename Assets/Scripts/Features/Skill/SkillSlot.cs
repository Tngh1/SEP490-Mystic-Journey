using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MysticJourney.API.Endpoints; // Khai báo thư viện API của bạn

public class SkillSlot : MonoBehaviour, IDropHandler
{
    public int requiredLevel;
    public int playerLevel = 1;

    public Image equippedIcon;
    public GameObject lockImage;

    void Start()
    {
        if (playerLevel < requiredLevel)
            lockImage.SetActive(true);
        else
            lockImage.SetActive(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (playerLevel < requiredLevel)
        {
            Debug.Log("Ô này bị khóa, chưa đủ level!");
            return;
        }

        SkillItem droppedSkill = eventData.pointerDrag.GetComponent<SkillItem>();
        if (droppedSkill != null)
        {
            // Lấy ID thật từ dữ liệu Server của thẻ skill bị kéo
            int targetPlayerSkillId = droppedSkill.serverData.PlayerSkillId;

            // Gọi API lên Server để xin phép trang bị
            SkillApi.Instance.EquipPlayerSkill(
                targetPlayerSkillId,
                true, // true = muốn trang bị
                onSuccess: (response) =>
                {
                    // Khi Server phản hồi OK (200), ta mới hiển thị hình ảnh
                    equippedIcon.sprite = droppedSkill.visualData.skillIcon;
                    equippedIcon.color = Color.white;
                    Debug.Log($"Trang bị thành công: {response.SkillName} lên ô!");
                },
                onError: (error) =>
                {
                    // Nếu lỗi (vd: Server check thấy skill này đang thời gian chờ cooldown, ko cho trang bị)
                    Debug.LogError("Server từ chối trang bị: " + error.Message);
                }
            );
        }
    }
}