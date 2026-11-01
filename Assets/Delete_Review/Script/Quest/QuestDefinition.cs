using UnityEngine;

/// <summary>
/// Static data for one quest. Everything designers may want to tune (text, target amount, reward) lives
/// here so no other script hardcodes it. questId is the stable identifier - never rename it once used.
/// </summary>
[CreateAssetMenu(fileName = "Quest_New", menuName = "Chicken Training/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    [Tooltip("Stable ID used by code and (later) save data. Never change it after the quest has shipped.")]
    public string questId = "new_quest";

    public string title = "New Quest";

    [TextArea(2, 4)]
    public string description;

    [Tooltip("Short objective line shown on the Quest HUD.")]
    public string objective;

    [Tooltip("Key that gameplay systems report progress with, e.g. QuestManager.ScrapObjective. Matching Active quests advance.")]
    public string objectiveKey = "scrap";

    [Tooltip("Text in front of the counter on the HUD: 'Ve chai' -> 'Ve chai: 3 / 10'.")]
    public string progressLabel = "Ve chai";

    [Min(1)] public int requiredAmount = 10;

    [Tooltip("Money paid once, when the quest is turned in.")]
    [Min(0)] public int rewardMoney = 100;

    [Header("Rules")]
    [Tooltip("OFF (default): the reward is paid once, ever. ON: after the reward the quest returns to NotStarted and can be accepted again.")]
    public bool repeatable = false;

    [Tooltip("Optional HUD text once the objective is done, e.g. 'Quay lại gặp bà để nhận tiền công'. Empty = default HUD text.")]
    public string turnInObjective;

    [Header("Feedback (optional)")]
    [Tooltip("Banner shown when the objective is done. Empty = the default 'Hoàn thành mục tiêu...' text.")]
    [TextArea(1, 3)] public string objectiveCompletedMessage;
    [Tooltip("ON: no 'objective done' / reward banner for this quest (story guide quests that finish silently).")]
    public bool silentCompletion;

    [Header("NPC dialogue (optional - overrides the NPC's own default lines). {0} = progress, {1} = required, {2} = reward")]
    [TextArea(2, 4)] public string[] offerLines;
    [TextArea(2, 4)] public string[] activeLines;
    [TextArea(2, 4)] public string[] readyLines;
    [TextArea(2, 4)] public string[] completedLines;

}
