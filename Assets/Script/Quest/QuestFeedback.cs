using UnityEngine;

/// <summary>
/// Turns quest events into banner messages: quest accepted, objective completed (10/10) and reward received.
/// Kept separate so MessageBannerUI stays generic and QuestManager stays free of UI.
/// </summary>
public class QuestFeedback : MonoBehaviour
{
    [SerializeField] QuestManager questManager;
    [SerializeField] MessageBannerUI banner;

    [Header("Texts ({0} = quest title / reward)")]
    [SerializeField] string acceptedText = "Đã nhận nhiệm vụ: {0}";
    [SerializeField] string objectiveCompletedText = "Hoàn thành mục tiêu! Quay lại gặp NPC để nhận tiền công.";
    [SerializeField] string rewardText = "Nhận thưởng: +{0} tiền";

    void OnEnable()
    {
        if (questManager == null) return;
        questManager.QuestAccepted += OnAccepted;
        questManager.ObjectiveCompleted += OnObjectiveCompleted;
        questManager.QuestCompleted += OnCompleted;
    }

    void OnDisable()
    {
        if (questManager == null) return;
        questManager.QuestAccepted -= OnAccepted;
        questManager.ObjectiveCompleted -= OnObjectiveCompleted;
        questManager.QuestCompleted -= OnCompleted;
    }

    void OnAccepted(QuestStatus quest)
    {
        if (banner != null) banner.Show(string.Format(acceptedText, quest.Definition.title));
    }

    void OnObjectiveCompleted(QuestStatus quest)
    {
        if (banner == null || quest.Definition.silentCompletion) return;
        string custom = quest.Definition.objectiveCompletedMessage;
        banner.Show(string.IsNullOrEmpty(custom) ? objectiveCompletedText : custom);
    }

    void OnCompleted(QuestStatus quest)
    {
        // quests without a reward (story steps) must not announce '+0 tiền' or overwrite their own banner
        if (banner == null || quest.Definition.silentCompletion || quest.Definition.rewardMoney <= 0) return;
        banner.Show(string.Format(rewardText, quest.Definition.rewardMoney));
    }
}
