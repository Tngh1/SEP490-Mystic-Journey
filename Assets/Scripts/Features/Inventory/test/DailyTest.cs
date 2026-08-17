using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
public class DailyTest : MonoBehaviour
{
    // Performs startup initialization for DailyTest on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        Debug.Log("DAILY LOGIN TEST START");

        List<UIItemDisplayData> dailyItems = new List<UIItemDisplayData>();

        string[] fakeNames = { "Gold", "Gem", "Small Health Potion", "Skill Upgrade Stone", "Lucky Ticket" };
        string[] fakeTypes = { "Currency", "Currency", "Consumable", "Material", "QuestItem" };
        string[] rarities = { "common", "rare", "epic", "legendary" };

        for (int i = 1; i <= 30; i++)
        {
            UIItemDisplayData item = new UIItemDisplayData();

            // Randomize the eligible candidates before selecting this gameplay result.
            int pick = Random.Range(0, fakeNames.Length);
            item.dayNumber = i;
            item.itemId = pick + 1;
            item.itemName = fakeNames[pick];
            item.category = fakeTypes[pick];
            // Randomize the eligible candidates before selecting this gameplay result.
            item.rarity = rarities[Random.Range(0, rarities.Length)];

            if (i % 7 == 0)
            {
                // Randomize the eligible candidates before selecting this gameplay result.
                item.quantity = Random.Range(100, 500);
            }
            else
            {
                // Randomize the eligible candidates before selecting this gameplay result.
                item.quantity = Random.Range(2, 20);
            }

            item.isClaimed = (i <= 5);

            item.icon = ItemIconDatabase.Instance.GetIcon(item.itemName, item.category);

            item.rawData = item;

            dailyItems.Add(item);
        }

        Debug.Log("CALL REFRESH DAILY: " + dailyItems.Count + " days");

        UIDailyLogin.Instance.RefreshDaily(dailyItems);
    }
}
