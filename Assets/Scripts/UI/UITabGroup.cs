using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class TabEvent : UnityEvent<int> { }

// Executes mono behaviour operation.
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

    // Performs startup initialization for TabEvent on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            int index = i;
            if (tabButtons[i] != null)
            {
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }
        }

        if (tabButtons.Count > 0)
        {
            SelectTab(0);
        }
    }

    // Executes select tab operation.
    public void SelectTab(int index)
    {
        if (index < 0 || index >= tabButtons.Count) return;

        currentTabIndex = index;

        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null) continue;

            Image btnImage = tabButtons[i].GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.color = (i == index) ? activeColor : inactiveColor;
            }
        }

        for (int i = 0; i < tabPages.Count; i++)
        {
            if (tabPages[i] != null)
            {
                tabPages[i].SetActive(i == index);
            }
        }

        onTabSelected?.Invoke(index);
    }
}
