using UnityEngine;

// Executes scriptable object operation.
[CreateAssetMenu(menuName = "Mystic Journey/NPC Data", fileName = "NewNPCData")]
public class NPCData : ScriptableObject
{
    [Header("Identity")]
    public int npcId;
    public string npcName;
    // Free-form NPC role label, such as Village Elder or Blacksmith; the code does not enforce a closed set of values.
    public string role;
    public Sprite portrait;

    [Header("Greeting")]
    [TextArea(2, 4)]
    public string greetingText;

    [Header("Quests")]
    public int[] availableQuestIds;
}
