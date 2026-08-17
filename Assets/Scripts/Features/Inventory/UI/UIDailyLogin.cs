using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class UIDailyLogin : MonoBehaviour
{
    public static UIDailyLogin Instance;

    [Header("UI Settings")]
    [SerializeField] private UIDailySlot dailySlotPrefab;
    [SerializeField] private Transform contentParent;

    [Header("Grid Layout Settings")]
    [SerializeField] private int columns = 6;
    [SerializeField] private bool autoFitCellSize = true;
    [SerializeField] private bool preserveSquareAspect = true;
    [SerializeField] private float slotAspectRatio = 1.0f;
    [SerializeField] private bool ensureMaskComponent = true;
    [SerializeField] private bool centerGridAlignment = true;

    private readonly List<UIDailySlot> slots = new List<UIDailySlot>();

    public Action<UIBaseItemSlot> OnDailyItemClaimed;

    // Initializes internal component caches and dependencies for UIDailyLogin upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        BindReferences();
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        BindReferences();
        UpdateGridLayout(slots.Count > 0 ? slots.Count : 30);
    }

    // Performs startup initialization for UIDailyLogin on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        UpdateGridLayout(slots.Count > 0 ? slots.Count : 30);
    }

    // Executes on rect transform dimensions change operation.
    private void OnRectTransformDimensionsChange()
    {
        if (gameObject.activeInHierarchy)
            UpdateGridLayout(slots.Count > 0 ? slots.Count : 30);
    }

    // Executes refresh daily operation.
    public void RefreshDaily(List<UIItemDisplayData> dailyItems)
    {
        BindReferences();
        dailyItems ??= new List<UIItemDisplayData>();

        if (dailySlotPrefab == null || contentParent == null)
        {
            Debug.LogWarning("[UIDailyLogin] Daily slot prefab or content parent is missing.", this);
            return;
        }

        for (int i = 0; i < dailyItems.Count; i++)
        {
            if (i >= slots.Count)
            {
                UIDailySlot newSlot = Instantiate(dailySlotPrefab, contentParent);
                newSlot.transform.localScale = Vector3.one;
                newSlot.OnSlotClicked += HandleSlotClicked;
                slots.Add(newSlot);
            }

            slots[i].gameObject.SetActive(true);
            slots[i].SetupDaily(dailyItems[i]);
        }

        for (int i = dailyItems.Count; i < slots.Count; i++)
        {
            slots[i].ClearSlot();
            slots[i].gameObject.SetActive(false);
        }

        UpdateGridLayout(dailyItems.Count);

        var rect = contentParent.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    // Executes update grid layout operation.
    public void UpdateGridLayout(int totalItemsCount = 30)
    {
        if (contentParent == null)
            return;

        var grid = contentParent.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        int cols = columns > 0 ? columns : 6;
        int activeCount = Mathf.Max(totalItemsCount, 30);
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)activeCount / cols));

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        if (centerGridAlignment)
        {
            grid.childAlignment = TextAnchor.MiddleCenter;
        }

        var containerRect = contentParent.GetComponent<RectTransform>();
        if (containerRect == null)
            return;

        var scrollRect = contentParent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null && scrollRect.content == containerRect)
        {
            float totalHeight = grid.padding.top + grid.padding.bottom + (rows * grid.cellSize.y) + ((rows - 1) * grid.spacing.y);
            containerRect.sizeDelta = new Vector2(containerRect.sizeDelta.x, totalHeight);
            return;
        }

        if (ensureMaskComponent)
        {
            var parentObj = contentParent.parent != null ? contentParent.parent.gameObject : contentParent.gameObject;
            if (parentObj.GetComponent<Mask>() == null && parentObj.GetComponent<RectMask2D>() == null)
            {
                parentObj.AddComponent<RectMask2D>();
            }
        }

        if (!autoFitCellSize)
            return;

        Vector2 containerSize = containerRect.rect.size;

        if (containerSize.x <= 0 || containerSize.y <= 0)
        {
            Canvas.ForceUpdateCanvases();
            containerSize = containerRect.rect.size;
        }

        if (containerSize.x > 0 && containerSize.y > 0)
        {
            float availableWidth = containerSize.x - grid.padding.left - grid.padding.right - (grid.spacing.x * (cols - 1));
            float availableHeight = containerSize.y - grid.padding.top - grid.padding.bottom - (grid.spacing.y * (rows - 1));

            if (availableWidth > 0 && availableHeight > 0)
            {
                float calculatedWidth = availableWidth / cols;
                float calculatedHeight = availableHeight / rows;

                if (preserveSquareAspect)
                {
                    float maxAllowedHeight = calculatedHeight;
                    float maxAllowedWidth = calculatedWidth;

                    float sideFromHeight = maxAllowedHeight * slotAspectRatio;
                    float finalWidth = Mathf.Min(maxAllowedWidth, sideFromHeight);
                    float finalHeight = finalWidth / slotAspectRatio;

                    grid.cellSize = new Vector2(finalWidth, finalHeight);
                }
                else
                {
                    grid.cellSize = new Vector2(calculatedWidth, calculatedHeight);
                }
            }
        }
    }

    // Executes handle slot clicked operation.
    private void HandleSlotClicked(UIBaseItemSlot clickedSlot)
    {
        OnDailyItemClaimed?.Invoke(clickedSlot);
    }

    // Executes bind references operation.
    private void BindReferences()
    {
        if (contentParent == null)
        {
            var grid = GetComponentInChildren<GridLayoutGroup>(true);
            if (grid != null)
                contentParent = grid.transform;
            else
                contentParent = FindChild("Content") ?? FindChild("Grid") ?? FindChild("DailyGrid");
        }

        if (slots.Count == 0)
        {
            var foundSlots = GetComponentsInChildren<UIDailySlot>(true);
            foreach (var slot in foundSlots)
            {
                if (!slots.Contains(slot))
                {
                    slot.OnSlotClicked -= HandleSlotClicked;
                    slot.OnSlotClicked += HandleSlotClicked;
                    slots.Add(slot);
                }
            }
        }
    }

    // Executes find child operation.
    private Transform FindChild(string objectName)
    {
        var children = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == objectName)
                return children[i];
        }

        return null;
    }
}
