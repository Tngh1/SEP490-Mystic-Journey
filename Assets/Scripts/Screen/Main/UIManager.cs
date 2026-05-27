using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Panels")]
    public GameObject inventoryPanel;
    public GameObject shopPanel;
    public GameObject skillPanel;
    public GameObject guidePanel;
    public GameObject dialoguePanel;
    public GameObject gachaPanel;
    public GameObject mapPanel;
    public GameObject questPanel;
    public GameObject chatPanel;
    public GameObject dungeonPanel;
    public GameObject friendPanel;
    public GameObject mailboxPanel;

    private GameObject currentPanel;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // ?? M? PANEL
    public void OpenPanel(GameObject panel)
    {
        // N?u click l?i panel ?ang m? ? ?óng luôn
        if (currentPanel == panel)
        {
            CloseCurrentPanel();
            return;
        }

        // ?óng panel c?
        if (currentPanel != null)
        {
            currentPanel.SetActive(false);
        }

        // M? panel m?i
        panel.SetActive(true);
        currentPanel = panel;
    }

    // ?? ?ÓNG PANEL HI?N T?I
    public void CloseCurrentPanel()
    {
        if (currentPanel != null)
        {
            currentPanel.SetActive(false);
            currentPanel = null;
        }
    }

    // ?? ?ÓNG PANEL C? TH? (r?t h?u ích cho nút X)
    public void ClosePanel(GameObject panel)
    {
        if (panel.activeSelf)
        {
            panel.SetActive(false);

            if (currentPanel == panel)
                currentPanel = null;
        }
    }

    // ?? ?ÓNG T?T C?
    public void CloseAll()
    {
        inventoryPanel.SetActive(false);
        shopPanel.SetActive(false);
        guidePanel.SetActive(false);
        dialoguePanel.SetActive(false);

        currentPanel = null;
    }
}