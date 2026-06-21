using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SkillPopup : MonoBehaviour
{
    public Image popupIcon;
    public TextMeshProUGUI popupName;
    public TextMeshProUGUI popupDesc;

    public void ShowPopup(SkillData data)
    {
        popupIcon.sprite = data.skillIcon;
        popupName.text = data.skillName;
        popupDesc.text = data.description;

        gameObject.SetActive(true); // Hiện popup
    }

    public void HidePopup()
    {
        gameObject.SetActive(false); // Ẩn popup
    }
}