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

        grid.padding.left = 6;
        grid.padding.right = 6;
        grid.padding.top = 6;
        grid.padding.bottom = 6;
        grid.spacing = new Vector2(4f, 4f);

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

        RectTransform parentRect = containerRect.parent as RectTransform;
        RectTransform grandParentRect = parentRect != null ? parentRect.parent as RectTransform : null;

        Vector2 maxContainerSize = Vector2.zero;
        if (grandParentRect != null)
        {
            Vector2 gSize = grandParentRect.rect.size;
            if (gSize.x > 0 && gSize.y > 0)
            {
                maxContainerSize = new Vector2(gSize.x - 60f, gSize.y - 130f);
            }
        }

        if (maxContainerSize.x <= 0 || maxContainerSize.y <= 0)
        {
            if (parentRect != null && parentRect.rect.width > 0 && parentRect.rect.height > 0)
            {
                maxContainerSize = parentRect.rect.size;
            }
            else
            {
                Canvas.ForceUpdateCanvases();
                maxContainerSize = containerRect.rect.size;
            }
        }

        if (maxContainerSize.x > 0 && maxContainerSize.y > 0)
        {
            float availableWidth = maxContainerSize.x - grid.padding.left - grid.padding.right - (grid.spacing.x * (cols - 1));
            float availableHeight = maxContainerSize.y - grid.padding.top - grid.padding.bottom - (grid.spacing.y * (rows - 1));

            if (availableWidth > 0 && availableHeight > 0)
            {
                float calculatedWidth = availableWidth / cols;
                float calculatedHeight = availableHeight / rows;

                float side = Mathf.Min(calculatedWidth, calculatedHeight);
                float finalWidth = side * slotAspectRatio;
                float finalHeight = side;

                grid.cellSize = new Vector2(finalWidth, finalHeight);

                if (parentRect != null && scrollRect == null)
                {
                    float requiredGridWidth = (cols * finalWidth) + ((cols - 1) * grid.spacing.x) + grid.padding.left + grid.padding.right;
                    float requiredGridHeight = (rows * finalHeight) + ((rows - 1) * grid.spacing.y) + grid.padding.top + grid.padding.bottom;

                    parentRect.anchorMin = new Vector2(0.5f, 0.52f);
                    parentRect.anchorMax = new Vector2(0.5f, 0.52f);
                    parentRect.pivot = new Vector2(0.5f, 0.5f);
                    parentRect.anchoredPosition = Vector2.zero;
                    parentRect.sizeDelta = new Vector2(requiredGridWidth + 16f, requiredGridHeight + 16f);

                    containerRect.anchorMin = Vector2.zero;
                    containerRect.anchorMax = Vector2.one;
                    containerRect.anchoredPosition = Vector2.zero;
                    containerRect.sizeDelta = Vector2.zero;
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
