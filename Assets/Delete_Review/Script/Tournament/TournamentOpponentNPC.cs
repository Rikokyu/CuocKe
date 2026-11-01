using UnityEngine;

/// <summary>
/// The villager that fronts one tournament opponent. Same interaction loop as VillagerFlavorNPC / QuestNPC (shared
/// prompt + DialogueUI, legacy E key, range check); the VillagerFlavorNPC on the same object is switched off in the
/// scene so only this script owns the E key. All rules (unlock order, rewards) live in TournamentManager.
/// </summary>
public class TournamentOpponentNPC : MonoBehaviour
{
    [SerializeField, Min(0)] int opponentIndex;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 3f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string promptText = "Nhấn E để nói chuyện";
    [SerializeField] Transform player;
    [SerializeField] Transform modelRoot;

    [Header("Scene references")]
    [SerializeField] TournamentManager manager;
    [SerializeField] DialogueUI dialogue;
    [SerializeField] InteractionPromptUI prompt;

    [Header("Dialogue")]
    [SerializeField] string npcName = "Dân làng";
    [SerializeField, TextArea(2, 4)] string[] flavorLines = { "..." };       // tournament not open yet
    [SerializeField, TextArea(2, 4)] string[] fightingLines = { "Đang đấu rồi, nhìn đi đâu đấy?!" };

    bool promptShown;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null || manager == null || dialogue == null || prompt == null)
            Debug.LogError("[TournamentOpponentNPC] Missing a required reference.", this);
    }

    void Update()
    {
        if (player == null || dialogue == null) return;
        bool can = InRange() && !dialogue.IsOpen;
        SetPrompt(can);
        if (can && Input.GetKeyDown(interactKey)) Interact();
    }

    bool InRange()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    /// <summary>E key action. Public so a test can drive the same path.</summary>
    public bool Interact()
    {
        if (dialogue == null || dialogue.IsOpen || manager == null) return false;

        OpponentDefinition def = manager.GetDefinition(opponentIndex);
        string who = def != null ? def.displayName : npcName;

        if (manager.IsBusy) dialogue.Open(who, fightingLines, null, null);
        else if (def == null || !manager.IsOpen) dialogue.Open(who, flavorLines, null, null);
        else if (manager.IsDefeated(opponentIndex)) dialogue.Open(who, def.defeatedLines, null, null);
        else if (!manager.IsUnlocked(opponentIndex)) dialogue.Open(who, def.lockedLines, null, null);
        else
        {
            int index = opponentIndex;
            dialogue.Open(who, manager.HasLost(index) ? def.retryLines : def.introLines, def.challengeLabel, () => manager.StartChallenge(index));
        }

        SetPrompt(false);
        FacePlayer();
        return true;
    }

    void FacePlayer()
    {
        Transform t = modelRoot != null ? modelRoot : transform;
        Vector3 to = player.position - t.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(to);
    }

    void SetPrompt(bool visible)
    {
        if (prompt == null || visible == promptShown) return;
        promptShown = visible;
        if (visible) prompt.Show(this, promptText);
        else prompt.Hide(this);
    }

    void OnDisable()
    {
        SetPrompt(false);
    }
}
