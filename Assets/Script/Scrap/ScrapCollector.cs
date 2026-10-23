using UnityEngine;

/// <summary>
/// Handles scrap collection for the whole scene (one instance).
///
/// Every frame it picks the NEAREST uncollected pickup within interactionDistance (plain distance maths on
/// a list gathered once in Start - no physics, no Find calls), shows ONE prompt for it, and on interactKey
/// (E, legacy Input like the rest of the project) collects exactly that one item. So one key press can never
/// collect two items.
///
/// Collecting an item, in this order:
///   1. ScrapPickup.MarkCollected()  - once-only gate; flipped BEFORE anything is awarded
///   2. CurrencyWallet.Add(type.moneyValue)
///   3. pickup message on the banner
///   4. QuestManager.ReportProgress(ScrapObjective)  - runs last so its "10/10" message is not overwritten.
///      QuestManager only advances an Active quest and clamps at the required amount.
///
/// Items can only be collected once the quest has been accepted (state != NotStarted), so nothing is
/// consumed or paid before the NPC offered the job. Surplus items collected after 10/10 pay money but
/// cannot raise the progress further. The quest REWARD is never paid here - only by the NPC turn-in.
/// </summary>
public class ScrapCollector : MonoBehaviour
{
    [Header("Scene references")]
    [Tooltip("Parent of all placed ScrapPickup objects. Add or remove items by adding/removing children.")]
    [SerializeField] Transform pickupsRoot;
    [Tooltip("Optional. If empty, the GameObject tagged 'Player' is used.")]
    [SerializeField] Transform player;
    [SerializeField] QuestManager questManager;
    [SerializeField] QuestDefinition quest;
    [SerializeField] CurrencyWallet wallet;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] MessageBannerUI banner;
    [SerializeField] DialogueUI dialogue;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] float interactionDistance = 2.2f;
    [SerializeField] KeyCode interactKey = KeyCode.E;

    [Header("Texts ({0} = item name, {1} = money)")]
    [SerializeField] string promptFormat = "Nhấn E để nhặt {0}";
    [SerializeField] string collectedFormat = "Nhặt được {0}  (+{1} tiền)";

    ScrapPickup[] pickups;
    ScrapPickup promptTarget;
    bool promptShown;

    void Start()
    {
        if (pickupsRoot != null) pickups = pickupsRoot.GetComponentsInChildren<ScrapPickup>(true);
        else Debug.LogError("[ScrapCollector] pickupsRoot is not assigned.", this);

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) Debug.LogError("[ScrapCollector] No player assigned and none tagged 'Player'.", this);
        if (questManager == null || quest == null || wallet == null)
            Debug.LogError("[ScrapCollector] QuestManager / Quest / CurrencyWallet reference missing.", this);
    }

    void Update()
    {
        if (pickups == null || player == null) return;

        ScrapPickup nearest = null;
        if (IsCollectionOpen() && !IsBlockedByUI())
            nearest = FindNearestInRange();

        UpdatePrompt(nearest);

        if (nearest != null && Input.GetKeyDown(interactKey))
        {
            TryCollect(nearest);
            UpdatePrompt(null); // the item is gone; the next frame decides what to show next
        }
    }

    /// <summary>Collects one pickup if allowed. Returns false (and awards nothing) otherwise.</summary>
    public bool TryCollect(ScrapPickup pickup)
    {
        if (pickup == null || pickup.IsCollected || !IsCollectionOpen()) return false;
        if (!pickup.MarkCollected()) return false;

        string itemName = pickup.Type != null ? pickup.Type.displayName : pickup.name;
        int money = pickup.Type != null ? pickup.Type.moneyValue : 0;

        if (wallet != null && money > 0) wallet.Add(money);
        if (banner != null) banner.Show(string.Format(collectedFormat, itemName, money));
        if (questManager != null) questManager.ReportProgress(QuestManager.ScrapObjective);

        Debug.Log("[Scrap] Collected " + itemName + " (+" + money + ")");
        return true;
    }

    bool IsCollectionOpen()
    {
        return questManager != null && quest != null
            && questManager.GetState(quest.questId) != QuestState.NotStarted;
    }

    bool IsBlockedByUI()
    {
        if (dialogue != null && dialogue.IsOpen) return true;
        return InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
    }

    ScrapPickup FindNearestInRange()
    {
        ScrapPickup best = null;
        float bestSqr = interactionDistance * interactionDistance;
        Vector3 playerPos = player.position;

        for (int i = 0; i < pickups.Length; i++)
        {
            ScrapPickup p = pickups[i];
            if (p == null || p.IsCollected) continue;

            Vector3 offset = p.transform.position - playerPos;
            offset.y = 0f; // ignore height: the player's pivot is at the CharacterController centre
            float sqr = offset.sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = p;
            }
        }
        return best;
    }

    void UpdatePrompt(ScrapPickup target)
    {
        if (target == null)
        {
            if (promptShown && prompt != null) prompt.Hide(this);
            promptShown = false;
            promptTarget = null;
            return;
        }

        if (prompt == null || (promptShown && promptTarget == target)) return;

        string itemName = target.Type != null ? target.Type.displayName : target.name;
        prompt.Show(this, string.Format(promptFormat, itemName));
        promptShown = true;
        promptTarget = target;
    }

    void OnDisable()
    {
        UpdatePrompt(null);
    }

    void OnDrawGizmosSelected()
    {
        if (pickupsRoot == null) return;
        Gizmos.color = Color.cyan;
        foreach (ScrapPickup p in pickupsRoot.GetComponentsInChildren<ScrapPickup>(true))
            Gizmos.DrawWireSphere(p.transform.position, interactionDistance);
    }
}
