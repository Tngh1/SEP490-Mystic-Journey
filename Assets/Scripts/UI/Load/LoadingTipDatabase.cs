using UnityEngine;

[CreateAssetMenu(
    fileName = "LoadingTipDatabase",
    menuName = "Mystic Journey/Loading Tip Database"
)]
public class LoadingTipDatabase : ScriptableObject
{
    [TextArea(2, 4)]
    [SerializeField] private string[] tips;

    public string GetRandomTip()
    {
        if (tips == null || tips.Length == 0)
            return string.Empty;

        int randomIndex = Random.Range(0, tips.Length);
        return tips[randomIndex].Trim();
    }
}