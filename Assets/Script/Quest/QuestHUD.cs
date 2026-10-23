using TMPro;
using UnityEngine;

/// <summary>
/// Quest HUD: title, short objective and "Ve chai: 3 / 10" progress of the tracked quest.
/// Hidden until a quest is accepted and hidden again once it is completed.
/// This component must live on an object that stays active; only the child 'panel' is toggled.
/// </summary>
public class QuestHUD : MonoBehaviour
{
    [SerializeField] QuestManager questManager;
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleLabel;
    [SerializeField] TMP_Text objectiveLabel;
    [SerializeField] TMP_Text progressLabel;
    [SerializeField] string readyToTurnInObjective = "Quay lại gặp NPC để nhận tiền công";

    void OnEnable()
    {
        if (questManager != null) questManager.QuestChanged += OnQuestChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (questManager != null) questManager.QuestChanged -= OnQuestChanged;
    }

    void OnQuestChanged(QuestStatus changed)
    {
        Refresh();
    }

void Refresh()
    {
        QuestStatus quest = questManager != null ? questManager.TrackedQuest : null;
        bool visible = quest != null && (quest.State == QuestState.Active || quest.State == QuestState.ReadyToTurnIn);

        if (panel != null) panel.SetActive(visible);
        if (!visible) return;

        QuestDefinition def = quest.Definition;
        if (titleLabel != null) titleLabel.text = def.title;
        if (objectiveLabel != null)
        {
            if (quest.State == QuestState.ReadyToTurnIn)
                objectiveLabel.text = string.IsNullOrEmpty(def.turnInObjective) ? readyToTurnInObjective : def.turnInObjective;
            else
                objectiveLabel.text = def.objective;
        }
        if (progressLabel != null)
            progressLabel.text = def.progressLabel + ": " + quest.Progress + " / " + def.requiredAmount;
    }
}
