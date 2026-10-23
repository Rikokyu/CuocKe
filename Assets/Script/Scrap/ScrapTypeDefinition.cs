using UnityEngine;

/// <summary>
/// Static data for one kind of scrap. Display name and money are tunable per type here, so no script
/// hardcodes them. (Design: bottle 5, can 10, cardboard 8, scrap metal 15 - "tunable examples".)
/// </summary>
[CreateAssetMenu(fileName = "Scrap_New", menuName = "Chicken Training/Scrap Type")]
public class ScrapTypeDefinition : ScriptableObject
{
    [Tooltip("Stable ID, e.g. plastic_bottle.")]
    public string typeId = "new_scrap";

    [Tooltip("Name shown in the prompt and pickup message.")]
    public string displayName = "Ve chai";

    [Tooltip("Money paid once when this item is collected.")]
    [Min(0)] public int moneyValue = 5;
}
