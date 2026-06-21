using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillSlot : MonoBehaviour, IDropHandler
{
    public int requiredLevel; // Level yêu cầu của ô này
    public int playerLevel = 1; // Tạm thời để level 1, sau này bạn lấy từ script Player

    public Image equippedIcon;
    public GameObject lockImage; // Hình ổ khóa

    void Start()
    {
        // Kiểm tra khóa ô
        if (playerLevel < requiredLevel)
        {
            lockImage.SetActive(true);
        }
        else
        {
            lockImage.SetActive(false);
        }
    }

    // Khi có một vật thể thả vào đây
    public void OnDrop(PointerEventData eventData)
    {
        if (playerLevel < requiredLevel)
        {
            Debug.Log("Ô này bị khóa, chưa đủ level!");
            return;
        }

        // Lấy thông tin skill vừa thả vào
        SkillItem droppedSkill = eventData.pointerDrag.GetComponent<SkillItem>();
        if (droppedSkill != null)
        {
            equippedIcon.sprite = droppedSkill.mySkillData.skillIcon; // Gán hình skill vào ô
            equippedIcon.color = Color.white; // Làm cho hình rõ lên (nếu trước đó đang trong suốt)
            Debug.Log("Đã trang bị skill: " + droppedSkill.mySkillData.skillName);
        }
    }
}