using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPlayerContextMenu : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text playerNameText;
    
    [Header("Buttons")]
    public Button viewProfileButton;
    public Button addFriendButton;
    public Button reportButton;

    private string currentPlayerName;
    private RectTransform menuRect;

    private void Awake()
    {
        menuRect = transform as RectTransform;

        if (viewProfileButton != null)
            viewProfileButton.onClick.AddListener(OnViewProfileClicked);
            
        if (addFriendButton != null)
            addFriendButton.onClick.AddListener(OnAddFriendClicked);
            
        if (reportButton != null)
            reportButton.onClick.AddListener(OnReportClicked);
    }

    private void Update()
    {
        // Khi menu đang hiện, nếu click chuột trái ra ngoài vùng menu → đóng
        if (Input.GetMouseButtonDown(0))
        {
            if (menuRect != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(menuRect, Input.mousePosition))
            {
                CloseMenu();
            }
        }
    }

    public void ShowMenu(string playerName, Vector3 position)
    {
        currentPlayerName = playerName;
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        gameObject.SetActive(true);
    }

    public void CloseMenu()
    {
        gameObject.SetActive(false);
    }

    private void OnViewProfileClicked()
    {
        Debug.Log($"[ContextMenu] Mở giao diện thông tin của: {currentPlayerName}");
        // TODO: Mở UI Profile, gọi API lấy data user...
        CloseMenu();
    }

    private void OnAddFriendClicked()
    {
        Debug.Log($"[ContextMenu] Gửi lời mời kết bạn tới: {currentPlayerName}");
        // TODO: Gọi API gửi request Add Friend
        CloseMenu();
    }

    private void OnReportClicked()
    {
        Debug.Log($"[ContextMenu] Báo cáo người chơi: {currentPlayerName}");
        // TODO: Mở UI báo cáo hoặc gọi thẳng API
        CloseMenu();
    }
}
