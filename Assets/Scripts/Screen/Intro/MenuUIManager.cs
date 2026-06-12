using UnityEngine;

public class MenuUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject waitPanel;
    public GameObject loginPanel;
    public GameObject registerPanel;

    void Start()
    {
        // Khi mới chạy game, bật WaitPanel và tắt 2 panel kia
        ShowWaitPanel();
    }

    public void ShowWaitPanel()
    {
        waitPanel.SetActive(true);
        loginPanel.SetActive(false);
        registerPanel.SetActive(false);
    }

    public void ShowLoginPanel()
    {
        waitPanel.SetActive(false);
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
    }

    public void ShowRegisterPanel()
    {
        waitPanel.SetActive(false);
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
    }
}