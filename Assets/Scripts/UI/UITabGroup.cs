using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class TabEvent : UnityEvent<int> { }

public class UITabGroup : MonoBehaviour
{
    [Header("Tab Buttons")]
    [Tooltip("List of buttons that act as tabs")]
    public List<Button> tabButtons = new List<Button>();
    
    [Header("Tab Visuals")]
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Tab Content Pages (Optional)")]
    [Tooltip("If you want to switch between different panels, assign them here")]
    public List<GameObject> tabPages = new List<GameObject>();

    [Header("Events")]
    public TabEvent onTabSelected;

    private int currentTabIndex = -1;

    private void Start()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            int index = i; // Capture for lambda
            if (tabButtons[i] != null)
            {
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }
        }

        // Select the first tab by default if it exists
        if (tabButtons.Count > 0)
        {
            SelectTab(0);
        }
    }

    public void SelectTab(int index)
    {
        if (index < 0 || index >= tabButtons.Count) return;

        currentTabIndex = index;

        // Update visuals
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null) continue;
            
            Image btnImage = tabButtons[i].GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.color = (i == index) ? activeColor : inactiveColor;
            }
        }

        // Update pages if they are assigned
        for (int i = 0; i < tabPages.Count; i++)
        {
            if (tabPages[i] != null)
            {
                tabPages[i].SetActive(i == index);
            }
        }

        // Fire event so other scripts (like Shop) know the category changed
        onTabSelected?.Invoke(index);
    }
}
