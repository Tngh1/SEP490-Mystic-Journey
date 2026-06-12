using System;
using System.Collections.Generic;
using UnityEngine;

public class UIDailyLogin : MonoBehaviour
{
    public static UIDailyLogin Instance;

    [Header("UI Settings")]
    [SerializeField] private UIDailySlot dailySlotPrefab;
    [SerializeField] private Transform contentParent;

    private List<UIDailySlot> slots = new List<UIDailySlot>();

    // B?n s? ki?n ra ngoài khi Mira click nh?n quà
    public Action<UIBaseItemSlot> OnDailyItemClaimed;

    private void Awake()
    {
        Instance = this;
    }

    public void RefreshDaily(List<UIItemDisplayData> dailyItems)
    {
        for (int i = 0; i < dailyItems.Count; i++)
        {
            if (i >= slots.Count)
            {
                UIDailySlot newSlot = Instantiate(dailySlotPrefab, contentParent);
                newSlot.transform.localScale = Vector3.one; // Ch?ng phình to
                newSlot.OnSlotClicked += HandleSlotClicked;
                slots.Add(newSlot);
            }

            slots[i].gameObject.SetActive(true);
            slots[i].SetupDaily(dailyItems[i]);
        }
    }

    private void HandleSlotClicked(UIBaseItemSlot clickedSlot)
    {
        UIItemDisplayData data = (UIItemDisplayData)clickedSlot.RawData;
        Debug.Log("Mira v?a nh?n quà Ngày " + data.dayNumber + "! Món ??: " + data.itemName);

        OnDailyItemClaimed?.Invoke(clickedSlot);
    }
}