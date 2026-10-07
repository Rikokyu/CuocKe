using UnityEngine;

/// <summary>
/// Battle Zone trigger: when the Player (with an acquired Main Chicken) walks in,
/// starts the battle via BattleManager. Attach to a trigger Collider placed in the
/// enemy's area. Does not touch combat - the existing AI already aggroes by proximity;
/// this only marks the formal "battle started" flow state.
/// </summary>
public class BattleTrigger : MonoBehaviour
{
    public bool triggerOnce = true;
    bool consumed;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnce && consumed) return;

        if (MainChickenAcquire.Instance == null || !MainChickenAcquire.Instance.IsAcquired)
        {
            Debug.Log("[BattleTrigger] Player entered battle zone without an acquired Main Chicken - ignored.");
            return;
        }

        if (BattleManager.Instance == null)
        {
            Debug.LogError("[BattleTrigger] No BattleManager in scene.");
            return;
        }

        if (BattleManager.Instance.IsBattleActive) return;

        consumed = true;
        BattleManager.Instance.StartBattle();
    }
}
